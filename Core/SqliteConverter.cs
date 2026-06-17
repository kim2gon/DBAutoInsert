using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace DbAutoInsert.Core
{
    public static class SqliteConverter
    {
        // .db 파일에서 테이블/데이터 읽기
        public static List<SqliteTableData> ReadFromDb(string dbFilePath)
        {
            var result = new List<SqliteTableData>();
            var cs = "Data Source=" + dbFilePath + ";Version=3;Read Only=True;";

            using (var conn = new SQLiteConnection(cs))
            {
                conn.Open();

                var tables = new List<string>();
                using (var cmd = new SQLiteCommand(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        tables.Add(reader.GetString(0));

                foreach (var table in tables)
                {
                    var tableData = new SqliteTableData { TableName = table.ToUpperInvariant() };

                    using (var cmd = new SQLiteCommand("PRAGMA table_info(\"" + table + "\")", conn))
                    using (var reader = cmd.ExecuteReader())
                        while (reader.Read())
                            tableData.Columns.Add(reader["name"].ToString().ToUpperInvariant());

                    using (var cmd = new SQLiteCommand("SELECT * FROM \"" + table + "\"", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var col in tableData.Columns)
                                row[col] = reader[col] != null ? reader[col].ToString() : string.Empty;
                            tableData.Rows.Add(row);
                        }
                    }
                    result.Add(tableData);
                }
            }
            return result;
        }

        // .sql/.txt SQLite 문법 -> Oracle 문법 변환
        public static List<string> ConvertSqlFile(string sqlFilePath)
        {
            var fallbackTableName = Path.GetFileNameWithoutExtension(sqlFilePath).ToUpperInvariant();
            var sql = File.ReadAllText(sqlFilePath, System.Text.Encoding.UTF8);
            var statements = new List<string>();

            foreach (var raw in sql.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var stmt = raw.Trim();
                if (string.IsNullOrWhiteSpace(stmt)) continue;
                stmt = ConvertStatement(stmt, fallbackTableName);
                if (!string.IsNullOrWhiteSpace(stmt))
                    statements.Add(stmt);
            }
            return statements;
        }

        private static string ConvertStatement(string stmt, string fallbackTableName)
        {
            // 1. "스키마"."테이블명" 을 한번에 처리
            stmt = Regex.Replace(stmt, "\"[^\"]+\"\\.\"([^\"]*)\"", m => {
                var tbl = m.Groups[1].Value;
                return "\"" + (string.IsNullOrEmpty(tbl) ? fallbackTableName : tbl.ToUpperInvariant()) + "\"";
            }, RegexOptions.IgnoreCase);

            // 2. 나머지 식별자 대문자화
            stmt = Regex.Replace(stmt, "\"([^\"]+)\"", m =>
                "\"" + m.Groups[1].Value.ToUpperInvariant() + "\"");

            // 3. SQLite 전용 구문 -> Oracle
            stmt = Regex.Replace(stmt, "INSERT\\s+OR\\s+IGNORE",  "INSERT", RegexOptions.IgnoreCase);
            stmt = Regex.Replace(stmt, "INSERT\\s+OR\\s+REPLACE", "INSERT", RegexOptions.IgnoreCase);
            stmt = Regex.Replace(stmt, "\\bAUTOINCREMENT\\b",     "",       RegexOptions.IgnoreCase);

            // 4. SQLite 타입 -> Oracle 타입 (CREATE TABLE 구문에서만 적용)
            if (stmt.TrimStart().ToUpperInvariant().StartsWith("CREATE TABLE"))
            {
                stmt = Regex.Replace(stmt, "\\bTEXT\\b",    "VARCHAR2(4000)", RegexOptions.IgnoreCase);
                stmt = Regex.Replace(stmt, "\\bINTEGER\\b", "NUMBER(10)",     RegexOptions.IgnoreCase);
                stmt = Regex.Replace(stmt, "\\bREAL\\b",    "NUMBER(18,6)",   RegexOptions.IgnoreCase);
                stmt = Regex.Replace(stmt, "\\bNUMERIC\\b", "NUMBER",         RegexOptions.IgnoreCase);
            }

            return stmt.Trim();
        }
    }

    public class SqliteTableData
    {
        public string TableName { get; set; } = "";
        public List<string> Columns { get; set; } = new List<string>();
        public List<Dictionary<string, string>> Rows { get; set; } = new List<Dictionary<string, string>>();
    }
}
