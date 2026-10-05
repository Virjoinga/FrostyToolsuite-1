using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Managers.Entries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice.Mathematics;
using D3D11 = Vortice.Direct3D11;

namespace AtlasTexturePlugin
{
    #region -- Screen --
    class AtlasTextureScreen : Screen
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

        public AtlasTexture TextureAsset
        {
            get => textureAsset;
            set
            {
                if (textureAsset != value)
                {
                    textureAsset = value;
                    ResetTexture();
                }
            }
        }
        private AtlasTexture textureAsset;

        public AtlasTextureScreen(AtlasTexture inTexture)
        {
            textureAsset = inTexture;
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
                    X = textureAsset.Width,
                    Y = textureAsset.Height
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
            texture = LoadTexture(Viewport.Device, textureAsset);

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

        private D3D11.ID3D11Texture2D LoadTexture(D3D11.ID3D11Device device, AtlasTexture textureAsset)
        {
            textureAsset.Data.Position = 0;

            byte[] buffer = new byte[textureAsset.Data.Length];
            textureAsset.Data.Read(buffer, 0, (int)textureAsset.Data.Length);

            D3D11.Texture2DDescription desc = new D3D11.Texture2DDescription()
            {
                BindFlags = D3D11.BindFlags.ShaderResource,
                Format = Format.BC3_UNorm,
                Width = textureAsset.Width,
                Height = textureAsset.Height,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = D3D11.ResourceUsage.Default,
                MiscFlags = D3D11.ResourceOptionFlags.None,
                CPUAccessFlags = D3D11.CpuAccessFlags.None,
                ArraySize = 1
            };

            D3D11.ID3D11Texture2D texture = device.CreateTexture2D(desc);

            ReadOnlySpan<byte> data = buffer;
            device.ImmediateContext.UpdateSubresource(data, texture, texture.CalculateSubResourceIndex(0, 0, out _), (textureAsset.Width * FormatHelper.GetBitsPerPixel(desc.Format) / 2), 0);

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
            pixelShader = FrostyShaderDb.GetShader<D3D11.ID3D11PixelShader>(Viewport.Device, "Texture");

            D3D11.InputElementDescription[] elements = new D3D11.InputElementDescription[]
            {
                new D3D11.InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0, D3D11.InputClassification.PerVertexData, 0),
                new D3D11.InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0, D3D11.InputClassification.PerVertexData, 0)
            };
            inputLayout = Viewport.Device.CreateInputLayout(elements, signature);

