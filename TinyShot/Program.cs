using System.Drawing.Imaging;
using TinyShot;

namespace TinyShotApp
{


    // Yes this is some bad code that need refactoring,fixes, more pactical design... but it works for now.
    // Frames are saved directly from the GPU to disk using SharpDX, this should avoir shenanigans
    // with sizes, screen DPI, etc....

    class Program
    {
        static void Main(string[] args)
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshot");
            Directory.CreateDirectory(outputDir);

            using var capture = new DeviceFrameCapture();
            if (capture == null)
            {
                Console.WriteLine("Failed to initialize frame capture.");
                return;
            }

            Console.WriteLine("Frame capture initialized. Starting capture...");
            var buffer = new BMPRingBuffer(20); // 20 frames de marge
            if (buffer == null)
            {
                Console.WriteLine("[FATAL]Failed to initialize ring buffer. Aborting...");

                Thread.Sleep(1500);
                return;
            }

            bool isStopRequested = false;

            // Producer thread (capture frames)
            var captureThread = new Thread(() =>
            {
                while (!isStopRequested)
                {
                    var frame = capture.CaptureFrame();
                    if (frame != null)
                    {
                        if (!buffer.TryAdd(frame))
                            frame.Dispose(); // Full buffer, dispose frame
                    }
                    else
                    {
                        Thread.Sleep(1); // Avoid spamming if no frame is available
                    }
                }
            });

            // Consummer thread (save frames)
            var saveThread = new Thread(() =>
            {
                int count = 0;

                while (!isStopRequested)
                {
                    if (buffer.TryGet(out var bmp))
                    {
                        string path = Path.Combine(outputDir, $"frame_{count:D5}.png");

                        // Lossless PNG format, but CPU heavy, using jpeg for speed by default
                        // bmp.Save(path, ImageFormat.Png);

                        bmp.Save(path, ImageFormat.Jpeg);
                        bmp.Dispose();
                        count++;
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });

            // Start threads
            captureThread.Start();
            saveThread.Start();

            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();

            // Signal threads to stop
            isStopRequested = true;

            // Wait for threads to finish
            captureThread.Join();
            saveThread.Join();

            Console.WriteLine("Capture terminated. Files saved to: " + outputDir);
        }
    }
}
