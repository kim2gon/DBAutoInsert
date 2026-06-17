namespace DbAutoInsert.Core
{
    public class AppLogger
    {
        private readonly string _logDir;
        private readonly object _lock = new object();

        public AppLogger(string logDirectory)
        {
            _logDir = Path.GetFullPath(logDirectory);
            Directory.CreateDirectory(_logDir);
        }

        public void Info (string msg) => Write("INFO ", msg);
        public void Ok   (string msg) => Write("OK   ", msg);
        public void Warn (string msg) => Write("WARN ", msg);
        public void Error(string msg) => Write("ERROR", msg);
        public void Section(string title) => Write("----", $"──── {title} ────────────────────────");

        private void Write(string level, string msg)
        {
            var fileName = $"dbinsert_{DateTime.Now:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDir, fileName);
            var line     = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}";
            lock (_lock)
            {
                try { File.AppendAllText(filePath, line + Environment.NewLine); }
                catch { }
            }
        }
    }
}
