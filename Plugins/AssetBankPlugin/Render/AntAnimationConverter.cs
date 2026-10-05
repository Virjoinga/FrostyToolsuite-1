using System.Collections.Generic;
using System.Numerics;
using AssetBankPlugin.Export;
using Frosty.Hash;
using MeshSetPlugin.Render;

namespace AssetBankPlugin.Render
{
    public static class AntAnimationConverter
    {
        public static MeshRenderAnim Convert(InternalAnimation anim, int frameCount = -1)
        {
            if (frameCount < 0)
            {
                frameCount = anim.Frames.Count > 0
                    ? anim.Frames[anim.Frames.Count - 1].FrameIndex + 1
                    : 1;
            }

            var renderAnim = new MeshRenderAnim(frameCount);

            var boneDict = new Dictionary<string, MeshRenderAnim.Bone>();

            MeshRenderAnim.Bone GetOrAdd(string name)
            {
                if (!boneDict.TryGetValue(name, out var bone))
                {
                    bone = new MeshRenderAnim.Bone { NameHash = Fnv1.HashString(name) };
                    boneDict[name] = bone;
                }
                return bone;
            }

            foreach (var frame in anim.Frames)
            {
                for (int i = 0; i < anim.RotationChannels.Count && i < frame.Rotations.Count; i++)
                {
                    GetOrAdd(anim.RotationChannels[i]).Rotations.Add(
                        new MeshRenderAnim.Keyframe<Quaternion>
                        {
                            FrameTime = frame.FrameIndex,
                            Value = ToSharpDX(frame.Rotations[i])
                        });
                }

                for (int i = 0; i < anim.PositionChannels.Count && i < frame.Positions.Count; i++)
                {
                    GetOrAdd(anim.PositionChannels[i]).Translations.Add(
                        new MeshRenderAnim.Keyframe<Vector3>
                        {
                            FrameTime = frame.FrameIndex,
                            Value = ToSharpDX(frame.Positions[i])
                        });
                }

                for (int i = 0; i < anim.ScaleChannels.Count && i < frame.Scales.Count; i++)
                {
                    GetOrAdd(anim.ScaleChannels[i]).Scales.Add(
                        new MeshRenderAnim.Keyframe<Vector3>
                        {
                            FrameTime = frame.FrameIndex,
                            Value = ToSharpDX(frame.Scales[i])
                        });
                }
            }

            renderAnim.AddBones(boneDict.Values);
            return renderAnim;
        }

        private static Quaternion ToSharpDX(Quaternion q)
            => new Quaternion(q.X, q.Y, q.Z, q.W);

        private static Vector3 ToSharpDX(Vector3 v)
            => new Vector3(v.X, v.Y, v.Z);
    }
}