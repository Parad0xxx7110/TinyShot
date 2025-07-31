using TinyShot;

class Program
{
    static async Task Main(string[] args)
    {
        string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
        Directory.CreateDirectory(outputDir);

        using var manager = new CaptureManager(60, 150); // 60 FPS, buffer 70 images

        manager.Start();

        using var cts = new CancellationTokenSource();

        var flushTask = manager.GetBuffer().FlushLoopAsync(cts.Token);

        Console.WriteLine("Capture started at 60 FPS. Press ENTER to stop...");
        Console.ReadLine();

        manager.Stop();

        cts.Cancel();
        await flushTask;

        Console.WriteLine($"Capture stopped. Screenshots saved to: {outputDir}");
    }
}
