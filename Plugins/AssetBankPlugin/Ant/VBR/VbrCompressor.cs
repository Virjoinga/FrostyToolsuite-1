using System;
using System.Collections.Generic;
using Assimp;
using Frosty.Core;

namespace AssetBankPlugin.Ant.VBR
{
    internal static class VbrCompressor
    {
        private static readonly float[,] DctFwd;

        private static readonly float[,] DctInv = new float[8, 8]
        {
            {  0.12500000f,  0.24519631f,  0.23096988f,  0.20786740f,  0.17677669f,  0.13889255f,  0.09567086f,  0.04877256f },
            {  0.12500000f,  0.20786740f,  0.09567086f, -0.04877258f, -0.17677669f, -0.24519633f, -0.23096988f, -0.13889250f },
            {  0.12500000f,  0.13889255f, -0.09567088f, -0.24519633f, -0.17677666f,  0.04877260f,  0.23096989f,  0.20786734f },
            {  0.12500000f,  0.04877256f, -0.23096991f, -0.13889250f,  0.17677675f,  0.20786734f, -0.09567098f, -0.24519630f },
            {  0.12500000f, -0.04877258f, -0.23096988f,  0.13889261f,  0.17677669f, -0.20786744f, -0.09567074f,  0.24519636f },
            {  0.12500000f, -0.13889259f, -0.09567078f,  0.24519631f, -0.17677681f, -0.04877255f,  0.23096983f, -0.20786740f },
            {  0.12500000f, -0.20786741f,  0.09567090f,  0.04877252f, -0.17677663f,  0.24519633f, -0.23096994f,  0.13889278f },
            {  0.12500000f, -0.24519633f,  0.23096989f, -0.20786744f,  0.17677671f, -0.13889271f,  0.09567098f, -0.04877289f },
        };

        private const int NumQTables = 256;

        static VbrCompressor()
        {
            DctFwd = new float[8, 8];
            for (int j = 0; j < 8; j++)
                for (int k = 0; k < 8; k++)
                    DctFwd[j, k] = (float)Math.Cos(Math.PI * (j + 0.5) * k / 8.0);
        }

        public static VbrAnimationAsset Compress(
            Scene scene,
            VbrAnimationAsset template,
            bool bigEndian,
            float maxRotErrPct = 0f,
            float maxTransErrPct = 0f,
            float maxTrajErrPct = 0f,
            float constThresh = 0.00001f,
            bool linearSearchTables = true,
            bool curveFit = true)
        {
            int totalQ = template.QuaternionCount + template.ConstQuaternionCount;
            int totalV = template.Vector3Count + template.ConstVector3Count;
            int totalF = template.NumFloat + template.ConstFloatCount;

            ExtractFromScene(scene, template, totalQ, totalV, totalF,
                out float[][][] quats,
                out float[][][] vecs,
                out float[][] floats,
                out int frameCount,
                out bool cycle);

            return CompressInternal(quats, vecs, floats, frameCount, cycle,
                template, bigEndian,
                maxRotErrPct, maxTransErrPct, maxTrajErrPct, constThresh,
                linearSearchTables, curveFit);
        }

        public static VbrAnimationAsset CompressFromData(
            float[][][] quats,      // [channel][frame][4]
            float[][][] vecs,       // [channel][frame][3]
            float[][] floats,       // [channel][frame]
            int frameCount,
            bool cycle,
            VbrAnimationAsset template,
            float maxRotErrPct = 0f,
            float maxTransErrPct = 0f,
            float maxTrajErrPct = 0f,
            float constThresh = 0.00001f,
            bool bigEndian = false,
            bool linearSearchTables = true,
            bool curveFit = true)
        {
            return CompressInternal(quats, vecs, floats, frameCount, cycle,
                template, bigEndian,
                maxRotErrPct, maxTransErrPct, maxTrajErrPct, constThresh,
                linearSearchTables, curveFit);
        }

        private static VbrAnimationAsset CompressInternal(
            float[][][] quats,
            float[][][] vecs,
            float[][] floats,
            int frameCount,
            bool cycle,
            VbrAnimationAsset template,
            bool bigEndian,
            float maxRotErrPct,
            float maxTransErrPct,
            float maxTrajErrPct,
            float constThresh,
            bool linearSearchTables = true,
            bool curveFit = true)
        {
            int totalQ = template.QuaternionCount + template.ConstQuaternionCount;
            int totalV = template.Vector3Count + template.ConstVector3Count;
            int totalF = template.NumFloat + template.ConstFloatCount;
            int totalCh = totalQ + totalV + totalF;

            bool[] constCh = MarkConstantChannels(
                quats, vecs, floats, frameCount, totalQ, totalV, totalF, constThresh);

            // If less than 10% of channels are constant, keep all channels animated to avoid overhead
            int cc = 0;
            for (int i = 0; i < totalCh; i++) if (constCh[i]) cc++;
            if (totalCh > 0 && (float)cc / totalCh < 0.1f)
                for (int i = 0; i < totalCh; i++) constCh[i] = false;

            int cQ = 0, cV = 0, cF = 0;
            for (int i = 0; i < totalQ; i++) if (constCh[i]) cQ++;
            for (int i = 0; i < totalV; i++) if (constCh[totalQ + i]) cV++;
            for (int i = 0; i < totalF; i++) if (constCh[totalQ + totalV + i]) cF++;
            int aQ = totalQ - cQ;
            int aV = totalV - cV;
            int aF = totalF - cF;

            int stride = aQ * 4 + aV * 3 + aF;
            int alignedStride = (stride + 3) & ~3;

            bool sepTraj = aV > 1 && !constCh[totalQ];

            float[] cData = SaveConstantChannels(
                quats, vecs, floats, constCh, totalQ, totalV, totalF, frameCount);

            // Fit curves to Vec3 and Float channels and compress residuals
            CurveFitHelper cfHelper = null;
            if (curveFit)
            {
                cfHelper = new CurveFitHelper(vecs, floats, constCh, totalQ, totalV, totalF, frameCount);
                cfHelper.DoCurveFitting();
            }

            ComputeMinMax(quats, vecs, floats, constCh, frameCount,
                totalQ, totalV, totalF, aQ, aV, aF,
                out float[] qMinCh, out float[] qMaxCh,
                out float[] vMinCh, out float[] vMaxCh,
                out float[] fMinCh, out float[] fMaxCh,
                cfHelper);

            float quatMin = 0f, quatMax = 0f;
            float v3Min = 0f, v3Max = 0f;
            float fltMin = 0f, fltMax = 0f;
            float tjMin = 0f, tjMax = 0f;

            int cdi = 0;
            for (int i = 0; i < cQ * 4; i++, cdi++) { quatMin = Math.Min(quatMin, cData[cdi]); quatMax = Math.Max(quatMax, cData[cdi]); }
            for (int i = 0; i < cV * 3; i++, cdi++) { v3Min = Math.Min(v3Min, cData[cdi]); v3Max = Math.Max(v3Max, cData[cdi]); }
            for (int i = 0; i < cF; i++, cdi++) { fltMin = Math.Min(fltMin, cData[cdi]); fltMax = Math.Max(fltMax, cData[cdi]); }

            for (int i = 0; i < aQ; i++) { quatMin = Math.Min(quatMin, qMinCh[i]); quatMax = Math.Max(quatMax, qMaxCh[i]); }

            if (aV > 0)
            {
                if (sepTraj) { tjMin = vMinCh[0]; tjMax = vMaxCh[0]; }
                else { v3Min = Math.Min(v3Min, vMinCh[0]); v3Max = Math.Max(v3Max, vMaxCh[0]); }
            }
            for (int i = 1; i < aV; i++) { v3Min = Math.Min(v3Min, vMinCh[i]); v3Max = Math.Max(v3Max, vMaxCh[i]); }

            for (int i = 0; i < aF; i++) { fltMin = Math.Min(fltMin, fMinCh[i]); fltMax = Math.Max(fltMax, fMaxCh[i]); }

            if (!sepTraj) { tjMin = v3Min; tjMax = v3Max; }

            for (int i = 0; i < aQ; i++) { qMinCh[i] = quatMin; qMaxCh[i] = quatMax; }
            for (int i = 0; i < aV; i++)
            {
                bool isTraj = i == 0 && sepTraj;
                vMinCh[i] = isTraj ? tjMin : v3Min;
                vMaxCh[i] = isTraj ? tjMax : v3Max;
            }
            for (int i = 0; i < aF; i++) { fMinCh[i] = fltMin; fMaxCh[i] = fltMax; }

            float[] errBlock = BuildErrorBlock(stride, aQ, aV, sepTraj,
                maxRotErrPct / 100f,
                maxTransErrPct / 100f,
                maxTrajErrPct / 100f);

            NormalizeConstantData(cData, cQ, cV, cF,
                quatMin, quatMax, v3Min, v3Max, fltMin, fltMax);

            float[] constPalette = BuildConstantPalette(cData, cQ, cV, cF, template.Name ?? "");

            float[] norm = NormalizeAnimated(quats, vecs, floats, constCh, frameCount,
                totalQ, totalV, totalF, aQ, aV, aF, alignedStride,
                qMinCh, qMaxCh, vMinCh, vMaxCh, fMinCh, fMaxCh,
                cfHelper);

            int frameBlocks = (frameCount + 7) >> 3;
            bool trajConstant = totalV > 0 && constCh[totalQ];

            byte[] cfVecOffsets = cfHelper?.VectorOffsets;
            byte[] cfFltOffsets = cfHelper?.FloatOffsets;
            float cfVecScale = cfHelper != null ? cfHelper.VectorOffsetScale : 0f;
            float cfFltScale = cfHelper != null ? cfHelper.FloatOffsetScale : 0f;
            ushort cfVecSize = cfHelper != null ? cfHelper.VectorOffsetSize : (ushort)0;
            ushort cfFltSize = cfHelper != null ? cfHelper.FloatOffsetSize : (ushort)0;

            if (stride == 0)
            {
                byte[] dataBlob0 = BitPackHeaderOnly(cData, constCh, constPalette,
                    totalQ, totalV, totalF, cQ, cV, cF, frameCount, cycle,
                    out ushort kts0, out ushort ccms0, out bool noChMap0, out bool noKT0);

                return BuildResult(template, frameCount, aQ, aV, aF, cQ, cV, cF,
                    quatMin, quatMax, tjMin, tjMax, v3Min, v3Max, fltMin, fltMax, 0f,
                    (ushort)constPalette.Length, kts0, ccms0, noKT0, noChMap0,
                    false, cycle, sepTraj, totalQ, trajConstant,
                    constPalette, Array.Empty<ushort>(), dataBlob0,
                    cfVecOffsets, cfFltOffsets, cfVecScale, cfFltScale, cfVecSize, cfFltSize);
            }

            float[] dctData = ForwardDCT(norm, frameCount, alignedStride, frameBlocks);

            GetDctMinMax(dctData, frameBlocks, stride, alignedStride,
                out float dctMin, out float dctMax);
            float dct = Math.Max(Math.Abs(dctMin), Math.Abs(dctMax));
            if (dct == 0f) dct = 1f;

            float[,] qTables = InitQuantizationTables(dctMin, dctMax);

            int numShorts = stride * 8 + 3;
            short[] intermediate = new short[frameBlocks * numShorts];

            for (int fb = 0; fb < frameBlocks; fb++)
            {
                DetermineQuantTables(fb, frameCount, dctData, norm, qTables, errBlock,
                    stride, alignedStride, aQ, aV, sepTraj, linearSearchTables,
                    out int rt, out int tjt, out int tat);

                intermediate[fb * numShorts + 0] = (short)rt;
                intermediate[fb * numShorts + 1] = (short)tjt;
                intermediate[fb * numShorts + 2] = (short)tat;

                QuantizeBlock(fb, frameCount, dctData, qTables, intermediate,
                    stride, alignedStride, aQ, aV, sepTraj, rt, tjt, tat, numShorts);
            }

            int[] numBits = ComputeNumBits(intermediate, stride, frameBlocks, numShorts,
                out bool fastBit);

            byte[] data = BitPackAll(constCh, cData, constPalette, intermediate, numBits,
                frameCount, frameBlocks,
                totalQ, totalV, totalF, aQ, aV, aF,
                stride, alignedStride, sepTraj, cycle,
                bigEndian, numShorts,
                cfVecOffsets, cfFltOffsets,
                out ushort[] frameBlockSizes,
                out ushort keyTimeSize, out ushort constChanMapSize,
                out bool noKeyTimes, out bool noChannelMap);

            return BuildResult(template, frameCount, aQ, aV, aF, cQ, cV, cF,
                quatMin, quatMax, tjMin, tjMax, v3Min, v3Max, fltMin, fltMax, dct,
                (ushort)constPalette.Length, keyTimeSize, constChanMapSize,
                noKeyTimes, noChannelMap, fastBit, cycle, sepTraj, totalQ, trajConstant,
                constPalette, frameBlockSizes, data,
                cfVecOffsets, cfFltOffsets, cfVecScale, cfFltScale, cfVecSize, cfFltSize);
        }


