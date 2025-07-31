using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;

namespace TinyShot
{
    public class CaptureManager : IDisposable
    {
        private readonly DXDeviceFrameCap _captureDevice;
        private readonly BMPRingBuffer _buffer;
        private readonly int _targetFps;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;

        public CaptureManager(int targetFps = 60, int bufferCapacity = 100)
        {
            _targetFps = targetFps;
            _captureDevice = new DXDeviceFrameCap();
            _buffer = new BMPRingBuffer(bufferCapacity);
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
            if (_cts == null)
                return;

            _cts.Cancel();
            try
            {
                _captureTask?.Wait();
            }
            catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is TaskCanceledException))
            {
                // Task cancelled expectedly
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _captureTask = null;
            }
        }

        private async Task CaptureLoopAsync(CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            long nextTickMs = 0;
            int intervalMs = 1000 / _targetFps;

            while (!token.IsCancellationRequested)
            {
                long nowMs = sw.ElapsedMilliseconds;
                if (nowMs >= nextTickMs)
                {
                    var bmp = _captureDevice.CaptureFrame();
                    if (bmp != null)
                    {
                        if (!_buffer.TryAdd(bmp))
                        {
                            // Buffer full, drop oldest frame by TryGet and add new
                            if (_buffer.TryGet(out var oldBmp))
                            {
                                oldBmp.Dispose();
                            }
                            _buffer.TryAdd(bmp);
                        }
                    }

                    nextTickMs += intervalMs;
                }
                else
                {
                    // Sleep minimal time to avoid busy loop
                    await Task.Delay(1, token);
                }
            }
        }

      public BMPRingBuffer GetBuffer() => _buffer;

        public void Dispose()
        {
            Stop();
            _captureDevice.Dispose();
            _buffer.Clear();
        }
    }
}
