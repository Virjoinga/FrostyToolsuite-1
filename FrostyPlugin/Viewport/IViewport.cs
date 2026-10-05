using Vortice.Direct3D11;

namespace Frosty.Core.Viewport
{
    public interface IViewport
    {
        Vortice.DXGI.IDXGISwapChain SwapChain { get; }
        ID3D11Device Device { get; }
        ID3D11DeviceContext Context { get; }
        ID3D11Texture2D ColorBuffer { get; }
        ID3D11RenderTargetView ColorBufferRTV { get; }
        ID3D11Texture2D DepthBuffer { get; }
        ID3D11DepthStencilView DepthBufferDSV { get; }
        ID3D11ShaderResourceView DepthBufferSRV { get; }

        int ViewportWidth { get; }
        int ViewportHeight { get; }
        float LastFrameTime { get; }
        float TotalTime { get; }

        Screen Screen { get; set; }

        System.Windows.Point TranslateMousePointToScreen(System.Windows.Point point);
    }
}
