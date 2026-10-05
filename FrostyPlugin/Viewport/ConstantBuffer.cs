using System;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct3D11;

namespace Frosty.Core.Viewport
{
    public class ConstantBuffer<T> : IDisposable where T : unmanaged
    {
        public ID3D11Buffer Buffer { get; private set; } = null;

        public ConstantBuffer(ID3D11Device device, T value)
        {
            BufferDescription description = new BufferDescription((uint)(Marshal.SizeOf<T>()), BindFlags.ConstantBuffer, ResourceUsage.Dynamic) { CPUAccessFlags = CpuAccessFlags.Write };
            Buffer = device.CreateBuffer(description);
            UpdateData(device.ImmediateContext, value);
        }

        public void UpdateData(ID3D11DeviceContext c, T value)
        {
            c.Map(Buffer, 0, MapMode.WriteDiscard, MapFlags.None, out MappedSubresource mappedResource);
            using DataStream stream = new(mappedResource.DataPointer, Buffer.Description.ByteWidth, true, true);
            stream.Write(value);
            c.Unmap(Buffer, 0);
        }

        public void Dispose()
        {
            Buffer.Dispose();
        }
    }
}
