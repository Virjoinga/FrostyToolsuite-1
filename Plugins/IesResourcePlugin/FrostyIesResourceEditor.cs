using System;
using FrostySdk.Interfaces;
using System.Windows;
using System.Runtime.InteropServices;
using Frosty.Core.Viewport;
using D3D11 = Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Frosty.Core.Controls;
using Frosty.Core;
using Vortice.Mathematics;
using System.Numerics;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice;

namespace IesResourcePlugin
{
    #region -- Screen --
    class IesResourceScreen : Screen
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

        private IesResource resource;

        public IesResourceScreen(IesResource inResource)
        {
            resource = inResource;
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
            Vortice.Mathematics.Viewport[] viewports = Viewport.Context.RSGetViewports<Vortice.Mathematics.Viewport>().ToArray();

            Constants constants = new Constants
            {
                TextureDim =
                {
                    X = resource.Size,
                    Y = resource.Size
                },
                ViewportDim =
                {
                    X = viewports[0].Width,
                    Y = viewports[0].Height
                },
                ChannelMask = Matrix4x4.Identity,
                SrgbEnabled = 0.0f,
                MipLevel = 0,
                SliceLevel = 0
            };

            Viewport.Context.ClearRenderTargetView(Viewport.ColorBufferRTV, new Color4(0, 0, 0, 0));
            Viewport.Context.UpdateSubresource(in constants, constantBuffer, 0);

            Viewport.Context.OMSetBlendState(blendState);
            Viewport.Context.OMSetDepthStencilState(depthStencilState);
            Viewport.Context.RSSetState(rasterizerState);

            Viewport.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
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

            textureSRV.Dispose();
            texture.Dispose();
            vertexBuffer.Dispose();
            constantBuffer.Dispose();

            samplerState.Dispose();
            blendState.Dispose();
            rasterizerState.Dispose();
            depthStencilState.Dispose();
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

            D3D11.BlendDescription desc = new();
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
                Filter = D3D11.Filter.Anisotropic,
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
            texture = LoadTexture(Viewport.Device, resource);

            // all texture types are represented by a 2D array (even a single T2D, just has one slice)
            textureSRV = Viewport.Device.CreateShaderResourceView(texture, new D3D11.ShaderResourceViewDescription()
            {
                Format = texture.Description.Format,
                ViewDimension = ShaderResourceViewDimension.Texture2DArray,
                Texture2DArray = new D3D11.Texture2DArrayShaderResourceView()
                {
                    ArraySize = texture.Description.ArraySize,
                    FirstArraySlice = 0,
                    MipLevels = unchecked((uint)(-1)),
                    MostDetailedMip = 0
                }
            });
        }

        private D3D11.ID3D11Texture2D LoadTexture(D3D11.ID3D11Device device, IesResource resource)
        {
            resource.Data.Position = 0;

            byte[] buffer = new byte[resource.Data.Length];
            resource.Data.Read(buffer, 0, (int)resource.Data.Length);

            D3D11.Texture2DDescription desc = new D3D11.Texture2DDescription()
            {
                BindFlags = D3D11.BindFlags.ShaderResource,
                Format = Format.R16_Float,
                Width = (uint)(resource.Size),
                Height = (uint)(resource.Size),
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = D3D11.ResourceUsage.Default,
                MiscFlags = D3D11.ResourceOptionFlags.None,
                CPUAccessFlags = D3D11.CpuAccessFlags.None,
                ArraySize = 1
            };

            D3D11.ID3D11Texture2D texture = device.CreateTexture2D(desc);

            ReadOnlySpan<byte> data = buffer;
            device.ImmediateContext.UpdateSubresource(data, texture, texture.CalculateSubResourceIndex(0, 0, out _), (uint)(resource.Size * (FormatHelper.GetBitsPerPixel(desc.Format) / 8)), 0);

            return texture;
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
            pixelShader = FrostyShaderDb.GetShader<D3D11.ID3D11PixelShader>(Viewport.Device, "IesResource");

            D3D11.InputElementDescription[] elements = new D3D11.InputElementDescription[]
            {
                new D3D11.InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0),
                new D3D11.InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0)
            };
            inputLayout = Viewport.Device.CreateInputLayout(elements, signature);

            constantBuffer = Viewport.Device.CreateBuffer((uint)(Marshal.SizeOf<Constants>()), D3D11.BindFlags.ConstantBuffer,
                D3D11.ResourceUsage.Default, D3D11.CpuAccessFlags.None, D3D11.ResourceOptionFlags.None, 0);
        }
    }
    #endregion

    [TemplatePart(Name = PART_Renderer, Type = typeof(FrostyViewport))]
    public class FrostyIesResourceEditor : FrostyAssetEditor
    {
        private const string PART_Renderer = "PART_Renderer";

        private FrostyViewport renderer;
        private IesResource resource;
        private IesResourceScreen screen;
        private bool firstTimeLoad = true;

        public FrostyIesResourceEditor(ILogger inLogger) 
            : base(inLogger)
        {
        }

        static FrostyIesResourceEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyIesResourceEditor), new FrameworkPropertyMetadata(typeof(FrostyIesResourceEditor)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            renderer = GetTemplateChild(PART_Renderer) as FrostyViewport;
            Loaded += FrostyAtlasTextureEditor_Loaded;
        }

        private void FrostyAtlasTextureEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstTimeLoad)
            {
                ulong resRid = ((dynamic)RootObject).SourceResource;

                resource = App.AssetManager.GetResAs<IesResource>(App.AssetManager.GetResEntry(resRid));
                screen = new IesResourceScreen(resource);

                firstTimeLoad = false;
            }

            renderer.Screen = screen;
            renderer.Width = 1024;
            renderer.Height = 1024;
        }
    }
}
