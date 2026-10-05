using Frosty.Core.Viewport;
using FrostySdk.Resources;
using Vortice.D3DCompiler;
using System.Runtime.InteropServices;
using D3D11 = Vortice.Direct3D11;
using System.Numerics;
using System;
using Vortice.Mathematics;

namespace Frosty.Core.Screens
{
    public class TextureScreen : Screen
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public Vector3 Position;
            public Vector2 TexCoord;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public Vector2 ViewportDim;
            public Vector2 TextureDim;
            public Matrix4x4 ChannelMask;
            public float SrgbEnabled;
            public float MipLevel;
            public float SliceLevel;
            public float Padding;
        }

        public bool RedChannelEnabled {
            get => redChannelEnabled;
            set => redChannelEnabled = value;
        }

        public bool GreenChannelEnabled {
            get => greenChannelEnabled;
            set => greenChannelEnabled = value;
        }

        public bool BlueChannelEnabled {
            get => blueChannelEnabled;
            set => blueChannelEnabled = value;
        }

        public bool AlphaChannelEnabled {
            get => alphaChannelEnabled;
            set => alphaChannelEnabled = value;
        }

        public bool SrgbEnabled {
            get => srgbEnabled;
            set => srgbEnabled = value;
        }

        public int MipLevel {
            get => mipLevel;
            set => mipLevel = value;
        }

        public int SliceLevel {
            get => sliceLevel;
            set => sliceLevel = value;
        }
        public Texture TextureAsset {
            get => textureAsset;
            set {
                if (textureAsset != value)
                {
                    textureAsset = value;
                    ResetTexture();
                }
            }
        }

        private Texture textureAsset;

        private D3D11.ID3D11Texture2D texture;
        private D3D11.ID3D11ShaderResourceView textureSRV;
        private D3D11.ID3D11SamplerState samplerState;
        private D3D11.ID3D11RasterizerState rasterizerState;
        private D3D11.ID3D11DepthStencilState depthStencilState;
        private D3D11.ID3D11BlendState blendState;
        private D3D11.ID3D11InputLayout inputLayout;
        private D3D11.ID3D11VertexShader vertexShader;
        private D3D11.ID3D11PixelShader pixelShader;
        private D3D11.ID3D11Buffer vertexBuffer;
        private D3D11.ID3D11Buffer constantBuffer;

        private bool redChannelEnabled;
        private bool greenChannelEnabled;
        private bool blueChannelEnabled;
        private bool alphaChannelEnabled;
        private bool srgbEnabled;
        private int mipLevel;
        private int sliceLevel;
        private bool recreateTexture = false;

        public TextureScreen(Texture inTexture)
        {
            textureAsset = inTexture;

            redChannelEnabled = true;
            greenChannelEnabled = true;
            blueChannelEnabled = true;
            alphaChannelEnabled = true;
            srgbEnabled = textureAsset.PixelFormat.Contains("SRGB") || ((textureAsset.Flags & TextureFlags.SrgbGamma) != 0);
        }

        public TextureScreen()
        {
            redChannelEnabled = true;
            greenChannelEnabled = true;
            blueChannelEnabled = true;
            alphaChannelEnabled = true;
            srgbEnabled = false;
        }

        public override void CreateBuffers()
        {
            CreateGlobalResources();
            CreateTextureResources();
            CreateMeshResources();
        }

        public override void Update(double timestep)
        {
        }

        public override void Render()
        {
            if (textureAsset == null)
                return;

            if (recreateTexture)
            {
                textureSRV?.Dispose();

                texture = TextureUtils.LoadTexture(Viewport.Device, textureAsset);

                // all texture types are represented by a 2D array (even a single T2D, just has one slice)
                textureSRV = Viewport.Device.CreateShaderResourceView(texture, new D3D11.ShaderResourceViewDescription()
                {
                    Format = TextureUtils.ToShaderFormat(textureAsset.PixelFormat, (textureAsset.Flags & TextureFlags.SrgbGamma) != 0),
                    ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Texture2DArray,
                    Texture2DArray = new D3D11.Texture2DArrayShaderResourceView()
                    {
                        ArraySize = texture.Description.ArraySize,
                        FirstArraySlice = 0,
                        MipLevels = unchecked((uint)(-1)),
                        MostDetailedMip = 0
                    }
                });

                recreateTexture = false;
            }

            Matrix4x4 channelMask = new();

            if (redChannelEnabled)
            {
                channelMask.M11 = 1;
                channelMask.M12 = (!greenChannelEnabled && !blueChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
                channelMask.M13 = (!greenChannelEnabled && !blueChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
            }
            if (greenChannelEnabled)
            {
                channelMask.M21 = (!redChannelEnabled && !blueChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
                channelMask.M22 = 1;
                channelMask.M23 = (!redChannelEnabled && !blueChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
            }
            if (blueChannelEnabled)
            {
                channelMask.M31 = (!redChannelEnabled && !greenChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
                channelMask.M32 = (!redChannelEnabled && !greenChannelEnabled && !alphaChannelEnabled) ? 1 : 0;
                channelMask.M33 = 1;
            }
            if (alphaChannelEnabled)
            {
                channelMask.M41 = (!redChannelEnabled && !greenChannelEnabled && !blueChannelEnabled) ? 1 : 0;
                channelMask.M42 = (!redChannelEnabled && !greenChannelEnabled && !blueChannelEnabled) ? 1 : 0;
                channelMask.M43 = (!redChannelEnabled && !greenChannelEnabled && !blueChannelEnabled) ? 1 : 0;
                channelMask.M44 = (redChannelEnabled || greenChannelEnabled || blueChannelEnabled && alphaChannelEnabled) ? 1 : 0;
            }
            channelMask = Matrix4x4.Transpose(channelMask);

            Vortice.Mathematics.Viewport[] viewports = Viewport.Context.RSGetViewports<Vortice.Mathematics.Viewport>().ToArray();

            Constants constants = new Constants
            {
                TextureDim =
                {
                    X = textureAsset.Width,
                    Y = textureAsset.Height
                },

                ViewportDim =
                {
                    X = viewports[0].Width,
                    Y = viewports[0].Height
                },

                ChannelMask = channelMask,
                SrgbEnabled = (srgbEnabled) ? 1.0f : 0.0f,
                MipLevel = mipLevel,
                SliceLevel = sliceLevel
            };

            Viewport.Context.ClearRenderTargetView(Viewport.ColorBufferRTV, new Color4(0, 0, 0, 0));
            Viewport.Context.UpdateSubresource(in constants, constantBuffer, 0);

            Viewport.Context.OMSetBlendState(blendState);
            Viewport.Context.OMSetDepthStencilState(depthStencilState);
            Viewport.Context.RSSetState(rasterizerState);

            Viewport.Context.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
            Viewport.Context.IASetInputLayout(inputLayout);
            Viewport.Context.IASetVertexBuffer(0, vertexBuffer, (uint)(Marshal.SizeOf<Vertex>()), 0);

            Viewport.Context.VSSetConstantBuffer(0, constantBuffer);
            Viewport.Context.PSSetConstantBuffer(0, constantBuffer);

            {
                Viewport.Context.VSSetShader(vertexShader);
                Viewport.Context.PSSetShader(pixelShader);
                Viewport.Context.PSSetShaderResource(0, textureSRV);
                Viewport.Context.PSSetSampler(0, samplerState);

                Viewport.Context.Draw(6, 0);
            }
        }

        public override void DisposeBuffers()
        {
            vertexShader.Dispose();
            pixelShader.Dispose();
            inputLayout.Dispose();

            if (texture != null)
            {
                textureSRV.Dispose();
                texture.Dispose();
            }

            vertexBuffer.Dispose();
            constantBuffer.Dispose();

            samplerState.Dispose();
            blendState.Dispose();
            rasterizerState.Dispose();
            depthStencilState.Dispose();
        }

        private void ResetTexture()
        {
            CreateTextureResources();
        }

        private void CreateGlobalResources()
        {
            rasterizerState = Viewport.Device.CreateRasterizerState(new D3D11.RasterizerDescription()
            {
                CullMode = D3D11.CullMode.None,
                DepthBias = 0,
                DepthBiasClamp = 0,
                FillMode = D3D11.FillMode.Solid,
                AntialiasedLineEnable = false,
                DepthClipEnable = false,
                FrontCounterClockwise = false,
                MultisampleEnable = false,
                ScissorEnable = false,
                SlopeScaledDepthBias = 0
            });

            D3D11.BlendDescription desc = new D3D11.BlendDescription();
            desc.RenderTarget[0].BlendEnable = true;
            desc.RenderTarget[0].SourceBlend = D3D11.Blend.SourceAlpha;
            desc.RenderTarget[0].DestinationBlend = D3D11.Blend.InverseSourceAlpha;
            desc.RenderTarget[0].BlendOperation = D3D11.BlendOperation.Add;
            desc.RenderTarget[0].SourceBlendAlpha = D3D11.Blend.One;
            desc.RenderTarget[0].DestinationBlendAlpha = D3D11.Blend.One;
            desc.RenderTarget[0].BlendOperationAlpha = D3D11.BlendOperation.Add;
            desc.RenderTarget[0].RenderTargetWriteMask = D3D11.ColorWriteEnable.All;
            blendState = Viewport.Device.CreateBlendState(desc);

            samplerState = Viewport.Device.CreateSamplerState(new D3D11.SamplerDescription()
            {
                AddressU = D3D11.TextureAddressMode.Wrap,
                AddressV = D3D11.TextureAddressMode.Wrap,
                AddressW = D3D11.TextureAddressMode.Wrap,
                BorderColor = new Color(0, 0, 0),
                ComparisonFunc = D3D11.ComparisonFunction.Always,
                Filter = D3D11.Filter.MinMagMipPoint,
                MaxAnisotropy = 16,
                MaxLOD = 20,
                MinLOD = 0,
                MipLODBias = 0
            });
            depthStencilState = Viewport.Device.CreateDepthStencilState(new D3D11.DepthStencilDescription()
            {
                DepthEnable = false,
                StencilEnable = false
            });
        }

        private void CreateTextureResources()
        {
            recreateTexture = true;
        }

        private void CreateMeshResources()
        {
            Vertex[] vertices = new Vertex[]
            {
                new Vertex() { Position = new Vector3(-1.0f, -1.0f, 0.0f), TexCoord = new Vector2(0, 1) },
                new Vertex() { Position = new Vector3( 1.0f, -1.0f, 0.0f), TexCoord = new Vector2(1, 1) },
                new Vertex() { Position = new Vector3(-1.0f,  1.0f, 0.0f), TexCoord = new Vector2(0, 0) },

                new Vertex() { Position = new Vector3( 1.0f,  1.0f, 0.0f), TexCoord = new Vector2(1, 0) },
                new Vertex() { Position = new Vector3(-1.0f,  1.0f, 0.0f), TexCoord = new Vector2(0, 0) },
                new Vertex() { Position = new Vector3( 1.0f, -1.0f, 0.0f), TexCoord = new Vector2(1, 1) }
            };
            vertexBuffer = Viewport.Device.CreateBuffer(vertices, D3D11.BindFlags.VertexBuffer);

            vertexShader = FrostyShaderDb.GetShaderWithSignature<D3D11.ID3D11VertexShader>(Viewport.Device, "Texture", out byte[] signature);
            pixelShader = FrostyShaderDb.GetShader<D3D11.ID3D11PixelShader>(Viewport.Device, "Texture");

            D3D11.InputElementDescription[] elements = new D3D11.InputElementDescription[]
            {
                new D3D11.InputElementDescription("POSITION", 0, Vortice.DXGI.Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0),
                new D3D11.InputElementDescription("TEXCOORD", 0, Vortice.DXGI.Format.R32G32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0)
            };
            inputLayout = Viewport.Device.CreateInputLayout(elements, signature);

            constantBuffer = Viewport.Device.CreateBuffer((uint)(Marshal.SizeOf<Constants>()), D3D11.BindFlags.ConstantBuffer,
              D3D11.ResourceUsage.Default, D3D11.CpuAccessFlags.None, D3D11.ResourceOptionFlags.None, 0);
        }
    }
}
