using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;

namespace FrameFlux
{
    public class CaptureManager : IDisposable
    {
        private readonly DXDeviceFrameCap _captureDevice;
        private readonly MemManager _buffer;
        private readonly TimeSpan _interval;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;

        public CaptureManager(TimeSpan interval, int bufferCapacity = 100)
        {
            _interval = interval;
            _captureDevice = new DXDeviceFrameCap();
            _buffer = new MemManager(bufferCapacity);
        }

        public void Start()
        {
            if (_captureTask != null && !_captureTask.IsCompleted)
                throw new InvalidOperationException("Capture already running");

            _cts = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            if (_cts == null) return;

            _cts.Cancel();
            try
            {
                _captureTask?.Wait();
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is TaskCanceledException)) { }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _captureTask = null;
            }
        }

        private async Task CaptureLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var bmp = _captureDevice.CaptureFrame();
                if (bmp != null)
                {
                    if (!_buffer.TryAdd(bmp))
                    {
                        // Drop oldest if full
                        if (_buffer.TryGet(out var oldBmp))
                        {
                            oldBmp.Dispose();
                        }
                        _buffer.TryAdd(bmp);
                    }
                }

                try
                {
                    await Task.Delay(_interval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public MemManager GetBuffer() => _buffer;

        public void Dispose()
        {
            Stop();
            _captureDevice.Dispose();
            _buffer.Clear();
        }
    }
}
