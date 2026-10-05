using FrostySdk.IO;
using FrostySdk.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using D3D11 = Vortice.Direct3D11;

namespace Frosty.Core.Viewport
{
    public class FrostyDeviceManager
    {
        private class Hashing
        {
            public static uint Hash(D3D11.SamplerDescription desc)
            {
                uint hash = 2166136261;
                hash = (hash * 16777619) ^ (uint)((int)desc.AddressU).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.AddressV).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.AddressW).GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.BorderColor.R.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.BorderColor.G.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.BorderColor.B.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.BorderColor.A.GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.ComparisonFunc).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.Filter).GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.MaxAnisotropy.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.MaxLOD.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.MinLOD.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.MipLODBias.GetHashCode();
                return hash;
            }

            public static uint Hash(D3D11.RasterizerDescription desc)
            {
                uint hash = 2166136261;
                hash = (hash * 16777619) ^ (uint)((int)desc.CullMode).GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.DepthBias.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.DepthBiasClamp.GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.FillMode).GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.AntialiasedLineEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.DepthClipEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.FrontCounterClockwise.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.MultisampleEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.ScissorEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.SlopeScaledDepthBias.GetHashCode();
                return hash;
            }

            public static uint Hash(D3D11.DepthStencilDescription desc)
            {
                uint hash = 2166136261;
                hash = (hash * 16777619) ^ (uint)((int)desc.BackFace.StencilFunc).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.BackFace.StencilDepthFailOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.BackFace.StencilFailOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.BackFace.StencilPassOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.FrontFace.StencilFunc).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.FrontFace.StencilDepthFailOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.FrontFace.StencilFailOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.FrontFace.StencilPassOp).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.DepthFunc).GetHashCode();
                hash = (hash * 16777619) ^ (uint)((int)desc.DepthWriteMask).GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.DepthEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.StencilEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.StencilReadMask.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.StencilWriteMask.GetHashCode();
                return hash;
            }

            public static unsafe uint Hash(D3D11.BlendDescription desc)
            {
                uint hash = 2166136261;
                hash = (hash * 16777619) ^ (uint)desc.AlphaToCoverageEnable.GetHashCode();
                hash = (hash * 16777619) ^ (uint)desc.IndependentBlendEnable.GetHashCode();
                const int DESC_COUNT = 8;
                for (int i = 0; i < DESC_COUNT; i++)
                {
                    RenderTargetBlendDescription rtDesc = desc.RenderTarget[i];
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.BlendOperationAlpha).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.BlendOperation).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.DestinationBlendAlpha).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.DestinationBlend).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.RenderTargetWriteMask).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.SourceBlendAlpha).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)((int)rtDesc.SourceBlend).GetHashCode();
                    hash = (hash * 16777619) ^ (uint)rtDesc.BlendEnable.GetHashCode();
                }
                return hash;
            }
        }

        #region -- Singleton --
        public static FrostyDeviceManager Current => current ?? (current = new FrostyDeviceManager());

        private static FrostyDeviceManager current;
        private FrostyDeviceManager()
        {
        }
        #endregion

        private D3D11.ID3D11Device device;
        private D3D11.Debug.ID3D11Debug debugDevice;

        // state lists
        private Dictionary<uint, D3D11.ID3D11SamplerState> samplerStates = new();
        private Dictionary<uint, D3D11.ID3D11DepthStencilState> depthStencilStates = new();
        private Dictionary<uint, D3D11.ID3D11BlendState> blendStates = new();
        private Dictionary<uint, D3D11.ID3D11RasterizerState> rasterizerStates = new();

        public Controls.FrostyViewport CurrentViewport { get; set; }

        /// <summary>
        /// Returns the global D3D11 device (creates it if necessary)
        /// </summary>
        public D3D11.ID3D11Device GetDevice()
        {
            if (device == null)
            {
                D3D11.DeviceCreationFlags flags = D3D11.DeviceCreationFlags.BgraSupport;
#if DEBUG
                flags |= D3D11.DeviceCreationFlags.Debug;
#endif

                var adapterIndex = Config.Get<uint>("RenderAdapterIndex", 0);
                //int adapterIndex = Config.Get<int>("Render", "AdapterIndex", 0);
                DXGI.CreateDXGIFactory1(out IDXGIFactory1 factory);
                factory.EnumAdapters(adapterIndex, out IDXGIAdapter adapter);

                App.Logger.Log("Display Adapters:");
                uint index = 0;

                while (factory.EnumAdapters(index, out IDXGIAdapter currentAdapter) != Vortice.DXGI.ResultCode.NotFound)
                {
                    App.Logger.Log(string.Format("  {0}: {1}", index++, currentAdapter.Description.Description));
                }

                D3D11.D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, flags, new Vortice.Direct3D.FeatureLevel[]
                {
                    Vortice.Direct3D.FeatureLevel.Level_11_0
                }, out device);
#if DEBUG
                debugDevice = device.QueryInterface<D3D11.Debug.ID3D11Debug>();
#endif
                factory.Dispose();
                App.Logger.Log(string.Format("Selected D3D11 Adapter {0}: {1}", adapterIndex, adapter.Description.Description));

                // keep device around until frosty is shutting down
                Application.Current.Dispatcher.ShutdownStarted += Dispatcher_ShutdownStarted;
            }

            return device;
        }

        public D3D11.ID3D11SamplerState GetOrCreateSamplerState(D3D11.SamplerDescription desc)
        {
            uint hash = Hashing.Hash(desc);
            if (!samplerStates.ContainsKey(hash))
                samplerStates.Add(hash, GetDevice().CreateSamplerState(desc));
            return samplerStates[hash];
        }

        public D3D11.ID3D11DepthStencilState GetOrCreateDepthStencilState(D3D11.DepthStencilDescription desc)
        {
            uint hash = Hashing.Hash(desc);
            if (!depthStencilStates.ContainsKey(hash))
                depthStencilStates.Add(hash, GetDevice().CreateDepthStencilState(desc));
            return depthStencilStates[hash];
        }

        public D3D11.ID3D11BlendState GetOrCreateBlendState(D3D11.BlendDescription desc)
        {
            uint hash = Hashing.Hash(desc);
            if (!blendStates.ContainsKey(hash))
                blendStates.Add(hash, GetDevice().CreateBlendState(desc));
            return blendStates[hash];
        }

        public D3D11.ID3D11RasterizerState GetOrCreateRasterizerState(D3D11.RasterizerDescription desc)
        {
            uint hash = Hashing.Hash(desc);
            if (!rasterizerStates.ContainsKey(hash))
                rasterizerStates.Add(hash, GetDevice().CreateRasterizerState(desc));
            return rasterizerStates[hash];
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            // if there is an active viewport, make sure it is shutdown 
            // before the device

            CurrentViewport?.Shutdown();

            // destroy device
            DisposeDevice();
        }

        public void DisposeDevice()
        {
            device?.Dispose();
            device = null;

#if DEBUG
            debugDevice.ReportLiveDeviceObjects(D3D11.Debug.ReportLiveDeviceObjectFlags.Detail | D3D11.Debug.ReportLiveDeviceObjectFlags.IgnoreInternal);
            debugDevice.Dispose();
#endif
        }
    }

    public class D3DUtils
    {
        public static D3D11.ID3D11SamplerState CreateSamplerState(D3D11.SamplerDescription desc) { return FrostyDeviceManager.Current.GetOrCreateSamplerState(desc); }
        public static D3D11.ID3D11BlendState CreateBlendState(D3D11.BlendDescription desc) { return FrostyDeviceManager.Current.GetOrCreateBlendState(desc); }
        public static D3D11.ID3D11RasterizerState CreateRasterizerState(D3D11.RasterizerDescription desc) { return FrostyDeviceManager.Current.GetOrCreateRasterizerState(desc); }
        public static D3D11.ID3D11DepthStencilState CreateDepthStencilState(D3D11.DepthStencilDescription desc) { return FrostyDeviceManager.Current.GetOrCreateDepthStencilState(desc); }

        public static D3D11.ID3D11DepthStencilState CreateDepthStencilState(
            bool depthEnabled = true,
            D3D11.DepthWriteMask depthWriteMask = D3D11.DepthWriteMask.All,
            D3D11.ComparisonFunction depthComparison = D3D11.ComparisonFunction.Less,
            bool stencilEnabled = false,
            byte stencilReadMask = 0xFF,
            byte stencilWriteMask = 0xFF,
            D3D11.DepthStencilOperationDescription? frontFace = null,
            D3D11.DepthStencilOperationDescription? backFace = null)
        {
            D3D11.DepthStencilDescription desc = new()
            {
                DepthEnable = depthEnabled,
                DepthWriteMask = depthWriteMask,
                DepthFunc = depthComparison,
                StencilEnable = stencilEnabled,
                StencilReadMask = stencilReadMask,
                StencilWriteMask = stencilWriteMask
            };
            if (frontFace.HasValue)
                desc.FrontFace = frontFace.Value;
            if (backFace.HasValue)
                desc.BackFace = backFace.Value;

            return CreateDepthStencilState(desc);
        }

        public static D3D11.ID3D11SamplerState CreateSamplerState(
            D3D11.TextureAddressMode address = D3D11.TextureAddressMode.Wrap,
            D3D11.TextureAddressMode addressU = 0,
            D3D11.TextureAddressMode addressV = 0,
            D3D11.TextureAddressMode addressW = 0,
            Color? borderColor = null,
            D3D11.ComparisonFunction comparisonFunc = D3D11.ComparisonFunction.Always,
            D3D11.Filter filter = D3D11.Filter.MinMagMipLinear,
            int maxAniso = 16,
            float maxLod = 20,
            float minLod = 0,
            float mipLodBias = 0)
        {
            D3D11.SamplerDescription desc = new()
            {
                AddressU = (addressU != 0) ? addressU : address,
                AddressV = (addressV != 0) ? addressV : address,
                AddressW = (addressW != 0) ? addressW : address,
                BorderColor = (borderColor.HasValue) ? borderColor.Value : Colors.Black,
                ComparisonFunc = comparisonFunc,
                Filter = filter,
                MaxAnisotropy = (uint)(maxAniso),
                MaxLOD = maxLod,
                MinLOD = minLod,
                MipLODBias = mipLodBias
            };
            return CreateSamplerState(desc);
        }

        public static D3D11.ID3D11RasterizerState CreateRasterizerState(
            D3D11.CullMode cullMode = D3D11.CullMode.Back,
            D3D11.FillMode fillMode = D3D11.FillMode.Solid,
            bool antialiasedLines = false,
            bool depthClip = false,
            bool frontCounterClockwise = false,
            bool multisampled = false,
            bool scissor = false,
            int depthBias = 0,
            float depthBiasClamp = 0.0f,
            float slopeScaledDepthBias = 0.0f)
        {
            D3D11.RasterizerDescription desc = new()
            {
                CullMode = cullMode,
                DepthBias = depthBias,
                DepthBiasClamp = depthBiasClamp,
                FillMode = fillMode,
                AntialiasedLineEnable = antialiasedLines,
                DepthClipEnable = depthClip,
                FrontCounterClockwise = frontCounterClockwise,
                MultisampleEnable = multisampled,
                ScissorEnable = scissor,
                SlopeScaledDepthBias = slopeScaledDepthBias
            };
            return CreateRasterizerState(desc);
        }

        public static D3D11.RenderTargetBlendDescription CreateBlendStateRenderTarget(bool alphaBlend = false)
        {
            D3D11.RenderTargetBlendDescription rtDesc = new()
            {
                BlendEnable = false,
                SourceBlend = D3D11.Blend.One,
                DestinationBlend = D3D11.Blend.Zero,
                BlendOperation = D3D11.BlendOperation.Add,
                SourceBlendAlpha = D3D11.Blend.One,
                DestinationBlendAlpha = D3D11.Blend.Zero,
                BlendOperationAlpha = D3D11.BlendOperation.Add,
                RenderTargetWriteMask = D3D11.ColorWriteEnable.All
            };
            if (alphaBlend)
            {
                rtDesc.SourceBlend = D3D11.Blend.SourceAlpha;
                rtDesc.DestinationBlend = D3D11.Blend.InverseSourceAlpha;
                rtDesc.BlendOperation = D3D11.BlendOperation.Add;
                rtDesc.SourceBlendAlpha = D3D11.Blend.One;
                rtDesc.DestinationBlendAlpha = D3D11.Blend.One;
                rtDesc.BlendOperationAlpha = D3D11.BlendOperation.Add;
            }
            return rtDesc;
        }

        public static D3D11.ID3D11BlendState CreateBlendState(
            params D3D11.RenderTargetBlendDescription[] targets)
        {
            D3D11.BlendDescription desc = new() { IndependentBlendEnable = targets.Length > 1 };
            for (int i = 0; i < targets.Length; i++)
            {
                desc.RenderTarget[i] = targets[i];
                desc.RenderTarget[i].BlendEnable = true;
            }
            return CreateBlendState(desc);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        public static void BeginPerfEvent(D3D11.ID3D11DeviceContext context, string name)
        {
#if FROSTY_DEVELOPER
            D3D11.ID3DUserDefinedAnnotation annotation = context.QueryInterface<D3D11.ID3DUserDefinedAnnotation>();
            annotation?.BeginEvent(name);
#endif
        }

        /// <summary>
        /// 
        /// </summary>
        public static void EndPerfEvent(D3D11.ID3D11DeviceContext context)
        {
#if FROSTY_DEVELOPER
            D3D11.ID3DUserDefinedAnnotation annotation = context.QueryInterface<D3D11.ID3DUserDefinedAnnotation>();
            annotation?.EndEvent();
#endif
        }
    }

    public static class TextureUtils
    {
        #region -- DDS --
        public enum DDSCaps
        {
            Complex = 0x08,
            MipMap = 0x400000,
            Texture = 0x1000
        };

        [Flags]
        public enum DDSCaps2
        {
            CubeMap = 0x200,
            CubeMapPositiveX = 0x400,
            CubeMapNegativeX = 0x800,
            CubeMapPositiveY = 0x1000,
            CubeMapNegativeY = 0x2000,
            CubeMapPositiveZ = 0x4000,
            CubeMapNegativeZ = 0x8000,
            Volume = 0x200000,

            CubeMapAllFaces = CubeMapPositiveX | CubeMapPositiveY | CubeMapPositiveZ | CubeMapNegativeX | CubeMapNegativeY | CubeMapNegativeZ
        }

        [Flags]
        public enum DDSFlags
        {
            Caps = 0x01,
            Height = 0x02,
            Width = 0x04,
            Pitch = 0x08,
            PixelFormat = 0x1000,
            MipMapCount = 0x20000,
            LinearSize = 0x80000,
            Depth = 0x800000,

            Required = Caps | Height | Width | PixelFormat
        }

        [Flags]
        public enum DDSPFFlags
        {
            AlphaPixels = 0x01,
            Alpha = 0x02,
            FourCC = 0x04,
            RGB = 0x40,
            YUV = 0x200,
            Luminance = 0x20000
        }

        public struct DDSHeaderDX10
        {
            public Vortice.DXGI.Format dxgiFormat;
            public D3D11.ResourceDimension resourceDimension;
            public uint miscFlag;
            public uint arraySize;
            public uint miscFlags2;
        }

        public struct DDSPixelFormat
        {
            public int dwSize;
            public DDSPFFlags dwFlags;
            public int dwFourCC;
            public int dwRGBBitCount;
            public uint dwRBitMask;
            public uint dwGBitMask;
            public uint dwBBitMask;
            public uint dwABitMask;
        }

        public class DDSHeader
        {
            public int dwMagic;
            public int dwSize;
            public DDSFlags dwFlags;
            public int dwHeight;
            public int dwWidth;
            public int dwPitchOrLinearSize;
            public int dwDepth;
            public int dwMipMapCount;
            public int[] dwReserved1;
            public DDSPixelFormat ddspf;
            public DDSCaps dwCaps;
            public DDSCaps2 dwCaps2;
            public int dwCaps3;
            public int dwCaps4;
            public int dwReserved2;

            public bool HasExtendedHeader;
            public DDSHeaderDX10 ExtendedHeader;

            public DDSHeader()
            {
                dwMagic = 0x20534444;
                dwSize = 0x7C;
                dwFlags = DDSFlags.Required;
                dwDepth = 0;
                dwCaps = DDSCaps.Texture;
                dwCaps2 = 0;
                dwReserved1 = new int[11];
                ddspf.dwSize = 0x20;
                ddspf.dwFlags = DDSPFFlags.FourCC;
                HasExtendedHeader = false;
            }

            public void Write(NativeWriter writer)
            {
                writer.Write(dwMagic);
                writer.Write(dwSize);
                writer.Write((int)dwFlags);
                writer.Write(dwHeight);
                writer.Write(dwWidth);
                writer.Write(dwPitchOrLinearSize);
                writer.Write(dwDepth);
                writer.Write(dwMipMapCount);
                for (int i = 0; i < 11; i++) writer.Write(dwReserved1[i]);
                writer.Write(ddspf.dwSize);
                writer.Write((int)ddspf.dwFlags);
                writer.Write(ddspf.dwFourCC);
                writer.Write(ddspf.dwRGBBitCount);
                writer.Write(ddspf.dwRBitMask);
                writer.Write(ddspf.dwGBitMask);
                writer.Write(ddspf.dwBBitMask);
                writer.Write(ddspf.dwABitMask);
                writer.Write((int)dwCaps);
                writer.Write((int)dwCaps2);
                writer.Write(dwCaps3);
                writer.Write(dwCaps4);
                writer.Write(dwReserved2);

                if (HasExtendedHeader)
                {
                    writer.Write((uint)ExtendedHeader.dxgiFormat);
                    writer.Write((uint)ExtendedHeader.resourceDimension);
                    writer.Write(ExtendedHeader.miscFlag);
                    writer.Write(ExtendedHeader.arraySize);
                    writer.Write(ExtendedHeader.miscFlags2);
                }
            }

            public bool Read(NativeReader reader)
            {
                dwMagic = reader.ReadInt();
                if (dwMagic != 0x20534444)
                    return false;

                dwSize = reader.ReadInt();
                if (dwSize != 0x7C)
                    return false;

                dwFlags = (DDSFlags)reader.ReadInt();
                dwHeight = reader.ReadInt();
                dwWidth = reader.ReadInt();
                dwPitchOrLinearSize = reader.ReadInt();
                dwDepth = reader.ReadInt();
                dwReserved1 = new int[11];
                dwMipMapCount = reader.ReadInt();
                for (int i = 0; i < 11; i++)
                    dwReserved1[i] = reader.ReadInt();
                ddspf.dwSize = reader.ReadInt();
                ddspf.dwFlags = (DDSPFFlags)reader.ReadInt();
                ddspf.dwFourCC = reader.ReadInt();
                ddspf.dwRGBBitCount = reader.ReadInt();
                ddspf.dwRBitMask = reader.ReadUInt();
                ddspf.dwGBitMask = reader.ReadUInt();
                ddspf.dwBBitMask = reader.ReadUInt();
                ddspf.dwABitMask = reader.ReadUInt();
                dwCaps = (DDSCaps)reader.ReadInt();
                dwCaps2 = (DDSCaps2)reader.ReadInt();
                dwCaps3 = reader.ReadInt();
                dwCaps4 = reader.ReadInt();
                dwReserved2 = reader.ReadInt();

                if (ddspf.dwFourCC == 0x30315844)
                {
                    HasExtendedHeader = true;
                    ExtendedHeader.dxgiFormat = (Vortice.DXGI.Format)reader.ReadUInt();
                    ExtendedHeader.resourceDimension = (D3D11.ResourceDimension)reader.ReadUInt();
                    ExtendedHeader.miscFlag = reader.ReadUInt();
                    ExtendedHeader.arraySize = reader.ReadUInt();
                    ExtendedHeader.miscFlags2 = reader.ReadUInt();
                }

                return true;
            }
        }
        #endregion


        #region -- Texture Loading --
        public static Vortice.DXGI.Format ToTextureFormat(string pixelFormat, bool bLegacySrgb = false)
        {
            if (bLegacySrgb)
            {
                if (pixelFormat.StartsWith("BC") && bLegacySrgb)
                    pixelFormat = pixelFormat.Replace("UNORM", "SRGB");
            }
            switch (pixelFormat)
            {
                //case "DXT1": return Vortice.DXGI.Format.BC1_UNorm;
                case "NormalDXT1": return Vortice.DXGI.Format.BC1_Typeless;
                case "NormalDXN": return Vortice.DXGI.Format.BC5_Typeless;
                //case "DXT1A": return Vortice.DXGI.Format.BC1_UNorm;
                case "BC1A_SRGB": return Vortice.DXGI.Format.BC1_Typeless;
                case "BC1A_UNORM": return Vortice.DXGI.Format.BC1_Typeless;
                case "BC1_SRGB": return Vortice.DXGI.Format.BC1_Typeless;
                case "BC1_UNORM": return Vortice.DXGI.Format.BC1_Typeless;
                case "BC2_SRGB": return Vortice.DXGI.Format.BC2_Typeless;
                case "BC2_UNORM": return Vortice.DXGI.Format.BC2_Typeless;
                //case "DXT3": return Vortice.DXGI.Format.BC2_UNorm;
                case "BC3_SRGB": return Vortice.DXGI.Format.BC3_Typeless;
                case "BC3_UNORM": return Vortice.DXGI.Format.BC3_Typeless;
                case "BC3A_UNORM": return Vortice.DXGI.Format.BC3_Typeless;
                case "BC3A_SRGB": return Vortice.DXGI.Format.BC3_Typeless;
                case "BC4_UNORM": return Vortice.DXGI.Format.BC4_Typeless;
                //case "DXT5": return Vortice.DXGI.Format.BC3_UNorm;
                //case "DXT5A": return Vortice.DXGI.Format.BC3_UNorm;
                case "BC5_UNORM": return Vortice.DXGI.Format.BC5_Typeless;
                case "BC6U_FLOAT": return Vortice.DXGI.Format.BC6H_Uf16;
                case "BC7": return Vortice.DXGI.Format.BC7_Typeless;
                case "BC7_SRGB": return Vortice.DXGI.Format.BC7_Typeless;
                case "BC7_UNORM": return Vortice.DXGI.Format.BC7_Typeless;
                case "R8_UNORM": return Vortice.DXGI.Format.R8_Typeless;
                case "R16G16B16A16_FLOAT": return Vortice.DXGI.Format.R16G16B16A16_Float;
                case "ARGB32F": return Vortice.DXGI.Format.R32G32B32A32_Float;
                case "R32G32B32A32_FLOAT": return Vortice.DXGI.Format.R32G32B32A32_Float;
                case "R9G9B9E5F": return Vortice.DXGI.Format.R9G9B9E5_SharedExp;
                case "R9G9B9E5_FLOAT": return Vortice.DXGI.Format.R9G9B9E5_SharedExp;
                case "R8G8B8A8_UNORM": return Vortice.DXGI.Format.R8G8B8A8_Typeless;
                case "R8G8B8A8_SRGB": return Vortice.DXGI.Format.R8G8B8A8_Typeless;
                case "B8G8R8A8_UNORM": return Vortice.DXGI.Format.B8G8R8A8_Typeless;
                case "R10G10B10A2_UNORM": return Vortice.DXGI.Format.R10G10B10A2_Typeless;
                case "L8": return Vortice.DXGI.Format.R8_Typeless;
                case "L16": return Vortice.DXGI.Format.R16_Typeless;
                case "ARGB8888": return Vortice.DXGI.Format.R8G8B8A8_Typeless;
                case "R16G16_UNORM": return Vortice.DXGI.Format.R16G16_Typeless;
                case "D16_UNORM": return Vortice.DXGI.Format.R16_UNorm;
                default: return Vortice.DXGI.Format.Unknown;
            }
        }

        public static Vortice.DXGI.Format ToShaderFormat(string pixelFormat, bool bLegacySrgb = false)
        {
            if (bLegacySrgb)
            {
                if (pixelFormat.StartsWith("BC") && bLegacySrgb)
                    pixelFormat = pixelFormat.Replace("UNORM", "SRGB");
            }

            switch (pixelFormat)
            {
                //case "DXT1": return Vortice.DXGI.Format.BC1_UNorm;
                case "NormalDXT1": return Vortice.DXGI.Format.BC1_UNorm;
                case "NormalDXN": return Vortice.DXGI.Format.BC5_UNorm;
                //case "DXT1A": return Vortice.DXGI.Format.BC1_UNorm;
                case "BC1A_SRGB": return Vortice.DXGI.Format.BC1_UNorm_SRgb;
                case "BC1A_UNORM": return Vortice.DXGI.Format.BC1_UNorm;
                case "BC1_SRGB": return Vortice.DXGI.Format.BC1_UNorm_SRgb;
                case "BC1_UNORM": return Vortice.DXGI.Format.BC1_UNorm;
                case "BC2_SRGB": return Vortice.DXGI.Format.BC2_UNorm_SRgb;
                case "BC2_UNORM": return Vortice.DXGI.Format.BC2_UNorm;
                //case "DXT3": return Vortice.DXGI.Format.BC2_UNorm;
                case "BC3_SRGB": return Vortice.DXGI.Format.BC3_UNorm_SRgb;
                case "BC3_UNORM": return Vortice.DXGI.Format.BC3_UNorm;
                case "BC3A_UNORM": return Vortice.DXGI.Format.BC3_UNorm;
                case "BC3A_SRGB": return Vortice.DXGI.Format.BC3_UNorm_SRgb;
                case "BC4_UNORM": return Vortice.DXGI.Format.BC4_UNorm;
                //case "DXT5": return Vortice.DXGI.Format.BC3_UNorm;
                //case "DXT5A": return Vortice.DXGI.Format.BC3_UNorm;
                case "BC5_UNORM": return Vortice.DXGI.Format.BC5_UNorm;
                case "BC6U_FLOAT": return Vortice.DXGI.Format.BC6H_Uf16;
                case "BC7": return Vortice.DXGI.Format.BC7_UNorm;
                case "BC7_SRGB": return Vortice.DXGI.Format.BC7_UNorm_SRgb;
                case "BC7_UNORM": return Vortice.DXGI.Format.BC7_UNorm;
                case "R8_UNORM": return Vortice.DXGI.Format.R8_UNorm;
                case "R16G16B16A16_FLOAT": return Vortice.DXGI.Format.R16G16B16A16_Float;
                case "ARGB32F": return Vortice.DXGI.Format.R32G32B32A32_Float;
                case "R32G32B32A32_FLOAT": return Vortice.DXGI.Format.R32G32B32A32_Float;
                case "R9G9B9E5F": return Vortice.DXGI.Format.R9G9B9E5_SharedExp;
                case "R9G9B9E5_FLOAT": return Vortice.DXGI.Format.R9G9B9E5_SharedExp;
                case "R8G8B8A8_UNORM": return Vortice.DXGI.Format.R8G8B8A8_UNorm;
                case "R8G8B8A8_SRGB": return Vortice.DXGI.Format.R8G8B8A8_UNorm_SRgb;
                case "B8G8R8A8_UNORM": return Vortice.DXGI.Format.B8G8R8A8_UNorm;
                case "R10G10B10A2_UNORM": return Vortice.DXGI.Format.R10G10B10A2_UNorm;
                case "L8": return Vortice.DXGI.Format.R8_UNorm;
                case "L16": return Vortice.DXGI.Format.R16_UNorm;
                case "ARGB8888": return Vortice.DXGI.Format.R8G8B8A8_UNorm;
                case "R16G16_UNORM": return Vortice.DXGI.Format.R16G16_UNorm;
                case "D16_UNORM": return Vortice.DXGI.Format.R16_UNorm;
                default: return Vortice.DXGI.Format.Unknown;
            }
        }

        public static D3D11.ID3D11Texture2D LoadTexture(D3D11.ID3D11Device device, string filename, bool generateMips = false)
        {
            D3D11.ID3D11Texture2D texture = null;
            using (NativeReader reader = new NativeReader(new FileStream(filename, FileMode.Open, FileAccess.Read)))
            {
                DDSHeader header = new DDSHeader();
                header.Read(reader);

                Vortice.DXGI.Format format = Vortice.DXGI.Format.Unknown;
                int arraySize = ((header.dwCaps2 & DDSCaps2.CubeMap) != 0) ? 6 : 1;

                if (header.HasExtendedHeader)
                {
                    format = header.ExtendedHeader.dxgiFormat;
                }

                D3D11.BindFlags bindFlags = D3D11.BindFlags.ShaderResource;
                D3D11.ResourceOptionFlags roFlags = D3D11.ResourceOptionFlags.None;
                roFlags |= ((header.dwCaps2 & DDSCaps2.CubeMap) != 0) ? D3D11.ResourceOptionFlags.TextureCube : D3D11.ResourceOptionFlags.None;

                int mipCount = header.dwMipMapCount;
                if (generateMips && mipCount == 1)
                {
                    roFlags |= D3D11.ResourceOptionFlags.GenerateMips;
                    bindFlags |= D3D11.BindFlags.RenderTarget;
                    mipCount = 1 + (int)Math.Floor(Math.Log(Math.Max(header.dwWidth, header.dwHeight), 2));
                }

                D3D11.Texture2DDescription desc = new D3D11.Texture2DDescription()
                {
                    BindFlags = bindFlags,
                    Format = format,
                    Width = (uint)(header.dwWidth),
                    Height = (uint)(header.dwHeight),
                    MipLevels = (uint)(mipCount),
                    SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                    Usage = D3D11.ResourceUsage.Default,
                    MiscFlags = roFlags,
                    CPUAccessFlags = D3D11.CpuAccessFlags.None,
                    ArraySize = (uint)(arraySize)
                };

                texture = device.CreateTexture2D(desc);

                var stride = (int)((Vortice.DXGI.FormatHelper.IsCompressed(format))
                    ? Vortice.DXGI.FormatHelper.GetBitsPerPixel(format) / 2
                    : Vortice.DXGI.FormatHelper.GetBitsPerPixel(format) / 8);
                int minSize = (Vortice.DXGI.FormatHelper.IsCompressed(format)) ? 4 : 1;

                for (uint sliceIdx = 0; sliceIdx < arraySize; sliceIdx++)
                {
                    int width = header.dwWidth;
                    int height = header.dwHeight;

                    for (uint mipIdx = 0; mipIdx < header.dwMipMapCount; mipIdx++)
                    {
                        uint subResourceId = texture.CalculateSubResourceIndex(mipIdx, sliceIdx, out _);

                        int mipSize = mipSize = Vortice.DXGI.FormatHelper.IsCompressed(format)
                            ? Math.Max(1, ((width + 3) / 4)) * stride * height
                            : width * stride * height;

                        byte[] buffer = reader.ReadBytes(mipSize);

                        ReadOnlySpan<byte> data = buffer;
                        device.ImmediateContext.UpdateSubresource(data, texture, subResourceId, (uint)(width * stride), 0);

                        width >>= 1;
                        height >>= 1;
                        if (width < minSize) width = minSize;
                        if (height < minSize) height = minSize;
                    }
                }
            }

            return texture;
        }

        public static D3D11.ID3D11Texture2D LoadTexture(D3D11.ID3D11Device device, Texture textureAsset, bool generateMips = false)
        {
            textureAsset.Data.Position = 0;

            // cube arrays need to use both slice and depth
            int arraySize = (textureAsset.Type == TextureType.TT_CubeArray)
                ? textureAsset.Depth * textureAsset.SliceCount
                : textureAsset.Depth;

            ushort width = textureAsset.Width;
            ushort height = textureAsset.Height;

            Vortice.DXGI.Format format = TextureUtils.ToTextureFormat(textureAsset.PixelFormat, (textureAsset.Flags & TextureFlags.SrgbGamma) != 0);
            D3D11.ResourceOptionFlags roFlags = D3D11.ResourceOptionFlags.None;
            D3D11.BindFlags bindFlags = D3D11.BindFlags.ShaderResource;
            int mipCount = textureAsset.MipCount;

            if (textureAsset.Type == TextureType.TT_Cube)
                roFlags |= D3D11.ResourceOptionFlags.TextureCube;

            if (!Vortice.DXGI.FormatHelper.IsCompressed(format))
            {
                if (generateMips && mipCount == 1)
                {
                    roFlags |= D3D11.ResourceOptionFlags.GenerateMips;
                    bindFlags |= D3D11.BindFlags.RenderTarget;
                    mipCount = 1 + (int)Math.Floor(Math.Log(Math.Max(textureAsset.Width, textureAsset.Height), 2));
                }
            }

            D3D11.Texture2DDescription desc = new()
            {
                BindFlags = bindFlags,
                Format = format,
                Width = textureAsset.Width,
                Height = textureAsset.Height,
                MipLevels = (uint)(mipCount),
                SampleDescription = new Vortice.DXGI.SampleDescription(1, 0),
                Usage = D3D11.ResourceUsage.Default,
                MiscFlags = roFlags,
                CPUAccessFlags = D3D11.CpuAccessFlags.None,
                ArraySize = (uint)(arraySize)
            };
            D3D11.ID3D11Texture2D texture = device.CreateTexture2D(desc);

            // stride differs between compressed formats and standard
            var stride = (int)((Vortice.DXGI.FormatHelper.IsCompressed(format))
                ? Vortice.DXGI.FormatHelper.GetBitsPerPixel(format) / 2
                : Vortice.DXGI.FormatHelper.GetBitsPerPixel(format) / 8);

            // fill in texture data
            for (uint mip = 0; mip < textureAsset.MipCount; mip++)
            {
                int mipSize = (int)textureAsset.MipSizes[mip];
                if (textureAsset.Type == TextureType.TT_3d)
                {
                    mipSize = Vortice.DXGI.FormatHelper.IsCompressed(format)
                        ? Math.Max(1, ((width + 3) / 4)) * stride * height
                        : width * stride * height;
                }

                for (uint slice = 0; slice < arraySize; slice++)
                {
                    byte[] buffer = new byte[mipSize];
                    textureAsset.Data.Read(buffer, 0, buffer.Length);

                    ReadOnlySpan<byte> data = buffer;
                    device.ImmediateContext.UpdateSubresource(data, texture, texture.CalculateSubResourceIndex(mip, slice, out _), (uint)(width * stride), 0);
                }

                width >>= 1;
                height >>= 1;
                if (width < 1) width = 1;
                if (height < 1) height = 1;
            }

            return texture;
        }

        public static bool IsCompressedFormat(string pixelFormat)
        {
            bool isCompressed = true;
            switch (pixelFormat)
            {
                case "R8_UNORM":
                case "R16G16B16A16_FLOAT":
                case "R32G32B32A32_FLOAT":
                case "R9G9B9E5_FLOAT":
                case "R8G8B8A8_UNORM":
                case "R8G8B8A8_SRGB":
                case "B8G8R8A8_UNORM":
                case "R10G10B10A2_UNORM":
                case "ARGB32F":
                case "R9G9B9E5F":
                case "L8":
                case "L16":
                case "ARGB8888":
                case "D16_UNORM":
                    isCompressed = false;
                    break;
            }
            return isCompressed;
        }

        public static int GetFormatBlockSize(string pixelFormat)
        {
            int blockSize = 8;
            switch (pixelFormat)
            {
                case "L8":
                    blockSize = 8;
                    break;

                case "BC3_UNORM":
                case "BC3_SRGB":
                case "BC5_UNORM":
                case "BC5_SRGB":
                case "BC6U_FLOAT":
                case "BC7_UNORM":
                case "BC7_SRGB":
                case "NormalDXN":
                case "BC2_UNORM":
                case "BC3A_UNORM":
                case "L16":
                case "D16_UNORM":
                    blockSize = 16;
                    break;

                case "R9G9B9E5_FLOAT":
                case "R8G8B8A8_UNORM":
                case "R8G8B8A8_SRGB":
                case "B8G8R8A8_UNORM":
                case "R10G10B10A2_UNORM":
                case "R9G9B9E5F":
                case "ARGB8888":
                    blockSize = 32;
                    break;

                case "R16G16B16A16_FLOAT":
                    blockSize = 64;
                    break;

                case "R32G32B32A32_FLOAT":
                case "ARGB32F":
                    blockSize = 128;
                    break;
            }
            return blockSize;
        }
    }
    #endregion
}
