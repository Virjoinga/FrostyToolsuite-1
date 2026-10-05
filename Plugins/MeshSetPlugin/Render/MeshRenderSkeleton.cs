using Frosty.Core.Viewport;
using Frosty.Hash;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MeshSetPlugin.Render
{
    public class MeshRenderSkeleton
    {
#if FROSTY_DEVELOPER
        public class ExpressionValue<T>
        {
            protected T value;
            public ExpressionValue(T inValue)
            {
                value = inValue;
            }

            public virtual T Evaluate(int hash)
            {
                return value;
            }
        }
        public class BoneQueryExpressionValue : ExpressionValue<Matrix4x4>
        {
            private int WorldTransformHash = Fnv1.HashString("WorldTransform");

            protected MeshRenderSkeleton skeleton;
            protected int boneId;

            public BoneQueryExpressionValue(MeshRenderSkeleton inSkeleton, int inBoneId)
                : base(Matrix4x4.Identity)
            {
                skeleton = inSkeleton;
                boneId = inBoneId;
            }

            public override Matrix4x4 Evaluate(int hash)
            {
                if (hash == WorldTransformHash)
                {
                    return skeleton.GetBoneWorldMatrix(boneId);
                }

                return skeleton.GetBone(boneId).LocalPose;
            }
        }
        public class TestRollBoneExpression
        {
            private int WorldTransformHash = Fnv1.HashString("WorldTransform");
            private int ValueHash = Fnv1.HashString("Value");

            private ExpressionValue<Vector3> param1;
            private ExpressionValue<float> param2;
            private ExpressionValue<Matrix4x4> param3;
            private ExpressionValue<Matrix4x4> param4;
            private ExpressionValue<Matrix4x4> param5;

            public TestRollBoneExpression(ExpressionValue<Vector3> inParam1, ExpressionValue<float> inParam2, ExpressionValue<Matrix4x4> inParam3, ExpressionValue<Matrix4x4> inParam4, ExpressionValue<Matrix4x4> inParam5)
            {
                param1 = inParam1;
                param2 = inParam2;
                param3 = inParam3;
                param4 = inParam4;
                param5 = inParam5;
            }

            public Matrix4x4 Evaluate(Matrix4x4 input)
            {
                Matrix4x4 tmp00 = input * param3.Evaluate(WorldTransformHash);
                Matrix4x4.Decompose(tmp00, out Vector3 tmp40, out Quaternion tmpD0, out Vector3 tmp50);
                Matrix4x4.Decompose(param4.Evaluate(WorldTransformHash), out Vector3 tmp60, out Quaternion tmpE0, out Vector3 tmp70);
                Matrix4x4.Decompose(param5.Evaluate(WorldTransformHash), out Vector3 tmp80, out Quaternion tmpF0, out Vector3 tmp90);
                Quaternion tmp100 = Quaternion.Inverse(tmpF0);
                Quaternion tmp110 = tmpE0 * tmp100;
                Vector3 tmpA0 = SharpDXUtils.ExtractEulerAngles(SharpDXUtils.FromQuaternion(tmp110)) * new Vector3((float)(Math.PI / 180.0));
                Vector3 tmpB0 = tmpA0 * param1.Evaluate(ValueHash);
                Vector3 tmpC0 = tmpB0 + new Vector3(0, 0, param2.Evaluate(ValueHash));
                Quaternion tmp120 = Quaternion.Normalize(SharpDXUtils.CreateFromEulerAngles(tmpC0.X, tmpC0.Y, tmpC0.Z));
                Quaternion tmp130 = tmp120 * tmpF0;
                return Matrix4x4.CreateScale(tmp40) * Matrix4x4.CreateFromQuaternion(tmp130) * Matrix4x4.CreateTranslation(tmp50);
            }
        }
#endif

        public class Bone
        {
            public int NameHash;
            public Matrix4x4 ModelPose;
            public Matrix4x4 LocalPose;
            public int ParentBoneId;
            public bool IsProcedural;
        }

        public int BoneCount => proceduralBoneIndex;
        public int TotalBoneCount => bones.Count;
        public IEnumerable<Bone> Bones => bones;

        private List<Bone> bones = new List<Bone>();
        private int proceduralBoneIndex = -1;

#if FROSTY_DEVELOPER
        private Dictionary<int, TestRollBoneExpression> expressions = new Dictionary<int, TestRollBoneExpression>();
        public void AddExpression(int boneId, TestRollBoneExpression expr)
        {
            expressions.Add(boneId, expr);
        }
#endif

        public void AddBone(Bone bone)
        {
            bones.Add(bone);
            if (bone.IsProcedural && proceduralBoneIndex == -1)
            {
                proceduralBoneIndex = bones.Count - 1;
            }
        }

        public Matrix4x4 GetBoneWorldMatrix(int idx)
        {
            if (idx >= bones.Count)
            {
                return Matrix4x4.Identity;
            }

            Matrix4x4 boneMatrix = bones[idx].LocalPose;
            while (idx != -1)
            {
                idx = bones[idx].ParentBoneId;
                if (idx != -1)
                {
                    boneMatrix *= bones[idx].LocalPose;
                }
            }

            return boneMatrix;
        }

        public Matrix4x4 GetBoneMatrix(int idx)
        {
            if (idx >= bones.Count)
            {
                return Matrix4x4.Identity;
            }

            Matrix4x4 invBoneMatrix = bones[idx].ModelPose;
            Matrix4x4 boneMatrix = GetBoneWorldMatrix(idx);

#if FROSTY_DEVELOPER
            if (expressions.ContainsKey(idx))
            {
                boneMatrix = expressions[idx].Evaluate(bones[idx].LocalPose);
            }
#endif

            boneMatrix = invBoneMatrix * boneMatrix;
            return boneMatrix;
        }

        public Bone GetBone(int boneId)
        {
            return bones[boneId];
        }

        public int GetBoneId(int nameHash)
        {
            return bones.FindIndex((Bone a) => a.NameHash == nameHash);
        }

        public void UpdateBone(int boneId, Matrix4x4? modelPose = null, Matrix4x4? localPose = null)
        {
            if (boneId >= bones.Count)
            {
                return;
            }

            if (modelPose.HasValue)
            {
                bones[boneId].ModelPose = modelPose.Value;
            }

            if (localPose.HasValue)
            {
                bones[boneId].LocalPose = localPose.Value;
            }
        }

        public void UpdateBone(string boneName, Matrix4x4? modelPose = null, Matrix4x4? localPose = null)
        {
            int hash = Fnv1.HashString(boneName);
            int boneId = bones.FindIndex((Bone a) => a.NameHash == hash);
            if (boneId == -1)
            {
                return;
            }

            UpdateBone(boneId, modelPose, localPose);
        }
    }
}