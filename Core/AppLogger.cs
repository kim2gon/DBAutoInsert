namespace DbAutoInsert.Core;

public class AppLogger
{
    private readonly string _logDir;
    private readonly object _lock = new();

    public AppLogger(string logDirectory)
    {
        _logDir = Path.GetFullPath(logDirectory);
        Directory.CreateDirectory(_logDir);
    }

    // ── 공개 메서드 ────────────────────────────────────────
    public void Info (string msg) => Write("INFO ", msg);
    public void Ok   (string msg) => Write("OK   ", msg);
    public void Warn (string msg) => Write("WARN ", msg);
    public void Error(string msg) => Write("ERROR", msg);

    public void Section(string title)
        => Write("----", $"──── {title} ────────────────────────");

    // ── 내부 기록 ──────────────────────────────────────────
    private void Write(string level, string msg)
    {
        // 날짜별 파일: logs/dbinsert_20240115.log
        var fileName = $"dbinsert_{DateTime.Now:yyyyMMdd}.log";
        var filePath = Path.Combine(_logDir, fileName);
        var line     = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {msg}";

        lock (_lock)
        {
            try { File.AppendAllText(filePath, line + Environment.NewLine); }
            catch { /* 로그 실패는 무시 — 메인 로직 방해 금지 */ }
        }
    }
}