            constantBuffer = Viewport.Device.CreateBuffer((uint)(Marshal.SizeOf<Constants>()), D3D11.BindFlags.ConstantBuffer,
                D3D11.ResourceUsage.Default, D3D11.CpuAccessFlags.None, D3D11.ResourceOptionFlags.None, 0);
        }

        private void ResetTexture()
        {
            textureSRV.Dispose();
            texture.Dispose();

            CreateTextureResources();
        }
    }
    #endregion

    [TemplatePart(Name = PART_Renderer, Type = typeof(FrostyViewport))]
    public class FrostyAtlasTextureEditor : FrostyAssetEditor
    {
        private const string PART_Renderer = "PART_Renderer";

        #region -- GridVisible --
        public static readonly DependencyProperty GridVisibleProperty = DependencyProperty.Register("GridVisible", typeof(bool), typeof(FrostyAtlasTextureEditor), new FrameworkPropertyMetadata(true));
        public bool GridVisible
        {
            get => (bool)GetValue(GridVisibleProperty);
            set => SetValue(GridVisibleProperty, value);
        }
        #endregion

        private FrostyViewport renderer;
        private AtlasTexture texture;
        private AtlasTextureScreen screen;
        private bool firstTimeLoad = true;

        static FrostyAtlasTextureEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyAtlasTextureEditor), new FrameworkPropertyMetadata(typeof(FrostyAtlasTextureEditor)));
        }

        public FrostyAtlasTextureEditor(ILogger inLogger) 
            : base(inLogger)
        {
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            renderer = GetTemplateChild(PART_Renderer) as FrostyViewport;
            Loaded += FrostyAtlasTextureEditor_Loaded;
        }

        public override List<ToolbarItem> RegisterToolbarItems()
        {
            return new List<ToolbarItem>()
            {
                new ToolbarItem("Export", "Export Atlas Texture", "Images/Export.png", new RelayCommand((object state) => { ExportButton_Click(this, new RoutedEventArgs()); })),
                new ToolbarItem("Import", "Import Atlas Texture", "Images/Import.png", new RelayCommand((object state) => { ImportButton_Click(this, new RoutedEventArgs()); })),
            };
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Open DDS", "*.dds (DDS File)|*.dds", "AtlasTexture");
            if (ofd.ShowDialog())
            {
                EbxAssetEntry assetEntry = AssetEntry as EbxAssetEntry;
                ulong resRid = ((dynamic)RootObject).Resource;
                bool bFailed = false;

                FrostyTaskWindow.Show("Importing DDS", "", (task) =>
                {
                    TextureUtils.DDSHeader header = new TextureUtils.DDSHeader();
                    byte[] data = null;

                    using (NativeReader reader = new NativeReader(new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read)))
                    {
                        header.Read(reader);
                        data = reader.ReadToEnd();
                    }

                    // DXT5 (BC3) format check
                    if (header.dwMagic != 0x35545844 && (header.HasExtendedHeader && header.ExtendedHeader.dxgiFormat != Format.BC3_UNorm))
                    {
                        bFailed = true;
                        logger.LogError("Atlas textures must be DXT5 (BC3) format.");
                    }

                    // Maximum width/height check
                    if (header.dwWidth > 16384 || header.dwHeight > 16384)
                    {
                        bFailed = true;
                        logger.LogError("Atlas textures cannot be greater than 16384 pixels in width or height.");
                    }

                    if(!bFailed)
                    {
                        ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);
                        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(texture.ChunkId);

                        // revert any modifications
                        App.AssetManager.RevertAsset(assetEntry, dataOnly: true);

                        // modify chunk (for now)
                        App.AssetManager.ModifyChunk(chunkEntry.Id, data);

                        // modify res
                        AtlasTexture newTexture = new AtlasTexture(texture);
                        newTexture.SetData(header.dwWidth, header.dwHeight, chunkEntry.Id, App.AssetManager);
                        App.AssetManager.ModifyRes(resRid, newTexture);

                        // update linkage
                        resEntry.LinkAsset(App.AssetManager.GetChunkEntry(texture.ChunkId));
                        assetEntry.LinkAsset(resEntry);

                        texture = newTexture;
                    }
                });

                if (!bFailed)
                {
                    screen.TextureAsset = texture;

                    UpdateView();
                    InvokeOnAssetModified();

                    logger.Log("{0} successfully imported", ofd.FileName);
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save DDS", "*.dds (DDS File)|*.dds", "AtlasTexture", AssetEntry.Filename);
            if (sfd.ShowDialog())
            {
                FrostyTaskWindow.Show("Exporting Atlas texture", AssetEntry.Filename, (task) =>
                {
                    TextureUtils.DDSHeader header = new TextureUtils.DDSHeader
                    {
                        dwHeight = texture.Height,
                        dwWidth = texture.Width,
                        dwMipMapCount = texture.MipCount,
                        dwPitchOrLinearSize = (int)texture.Data.Length,
                        ddspf = {dwFourCC = 0x35545844}
                    };

                    texture.Data.Position = 0;
                    using (NativeWriter writer = new NativeWriter(new FileStream(sfd.FileName, FileMode.Create)))
                    {
                        header.Write(writer);

                        byte[] tmpBuf = new byte[texture.Data.Length];
                        texture.Data.Read(tmpBuf, 0, tmpBuf.Length);

                        writer.Write(tmpBuf);
                    }
                });

                logger.Log("Exported {0} to {1}", AssetEntry.Name, sfd.FileName);
            }
        }

        private void FrostyAtlasTextureEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstTimeLoad)
            {
                ulong resRid = ((dynamic)RootObject).Resource;

                texture = App.AssetManager.GetResAs<AtlasTexture>(App.AssetManager.GetResEntry(resRid));
                screen = new AtlasTextureScreen(texture);

                firstTimeLoad = false;
            }

            renderer.Screen = screen;
            UpdateView();
        }

        private void UpdateView()
        {
            float newWidth = texture.Width;
            float newHeight = texture.Height;

            if (newWidth > 2048)
            {
                newWidth = 2048;
                newHeight = (newHeight * (newWidth / texture.Width));
            }
            if (newHeight > 2048)
            {
                newHeight = 2048;
                newWidth = (newWidth * (newHeight / texture.Height));
            }

            renderer.Width = newWidth;
            renderer.Height = newHeight;

            // @todo: cleanup lines and re-add on refresh

            Grid grid = renderer.Parent as Grid;

            dynamic obj = RootObject;
            if (obj.AnimationFrameCount > 1)
            {
                // tile columns
                int animationColumnCount = obj.AnimationColumnCount;
                for (int i = 0; i < animationColumnCount - 1; i++)
                    grid.Children.Add(createLine(new Thickness(newWidth * ((i + 1) / (float)animationColumnCount), 0, 0, 0), 0, 0, 0, newHeight));

                // tile rows
                int animationRowCount = obj.AnimationFrameCount / animationColumnCount;
                for (int i = 0; i < animationRowCount - 1; i++)
                    grid.Children.Add(createLine(new Thickness(0, newHeight * ((i + 1) / (float)animationRowCount), 0, 0), 0, 0, newWidth, 0));

                // outline rectangle
                grid.Children.Add(createLine(new Thickness(1, 0, 0, 0), 0, 0, 0, newHeight));
                grid.Children.Add(createLine(new Thickness(newWidth - 1, 0, 0, 0), 0, 0, 0, newHeight));
                grid.Children.Add(createLine(new Thickness(0, 1, 0, 0), 0, 0, newWidth, 0));
                grid.Children.Add(createLine(new Thickness(0, newHeight - 1, 0, 0), 0, 0, newWidth, 0));
            }
        }

        private Line createLine(Thickness margin, double startX, double startY, double endX, double endY)
        {
            Line line = new Line
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = margin,
                StrokeThickness = 2.0,
                Stroke = System.Windows.Media.Brushes.LightGreen,
                X1 = startX,
                Y1 = startY,
                X2 = endX,
                Y2 = endY
            };

            return line;
        }
    }
}
