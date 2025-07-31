using System.Drawing;

namespace TinyShot
{
    internal class BMPRingBuffer
    {
        private readonly Bitmap[] _buffer;
        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;
        private readonly int _capacity;
        private readonly object _lock = new();

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
                        return false; // Buffer is full

                    _buffer[_tail]?.Dispose(); // Dispose previous if not null
                    _buffer[_tail] = (Bitmap)scrShot.Clone();
                    _tail = (_tail + 1) % _capacity;
                    _count++;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding bitmap to ring buffer: {ex.Message}");
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
                        return false; // Buffer is empty

                    scrShot = _buffer[_head];
                    _buffer[_head] = null;
                    _head = (_head + 1) % _capacity;
                    _count--;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving bitmap from ring buffer: {ex.Message}");
                return false;
            }
        }

        public int Count
        {
            get { lock (_lock) return _count; }
        }

        public bool IsEmpty
        {
            get { lock (_lock) return _count == 0; }
        }

        public bool IsFull
        {
            get { lock (_lock) return _count == _capacity; }
        }

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
    }
}
