namespace DbAutoInsert.Models;

// ── oracle_connection.json 매핑 ──────────────────────────────────
public class OracleConnectionFile
{
    public string TnsAlias { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
    public int ConnectionTimeout { get; set; } = 30;
    public int MinPoolSize { get; set; } = 1;
    public int MaxPoolSize { get; set; } = 10;
}

public class OracleConnectionConfig
{
    public string TnsAlias { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1521;
    public string ServiceName { get; set; } = "";
    public string Protocol { get; set; } = "TCP";
    public string UserId { get; set; } = "";
    public string Password { get; set; } = "";
    public int ConnectionTimeout { get; set; } = 30;
    public int MinPoolSize { get; set; } = 1;
    public int MaxPoolSize { get; set; } = 10;

    public string ToTnsDataSource() =>
        $"(DESCRIPTION=" +
        $"(ADDRESS_LIST=(ADDRESS=(PROTOCOL={Protocol})(HOST={Host})(PORT={Port})))" +
        $"(CONNECT_DATA=(SERVICE_NAME={ServiceName})))";
}

// ── config.json 매핑 ────────────────────────────────────────────
public class AppConfig
{
    /// <summary>CSV 파일들이 들어있는 폴더 (EXE 기준 상대경로)</summary>
    public string CsvDirectory { get; set; } = "csv";
    /// <summary>SQL 파일들이 들어있는 폴더 (EXE 기준 상대경로)</summary>
    public string SqlDirectory { get; set; } = "sql";
    public string LogDirectory { get; set; } = "logs";
}

// ── CSV 헤더에서 자동 생성되는 테이블 정보 ──────────────────────
public class AutoTableConfig
{
    public string TableName { get; set; } = "";
    public List<string> Columns { get; set; } = new();
    public string FilePath { get; set; } = "";
}