        // Extraction from Assimp scene

        private static void ExtractFromScene(
            Scene scene, VbrAnimationAsset template,
            int totalQ, int totalV, int totalF,
            out float[][][] quats, out float[][][] vecs, out float[][] floats,
            out int frameCount, out bool cycle)
        {
            float fps = template.FPS > 0f ? template.FPS : 30f;

            Animation anim = (scene.AnimationCount > 0) ? scene.Animations[0] : null;

            double durationSec = 0.0;
            if (anim != null && anim.TicksPerSecond > 0.0)
                durationSec = anim.DurationInTicks / anim.TicksPerSecond;
            frameCount = Math.Max(2, (int)Math.Ceiling(durationSec * fps) + 1);

            cycle = (template.Flags & 1) != 0;

            var animMap = new Dictionary<string, NodeAnimationChannel>(StringComparer.OrdinalIgnoreCase);
            if (anim != null)
                foreach (var ch in anim.NodeAnimationChannels)
                    animMap[ch.NodeName] = ch;

            List<string> quatNames = new List<string>();
            List<string> vecNames = new List<string>();
            List<string> floatNames = new List<string>();

            if (template.OrderedChannels != null)
            {
                foreach (var ch in template.OrderedChannels)
                {
                    string raw = ch.Name ?? "";
                    string name = raw.Replace(".q", "").Replace(".t", "").Replace(".s", "").Trim();
                    switch (ch.Type)
                    {
                        case BoneChannelType.Rotation: quatNames.Add(name); break;
                        case BoneChannelType.Position:
                        case BoneChannelType.Scale: vecNames.Add(name); break;
                        default: floatNames.Add(name); break;
                    }
                }
            }

            while (quatNames.Count < totalQ) quatNames.Add("");
            while (vecNames.Count < totalV) vecNames.Add("");
            while (floatNames.Count < totalF) floatNames.Add("");

            quats = new float[totalQ][][];
            vecs = new float[totalV][][];
            floats = new float[totalF][];

            for (int i = 0; i < totalQ; i++)
            {
                quats[i] = new float[frameCount][];
                var nodeCh = FindNode(animMap, quatNames[i]);
                for (int f = 0; f < frameCount; f++)
                    quats[i][f] = SampleQuat(nodeCh, (double)f / fps, anim);
                EnforceQuatContinuity(quats[i]);
            }

            // Distinguish Scale vs Position from template channel types
            var vecIsScale = new bool[totalV];
            int vSlot = 0;
            if (template.OrderedChannels != null)
            {
                foreach (var ch in template.OrderedChannels)
                {
                    if (ch.Type == BoneChannelType.Position || ch.Type == BoneChannelType.Scale)
                    {
                        if (vSlot < totalV)
                            vecIsScale[vSlot++] = ch.Type == BoneChannelType.Scale;
                    }
                }
            }

            for (int i = 0; i < totalV; i++)
            {
                vecs[i] = new float[frameCount][];
                var nodeCh = FindNode(animMap, vecNames[i]);
                for (int f = 0; f < frameCount; f++)
                    vecs[i][f] = SampleVec3(nodeCh, (double)f / fps, anim, vecIsScale[i]);
            }

            for (int i = 0; i < totalF; i++)
            {
                floats[i] = new float[frameCount];
                for (int f = 0; f < frameCount; f++) floats[i][f] = 0f;
            }
        }


        // All helper methods

        private static NodeAnimationChannel FindNode(
            Dictionary<string, NodeAnimationChannel> map, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (map.TryGetValue(name, out var ch)) return ch;
            if (name.Length > 2 && map.TryGetValue(name.Substring(0, name.Length - 2), out ch)) return ch;
            if (name.Length > 1 && map.TryGetValue(name.Substring(0, name.Length - 1), out ch)) return ch;
            return null;
        }

        private static float[] SampleQuat(NodeAnimationChannel nodeCh, double t, Animation anim)
        {
            if (nodeCh == null || nodeCh.RotationKeyCount == 0)
                return new float[] { 0f, 0f, 0f, 1f };

            double ticks = t * (anim != null && anim.TicksPerSecond > 0.0 ? anim.TicksPerSecond : 1.0);
            var keys = nodeCh.RotationKeys;
            if (keys.Count == 1)
                return QuatToArray(keys[0].Value);

            int lo = keys.Count - 2;
            for (int k = 0; k < keys.Count - 1; k++)
            {
                if (ticks <= keys[k + 1].Time) { lo = k; break; }
            }

            double span = keys[lo + 1].Time - keys[lo].Time;
            float alpha = span > 0.0 ? (float)((ticks - keys[lo].Time) / span) : 0f;
            alpha = Math.Max(0f, Math.Min(1f, alpha));

            return QuatToArray(Assimp.Quaternion.Slerp(keys[lo].Value, keys[lo + 1].Value, alpha));
        }

