using System.Drawing;
using System.Drawing.Imaging;

namespace TinyShot
{


    // This might seem overkill, but since the target is to capture 144fps/second max for thoses who want it,
    // we need a buffer to avoid losing frames during high load, handle latency ect...

    public sealed class BMPRingBuffer
    {
        private readonly Bitmap[] _buffer;
        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;
        private readonly int _capacity;
        private readonly object _lock = new();

        private readonly SemaphoreSlim _flushSignal = new(0);

        public BMPRingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new Bitmap[_capacity];
        }

        public bool TryAdd(Bitmap scrShot)
        {
            if (scrShot == null) return false;

            try
            {
                lock (_lock)
                {
                    if (_count == _capacity)
                        return false; // Buffer full

                    _buffer[_tail]?.Dispose();
                    _buffer[_tail] = (Bitmap)scrShot.Clone();
                    _tail = (_tail + 1) % _capacity;
                    _count++;
                    _flushSignal.Release(); // hello world !
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TryAdd] Error: {ex.Message}");
                return false;
            }
        }

        public bool TryGet(out Bitmap scrShot)
        {
            scrShot = null;

            try
            {
                lock (_lock)
                {
                    if (_count == 0)
                        return false;

                    scrShot = _buffer[_head];
                    _buffer[_head] = null;
                    _head = (_head + 1) % _capacity;
                    _count--;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TryGet] Error: {ex.Message}");
                return false;
            }
        }

        private static async Task SaveBMPAsync(Bitmap bitmap, string path)
        {
            using var mem = new MemoryStream();
            bitmap.Save(mem, ImageFormat.Png);
            mem.Position = 0;

            await using var fs = new FileStream(path, FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

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
                        string path = Path.Combine("screenshots", $"{DateTime.Now:HHmmss_fff}.png");
                        await SaveBMPAsync(bmp, path);
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

            // Flush remaining images on cancellation
            while (TryGet(out var bmp))
            {
                try
                {
                    string path = Path.Combine("screenshots", $"{DateTime.Now:HHmmss_fff}.png");
                    await SaveBMPAsync(bmp, path);
                }
                catch { }
                finally
                {
                    bmp.Dispose();
                }
            }
        }

        public int Count => lockAndReturn(() => _count);
        public bool IsEmpty => lockAndReturn(() => _count == 0);
        public bool IsFull => lockAndReturn(() => _count == _capacity);

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

        private T lockAndReturn<T>(Func<T> func)
        {
            lock (_lock) return func();
        }
    }
}
