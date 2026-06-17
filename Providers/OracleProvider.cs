using DbAutoInsert.Database;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace DbAutoInsert.Providers
{
    public class OracleProvider : IDisposable
    {
        private readonly OracleHelper _db;

        public OracleProvider(OracleHelper db) => _db = db;

        // ── 스키마: 테이블 ───────────────────────────────────────────
        public bool TableExists(string tableName)
        {
            var result = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM user_tables WHERE table_name = UPPER(:1)",
                new OracleParameter(":1", tableName));
            return Convert.ToInt64(result) > 0;
        }

        public void CreateTableFromHeaders(string tableName, List<string> headers)
        {
            if (TableExists(tableName)) return;
            var colDefs = string.Join(", ", headers.Select(h => Q(h) + " VARCHAR2(4000)"));
            _db.ExecuteNonQuery("CREATE TABLE " + Q(tableName) + " (" + colDefs + ")");
        }

        // ── 스키마: 컬럼 ────────────────────────────────────────────
        public bool ColumnExists(string tableName, string columnName)
        {
            var result = _db.ExecuteScalar(
                "SELECT COUNT(*) FROM user_tab_columns WHERE table_name = UPPER(:1) AND column_name = UPPER(:2)",
                new OracleParameter(":1", tableName),
                new OracleParameter(":2", columnName));
            return Convert.ToInt64(result) > 0;
        }

        public void AddColumnByName(string tableName, string columnName)
            => _db.ExecuteNonQuery("ALTER TABLE " + Q(tableName) + " ADD " + Q(columnName) + " VARCHAR2(4000)");

        // ── 데이터 삽입 (중복 방지) ──────────────────────────────────
        public int UpsertRowsAuto(string tableName, List<string> headers,
                                  IEnumerable<Dictionary<string, string>> rows)
        {
            int inserted = 0;
            using (var tx = _db.GetOracleTransaction())
            {
                foreach (var row in rows)
                {
                    var cols = headers.Where(row.ContainsKey).ToList();
                    if (cols.Count == 0) continue;
                    if (IsDuplicateAuto(tableName, cols, row, tx)) continue;

                    // 값을 직접 SQL에 삽입 (파라미터 바인딩 대신)
                    var colPart = string.Join(", ", cols.Select(c => Q(c)));
                    var valPart = string.Join(", ", cols.Select(c =>
                        string.IsNullOrEmpty(row[c]) ? "NULL" : "'" + row[c].Replace("'", "''") + "'"));

                    var sql = "INSERT INTO " + Q(tableName) + " (" + colPart + ") VALUES (" + valPart + ")";
                    _db.ExecuteNonQuery(tx, sql);
                    inserted++;
                }
                tx.Commit();
            }
            return inserted;
        }

        // ── 단일 SQL 구문 실행 ──────────────────────────────────────
        public void ExecuteSql(string sql) => _db.ExecuteNonQuery(sql);

        // ── SQL 파일 직접 실행 ───────────────────────────────────────
        public void ExecuteSqlFile(string path)
        {
            var sql = File.ReadAllText(path, System.Text.Encoding.UTF8);
            foreach (var stmt in sql.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = stmt.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    _db.ExecuteNonQuery(trimmed);
            }
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────
        private bool IsDuplicateAuto(string tableName, List<string> cols,
                                      Dictionary<string, string> row, OracleTransaction tx)
        {
            var checkCols = cols.Where(c => !string.IsNullOrEmpty(row[c])).ToList();
            if (checkCols.Count == 0) return false;

            var where = string.Join(" AND ", checkCols.Select(c =>
                Q(c) + " = '" + row[c].Replace("'", "''") + "'"));

            var result = _db.ExecuteScalar(tx,
                "SELECT COUNT(*) FROM " + Q(tableName) + " WHERE " + where);
            return Convert.ToInt64(result) > 0;
        }

        private static string Q(string s) => "\"" + s.ToUpperInvariant() + "\"";

        public void Dispose() { }
    }
}