        private static float[] SampleVec3(NodeAnimationChannel nodeCh, double t, Animation anim, bool isScale)
        {
            if (nodeCh == null)
                return isScale ? new float[] { 1f, 1f, 1f } : new float[] { 0f, 0f, 0f };

            double ticks = t * (anim != null && anim.TicksPerSecond > 0.0 ? anim.TicksPerSecond : 1.0);

            if (isScale)
            {
                if (nodeCh.ScalingKeyCount == 0) return new float[] { 1f, 1f, 1f };
                var keys = nodeCh.ScalingKeys;
                if (keys.Count == 1) return Vec3ToArray(keys[0].Value);

                int lo = keys.Count - 2;
                for (int k = 0; k < keys.Count - 1; k++)
                    if (ticks <= keys[k + 1].Time) { lo = k; break; }

                double span = keys[lo + 1].Time - keys[lo].Time;
                float alpha = span > 0.0 ? (float)((ticks - keys[lo].Time) / span) : 0f;
                alpha = Math.Max(0f, Math.Min(1f, alpha));

                var va = keys[lo].Value;
                var vb = keys[lo + 1].Value;
                return new float[]
                {
                    va.X + (vb.X - va.X) * alpha,
                    va.Y + (vb.Y - va.Y) * alpha,
                    va.Z + (vb.Z - va.Z) * alpha,
                };
            }

            if (nodeCh.PositionKeyCount == 0) return new float[] { 0f, 0f, 0f };

            {
                var keys = nodeCh.PositionKeys;
                if (keys.Count == 1) return Vec3ToArray(keys[0].Value);

                int lo = keys.Count - 2;
                for (int k = 0; k < keys.Count - 1; k++)
                    if (ticks <= keys[k + 1].Time) { lo = k; break; }

                double span = keys[lo + 1].Time - keys[lo].Time;
                float alpha = span > 0.0 ? (float)((ticks - keys[lo].Time) / span) : 0f;
                alpha = Math.Max(0f, Math.Min(1f, alpha));

                var va = keys[lo].Value;
                var vb = keys[lo + 1].Value;
                return new float[]
                {
                    va.X + (vb.X - va.X) * alpha,
                    va.Y + (vb.Y - va.Y) * alpha,
                    va.Z + (vb.Z - va.Z) * alpha,
                };
            }
        }

        private static void EnforceQuatContinuity(float[][] frames)
        {
            if (frames == null || frames.Length < 2) return;
            for (int f = 1; f < frames.Length; f++)
            {
                float[] prev = frames[f - 1];
                float[] cur = frames[f];
                if (prev == null || cur == null || prev.Length < 4 || cur.Length < 4) continue;
                float dot = prev[0] * cur[0] + prev[1] * cur[1] + prev[2] * cur[2] + prev[3] * cur[3];
                if (dot < 0f)
                {
                    cur[0] = -cur[0];
                    cur[1] = -cur[1];
                    cur[2] = -cur[2];
                    cur[3] = -cur[3];
                }
            }
        }

        private static float[] QuatToArray(Assimp.Quaternion q) => new float[] { q.X, q.Y, q.Z, q.W };
        private static float[] Vec3ToArray(Vector3D v) => new float[] { v.X, v.Y, v.Z };

        private static bool[] MarkConstantChannels(
            float[][][] quats, float[][][] vecs, float[][] floats,
            int frameCount, int totalQ, int totalV, int totalF, float threshold)
        {
            bool[] c = new bool[totalQ + totalV + totalF];
            int idx = 0;

            for (int i = 0; i < totalQ; i++, idx++)
            {
                if (frameCount < 2) { c[idx] = true; continue; }
                float[] ref4 = quats[i][0];
                float m0 = 0f, m1 = 0f, m2 = 0f, m3 = 0f;
                for (int f = 1; f < frameCount; f++)
                {
                    float[] cur = quats[i][f];
                    m0 += Math.Abs(ref4[0] - cur[0]);
                    m1 += Math.Abs(ref4[1] - cur[1]);
                    m2 += Math.Abs(ref4[2] - cur[2]);
                    m3 += Math.Abs(ref4[3] - cur[3]);
                }
                c[idx] = m0 <= threshold && m1 <= threshold && m2 <= threshold && m3 <= threshold;
            }

            for (int i = 0; i < totalV; i++, idx++)
            {
                if (frameCount < 2) { c[idx] = true; continue; }
                float[] ref3 = vecs[i][0];
                float m0 = 0f, m1 = 0f, m2 = 0f;
                for (int f = 1; f < frameCount; f++)
                {
                    float[] cur = vecs[i][f];
                    m0 += Math.Abs(ref3[0] - cur[0]);
                    m1 += Math.Abs(ref3[1] - cur[1]);
                    m2 += Math.Abs(ref3[2] - cur[2]);
                }
                c[idx] = m0 <= threshold && m1 <= threshold && m2 <= threshold;
            }

            for (int i = 0; i < totalF; i++, idx++)
            {
                if (frameCount < 2) { c[idx] = true; continue; }
                float m0 = 0f;
                float ref1 = floats[i][0];
                for (int f = 1; f < frameCount; f++)
                    m0 += Math.Abs(ref1 - floats[i][f]);
                c[idx] = m0 <= threshold;
            }

            return c;
        }

        private static float[] SaveConstantChannels(
            float[][][] quats, float[][][] vecs, float[][] floats,
            bool[] constCh, int totalQ, int totalV, int totalF, int frameCount)
        {
            int size = 0;
            for (int i = 0; i < totalQ; i++) if (constCh[i]) size += 4;
            for (int i = 0; i < totalV; i++) if (constCh[totalQ + i]) size += 3;
            for (int i = 0; i < totalF; i++) if (constCh[totalQ + totalV + i]) size++;

            float[] data = new float[size];
            int pos = 0;

            for (int i = 0; i < totalQ; i++)
                if (constCh[i]) { float[] q = quats[i][0]; data[pos++] = q[0]; data[pos++] = q[1]; data[pos++] = q[2]; data[pos++] = q[3]; }
            for (int i = 0; i < totalV; i++)
                if (constCh[totalQ + i]) { float[] v = vecs[i][0]; data[pos++] = v[0]; data[pos++] = v[1]; data[pos++] = v[2]; }
            for (int i = 0; i < totalF; i++)
                if (constCh[totalQ + totalV + i]) data[pos++] = floats[i][0];

            return data;
        }

        private static void ComputeMinMax(
            float[][][] quats, float[][][] vecs, float[][] floats,
            bool[] constCh, int frameCount,
            int totalQ, int totalV, int totalF, int aQ, int aV, int aF,
            out float[] qMin, out float[] qMax,
            out float[] vMin, out float[] vMax,
            out float[] fMin, out float[] fMax,
            CurveFitHelper cfHelper = null)
        {
            qMin = new float[aQ]; qMax = new float[aQ];
            vMin = new float[aV]; vMax = new float[aV];
            fMin = new float[aF]; fMax = new float[aF];

            int qi = 0;
            for (int i = 0; i < totalQ; i++)
            {
                if (constCh[i]) continue;
                float mn = 0f, mx = 0f;
                for (int f = 0; f < frameCount; f++)
                {
                    float[] q = quats[i][f];
                    for (int c = 0; c < 4; c++) { if (q[c] < mn) mn = q[c]; if (q[c] > mx) mx = q[c]; }
                }
                qMin[qi] = mn; qMax[qi] = mx; qi++;
            }

            int vi = 0;
            for (int i = 0; i < totalV; i++)
            {
                if (constCh[totalQ + i]) continue;
                float mn = 0f, mx = 0f;
                for (int f = 0; f < frameCount; f++)
                {
                    float vx, vy, vz;
                    if (cfHelper != null)
                        cfHelper.GetToDeltaChannelsVector3(vi, f, out vx, out vy, out vz);
                    else { float[] v_ = vecs[i][f]; vx = v_[0]; vy = v_[1]; vz = v_[2]; }
                    if (vx < mn) mn = vx; if (vx > mx) mx = vx;
                    if (vy < mn) mn = vy; if (vy > mx) mx = vy;
                    if (vz < mn) mn = vz; if (vz > mx) mx = vz;
                }
                vMin[vi] = mn; vMax[vi] = mx; vi++;
            }

            int fi = 0;
            for (int i = 0; i < totalF; i++)
            {
                if (constCh[totalQ + totalV + i]) continue;
                float mn = 0f, mx = 0f;
                for (int f = 0; f < frameCount; f++)
                {
                    float fv = cfHelper != null ? cfHelper.GetToDeltaChannelFloat(fi, f) : floats[i][f];
                    if (fv < mn) mn = fv; if (fv > mx) mx = fv;
                }
                fMin[fi] = mn; fMax[fi] = mx; fi++;
            }
        }

        private static float[] BuildErrorBlock(
           int stride, int aQ, int aV, bool sepTraj,
           float rotErr, float transErr, float trajErr)
        {
            float[] e = new float[stride];

            for (int i = 0; i < aQ * 4; i++)
                e[i] = rotErr;

            int trajEnd = aQ * 4;
            if (aV > 0 && sepTraj)
            {
                e[aQ * 4 + 0] = trajErr;
                e[aQ * 4 + 1] = trajErr;
                e[aQ * 4 + 2] = trajErr;
                trajEnd = aQ * 4 + 3;
            }

            for (int i = trajEnd; i < stride; i++)
                e[i] = transErr;

            return e;
        }

        private static bool IsSimilar(float a, float b, float epsilon = 1.1920929e-7f)
            => Math.Abs(a - b) < epsilon;

