using DbAutoInsert.Models;
using Oracle.ManagedDataAccess.Client;

namespace DbAutoInsert.Providers;

public class OracleProvider : IDisposable
{
    private readonly OracleConnectionConfig _cfg;
    private OracleConnection? _conn;

    public OracleProvider(OracleConnectionConfig cfg) => _cfg = cfg;

    // ── 연결 ─────────────────────────────────────────────────────
    public void Connect()
    {
        var cs = new OracleConnectionStringBuilder
        {
            DataSource = _cfg.ToTnsDataSource(),
            UserID = _cfg.UserId,
            Password = _cfg.Password,
            ConnectionTimeout = _cfg.ConnectionTimeout,
            MinPoolSize = _cfg.MinPoolSize,
            MaxPoolSize = _cfg.MaxPoolSize,
        }.ToString();

        _conn = new OracleConnection(cs);
        _conn.Open();
    }

    // ── 스키마: 테이블 ───────────────────────────────────────────
    public bool TableExists(string tableName)
    {
        using var cmd = Cmd(
            "SELECT COUNT(*) FROM user_tables WHERE table_name = UPPER(:n)");
        cmd.Parameters.Add(new OracleParameter(":n", tableName));
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    /// <summary>CSV 헤더 기반으로 테이블 생성 (모든 컬럼 VARCHAR2(4000))</summary>
    public void CreateTableFromHeaders(string tableName, List<string> headers)
    {
        if (TableExists(tableName)) return;

        var colDefs = string.Join(", ", headers.Select(h => $"{Q(h)} VARCHAR2(4000)"));
        Exec($"CREATE TABLE {Q(tableName)} ({colDefs})");
    }

    // ── 스키마: 컬럼 ────────────────────────────────────────────
    public bool ColumnExists(string tableName, string columnName)
    {
        using var cmd = Cmd(
            "SELECT COUNT(*) FROM user_tab_columns " +
            "WHERE table_name = UPPER(:t) AND column_name = UPPER(:c)");
        cmd.Parameters.Add(new OracleParameter(":t", tableName));
        cmd.Parameters.Add(new OracleParameter(":c", columnName));
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    /// <summary>컬럼명만으로 VARCHAR2(4000) 컬럼 추가</summary>
    public void AddColumnByName(string tableName, string columnName)
        => Exec($"ALTER TABLE {Q(tableName)} ADD {Q(columnName)} VARCHAR2(4000)");

    // ── 데이터 삽입 (중복 방지) ──────────────────────────────────
    /// <summary>
    /// 헤더 기반 자동 삽입.
    /// 중복 체크: 모든 컬럼 값이 완전히 동일한 행은 스킵
    /// </summary>
    public int UpsertRowsAuto(string tableName,
                              List<string> headers,
                              IEnumerable<Dictionary<string, string>> rows)
    {
        int inserted = 0;
        using var tx = _conn!.BeginTransaction();

        foreach (var row in rows)
        {
            var cols = headers.Where(row.ContainsKey).ToList();
            if (cols.Count == 0) continue;

            // 중복 체크: 모든 컬럼이 동일한 행 존재 여부
            if (IsDuplicateAuto(tableName, cols, row, tx)) continue;

            var colPart = string.Join(", ", cols.Select(c => Q(c)));
            var paramPart = string.Join(", ", cols.Select((_, i) => $":p{i}"));
            var sql = $"INSERT INTO {Q(tableName)} ({colPart}) VALUES ({paramPart})";

            using var cmd = Cmd(sql);
            cmd.Transaction = tx;
            for (int i = 0; i < cols.Count; i++)
            {
                var val = row[cols[i]];
                cmd.Parameters.Add(new OracleParameter($":p{i}",
                    string.IsNullOrEmpty(val) ? (object)DBNull.Value : val));
            }

            inserted += cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return inserted;
    }

    // ── SQL 파일 직접 실행 ───────────────────────────────────────
    public void ExecuteSqlFile(string path)
    {
        var sql = File.ReadAllText(path, System.Text.Encoding.UTF8);
        foreach (var stmt in sql.Split(';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(stmt))
                Exec(stmt);
        }
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────
    private bool IsDuplicateAuto(string tableName,
                                  List<string> cols,
                                  Dictionary<string, string> row,
                                  OracleTransaction tx)
    {
        // 값이 있는 컬럼만 조건으로 사용
        var checkCols = cols.Where(c => !string.IsNullOrEmpty(row[c])).ToList();
        if (checkCols.Count == 0) return false;

        var where = string.Join(" AND ", checkCols.Select((c, i) => $"{Q(c)} = :chk{i}"));
        using var cmd = Cmd($"SELECT COUNT(*) FROM {Q(tableName)} WHERE {where}");
        cmd.Transaction = tx;
        for (int i = 0; i < checkCols.Count; i++)
            cmd.Parameters.Add(new OracleParameter($":chk{i}", row[checkCols[i]]));

        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    // Oracle 식별자 대문자 인용부호
    private static string Q(string s) => $"\"{s.ToUpperInvariant()}\"";

    private OracleCommand Cmd(string sql)
    {
        var cmd = _conn!.CreateCommand();
        cmd.CommandText = sql;
        return cmd;
    }

    private void Exec(string sql)
    {
        using var cmd = Cmd(sql);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn?.Dispose();
}