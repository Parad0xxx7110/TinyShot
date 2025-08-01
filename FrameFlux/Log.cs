using NLog;


namespace FrameFlux
{
    public static class Log
    {
        public static readonly Logger Instance = LogManager.GetCurrentClassLogger();

        public static void Info(string message) => Instance.Info(message);
        public static void Warn(string message) => Instance.Warn(message);
        public static void Error(string message) => Instance.Error(message);
        public static void Debug(string message) => Instance.Debug(message);
    }
}