        private static void NormalizeConstantData(
            float[] cData, int cQ, int cV, int cF,
            float quatMin, float quatMax,
            float v3Min, float v3Max,
            float fltMin, float fltMax)
        {
            // Avoid division by zero on flat channels
            float qRng = !IsSimilar(quatMax - quatMin, 0f) ? 1f / (quatMax - quatMin) : 1f;
            float vRng = !IsSimilar(v3Max - v3Min, 0f) ? 1f / (v3Max - v3Min) : 1f;
            float fRng = !IsSimilar(fltMax - fltMin, 0f) ? 1f / (fltMax - fltMin) : 1f;

            int pos = 0;
            for (int i = 0; i < cQ * 4; i++, pos++) cData[pos] = (cData[pos] - quatMin) * qRng;
            for (int i = 0; i < cV * 3; i++, pos++) cData[pos] = (cData[pos] - v3Min) * vRng;
            for (int i = 0; i < cF; i++, pos++) cData[pos] = (cData[pos] - fltMin) * fRng;
        }

        private static float[] NormalizeAnimated(
            float[][][] quats, float[][][] vecs, float[][] floats,
            bool[] constCh, int frameCount,
            int totalQ, int totalV, int totalF, int aQ, int aV, int aF, int alignedStride,
            float[] qMinCh, float[] qMaxCh, float[] vMinCh, float[] vMaxCh, float[] fMinCh, float[] fMaxCh,
            CurveFitHelper cfHelper = null)
        {
            float[] norm = new float[frameCount * alignedStride];

            int qi = 0;
            for (int i = 0; i < totalQ; i++)
            {
                if (constCh[i]) continue;
                float mn = qMinCh[qi], rng = !IsSimilar(qMaxCh[qi] - mn, 0f) ? 1f / (qMaxCh[qi] - mn) : 1f;
                for (int f = 0; f < frameCount; f++)
                {
                    float[] q = quats[i][f];
                    int base_ = f * alignedStride + qi * 4;
                    norm[base_ + 0] = (q[0] - mn) * rng;
                    norm[base_ + 1] = (q[1] - mn) * rng;
                    norm[base_ + 2] = (q[2] - mn) * rng;
                    norm[base_ + 3] = (q[3] - mn) * rng;
                }
                qi++;
            }

            int vi = 0;
            for (int i = 0; i < totalV; i++)
            {
                if (constCh[totalQ + i]) continue;
                float mn = vMinCh[vi], rng = !IsSimilar(vMaxCh[vi] - mn, 0f) ? 1f / (vMaxCh[vi] - mn) : 1f;
                for (int f = 0; f < frameCount; f++)
                {
                    float vx, vy, vz;
                    if (cfHelper != null)
                        cfHelper.GetToDeltaChannelsVector3(vi, f, out vx, out vy, out vz);
                    else { float[] v_ = vecs[i][f]; vx = v_[0]; vy = v_[1]; vz = v_[2]; }
                    int base_ = f * alignedStride + aQ * 4 + vi * 3;
                    norm[base_ + 0] = (vx - mn) * rng;
                    norm[base_ + 1] = (vy - mn) * rng;
                    norm[base_ + 2] = (vz - mn) * rng;
                }
                vi++;
            }

            int fi = 0;
            for (int i = 0; i < totalF; i++)
            {
                if (constCh[totalQ + totalV + i]) continue;
                float mn = fMinCh[fi], rng = !IsSimilar(fMaxCh[fi] - mn, 0f) ? 1f / (fMaxCh[fi] - mn) : 1f;
                for (int f = 0; f < frameCount; f++)
                {
                    float fv = cfHelper != null ? cfHelper.GetToDeltaChannelFloat(fi, f) : floats[i][f];
                    norm[f * alignedStride + aQ * 4 + aV * 3 + fi] = (fv - mn) * rng;
                }
                fi++;
            }

            return norm;
        }

        private static float[] ForwardDCT(
            float[] norm, int frameCount, int alignedStride, int frameBlocks)
        {
            float[] dct = new float[frameBlocks * 8 * alignedStride];

            for (int fb = 0; fb < frameBlocks; fb++)
            {
                int startFrame;
                if (frameCount < 8) startFrame = 0;
                else if (fb * 8 + 8 <= frameCount) startFrame = fb * 8;
                else startFrame = frameCount - 8;

                float[] input = new float[8];
                for (int c = 0; c < alignedStride; c++)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        int frameIdx = (frameCount < 8)
                            ? (j < frameCount ? j : frameCount - 1)
                            : startFrame + j;
                        input[j] = norm[frameIdx * alignedStride + c] - 0.5f;
                    }

                    for (int k = 0; k < 8; k++)
                    {
                        float sum = 0f;
                        for (int j = 0; j < 8; j++) sum += input[j] * DctFwd[j, k];
                        dct[(fb * 8 + k) * alignedStride + c] = sum;
                    }
                }
            }

