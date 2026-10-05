using System.Numerics;
using AssetBankPlugin.Export;
using Frosty.Hash;
using MeshSetPlugin.Render;

namespace AssetBankPlugin.Render
{
    public static class AntSkeletonConverter
    {
        public static MeshRenderSkeleton Convert(InternalSkeleton skeleton)
        {
            var renderSkeleton = new MeshRenderSkeleton();

            for (int i = 0; i < skeleton.BoneNames.Count; i++)
            {
                Matrix4x4.Invert(skeleton.BoneTransforms[i].Matrix, out var invModelPose);

                renderSkeleton.AddBone(new MeshRenderSkeleton.Bone
                {
                    NameHash = Fnv1.HashString(skeleton.BoneNames[i]),
                    ParentBoneId = skeleton.BoneParents[i],
                    ModelPose = ToSharpDX(invModelPose),
                    LocalPose = ToSharpDX(skeleton.LocalTransforms[i].Matrix),
                    IsProcedural = false
                });
            }

            return renderSkeleton;
        }

        private static Matrix4x4 ToSharpDX(Matrix4x4 m)
        {
            return new Matrix4x4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);
        }
    }
}