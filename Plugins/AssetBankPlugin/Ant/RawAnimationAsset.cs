using AssetBankPlugin.Export;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace AssetBankPlugin.Ant
{
    public class RawAnimationAsset : AnimationAsset
    {
        public ushort[] KeyTimes { get; set; }
        public ushort[] MappingIndices { get; set; }
        public ushort[] ChannelIndices { get; set; }
        public float[] Data { get; set; }
        public float[] ConstData { get; set; }

        public uint NumKeys { get; set; }
        public ushort FloatCount { get; set; }
        public ushort Vec3Count { get; set; }
        public ushort QuatCount { get; set; }
        public ushort ConstFloatCount { get; set; }
        public ushort ConstVec3Count { get; set; }
        public ushort ConstQuatCount { get; set; }
        public bool Cycle { get; set; }

        public RawAnimationAsset() { }

        public override void SetData(Dictionary<string, object> data)
        {
            base.SetData(data);      

            KeyTimes = GetPropertyArray<ushort>("KeyTimes");
            MappingIndices = GetPropertyArray<ushort>("MappingIndices");
            ChannelIndices = GetPropertyArray<ushort>("ChannelIndices");
            Data = GetPropertyArray<float>("Data");
            ConstData = GetPropertyArray<float>("ConstData");

            NumKeys = GetProperty<uint>("NumKeys");
            Cycle = GetProperty<bool>("Cycle");

            FloatCount = GetProperty<ushort>("FloatCount");
            Vec3Count = GetProperty<ushort>("Vec3Count");
            QuatCount = GetProperty<ushort>("QuatCount");
            ConstFloatCount = GetProperty<ushort>("ConstFloatCount");
            ConstVec3Count = GetProperty<ushort>("ConstVec3Count");
            ConstQuatCount = GetProperty<ushort>("ConstQuatCount");
        }

        public override InternalAnimation ConvertToInternal()
        {
            var ret = new InternalAnimation();

            List<ChannelInfo> orderedChannels = OrderedChannels;
            if (orderedChannels == null || orderedChannels.Count == 0)
                return ret;

            List<string> posChannels = new List<string>();
            List<string> rotChannels = new List<string>();
            List<string> scaleChannels = new List<string>();

            foreach (var ch in orderedChannels)
            {
                if (ch.Type == BoneChannelType.Rotation) rotChannels.Add(ch.Name);
                else if (ch.Type == BoneChannelType.Position) posChannels.Add(ch.Name);
                else if (ch.Type == BoneChannelType.Scale) scaleChannels.Add(ch.Name);
            }

            int totalChannels = orderedChannels.Count;
            int[] globalToLocalRot = new int[totalChannels];
            int[] globalToLocalPos = new int[totalChannels];
            int[] globalToLocalScale = new int[totalChannels];

            for (int i = 0; i < totalChannels; i++)
            {
                globalToLocalRot[i] = -1;
                globalToLocalPos[i] = -1;
                globalToLocalScale[i] = -1;
            }

            int localRot = 0, localPos = 0, localScale = 0;
            for (int g = 0; g < totalChannels; g++)
            {
                switch (orderedChannels[g].Type)
                {
                    case BoneChannelType.Rotation: globalToLocalRot[g] = localRot++; break;
                    case BoneChannelType.Position: globalToLocalPos[g] = localPos++; break;
                    case BoneChannelType.Scale: globalToLocalScale[g] = localScale++; break;
                }
            }

            Quaternion[] defaultRot = new Quaternion[rotChannels.Count];
            Vector3[] defaultPos = new Vector3[posChannels.Count];
            Vector3[] defaultScale = new Vector3[scaleChannels.Count];

            for (int i = 0; i < totalChannels; i++)
            {
                var ch = orderedChannels[i];
                if (ch.Type == BoneChannelType.Rotation)
                {
                    int local = globalToLocalRot[i];
                    if (local == -1) continue;
                    if (Additive) defaultRot[local] = Quaternion.Identity;
                    else if (TryGetRigDefaultVector4(ch.DofId, out var v4))
                        defaultRot[local] = Quaternion.Normalize(new Quaternion(v4.X, v4.Y, v4.Z, v4.W));
                    else defaultRot[local] = Quaternion.Identity;
                }
                else if (ch.Type == BoneChannelType.Position)
                {
                    int local = globalToLocalPos[i];
                    if (local == -1) continue;
                    if (Additive) defaultPos[local] = Vector3.Zero;
                    else if (TryGetRigDefaultVector3(ch.DofId, out var v3))
                        defaultPos[local] = v3;
                    else defaultPos[local] = Vector3.Zero;
                }
                else if (ch.Type == BoneChannelType.Scale)
                {
                    int local = globalToLocalScale[i];
                    if (local == -1) continue;
                    if (Additive) defaultScale[local] = Vector3.Zero;
                    else if (TryGetRigDefaultVector3(ch.DofId, out var v3))
                        defaultScale[local] = v3;
                    else defaultScale[local] = Vector3.One;
                }
            }

            bool isBfnFormat = MappingIndices != null && MappingIndices.Length > 0;

            ushort[] runtimeMapping = MappingIndices;
            if (!isBfnFormat)
            {
                runtimeMapping = new ushort[totalChannels];
                ushort mapIdx = 0;
                for (int i = 0; i < totalChannels; i++)
                    if (orderedChannels[i].Type == BoneChannelType.Rotation) runtimeMapping[mapIdx++] = (ushort)i;
                for (int i = 0; i < totalChannels; i++)
                    if (orderedChannels[i].Type == BoneChannelType.Position || orderedChannels[i].Type == BoneChannelType.Scale) runtimeMapping[mapIdx++] = (ushort)i;
                for (int i = 0; i < totalChannels; i++)
                    if (orderedChannels[i].Type == BoneChannelType.None) runtimeMapping[mapIdx++] = (ushort)i;  
            }

            int numAnimated = QuatCount + Vec3Count + FloatCount;

            if (isBfnFormat && ConstData != null && ConstData.Length > 0)
            {
                int constOffset = 0;
                int compactedIdx = numAnimated;

                for (int i = 0; i < ConstQuatCount; i++)
                {
                    if (constOffset + 3 >= ConstData.Length) break;
                    int absIdx = runtimeMapping[compactedIdx++];
                    int local = globalToLocalRot[absIdx];
                    if (local != -1)
                    {
                        defaultRot[local] = Quaternion.Normalize(new Quaternion(
                            ConstData[constOffset], ConstData[constOffset + 1],
                            ConstData[constOffset + 2], ConstData[constOffset + 3]));
                    }
                    constOffset += 4;
                }

                for (int i = 0; i < ConstVec3Count; i++)
                {
                    if (constOffset + 2 >= ConstData.Length) break;
                    int absIdx = runtimeMapping[compactedIdx++];
                    int posLoc = globalToLocalPos[absIdx];
                    int scaleLoc = globalToLocalScale[absIdx];
                    var v = new Vector3(ConstData[constOffset], ConstData[constOffset + 1], ConstData[constOffset + 2]);

                    if (posLoc != -1) defaultPos[posLoc] = v;
                    if (scaleLoc != -1) defaultScale[scaleLoc] = v;

                    constOffset += 4;  
                }
            }

            if (Data != null && Data.Length > 0 && KeyTimes != null && KeyTimes.Length > 0)
            {
                int framesToRead = (int)Math.Min(NumKeys, KeyTimes.Length);
                ret.Frames = new List<Frame>(framesToRead);

                int vec3Floats = 4;
                int unalignedFloats = (QuatCount * 4) + (Vec3Count * vec3Floats) + FloatCount;
                int frameStride = isBfnFormat ? ((unalignedFloats + 3) & ~3) : unalignedFloats;

                for (int frameIdx = 0; frameIdx < framesToRead; frameIdx++)
                {
                    var frame = new Frame
                    {
                        FrameIndex = KeyTimes[frameIdx],
                        Rotations = new List<Quaternion>(defaultRot),
                        Positions = new List<Vector3>(defaultPos),
                        Scales = new List<Vector3>(defaultScale)
                    };

                    int dataOffset = frameIdx * frameStride;
                    int compactedIdx = 0;

                    for (int i = 0; i < QuatCount; i++)
                    {
                        if (dataOffset + 3 >= Data.Length) break;
                        int absIdx = runtimeMapping[compactedIdx++];
                        int local = globalToLocalRot[absIdx];
                        if (local != -1)
                        {
                            frame.Rotations[local] = Quaternion.Normalize(new Quaternion(
                                Data[dataOffset], Data[dataOffset + 1],
                                Data[dataOffset + 2], Data[dataOffset + 3]));
                        }
                        dataOffset += 4;
                    }

                    for (int i = 0; i < Vec3Count; i++)
                    {
                        if (dataOffset + 2 >= Data.Length) break;
                        int absIdx = runtimeMapping[compactedIdx++];
                        int posLoc = globalToLocalPos[absIdx];
                        int scaleLoc = globalToLocalScale[absIdx];
                        var v = new Vector3(Data[dataOffset], Data[dataOffset + 1], Data[dataOffset + 2]);

                        if (posLoc != -1) frame.Positions[posLoc] = v;
                        if (scaleLoc != -1) frame.Scales[scaleLoc] = v;

                        dataOffset += vec3Floats;       
                    }

                    ret.Frames.Add(frame);
                }
            }

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
    }
}