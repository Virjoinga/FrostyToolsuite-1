using Frosty.Core.Viewport;
using MeshSetPlugin.Resources;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct3D;
using D3D11 = Vortice.Direct3D11;

namespace MeshSetPlugin.Render
{
    public class MeshRenderSection
    {
        public ShaderPermutation Permutation;
        public MeshRenderSkeleton Skeleton;
        public D3D11.ID3D11Buffer VertexParameters;
        public D3D11.ID3D11Buffer PixelParameters;

        public D3D11.ID3D11Buffer[] VertexBuffers;
        public uint[] Strides;
        public uint[] Offsets;

        public List<D3D11.ID3D11ShaderResourceView> VertexTextures = new();
        public List<D3D11.ID3D11SamplerState> VertexSamplers = new();
        public List<D3D11.ID3D11ShaderResourceView> PixelTextures = new();
        public MeshSetSection MeshSection;
        public int StartIndex;
        public int VertexOffset;
        public int PrimitiveCount;
        public PrimitiveTopology PrimitiveType;
        public int VertexStride;

        public bool IsFallback;
        public bool IsSelected;
        public bool IsVisible = true;

        // @temp
        public List<uint> BoneIndices = new List<uint>();

        public string DebugName => MeshSection.Name;

        public void SetState(D3D11.ID3D11DeviceContext context, MeshRenderPath renderPath)
        {
            if (Permutation.IsSkinned)
            {
                // obtain bone matrices from skeleton
                List<Matrix4x4> boneMatrices = new();
                for (int i = 0; i < BoneIndices.Count; i++)
                {
                    int boneIndex = (int)BoneIndices[i];
                    if ((BoneIndices[i] & 0x8000) != 0)
                        boneIndex = (boneIndex & 0x7FFF) + Skeleton.BoneCount;

                    while (i >= boneMatrices.Count)
                        boneMatrices.Add(Matrix4x4.Identity);

                    if (boneIndex == -1)
                        continue;

                    boneMatrices[i] = Skeleton.GetBoneMatrix(boneIndex);
                }

                // update the bone buffer
                Permutation.boneBuffer.Update(context, Skeleton.BoneCount, boneMatrices.ToArray());
            }

            Permutation.SetState(context, renderPath);

            context.IASetPrimitiveTopology(PrimitiveType);
            context.IASetVertexBuffers(0, VertexBuffers, Strides, Offsets);

            if (renderPath != MeshRenderPath.Shadows)
            {
                context.PSSetShaderResources(1, PixelTextures.ToArray());
                context.PSSetConstantBuffer(2, PixelParameters);
            }
        }

        public void Draw(D3D11.ID3D11DeviceContext context)
        {
            context.DrawIndexed((uint)(PrimitiveCount * 3), (uint)(StartIndex), 0);
        }
    }
}