            return dct;
        }

        private static void GetDctMinMax(
            float[] dctData, int frameBlocks, int stride, int alignedStride,
            out float dctMin, out float dctMax)
        {
            dctMin = float.MaxValue; dctMax = float.MinValue;
            for (int fb = 0; fb < frameBlocks; fb++)
                for (int k = 0; k < 8; k++)
                    for (int c = 0; c < stride; c++)
                    {
                        float v = dctData[(fb * 8 + k) * alignedStride + c];
                        if (v < dctMin) dctMin = v;
                        if (v > dctMax) dctMax = v;
                    }

            if (dctMin == float.MaxValue) { dctMin = 0f; dctMax = 0f; }
        }

        private static float[,] InitQuantizationTables(float dctMin, float dctMax)
        {
            float scalar = Math.Max(Math.Abs(dctMin), Math.Abs(dctMax));
            if (scalar == 0f) scalar = 1f;

            float[,] t = new float[NumQTables, 8];
            for (int i = 0; i < NumQTables; i++)
            {
                float scale = (i + 1) * 0.2f;
                for (int k = 0; k < 8; k++)
                {
                    double ln = Math.Log(k + 2);
                    t[i, k] = (float)((1.0 + scalar * scale * ln) * scalar / 32768.0);
                }
            }
            return t;
        }

        private static void DetermineQuantTables(
            int fb, int frameCount, float[] dctData, float[] norm,
            float[,] qTables, float[] errBlock,
            int stride, int alignedStride, int aQ, int aV, bool sepTraj,
            bool linearSearchTables,
            out int rotTable, out int trjTable, out int traTable)
        {
            int Search(int offset, int count)
            {
                return linearSearchTables
                    ? FindBestQuantTableLinear(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, offset, count)
                    : FindBestQuantTable(fb, frameCount, dctData, norm, qTables, errBlock, stride, alignedStride, offset, count);
            }

            rotTable = Search(0, aQ * 4);

            int transCompOffset;
            int transCompCount;

            if (sepTraj)
            {
                trjTable = Search(aQ * 4, 4);
                transCompOffset = (aQ + 1) * 4;
                transCompCount = stride - transCompOffset;
                traTable = transCompCount > 0 ? Search(transCompOffset, transCompCount) : trjTable;
            }
            else
            {
                transCompOffset = aQ * 4;
                transCompCount = stride - transCompOffset;
                trjTable = traTable = transCompCount > 0 ? Search(transCompOffset, transCompCount) : 0;
            }
        }

        private static int FindBestQuantTable(
            int fb, int frameCount, float[] dctData, float[] norm,
            float[,] qTables, float[] errBlock,
            int stride, int alignedStride, int compOffset, int compCount)
        {
            if (compCount <= 0) return 0;

            int first = 0, d = NumQTables;
            while (d > 0)
            {
                int d2 = d >> 1;
                int mid = first + d2;
                if (TestQuantizationTable(fb, frameCount, dctData, norm, qTables, errBlock,
                        stride, alignedStride, compOffset, compCount, mid))
                {
                    first = mid + 1;
                    d -= d2 + 1;
                }
                else d = d2;
            }

            if (first > 0) first--;
            return first;
        }

        private static int FindBestQuantTableLinear(
            int fb, int frameCount, float[] dctData, float[] norm,
            float[,] qTables, float[] errBlock,
            int stride, int alignedStride, int compOffset, int compCount)
        {
            if (compCount <= 0) return 0;

            for (int i = NumQTables - 1; i >= 0; --i)
            {
                if (TestQuantizationTable(fb, frameCount, dctData, norm, qTables, errBlock,
                        stride, alignedStride, compOffset, compCount, i))
                {
                    return i;
                }
            }
            return 0;
        }

        private static bool TestQuantizationTable(
            int fb, int frameCount, float[] dctData, float[] norm,
            float[,] qTables, float[] errBlock,
            int stride, int alignedStride, int compOffset, int compCount, int tableIdx)
        {
            int startFrame;
            if (frameCount < 8) startFrame = 0;
            else if (fb * 8 + 8 <= frameCount) startFrame = fb * 8;
            else startFrame = frameCount - 8;

            int itr = Math.Min(8, frameCount);
            int compEnd = Math.Min(compOffset + compCount, stride);

            for (int c = compOffset; c < compEnd; c++)
            {
                float[] dequant = new float[8];
                for (int k = 0; k < 8; k++)
                {
                    float qt = qTables[tableIdx, k];
                    double qD = Math.Round(dctData[(fb * 8 + k) * alignedStride + c] / qt, MidpointRounding.AwayFromZero);
                    if (qD > 32767.0) qD = 32767.0;
                    if (qD < -32768.0) qD = -32768.0;
                    dequant[k] = (short)qD * qt;
                }

                for (int j = 0; j < itr; j++)
                {
                    float recon = 0.5f;
                    for (int k = 0; k < 8; k++) recon += dequant[k] * DctInv[j, k];
                    float original = norm[(startFrame + j) * alignedStride + c];
                    if (Math.Abs(recon - original) > errBlock[c]) return false;
                }
            }

            return true;
        }

        private static void QuantizeBlock(
            int fb, int frameCount, float[] dctData, float[,] qTables,
            short[] intermediate, int stride, int alignedStride,
            int aQ, int aV, bool sepTraj,
            int rotTable, int trjTable, int traTable, int numShorts)
        {
            int trajEnd = sepTraj ? aQ * 4 + 4 : -1;

            for (int c = 0; c < stride; c++)
            {
                int tableIdx;
                if (c < aQ * 4) tableIdx = rotTable;
                else if (sepTraj && c < trajEnd) tableIdx = trjTable;
                else tableIdx = traTable;

                for (int k = 0; k < 8; k++)
                {
                    float qt = qTables[tableIdx, k];
                    float dct = dctData[(fb * 8 + k) * alignedStride + c];
                    double qD = Math.Round(dct / qt, MidpointRounding.AwayFromZero);
                    if (qD > 32767.0) qD = 32767.0;
                    if (qD < -32768.0) qD = -32768.0;
                    intermediate[fb * numShorts + c * 8 + k + 3] = (short)qD;
                }
            }
        }

        private static int[] ComputeNumBits(
            short[] intermediate, int stride, int frameBlocks, int numShorts,
            out bool fastBitDecoder)
        {
            int[] maxAbs = new int[stride * 8];

            for (int fb = 0; fb < frameBlocks; fb++)
                for (int c = 0; c < stride; c++)
                    for (int k = 0; k < 8; k++)
                    {
                        int v = Math.Abs((int)intermediate[fb * numShorts + c * 8 + k + 3]);
                        int idx = c * 8 + k;
                        if (v > maxAbs[idx]) maxAbs[idx] = v;
                    }

            int[] numBits = new int[stride * 8];
            int maxBitsPerBlock = 0;

            for (int c = 0; c < stride; c++)
            {
                int bitsThisChannel = 0;
                for (int k = 0; k < 8; k++)
                {
                    int idx = c * 8 + k;
                    int mxAb = maxAbs[idx];
                    int nb = 0;
                    if (mxAb > 0)
                    {
                        bitsThisChannel += 2;
                        nb = (int)Math.Ceiling(Math.Log(mxAb + 0.5) / Math.Log(2.0));
                        bitsThisChannel += nb;
                    }
                    numBits[idx] = nb;
                }
                if (bitsThisChannel > maxBitsPerBlock) maxBitsPerBlock = bitsThisChannel;
            }

            fastBitDecoder = maxBitsPerBlock <= 128;
            return numBits;
        }

        private static float[] BuildConstantPalette(
            float[] cData, int cQ, int cV, int cF, string clipName)
        {
            int chCnt = cQ * 4 + cV * 3 + cF;
            if (chCnt == 0) return Array.Empty<float>();

            const float Epsilon = 4f * 1.1920929e-7f;
            var exactPalette = new List<float>();

            bool paletteOverflow = false;
            int i = 0;
            for (; i < chCnt; i++)
            {
                float v = cData[i];
                bool found = false;
                for (int k = 0; k < exactPalette.Count; k++)
                {
                    if (Math.Abs(v - exactPalette[k]) <= Epsilon)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    exactPalette.Add(v);
                    if (exactPalette.Count >= 4096 && i < chCnt - 1)
                    {
                        paletteOverflow = true;
                        break;
                    }
                }
            }

            if (!paletteOverflow && i == chCnt)
                return exactPalette.ToArray();

            // Fall back to a quantized 4096 entry uniform palette
            const int PaletteSize = 4096;
            float[] uniformPalette = new float[PaletteSize];
            for (int idx = 0; idx < PaletteSize; idx++)
                uniformPalette[idx] = (float)idx / (PaletteSize - 1);

            bool[] used = new bool[PaletteSize];
            for (int idx = 0; idx < chCnt; idx++)
            {
                int best = FindClosestPaletteIndex(cData[idx], uniformPalette);
                used[best] = true;
            }

            var compactPalette = new List<float>();
            for (int idx = 0; idx < PaletteSize; idx++)
                if (used[idx]) compactPalette.Add(uniformPalette[idx]);

            return compactPalette.ToArray();
        }

        private static int FindClosestPaletteIndex(float value, float[] palette)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                float d = Math.Abs(palette[i] - value);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        private static byte[] BitPackHeaderOnly(
            float[] cData, bool[] constCh, float[] constPalette,
            int totalQ, int totalV, int totalF,
            int cQ, int cV, int cF,
            int frameCount, bool cycle,
            out ushort keyTimeSize, out ushort constChanMapSize,
            out bool noChannelMap, out bool noKeyTimes)
        {
            var writer = new BitWriter();
            keyTimeSize = 0;
            noKeyTimes = true;
            WriteConstantChannelHeader(writer, cData, constCh, constPalette,
                totalQ, totalV, totalF, cQ, cV, cF,
                out constChanMapSize, out noChannelMap);
            return writer.ToArray();
        }

        private static byte[] BitPackAll(
            bool[] constCh, float[] cData, float[] constPalette,
            short[] intermediate, int[] numBits,
            int frameCount, int frameBlocks,
            int totalQ, int totalV, int totalF,
            int aQ, int aV, int aF,
            int stride, int alignedStride,
            bool sepTraj, bool cycle, bool bigEndian, int numShorts,
            byte[] vectorOffsets, byte[] floatOffsets,
            out ushort[] frameBlockSizes,
            out ushort keyTimeSize, out ushort constChanMapSize,
            out bool noKeyTimes, out bool noChannelMap)
        {
            var writer = new BitWriter();
            keyTimeSize = 0;
            noKeyTimes = true;
            WriteConstantChannelHeader(writer, cData, constCh, constPalette,
                totalQ, totalV, totalF,
                totalQ - aQ, totalV - aV, totalF - aF,
                out constChanMapSize, out noChannelMap);

            for (int c = 0; c < stride; c++)
                for (int k = 0; k < 8; k++)
                    writer.WriteBits((uint)numBits[c * 8 + k], 4);

            int vectorOffsetSize = 0;
            if (vectorOffsets != null)
            {
                vectorOffsetSize = vectorOffsets.Length;
                for (int i = 0; i < vectorOffsetSize; i++)
                    writer.WriteBits(vectorOffsets[i], 8);
            }

            int floatOffsetSize = 0;
            if (floatOffsets != null)
            {
                floatOffsetSize = floatOffsets.Length;
                for (int i = 0; i < floatOffsetSize; i++)
                    writer.WriteBits(floatOffsets[i], 8);
            }

            writer.Flush();

            int headerBytes = writer.ByteCount;
            int[] fbStart = new int[frameBlocks];
            frameBlockSizes = new ushort[frameBlocks];

            for (int fb = 0; fb < frameBlocks; fb++)
            {
                fbStart[fb] = writer.ByteCount;
                writer.ResetBitCount();

                writer.WriteBits((uint)(byte)intermediate[fb * numShorts + 0], 8);
                writer.WriteBits((uint)(byte)intermediate[fb * numShorts + 1], 8);
                writer.WriteBits((uint)(byte)intermediate[fb * numShorts + 2], 8);

                for (int c = 0; c < stride; c++)
                {
                    for (int k = 0; k < 8; k++)
                        if (numBits[c * 8 + k] > 0)
                            writer.WriteBits(intermediate[fb * numShorts + c * 8 + k + 3] != 0 ? 1u : 0u, 1);

                    for (int k = 0; k < 8; k++)
                    {
                        int nb = numBits[c * 8 + k];
                        if (nb > 0)
                        {
                            short v = intermediate[fb * numShorts + c * 8 + k + 3];
                            if (v != 0) writer.WriteBits(v > 0 ? 1u : 0u, 1);
                        }
                    }

                    for (int k = 0; k < 8; k++)
                    {
                        int nb = numBits[c * 8 + k];
                        if (nb > 0)
                        {
                            short v = intermediate[fb * numShorts + c * 8 + k + 3];
                            if (v != 0) writer.WriteBits((uint)Math.Abs((int)v), nb);
                        }
                    }
                }

                writer.Flush();
                frameBlockSizes[fb] = (ushort)(writer.ByteCount - fbStart[fb]);
            }

            byte[] result = writer.ToArray();

            if (!bigEndian)
            {
                int cQ_cnt = totalQ - aQ;
                int cV_cnt = totalV - aV;
                int cF_cnt = totalF - aF;
                int constDofCnt = cQ_cnt * 4 + cV_cnt * 3 + cF_cnt;
                bool use8Bit = constPalette.Length <= 256;

                int buggyOffset = keyTimeSize + constDofCnt + constChanMapSize + stride * 4 + vectorOffsetSize + floatOffsetSize;

                int leSwapBase = use8Bit ? headerBytes : buggyOffset;

                int curPos = leSwapBase;
                for (int fb = 0; fb < frameBlocks; fb++)
                {
                    int sz = frameBlockSizes[fb];
                    if (sz > 3)
                    {
                        int lo = curPos + 3, hi = curPos + sz - 1;
                        while (lo < hi && lo >= 0 && hi < result.Length)
                        {
                            byte tmp = result[lo]; result[lo] = result[hi]; result[hi] = tmp;
                            lo++; hi--;
                        }
                    }
                    curPos += sz;
                }
            }

            return result;
        }

        private static void WriteConstantChannelHeader(
            BitWriter writer,
            float[] cData, bool[] constCh, float[] constPalette,
            int totalQ, int totalV, int totalF,
            int cQ, int cV, int cF,
            out ushort constChanMapSize, out bool noChannelMap)
        {
            int chCnt = cQ * 4 + cV * 3 + cF;
            bool use8Bit = constPalette.Length <= 256;
            int idxBits = use8Bit ? 8 : 16;

            for (int i = 0; i < chCnt; i++)
                writer.WriteBits((uint)FindClosestPaletteIndex(cData[i], constPalette), idxBits);
            writer.Flush();

            int totalCh = totalQ + totalV + totalF;
            noChannelMap = chCnt == 0;
            constChanMapSize = 0;

            if (!noChannelMap)
            {
                bool curVal = false;
                int curCnt = 0;

                for (int i = 0; i < totalCh;)
                {
                    if (constCh[i] == curVal && curCnt < 255)
                    {
                        curCnt++; i++;
                    }
                    else
                    {
                        writer.WriteBits((uint)curCnt, 8);
                        constChanMapSize++;
                        curCnt = 0;
                        curVal = !curVal;
                    }
                }
                if (curCnt > 0) { writer.WriteBits((uint)curCnt, 8); constChanMapSize++; }
                writer.Flush();
            }
        }

        private static VbrAnimationAsset BuildResult(
            VbrAnimationAsset template,
            int frameCount,
            int aQ, int aV, int aF,
            int cQ, int cV, int cF,
            float quatMin, float quatMax,
            float tjMin, float tjMax,
            float v3Min, float v3Max,
            float fltMin, float fltMax,
            float dct,
            ushort constPaletteSize,
            ushort keyTimeSize,
            ushort constChanMapSize,
            bool noKeyTimes,
            bool noChannelMap,
            bool fastBitDecoder,
            bool cycle,
            bool sepTraj,
            int totalQ,
            bool trajConstant,
            float[] constPalette,
            ushort[] frameBlockSizes,
            byte[] data,
            byte[] vectorOffsets = null,
            byte[] floatOffsets = null,
            float vectorOffsetScale = 0f,
            float floatOffsetScale = 0f,
            ushort vectorOffsetSize = 0,
            ushort floatOffsetSize = 0)
        {
            ushort flags = 0;
            if (cycle) flags |= 1;
            if (noKeyTimes) flags |= 2;
            if (fastBitDecoder) flags |= 4;
            if (noChannelMap) flags |= 8;
            if (trajConstant) flags |= 128;

            var r = new VbrAnimationAsset();
            r.AssetType = template.AssetType;
            r.Bank = template.Bank;
            r.OrderedChannels = template.OrderedChannels;
            r.Channels = template.Channels;
            r.ChannelToDofAsset = template.ChannelToDofAsset;
            r.DofSetList = template.DofSetList;
            r.AnimId = template.AnimId;
            r.CodecType = template.CodecType;
            r.TrimOffset = template.TrimOffset;
            r.Additive = template.Additive;
            r.FPS = template.FPS;
            r.EndFrame = (ushort)(frameCount - 1);
            r.NumKeys = (ushort)frameCount;
            r.QuaternionCount = (ushort)aQ;
            r.Vector3Count = (ushort)aV;
            r.NumFloat = (ushort)aF;
            r.ConstQuaternionCount = (ushort)cQ;
            r.ConstVector3Count = (ushort)cV;
            r.ConstFloatCount = (ushort)cF;
            r.QuatMin = quatMin; r.QuatMax = quatMax;
            r.TrajMin = tjMin; r.TrajMax = tjMax;
            r.Vec3Min = v3Min; r.Vec3Max = v3Max;
            r.FloatMin = fltMin; r.FloatMax = fltMax;
            r.Dct = dct;
            r.KeyTimeSize = keyTimeSize;
            r.ConstChanMapSize = constChanMapSize;
            r.ConstPaletteSize = constPaletteSize;
            r.VectorOffsetScale = vectorOffsetScale;
            r.FloatOffsetScale = floatOffsetScale;
            r.VectorOffsetSize = vectorOffsetSize;
            r.FloatOffsetSize = floatOffsetSize;
            r.VectorOffsets = vectorOffsets;
            r.Flags = flags;
            r.ConstantPalette = constPalette;
            r.FrameBlockSizes = frameBlockSizes;
            r.Data = data;

            var rd = template.RawData != null
                ? new Dictionary<string, object>(template.RawData)
                : new Dictionary<string, object>();

            rd["QuaternionCount"] = (ushort)aQ;
            rd["Vector3Count"] = (ushort)aV;
            rd["FloatCount"] = (ushort)aF;
            rd["ConstQuaternionCount"] = (ushort)cQ;
            rd["ConstVector3Count"] = (ushort)cV;
            rd["ConstFloatCount"] = (ushort)cF;
            rd["NumKeys"] = (ushort)frameCount;
            rd["EndFrame"] = (ushort)(frameCount - 1);
            rd["KeyTimeSize"] = keyTimeSize;
            rd["ConstChanMapSize"] = constChanMapSize;
            rd["ConstPaletteSize"] = constPaletteSize;
            rd["VectorOffsetSize"] = vectorOffsetSize;
            rd["FloatOffsetSize"] = floatOffsetSize;
            rd["Flags"] = flags;
            rd["QuatMin"] = quatMin; rd["QuatMax"] = quatMax;
            rd["TrajMin"] = tjMin; rd["TrajMax"] = tjMax;
            rd["Vec3Min"] = v3Min; rd["Vec3Max"] = v3Max;
            rd["FloatMin"] = fltMin; rd["FloatMax"] = fltMax;
            rd["Dct"] = dct;
            rd["VectorOffsetScale"] = vectorOffsetScale;
            rd["FloatOffsetScale"] = floatOffsetScale;
            rd["VectorOffsets"] = vectorOffsets;
            rd["FloatOffsets"] = floatOffsets;
            rd["ConstantPalette"] = constPalette;
            rd["FrameBlockSizes"] = frameBlockSizes;
            rd["Data"] = data;

            r.RawData = rd;
            return r;
        }

        /// <summary>
        /// Computes piecewise linear offset curves for non-constant channels to reduce DCT dynamic range.
        /// </summary>
        private sealed class CurveFitHelper
        {
            private struct CurvePoint { public uint X; public float Y; }

            private readonly float[][][] _allVecs;
            private readonly float[][] _allFloats;
            private readonly bool[] _constCh;
            private readonly int _totalQ, _totalV, _totalF;
            private readonly int _mVectors;
            private readonly int _mFloats;
            private readonly int _frameCount;

            private float[] _minChannels;
            private float[] _maxChannels;
            private float[] _minDeltaChannels;
            private float[] _maxDeltaChannels;
            private float[] _usedEpsilon;
            private float[] _deltaChannel;
            private float[] _toDeltaChannels;
            private List<CurvePoint>[] _deltaCurves;

            public float VectorOffsetScale;
            public float FloatOffsetScale;
            public ushort VectorOffsetSize;
            public ushort FloatOffsetSize;
            public byte[] VectorOffsets;
            public byte[] FloatOffsets;

            public CurveFitHelper(float[][][] vecs, float[][] floats, bool[] constCh,
                int totalQ, int totalV, int totalF, int frameCount)
            {
                _allVecs = vecs;
                _allFloats = floats;
                _constCh = constCh;
                _totalQ = totalQ;
                _totalV = totalV;
                _totalF = totalF;
                _frameCount = frameCount;

                int nConstV = 0;
                for (int i = 0; i < totalV; i++)
                    if (constCh[totalQ + i]) nConstV++;

                int nConstF = 0;
                for (int i = 0; i < totalF; i++)
                    if (constCh[totalQ + totalV + i]) nConstF++;

                _mVectors = totalV - nConstV;
                _mFloats = totalF - nConstF;
            }

            public float GetToDeltaChannelFloat(int floatIdx, int frame)
            {
                int stride = _mVectors * 3 + _mFloats;
                return _toDeltaChannels[stride * frame + _mVectors * 3 + floatIdx];
            }

            public void GetToDeltaChannelsVector3(int vecIdx, int frame,
                out float x, out float y, out float z)
            {
                int stride = _mVectors * 3 + _mFloats;
                int b = stride * frame + vecIdx * 3;
                x = _toDeltaChannels[b + 0];
                y = _toDeltaChannels[b + 1];
                z = _toDeltaChannels[b + 2];
            }

            // Curve Fitting
            public void DoCurveFitting()
            {
                int stride = _mVectors * 3 + _mFloats;
                if (stride == 0) return;

                _minChannels = new float[stride];
                _maxChannels = new float[stride];
                _minDeltaChannels = new float[stride];
                _maxDeltaChannels = new float[stride];
                _usedEpsilon = new float[stride];
                _deltaChannel = new float[_frameCount];
                _toDeltaChannels = new float[stride * _frameCount];
                _deltaCurves = new List<CurvePoint>[stride];

                for (int i = 0; i < stride; i++)
                {
                    _minChannels[i] = float.MaxValue;
                    _maxChannels[i] = -float.MaxValue;
                }

                // Fill toDeltaChannels from non-constant vec3 channels
                int idx = 0;
                for (int i = 0; i < _totalV; i++)
                {
                    if (_constCh[_totalQ + i]) continue;
                    for (int j = 0; j < _frameCount; j++)
                    {
                        float[] v = _allVecs[i][j];
                        int b = stride * j + idx * 3;
                        _toDeltaChannels[b + 0] = v[0];
                        _toDeltaChannels[b + 1] = v[1];
                        _toDeltaChannels[b + 2] = v[2];
                        if (v[0] < _minChannels[idx * 3 + 0]) _minChannels[idx * 3 + 0] = v[0];
                        if (v[0] > _maxChannels[idx * 3 + 0]) _maxChannels[idx * 3 + 0] = v[0];
                        if (v[1] < _minChannels[idx * 3 + 1]) _minChannels[idx * 3 + 1] = v[1];
                        if (v[1] > _maxChannels[idx * 3 + 1]) _maxChannels[idx * 3 + 1] = v[1];
                        if (v[2] < _minChannels[idx * 3 + 2]) _minChannels[idx * 3 + 2] = v[2];
                        if (v[2] > _maxChannels[idx * 3 + 2]) _maxChannels[idx * 3 + 2] = v[2];
                    }
                    idx++;
                }

                // Fill toDeltaChannels from non-constant float channels
                idx = 0;
                for (int i = 0; i < _totalF; i++)
                {
                    if (_constCh[_totalQ + _totalV + i]) continue;
                    for (int j = 0; j < _frameCount; j++)
                    {
                        float fv = _allFloats[i][j];
                        _toDeltaChannels[stride * j + _mVectors * 3 + idx] = fv;
                        if (fv < _minChannels[_mVectors * 3 + idx]) _minChannels[_mVectors * 3 + idx] = fv;
                        if (fv > _maxChannels[_mVectors * 3 + idx]) _maxChannels[_mVectors * 3 + idx] = fv;
                    }
                    idx++;
                }

                Array.Copy(_minChannels, _minDeltaChannels, stride);
                Array.Copy(_maxChannels, _maxDeltaChannels, stride);

                for (int i = 0; i < stride; i++)
                    FirstPassCurveFit(i);

                bool keepFitting = true;
                while (keepFitting)
                {
                    keepFitting = false;
                    for (int i = 0; i < _mVectors * 3; i++)
                        keepFitting |= SecondPassCurveFit(i, 0, _mVectors * 3);
                    for (int i = 0; i < _mFloats; i++)
                        keepFitting |= SecondPassCurveFit(_mVectors * 3 + i, _mVectors * 3, _mFloats);
                }

                VectorOffsetScale = 0f;
                uint numVCurves = 0, numVPoints = 0;
                FindOffsetScale(0, _mVectors * 3, ref VectorOffsetScale, ref numVCurves, ref numVPoints);
                ReduceQualityAndCompressCurve(0, _mVectors * 3, true,
                    VectorOffsetScale, numVCurves, numVPoints,
                    out VectorOffsetSize, out VectorOffsets);

                FloatOffsetScale = 0f;
                uint numFCurves = 0, numFPoints = 0;
                FindOffsetScale(_mVectors * 3, _mFloats, ref FloatOffsetScale, ref numFCurves, ref numFPoints);
                ReduceQualityAndCompressCurve(_mVectors * 3, _mFloats, false,
                    FloatOffsetScale, numFCurves, numFPoints,
                    out FloatOffsetSize, out FloatOffsets);

                for (int i = 0; i < stride; i++)
                {
                    if (_deltaCurves[i] != null)
                        SubtractDelta(i, _deltaCurves[i]);
                }
            }

            private void FirstPassCurveFit(int ch)
            {
                const float Eps = 1.1920929e-7f;
                _usedEpsilon[ch] = 0.1f;

                var pts = new List<CurvePoint>();
                ComputeSimplified(ch, _usedEpsilon[ch], pts);
                CalculateDelta(ch, pts);

                float mn = float.MaxValue, mx = -float.MaxValue;
                for (int i = 0; i < _frameCount; i++)
                {
                    if (_deltaChannel[i] < mn) mn = _deltaChannel[i];
                    if (_deltaChannel[i] > mx) mx = _deltaChannel[i];
                }

                float newRange = Math.Abs(mx - mn);
                float oldRange = Math.Abs(_maxChannels[ch] - _minChannels[ch]);
                float newAvg = Math.Abs(mx + mn) / 2f;
                float oldAvg = Math.Abs(_maxChannels[ch] + _minChannels[ch]) / 2f;

                bool rangeImproved = (newRange > Eps)
                    ? (oldRange / newRange) > 2f
                    : Math.Abs(oldRange - newRange) > 1f;
                bool deltaImproved = (Math.Abs(oldAvg) - Math.Abs(newAvg)) > 0.75f;

                // Keep at least one curve every 191 channels to avoid byte-range index overflow in decoding
                if (rangeImproved || deltaImproved || ch % 191 == 0)
                {
                    _minDeltaChannels[ch] = mn;
                    _maxDeltaChannels[ch] = mx;
                    _deltaCurves[ch] = pts;
                }
            }

            private bool SecondPassCurveFit(int ch, int groupStart, int groupCount)
            {
                const float Eps = 1.1920929e-7f;
                if (_deltaCurves[ch] == null) return false;

                float mn = float.MaxValue, mx = -float.MaxValue;
                for (int i = groupStart; i < groupStart + groupCount; i++)
                {
                    if (i == ch) continue;
                    if (_minDeltaChannels[i] < mn) mn = _minDeltaChannels[i];
                    if (_maxDeltaChannels[i] > mx) mx = _maxDeltaChannels[i];
                }

                float minPre = Math.Min(mn, _minChannels[ch]);
                float maxPre = Math.Max(mx, _maxChannels[ch]);
                float minPost = Math.Min(mn, _minDeltaChannels[ch]);
                float maxPost = Math.Max(mx, _maxDeltaChannels[ch]);
                float rangePre = maxPre - minPre;
                float rangePost = maxPost - minPost;

                bool keepDelta = false;
                if (rangePre > Eps)
                {
                    keepDelta = (rangePre - rangePost) / rangePre > 0.2f;
                    keepDelta |= (ch % 191 == 0);
                }

                if (keepDelta)
                {
                    bool optimized = true;
                    float prevEps = _usedEpsilon[ch];
                    float prevRange = rangePost;
                    float optMin = float.MaxValue, optMax = -float.MaxValue;
                    var optPts = new List<CurvePoint>();

                    if (prevRange > Eps)
                    {
                        while (optimized && prevEps < 100f)
                        {
                            prevEps *= 1.1f;
                            var newPts = new List<CurvePoint>();
                            ComputeSimplified(ch, prevEps, newPts);
                            CalculateDelta(ch, newPts);

                            float dMin = float.MaxValue, dMax = -float.MaxValue;
                            for (int i = 0; i < _frameCount; i++)
                            {
                                if (_deltaChannel[i] < dMin) dMin = _deltaChannel[i];
                                if (_deltaChannel[i] > dMax) dMax = _deltaChannel[i];
                            }

                            float tMin = Math.Min(mn, dMin);
                            float tMax = Math.Max(mx, dMax);
                            float optRange = tMax - tMin;
                            optimized = (optRange - prevRange) / prevRange < 0.2f;

                            if (optimized)
                            {
                                bool setIt = newPts.Count < _deltaCurves[ch].Count;
                                if (optPts.Count != 0) setIt = newPts.Count < optPts.Count;
                                if (setIt)
                                {
                                    optPts = new List<CurvePoint>(newPts);
                                    optMin = dMin; optMax = dMax;
                                    if (optPts.Count == 1) optimized = false;
                                }
                            }
                        }
                    }

                    if (optPts.Count > 0 && _deltaCurves[ch].Count > optPts.Count)
                    {
                        _deltaCurves[ch] = optPts;
                        _minDeltaChannels[ch] = optMin;
                        _maxDeltaChannels[ch] = optMax;
                    }
                    return false;
                }
                else
                {
                    _deltaCurves[ch] = null;
                    _minDeltaChannels[ch] = _minChannels[ch];
                    _maxDeltaChannels[ch] = _maxChannels[ch];
                    _usedEpsilon[ch] = 0f;
                    return true;
                }
            }

            private void FindOffsetScale(int offset, int count,
                ref float scale, ref uint numCurves, ref uint numPoints)
            {
                scale = 0f; numCurves = 0; numPoints = 0;
                for (int i = 0; i < count; i++)
                {
                    var cv = _deltaCurves[offset + i];
                    if (cv == null) continue;
                    numCurves++;
                    for (int j = 0; j < cv.Count; j++)
                    {
                        numPoints++;
                        float a = Math.Abs(cv[j].Y);
                        if (a > scale) scale = a;
                    }
                }
            }

            // Binary layout:
            // [0]       NumCurves
            // For each curve:
            //   [+0]    DeltaChannelIndex
            //   [+1]    PointCount
            //   [+2]    InitialValue (Y0)
            //   For each subsequent point:
            //     [+0]  DeltaTick (X >> 3)
            //     [+1]  Value (Y)
            private void ReduceQualityAndCompressCurve(int offset, int count,
                bool aRecalculate, float scale,
                uint numCurves, uint numPoints,
                out ushort outSize, out byte[] outBytes)
            {
                outSize = 0;
                outBytes = null;
                if (numCurves == 0) return;

                outSize = (ushort)(1 + numCurves * 3 + (numPoints - numCurves) * 2);
                outBytes = new byte[outSize];

                int w = 0;
                uint prevIdx = 0;
                outBytes[w++] = (byte)numCurves;

                for (int i = 0; i < count; i++)
                {
                    var cv = _deltaCurves[offset + i];
                    if (cv == null) continue;

                    uint recalced = (uint)i;
                    if (aRecalculate)
                    {
                        recalced = (uint)(i / 3) * 4;
                        recalced += (uint)(i % 3);
                    }
                    outBytes[w++] = (byte)(recalced - prevIdx);
                    prevIdx = recalced;
                    outBytes[w++] = (byte)cv.Count;

                    uint prevX = 0;
                    for (int j = 0; j < cv.Count; j++)
                    {
                        uint x = cv[j].X;
                        uint deltaX = x - prevX;
                        prevX = x;

                        if (j != 0)
                            outBytes[w++] = (byte)(deltaX >> 3);

                        sbyte b = (sbyte)(scale != 0f ? cv[j].Y / scale * 127f : 0f);
                        outBytes[w++] = (byte)b;

                        var pt = cv[j];
                        pt.Y = (float)b / 127f * scale;
                        cv[j] = pt;
                    }
                }
            }

            private void CalculateDelta(int ch, List<CurvePoint> pts)
            {
                int stride = _mVectors * 3 + _mFloats;
                var sf = new SegmentFinder();
                for (int i = 0; i < _frameCount; i++)
                    _deltaChannel[i] = sf.Delta((uint)i, _toDeltaChannels[stride * i + ch], pts);
            }

            private void SubtractDelta(int ch, List<CurvePoint> pts)
            {
                int stride = _mVectors * 3 + _mFloats;
                var sf = new SegmentFinder();
                for (int i = 0; i < _frameCount; i++)
                    _toDeltaChannels[stride * i + ch] =
                        sf.Delta((uint)i, _toDeltaChannels[stride * i + ch], pts);
            }

            private void ComputeSimplified(int ch, float eps, List<CurvePoint> pts)
            {
                int stride = _mVectors * 3 + _mFloats;
                var binner = new Binner(ch, 8u, _frameCount, _toDeltaChannels, stride);

                binner.GetInitial(out uint prevX, out float prevY);
                pts.Add(new CurvePoint { X = prevX, Y = prevY });

                binner.GetCurrent(out uint curX, out float curY);
                var stab = new StabbingSet(prevX, prevY, curX, curY, eps);

                while (!binner.AtEnd())
                {
                    binner.GetCurrent(out curX, out curY);
                    if (!stab.Add(curX, curY))
                    {
                        var tail = stab.GetTail();
                        pts.Add(tail);
                        stab = new StabbingSet(tail.X, tail.Y, curX, curY, eps);
                    }
                }

                if (pts.Count >= 2)
                {
                    var pre = pts[pts.Count - 2];
                    var last = pts[pts.Count - 1];
                    if (Math.Abs(pre.Y - last.Y) < eps && (pre.X + last.X < 255u * 8u))
                        pts.RemoveAt(pts.Count - 1);
                }

                if (pts.Count >= 2)
                {
                    var last = pts[pts.Count - 1];
                    if ((last.X % 8) != 0)
                    {
                        var pre = pts[pts.Count - 2];
                        uint dx = last.X - pre.X;
                        float slope = (last.Y - pre.Y) / (float)dx;
                        uint addX = 8u - (dx % 8u);
                        pts[pts.Count - 1] = new CurvePoint
                        {
                            X = last.X + addX,
                            Y = last.Y + (float)addX * slope
                        };
                    }
                }
            }

            private sealed class Binner
            {
                private readonly int _ch;
                private uint _cur;
                private readonly uint _half;
                private readonly uint _bin;
                private readonly uint _frameCount;
                private readonly uint _stride;
                private readonly float[] _data;

                public Binner(int ch, uint binSize, int frameCount, float[] data, int stride)
                {
                    _ch = ch;
                    _cur = 0u;
                    _half = binSize / 2u;
                    _bin = binSize;
                    _frameCount = (uint)frameCount;
                    _stride = (uint)stride;
                    _data = data;
                }

                public void GetInitial(out uint pos, out float val)
                {
                    pos = 0u;
                    val = _data[_ch];
                }

                public void GetCurrent(out uint pos, out float val)
                {
                    uint remaining = _frameCount - _cur;
                    if (remaining < _bin * 2u)
                    {
                        if (remaining < _bin)
                        {
                            _cur = _frameCount - 1u;
                            pos = _cur;
                            val = _data[_stride * _cur + (uint)_ch];
                        }
                        else
                        {
                            float acc = 0f;
                            uint start = _cur + _half;
                            for (uint i = start; i < _frameCount; i++)
                                acc += _data[_stride * i + (uint)_ch];
                            _cur = _frameCount - 1u;
                            pos = _cur;
                            val = acc / (float)(_frameCount - start);
                        }
                    }
                    else
                    {
                        uint start = _cur + _half;
                        float acc = 0f;
                        for (uint i = start; i < start + _bin; i++)
                            acc += _data[_stride * i + (uint)_ch];
                        _cur += _bin;
                        pos = _cur;
                        val = acc / (float)_bin;
                    }
                }

                public bool AtEnd() => _cur >= _frameCount - 1u;
            }

            private struct StabbingSet
            {
                private float _eps;
                private CurvePoint _tail;
                private CurvePoint _pivot;
                private float _uX, _uY;
                private float _lX, _lY;

                public StabbingSet(uint originX, float originY,
                                   uint initX, float initY, float eps)
                {
                    _eps = eps;
                    _tail = new CurvePoint { X = initX, Y = initY };

                    uint nX = initX - originX;
                    float nY = initY - originY;
                    _uX = (nY + 2f * eps) / (float)nX;
                    _uY = -eps;
                    _lX = (nY - 2f * eps) / (float)nX;
                    _lY = eps;

                    if (eps > 0f)
                    {
                        float crossX = (2f * eps) / (_uX - _lX);
                        _pivot = new CurvePoint
                        {
                            X = (uint)(originX + crossX),
                            Y = originY + crossX * _uX + _uY
                        };
                    }
                    else
                    {
                        _pivot = _tail;
                    }
                }

                public bool Add(uint x, float y)
                {
                    uint nXu = x - _pivot.X;
                    float nXf = (float)nXu;
                    float nY = y - _pivot.Y;

                    float upper = nXf * _uX + _uY;
                    float lower = nXf * _lX + _lY;

                    bool withinReach = nXu < 255u * 8u;
                    bool withinUpper = (nY - _eps) <= upper;
                    bool withinLower = (nY + _eps) >= lower;
                    bool stabbable = withinUpper && withinLower && withinReach;

                    if (stabbable)
                    {
                        if ((nY + _eps) < upper) { _uX = (nY + _eps) / nXf; _uY = 0f; }
                        if ((nY - _eps) > lower) { _lX = (nY - _eps) / nXf; _lY = 0f; }
                        _tail = new CurvePoint { X = x, Y = y };
                    }
                    return stabbable;
                }

                public CurvePoint GetTail() => _tail;
            }

            private struct SegmentFinder
            {
                private int _cur;

                public float Delta(uint x, float y, List<CurvePoint> pts)
                {
                    while (_cur != pts.Count - 1 && pts[_cur + 1].X < x)
                        _cur++;

                    if (_cur != pts.Count - 1)
                    {
                        var right = pts[_cur + 1];
                        var left = pts[_cur];
                        uint normX = x - left.X;
                        float span = (float)(right.X - left.X);
                        float interp = (right.Y - left.Y) / span * (float)normX + left.Y;
                        return y - interp;
                    }
                    else
                    {
                        return y - pts[_cur].Y;
                    }
                }
            }
        }

        // BitWriter
        private sealed class BitWriter
        {
            private readonly List<byte> _buf = new List<byte>(4096);
            private byte _bucket;
            private int _stored;
            private int _byteCountAtReset;

            public int ByteCount => _buf.Count;

            public void ResetBitCount() => _byteCountAtReset = _buf.Count;

            public int BitsSinceReset => (_buf.Count - _byteCountAtReset) * 8;

            public void WriteBits(uint value, int bits)
            {
                while (bits > 0)
                {
                    int left = 8 - _stored;
                    int take = bits < left ? bits : left;
                    uint mask = (1u << take) - 1u;
                    _bucket |= (byte)((value & mask) << _stored);
                    value >>= take;
                    _stored += take;
                    bits -= take;
                    if (_stored == 8) FlushInner();
                }
            }

            public void Flush() { if (_stored != 0) FlushInner(); }

            private void FlushInner()
            {
                _buf.Add(_bucket);
                _bucket = 0;
                _stored = 0;
            }

            public byte[] ToArray() => _buf.ToArray();
        }
    }
}
