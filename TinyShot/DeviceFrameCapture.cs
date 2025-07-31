using System;
using System.Drawing;
using System.Drawing.Imaging;
using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using ResultCode = SharpDX.DXGI.ResultCode;

namespace TinyShot
{
    public class DeviceFrameCapture : IDisposable
    {
        private readonly Factory1 _factory;
        private readonly Adapter1 _adapter;
        private readonly Device _device;
        private readonly Output1 _output1;
        private readonly Texture2D _stagingTexture;
        private readonly OutputDuplication _duplicatedOutput;
        private readonly int _width;
        private readonly int _height;

        public DeviceFrameCapture(int adapterIndex = 0, int outputIndex = 0)
        {
            _factory = new Factory1();
            _adapter = _factory.GetAdapter1(adapterIndex);
            _device = new Device(_adapter);

            var output = _adapter.GetOutput(outputIndex);
            _output1 = output.QueryInterface<Output1>();

            var bounds = output.Description.DesktopBounds;
            _width = bounds.Right - bounds.Left;
            _height = bounds.Bottom - bounds.Top;

            _stagingTexture = new Texture2D(_device, new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = _width,
                Height = _height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging
            });

            _duplicatedOutput = _output1.DuplicateOutput(_device);
        }

        public Bitmap? CaptureFrame()
        {
            try
            {
                _duplicatedOutput.TryAcquireNextFrame(10000, out var _, out var screenResource);
                if (screenResource == null) return null;

                using (screenResource)
                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(screenTexture, _stagingTexture);
                }

                var map = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);

                var bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
                var bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, _width, _height),
                    ImageLockMode.WriteOnly,
                    bitmap.PixelFormat);

                int rowBytes = _width * 4;
                unsafe
                {
                    byte* src = (byte*)map.DataPointer;
                    byte* dst = (byte*)bmpData.Scan0;

                    for (int y = 0; y < _height; y++)
                    {
                        new ReadOnlySpan<byte>(src + y * map.RowPitch, rowBytes)
                            .CopyTo(new Span<byte>(dst + y * bmpData.Stride, rowBytes));
                    }
                }

                bitmap.UnlockBits(bmpData);
                _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                _duplicatedOutput.ReleaseFrame();
                return bitmap;
            }
            catch (SharpDXException ex) when (ex.ResultCode == ResultCode.WaitTimeout)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _duplicatedOutput?.Dispose();
            _stagingTexture?.Dispose();
            _output1?.Dispose();
            _device?.Dispose();
            _adapter?.Dispose();
            _factory?.Dispose();
        }
    }
}