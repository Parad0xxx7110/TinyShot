using System.Drawing;
using System.Drawing.Imaging;

#pragma warning disable CA1416

namespace FrameFlux
{
    public sealed class MemManager : IDisposable
    {
        private readonly Bitmap[] _buffer;
        private readonly SemaphoreSlim _flushSignal;

        private readonly int _capacity;
        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;
        private long _frameId = 0;
        private long _droppedFrames = 0;
        
        private readonly object _lock = new();
        


        public MemManager(int capacity)
        {
            _capacity = capacity;
            _buffer = new Bitmap[_capacity];
            _flushSignal = new SemaphoreSlim(0, _capacity);
            Directory.CreateDirectory("screenshots");
        }

        public bool TryAdd(Bitmap scrShot)
        {
            if (scrShot == null) return false;

            try
            {
                lock (_lock)
                {
                    if (_count == _capacity)
                    {
                        Interlocked.Increment(ref _droppedFrames);
                        scrShot.Dispose(); // Fixed memory leak
                        return false;
                    }

                    _buffer[_tail]?.Dispose();
                    _buffer[_tail] = (Bitmap)scrShot.Clone();
                    _tail = (_tail + 1) % _capacity;
                    _count++;
                    _flushSignal.Release();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MemManager] Error adding screenshot: {ex.Message}");
                scrShot.Dispose();
                return false;
            }
        }

        public bool TryGet(out Bitmap scrShot)
        {
            scrShot = null;
            lock (_lock)
            {
                if (_count == 0) return false;

                scrShot = _buffer[_head];
                _buffer[_head] = null;
                _head = (_head + 1) % _capacity;
                _count--;
                return true;
            }
        }

        private static async Task WritePNGAsync(Bitmap bitmap, string path)
        {
            using var mem = new MemoryStream();
            bitmap.Save(mem, ImageFormat.Png);
            mem.Position = 0;

            await using var fs = new FileStream(path, FileMode.Create,
                FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await mem.CopyToAsync(fs);
        }

        public async Task FlushLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _flushSignal.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                while (TryGet(out var bmp))
                {
                    try
                    {
                        string path = Path.Combine("screenshots", $"{DateTime.Now:HH-mm-ss_fff}_{Interlocked.Increment(ref _frameId)}.png");
                        await WritePNGAsync(bmp, path);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Flush] Save error: {ex.Message}");
                    }
                    finally
                    {
                        bmp.Dispose();
                    }
                }
            }

            // Final flush to ensure all remaining images are saved
            while (TryGet(out var bmp))
            {
                try
                {
                    string path = Path.Combine("screenshots", $"{DateTime.Now:HH-mm-ss_fff}_{Interlocked.Increment(ref _frameId)}.png");
                    await WritePNGAsync(bmp, path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Final Flush] Save error: {ex.Message}");
                }
                finally
                {
                    bmp.Dispose();
                }
            }
        }

        // Buffer stats, useful for debug/monitoring/UI.....
        public int Count => lockAndReturn(() => _count);
        public bool IsEmpty => lockAndReturn(() => _count == 0);
        public bool IsFull => lockAndReturn(() => _count == _capacity);
        public long DroppedFrames => Interlocked.Read(ref _droppedFrames);

        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _capacity; i++)
                {
                    _buffer[i]?.Dispose();
                    _buffer[i] = null;
                }
                _head = _tail = _count = 0;
            }
        }


        // 2 lazy to make a proper helper class...
        private T lockAndReturn<T>(Func<T> func)
        {
            lock (_lock) return func();
        }

        public void Dispose()
        {
            Clear();
            _flushSignal.Dispose();
        }
    }
}