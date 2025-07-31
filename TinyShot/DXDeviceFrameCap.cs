using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Drawing.Imaging;

using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using ResultCode = SharpDX.DXGI.ResultCode;

#pragma warning disable CA1416

namespace TinyShot
{
    public class DXDeviceFrameCap : IDisposable
    {
        private Factory1 _factory;
        private Adapter1 _adapter;
        private Device _device;
        private Output1 _output1;
        private Output _output;
        private OutputDuplication _duplicatedOutput;
        private Texture2D _stagingTexture;
        private Bitmap _bitmap;
        private BitmapData _bmpData;
        private int _width;
        private int _height;
        private bool _disposed;

        public DXDeviceFrameCap(int adapterIndex = 0, int outputIndex = 0)
        {
            Initialize(adapterIndex, outputIndex);
        }

        private void Initialize(int adapterIndex, int outputIndex)
        {
            _factory = new Factory1();
            _adapter = _factory.GetAdapter1(adapterIndex);
            _device = new Device(_adapter);

            _output = _adapter.GetOutput(outputIndex);
            _output1 = _output.QueryInterface<Output1>();

            var bounds = _output.Description.DesktopBounds;
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

            _bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
            _bmpData = _bitmap.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.WriteOnly, _bitmap.PixelFormat);
        }

        public Bitmap? CaptureFrame()
        {
            if (_disposed) return null;

            try
            {
                var result = _duplicatedOutput.TryAcquireNextFrame(500, out var info, out var screenResource);
                if (!result.Success)
                    return null;

                using (screenResource)
                using (var screenTexture = screenResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(screenTexture, _stagingTexture);
                }

                var map = _device.ImmediateContext.MapSubresource(_stagingTexture, 0, MapMode.Read, MapFlags.None);

                Bitmap bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);

                try
                {
                    var bmpData = bitmap.LockBits(new Rectangle(0, 0, _width, _height), ImageLockMode.WriteOnly, bitmap.PixelFormat);

                    unsafe
                    {
                        byte* src = (byte*)map.DataPointer;
                        byte* dst = (byte*)bmpData.Scan0;
                        int rowBytes = _width * 4;

                        for (int y = 0; y < _height; y++)
                        {
                            new ReadOnlySpan<byte>(src + y * map.RowPitch, rowBytes)
                                .CopyTo(new Span<byte>(dst + y * bmpData.Stride, rowBytes));
                        }
                    }

                    bitmap.UnlockBits(bmpData);
                }
                catch
                {
                    bitmap.Dispose();
                    throw;
                }
                finally
                {
                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
                }

                _duplicatedOutput.ReleaseFrame();

                return bitmap;
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == ResultCode.AccessLost)
                {
                    ResetDuplication();
                }
                else if (ex.ResultCode == ResultCode.WaitTimeout)
                {
                    return null;
                }
                else
                {
                    Console.WriteLine($"[ERROR] CaptureFrame failed: {ex.Message}");
                }
            }

            return null;
        }


        private void ResetDuplication()
        {
            Console.WriteLine("[INFO] Reinitializing screen duplication due to AccessLost...");

            _duplicatedOutput?.Dispose();
            _stagingTexture?.Dispose();

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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _duplicatedOutput?.Dispose();
            _stagingTexture?.Dispose();
            _output1?.Dispose();
            _output?.Dispose();
            _device?.Dispose();
            _adapter?.Dispose();
            _factory?.Dispose();

            if (_bmpData != null && _bitmap != null)
                _bitmap.UnlockBits(_bmpData);

            _bitmap?.Dispose();
        }
    }
}
