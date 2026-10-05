using AssetBankPlugin.Export;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AssetBankPlugin.Ant
{
    public partial class VbrAnimationAsset : AnimationAsset
    {
        public byte[] Data = new byte[0];
        public ushort NumKeys;
        public ushort NumVec3;
        public ushort NumFloat;
        public ushort ConstFloatCount;
        public int DataSize;
        public bool Cycle;
        public float QuatMin;
        public float TrajMin;
        public float Vec3Min;
        public float FloatMin;
        public float QuatMax;
        public float TrajMax;
        public float Vec3Max;
        public float FloatMax;
        public float VectorOffsetScale;
        public float FloatOffsetScale;
        public float Dct;
        public ushort NumQuats;
        public ushort NumFloatVec;
        public ushort ConstChanMapSize;
        public ushort QuaternionCount;
        public ushort ConstPaletteSize;
        public ushort Vector3Count;
        public ushort ConstQuaternionCount;
        public ushort ConstVector3Count;
        public ushort VectorOffsetSize;
        public ushort FloatOffsetSize;
        public ushort CodecTypeID;
        public ushort EndFrame2;
        public ushort Flags;
        public int offset;
        public int FrameBlockSize;
        public ushort[] PaletteIndexes;
        public byte[] ConstChanMap;
        public byte[] VectorOffsets;
        public byte QuantizeMultSubblock;
        public byte CatchAllBitCount;
        public float[] ConstantPalette;
        public ushort[] FrameBlockSizes;

        public short[] DeltaBaseX;
        public short[] DeltaBaseY;
        public short[] DeltaBaseZ;
        public short[] DeltaBaseW;
        public ushort[] BitsPerSubblock;

        public ushort[] KeyTimes;
        public ushort QuantizeMultBlock;

        public ushort KeyTimeSize;

        private List<Vector4> DecompressedData;
        private const ushort eNoChannelMap = 8;

        public VbrAnimationAsset() { }

        public void InvalidateDecompressedCache()
        {
            DecompressedData = null;
        }

        public override void SetData(Dictionary<string, object> data)
        {
            InvalidateDecompressedCache();
            ParseBasicData(data);

            if (data.TryGetValue("QuatMin", out object qmin)) QuatMin = Convert.ToSingle(qmin);
            if (data.TryGetValue("TrajMin", out object tmin)) TrajMin = Convert.ToSingle(tmin);
            if (data.TryGetValue("Vec3Min", out object vmin)) Vec3Min = Convert.ToSingle(vmin);
            if (data.TryGetValue("FloatMin", out object fmin)) FloatMin = Convert.ToSingle(fmin);
            if (data.TryGetValue("QuatMax", out object qmax)) QuatMax = Convert.ToSingle(qmax);
            if (data.TryGetValue("TrajMax", out object tmax)) TrajMax = Convert.ToSingle(tmax);
            if (data.TryGetValue("Vec3Max", out object vmax)) Vec3Max = Convert.ToSingle(vmax);
            if (data.TryGetValue("FloatMax", out object fmax)) FloatMax = Convert.ToSingle(fmax);
            if (data.TryGetValue("VectorOffsetScale", out object vos)) VectorOffsetScale = Convert.ToSingle(vos);
            if (data.TryGetValue("FloatOffsetScale", out object fos)) FloatOffsetScale = Convert.ToSingle(fos);
            if (data.TryGetValue("Dct", out object dct)) Dct = Convert.ToSingle(dct);
            if (data.TryGetValue("QuaternionCount", out object qc)) QuaternionCount = Convert.ToUInt16(qc);
            if (data.TryGetValue("Vector3Count", out object v3c)) Vector3Count = Convert.ToUInt16(v3c);
            if (data.TryGetValue("ConstQuaternionCount", out object cqc)) ConstQuaternionCount = Convert.ToUInt16(cqc);
            if (data.TryGetValue("ConstVector3Count", out object cv3c)) ConstVector3Count = Convert.ToUInt16(cv3c);
            if (data.TryGetValue("NumKeys", out object nk)) NumKeys = Convert.ToUInt16(nk);
            if (data.TryGetValue("ConstChanMapSize", out object ccms)) ConstChanMapSize = Convert.ToUInt16(ccms);
            if (data.TryGetValue("ConstPaletteSize", out object cps)) ConstPaletteSize = Convert.ToUInt16(cps);
            if (data.TryGetValue("VectorOffsetSize", out object vsz)) VectorOffsetSize = Convert.ToUInt16(vsz);
            if (data.TryGetValue("FloatOffsetSize", out object fsz)) FloatOffsetSize = Convert.ToUInt16(fsz);
            if (data.TryGetValue("Flags", out object flg)) Flags = Convert.ToUInt16(flg);
            if (data.TryGetValue("ConstantPalette", out object cp)) ConstantPalette = ConvertArray<float>(cp);
            if (data.TryGetValue("FrameBlockSizes", out object fbs)) FrameBlockSizes = ConvertArray<ushort>(fbs);
            if (data.TryGetValue("Data", out object d)) Data = ConvertArray<byte>(d);

            // Parse KeyTimeSize
            if (data.TryGetValue("KeyTimeSize", out object kts))
                KeyTimeSize = Convert.ToUInt16(kts);
            else
                KeyTimeSize = 0;   // default for assets missing the field

            if (FrameBlockSizes != null) FrameBlockSize = FrameBlockSizes.Length;
            if (data.TryGetValue("FloatCount", out object fc)) NumFloat = Convert.ToUInt16(fc);
            else if (data.TryGetValue("NumFloat", out object nf)) NumFloat = Convert.ToUInt16(nf);
            if (data.TryGetValue("ConstFloatCount", out object cfc)) ConstFloatCount = Convert.ToUInt16(cfc);

            base.SetData(data);
        }

        public override InternalAnimation ConvertToInternal()
        {
            try
            {
                if (DecompressedData == null || DecompressedData.Count == 0)
                    DecompressedData = Decompress();

                var ret = new InternalAnimation();

                List<ChannelInfo> orderedChannels = OrderedChannels;
                if (orderedChannels == null || orderedChannels.Count == 0)
                    return ret;

                // Separate channel names for the exporter
                List<string> posChannels = new List<string>();
                List<string> rotChannels = new List<string>();
                List<string> scaleChannels = new List<string>();
                foreach (var ch in orderedChannels)
                {
                    if (ch.Type == BoneChannelType.Rotation) rotChannels.Add(ch.Name);
                    else if (ch.Type == BoneChannelType.Position) posChannels.Add(ch.Name);
                    else if (ch.Type == BoneChannelType.Scale) scaleChannels.Add(ch.Name);
                }

                int total = orderedChannels.Count;
                int[] globalToLocalRot = new int[total];
                int[] globalToLocalPos = new int[total];
                int[] globalToLocalScale = new int[total];
                for (int i = 0; i < total; i++)
                {
                    globalToLocalRot[i] = -1;
                    globalToLocalPos[i] = -1;
                    globalToLocalScale[i] = -1;
                }
                int localRot = 0, localPos = 0, localScale = 0;
                for (int g = 0; g < total; g++)
                {
                    switch (orderedChannels[g].Type)
                    {
                        case BoneChannelType.Rotation: globalToLocalRot[g] = localRot++; break;
                        case BoneChannelType.Position: globalToLocalPos[g] = localPos++; break;
                        case BoneChannelType.Scale: globalToLocalScale[g] = localScale++; break;
                    }
                }

                int dofCount = QuaternionCount + Vector3Count + NumFloat;

                // Precompute default (bind pose) values
                Quaternion[] defaultRot = new Quaternion[rotChannels.Count];
                Vector3[] defaultPos = new Vector3[posChannels.Count];
                Vector3[] defaultScale = new Vector3[scaleChannels.Count];

                for (int i = 0; i < total; i++)
                {
                    var ch = orderedChannels[i];
                    if (ch.Type == BoneChannelType.Rotation)
                    {
                        int local = globalToLocalRot[i];
                        if (local == -1) continue;
                        if (Additive)
                            defaultRot[local] = Quaternion.Identity;
                        else if (TryGetRigDefaultVector4(ch.DofId, out var v4))
                            defaultRot[local] = Quaternion.Normalize(new Quaternion(v4.X, v4.Y, v4.Z, v4.W));
                        else
                            defaultRot[local] = Quaternion.Identity;
                    }
                    else if (ch.Type == BoneChannelType.Position)
                    {
                        int local = globalToLocalPos[i];
                        if (local == -1) continue;
                        if (Additive)
                            defaultPos[local] = Vector3.Zero;
                        else if (TryGetRigDefaultVector3(ch.DofId, out var v3))
                            defaultPos[local] = v3;
                        else
                            defaultPos[local] = Vector3.Zero;
                    }
                    else if (ch.Type == BoneChannelType.Scale)
                    {
                        int local = globalToLocalScale[i];
                        if (local == -1) continue;
                        if (Additive)
                            defaultScale[local] = Vector3.Zero;
                        else if (TryGetRigDefaultVector3(ch.DofId, out var v3))
                            defaultScale[local] = v3;
                        else
                            defaultScale[local] = Vector3.One;
                    }
                }

                // eNoChannelMap fast path
                if ((Flags & eNoChannelMap) != 0)
                {
                    Console.WriteLine($"[VBR] eNoChannelMap active - direct mapping.");

                    List<int> quatOrdered = new List<int>();
                    List<int> vecOrdered = new List<int>();
                    for (int i = 0; i < total; i++)
                    {
                        var ch = orderedChannels[i];
                        if (ch.Type == BoneChannelType.Rotation) quatOrdered.Add(i);
                        else if (ch.Type == BoneChannelType.Position || ch.Type == BoneChannelType.Scale) vecOrdered.Add(i);
                    }

                    ret.Frames = new List<Frame>(NumKeys);
                    for (int frameIdx = 0; frameIdx < NumKeys; frameIdx++)
                    {
                        Frame frame = new Frame();
                        frame.Rotations = defaultRot.ToList();
                        frame.Positions = defaultPos.ToList();
                        frame.Scales = defaultScale.ToList();

                        int dataOffset = frameIdx * dofCount;

                        // Quaternions
                        for (int i = 0; i < QuaternionCount && i < quatOrdered.Count; i++)
                        {
                            int orderedIdx = quatOrdered[i];
                            var elem = DecompressedData[dataOffset + i];
                            int localIdx = globalToLocalRot[orderedIdx];
                            if (localIdx != -1 && localIdx < frame.Rotations.Count)
                                frame.Rotations[localIdx] = Quaternion.Normalize(new Quaternion(elem.X, elem.Y, elem.Z, elem.W));
                        }

                        // Vectors
                        for (int i = 0; i < Vector3Count && i < vecOrdered.Count; i++)
                        {
                            int orderedIdx = vecOrdered[i];
                            var elem = DecompressedData[dataOffset + QuaternionCount + i];
                            int posLoc = globalToLocalPos[orderedIdx];
                            int sclLoc = globalToLocalScale[orderedIdx];
                            if (posLoc != -1 && posLoc < frame.Positions.Count)
                                frame.Positions[posLoc] = new Vector3(elem.X, elem.Y, elem.Z);
                            if (sclLoc != -1 && sclLoc < frame.Scales.Count)
                                frame.Scales[sclLoc] = new Vector3(elem.X, elem.Y, elem.Z);
                        }

                        frame.FrameIndex = frameIdx * 3;
                        ret.Frames.Add(frame);
                    }
                }
                else   // Normal path with constant channels
                {
                    List<int> constQuatOrdered = new List<int>();
                    List<int> constVecOrdered = new List<int>();
                    List<int> constFloatOrdered = new List<int>();
                    List<int> animQuatOrdered = new List<int>();
                    List<int> animVecOrdered = new List<int>();
                    List<int> animFloatOrdered = new List<int>();

                    if (ConstChanMap != null && ConstChanMap.Length > 0)
                    {
                        // Constant walk
                        int orderedIdx = ConstChanMap[0];
                        int offset = 1;
                        int count = 0;

                        void AdvanceConst()
                        {
                            if (offset >= ConstChanMap.Length) return;
                            if (count >= ConstChanMap[offset])
                            {
                                if (offset + 1 < ConstChanMap.Length)
                                    orderedIdx += ConstChanMap[offset + 1];
                                offset += 2;
                                count = 0;
                                if (offset < ConstChanMap.Length && count >= ConstChanMap[offset])
                                {
                                    if (offset + 1 < ConstChanMap.Length)
                                        orderedIdx += ConstChanMap[offset + 1];
                                    offset += 2;
                                    count = 0;
                                }
                            }
                        }

                        for (int i = 0; i < ConstQuaternionCount; i++) { AdvanceConst(); if (orderedIdx < total) constQuatOrdered.Add(orderedIdx); orderedIdx++; count++; }
                        for (int i = 0; i < ConstVector3Count; i++) { AdvanceConst(); if (orderedIdx < total) constVecOrdered.Add(orderedIdx); orderedIdx++; count++; }
                        for (int i = 0; i < ConstFloatCount; i++) { AdvanceConst(); if (orderedIdx < total) constFloatOrdered.Add(orderedIdx); orderedIdx++; count++; }

                        // Animated walk
                        orderedIdx = 0;
                        offset = 0;
                        count = 0;

                        void AdvanceAnim()
                        {
                            if (offset >= ConstChanMap.Length) return;
                            if (count >= ConstChanMap[offset])
                            {
                                if (offset + 1 < ConstChanMap.Length)
                                    orderedIdx += ConstChanMap[offset + 1];
                                offset += 2;
                                count = 0;
                                if (offset < ConstChanMap.Length && count >= ConstChanMap[offset])
                                {
                                    if (offset + 1 < ConstChanMap.Length)
                                        orderedIdx += ConstChanMap[offset + 1];
                                    offset += 2;
                                    count = 0;
                                }
                            }
                        }

                        for (int i = 0; i < QuaternionCount; i++) { AdvanceAnim(); if (orderedIdx < total) animQuatOrdered.Add(orderedIdx); orderedIdx++; count++; }
                        for (int i = 0; i < Vector3Count; i++) { AdvanceAnim(); if (orderedIdx < total) animVecOrdered.Add(orderedIdx); orderedIdx++; count++; }
                        for (int i = 0; i < NumFloat; i++) { AdvanceAnim(); if (orderedIdx < total) animFloatOrdered.Add(orderedIdx); orderedIdx++; count++; }
                    }
                    else
                    {
                        // No segment map: orderedChannels is assumed to be in type order
                        for (int i = 0; i < total; i++)
                        {
                            var ch = orderedChannels[i];
                            if (ch.Type == BoneChannelType.Rotation)
                            {
                                if (animQuatOrdered.Count < QuaternionCount) animQuatOrdered.Add(i);
                                else if (constQuatOrdered.Count < ConstQuaternionCount) constQuatOrdered.Add(i);
                            }
                            else if (ch.Type == BoneChannelType.Position || ch.Type == BoneChannelType.Scale)
                            {
                                if (animVecOrdered.Count < Vector3Count) animVecOrdered.Add(i);
                                else if (constVecOrdered.Count < ConstVector3Count) constVecOrdered.Add(i);
                            }
                            else
                            {
                                if (animFloatOrdered.Count < NumFloat) animFloatOrdered.Add(i);
                                else if (constFloatOrdered.Count < ConstFloatCount) constFloatOrdered.Add(i);
                            }
                        }
                    }

                    Vector4 qDelta = new Vector4(QuatMax - QuatMin);
                    Vector4 qMin = new Vector4(QuatMin);
                    Vector3 vDelta = new Vector3(Vec3Max - Vec3Min);
                    Vector3 vMin = new Vector3(Vec3Min);

                    ret.Frames = new List<Frame>(NumKeys);
                    for (int frameIdx = 0; frameIdx < NumKeys; frameIdx++)
                    {
                        Frame frame = new Frame();
                        frame.Rotations = defaultRot.ToList();
                        frame.Positions = defaultPos.ToList();
                        frame.Scales = defaultScale.ToList();

                        int paletteIdx = 0;

                        // Constant quaternions
                        for (int i = 0; i < constQuatOrdered.Count; i++)
                        {
                            int orderedIdx = constQuatOrdered[i];
                            if (paletteIdx + 3 >= PaletteIndexes.Length) break;
                            int i0 = PaletteIndexes[paletteIdx];
                            int i1 = PaletteIndexes[paletteIdx + 1];
                            int i2 = PaletteIndexes[paletteIdx + 2];
                            int i3 = PaletteIndexes[paletteIdx + 3];
                            if (i0 >= ConstantPalette.Length || i1 >= ConstantPalette.Length ||
                                i2 >= ConstantPalette.Length || i3 >= ConstantPalette.Length)
                                break;

                            Vector4 elem = new Vector4(
                                ConstantPalette[i0], ConstantPalette[i1],
                                ConstantPalette[i2], ConstantPalette[i3]);
                            elem = elem * qDelta + qMin;

                            int local = globalToLocalRot[orderedIdx];
                            if (local != -1 && local < frame.Rotations.Count)
                                frame.Rotations[local] = Quaternion.Normalize(
                                    new Quaternion(elem.X, elem.Y, elem.Z, elem.W));
                            paletteIdx += 4;
                        }

                        // Animated quaternions
                        for (int i = 0; i < animQuatOrdered.Count; i++)
                        {
                            int orderedIdx = animQuatOrdered[i];
                            int pos = frameIdx * dofCount + i;
                            if (pos >= DecompressedData.Count) break;
                            Vector4 elem = DecompressedData[pos];
                            int local = globalToLocalRot[orderedIdx];
                            if (local != -1 && local < frame.Rotations.Count)
                                frame.Rotations[local] = Quaternion.Normalize(
                                    new Quaternion(elem.X, elem.Y, elem.Z, elem.W));
                        }

                        // Constant vectors
                        for (int i = 0; i < constVecOrdered.Count; i++)
                        {
                            int orderedIdx = constVecOrdered[i];
                            if (paletteIdx + 2 >= PaletteIndexes.Length) break;
                            int i0 = PaletteIndexes[paletteIdx];
                            int i1 = PaletteIndexes[paletteIdx + 1];
                            int i2 = PaletteIndexes[paletteIdx + 2];
                            if (i0 >= ConstantPalette.Length || i1 >= ConstantPalette.Length ||
                                i2 >= ConstantPalette.Length)
                                break;

                            Vector3 elem = new Vector3(
                                ConstantPalette[i0], ConstantPalette[i1], ConstantPalette[i2]);
                            elem = elem * vDelta + vMin;

                            int posLoc = globalToLocalPos[orderedIdx];
                            int sclLoc = globalToLocalScale[orderedIdx];
                            if (posLoc != -1 && posLoc < frame.Positions.Count) frame.Positions[posLoc] = elem;
                            if (sclLoc != -1 && sclLoc < frame.Scales.Count) frame.Scales[sclLoc] = elem;
                            paletteIdx += 3;
                        }

                        // Animated vectors
                        for (int i = 0; i < animVecOrdered.Count; i++)
                        {
                            int orderedIdx = animVecOrdered[i];
                            int pos = frameIdx * dofCount + QuaternionCount + i;
                            if (pos >= DecompressedData.Count) break;
                            Vector4 elem = DecompressedData[pos];
                            int posLoc = globalToLocalPos[orderedIdx];
                            int sclLoc = globalToLocalScale[orderedIdx];
                            if (posLoc != -1 && posLoc < frame.Positions.Count)
                                frame.Positions[posLoc] = new Vector3(elem.X, elem.Y, elem.Z);
                            if (sclLoc != -1 && sclLoc < frame.Scales.Count)
                                frame.Scales[sclLoc] = new Vector3(elem.X, elem.Y, elem.Z);
                        }

                        // Constant floats (consume palette, no export storage)
                        for (int i = 0; i < constFloatOrdered.Count; i++)
                        {
                            if (paletteIdx >= PaletteIndexes.Length) break;
                            if (PaletteIndexes[paletteIdx] >= ConstantPalette.Length) break;
                            paletteIdx += 1;
                        }

                        frame.FrameIndex = frameIdx;
                        ret.Frames.Add(frame);
                    }
                }

                // Clean channel names (remove suffixes)
                for (int r = 0; r < rotChannels.Count; r++) rotChannels[r] = rotChannels[r].Replace(".q", "");
                for (int r = 0; r < posChannels.Count; r++) posChannels[r] = posChannels[r].Replace(".t", "");
                for (int r = 0; r < scaleChannels.Count; r++) scaleChannels[r] = scaleChannels[r].Replace(".s", "");

                ret.Name = Name;
                ret.PositionChannels = posChannels;
                ret.RotationChannels = rotChannels;
                ret.ScaleChannels = scaleChannels;
                ret.Additive = Additive;
                return ret;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in {this.Name}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
    }
}