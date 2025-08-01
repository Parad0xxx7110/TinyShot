using FrameFlux;

class Program
{
    static async Task Main(string[] args)
    {
        //
        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        Directory.CreateDirectory(outputDir);
        TimeSpan captureInterval = TimeSpan.FromMilliseconds(3000); 
        using var manager = new CaptureManager(captureInterval, 150);

        manager.Start();

        using var cts = new CancellationTokenSource();

        var flushTask = manager.FlushBufferAsync(cts.Token);
        Console.WriteLine("Capture started at 60 FPS. Press ENTER to stop...");
        Console.ReadLine();

        manager.Stop();

        cts.Cancel();
        await flushTask;

        Console.WriteLine($"Capture stopped. Screenshots saved to: {outputDir}");
    }
}
