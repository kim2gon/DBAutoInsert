using DbAutoInsert.Models;
using DbAutoInsert.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DbAutoInsert.Core
{
    public class DbEngine
    {
        private readonly OracleProvider _db;
        private readonly AppConfig      _cfg;
        private readonly AppLogger      _log;
        private readonly string         _basePath;

        public DbEngine(OracleProvider db, AppConfig cfg, AppLogger log, string basePath)
        {
            _db       = db;
            _cfg      = cfg;
            _log      = log;
            _basePath = basePath;
        }

        public void Run()
        {
            _log.Section("Oracle 연결");
            _log.Ok("Oracle 연결 성공");

            var dataDir = AbsPath(_cfg.DataDirectory);
            if (!Directory.Exists(dataDir))
            {
                _log.Warn("데이터 폴더 없음 → 건너뜀: " + dataDir);
                _log.Ok("모든 작업 완료");
                return;
            }

            var allFiles = Directory.GetFiles(dataDir, "*.*")
                .Where(f => new[] { ".csv", ".sql", ".txt", ".db" }
                    .Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetExtension(f).ToLowerInvariant() == ".csv" ? 0 :
                              Path.GetExtension(f).ToLowerInvariant() == ".db"  ? 2 : 1)
                .ThenBy(f => f)
                .ToList();

            _log.Section("데이터 폴더 스캔: " + dataDir + "  (" + allFiles.Count + "개)");

            foreach (var file in allFiles)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                switch (ext)
                {
                    case ".csv": ProcessCsvFile(file);      break;
                    case ".sql":
                    case ".txt": ProcessSqlFile(file);      break;
                    case ".db":  ProcessSqliteDbFile(file); break;
                }
            }

            _log.Ok("모든 작업 완료");
        }

        private void ProcessCsvFile(string filePath)
        {
            var fileName  = Path.GetFileName(filePath);
            var tableName = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();

            _log.Section("CSV 처리: " + fileName + " → 테이블: " + tableName);

            List<Dictionary<string, string>> rows;
            List<string> headers;
            try
            {
                var result = CsvDataReader.ReadWithHeaders(filePath);
                rows    = result.Item1;
                headers = result.Item2;
                _log.Info("  CSV 로드: " + rows.Count + "행  컬럼: " + string.Join(", ", headers));


            }
            catch (Exception ex)
            {
                _log.Error("  CSV 읽기 실패: " + ex.Message);
                return;
            }

            if (headers.Count == 0) { _log.Warn("  헤더 없음 → 건너뜀"); return; }

            if (!_db.TableExists(tableName))
            {
                _log.Warn("  테이블 없음 → CREATE TABLE: " + tableName);
                _db.CreateTableFromHeaders(tableName, headers);
                _log.Ok("  테이블 생성 완료: " + tableName);
            }
            else
            {
                _log.Info("  테이블 존재: " + tableName);
            }

            foreach (var col in headers)
            {
                if (!_db.ColumnExists(tableName, col))
                {
                    _log.Warn("  컬럼 없음 → ADD: " + col);
                    _db.AddColumnByName(tableName, col);
                    _log.Ok("  컬럼 추가 완료: " + col);
                }
            }

            var inserted = _db.UpsertRowsAuto(tableName, headers, rows);
            _log.Ok("  삽입: " + inserted + "행  |  중복 스킵: " + (rows.Count - inserted) + "행");
        }

        private void ProcessSqlFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            _log.Section("SQL 처리: " + fileName);

            try
            {
                var content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var upper   = content.ToUpperInvariant();

                bool isSqlite = content.Contains("\".\"")         ||
                                content.Contains("\"main\".")      ||
                                upper.Contains("AUTOINCREMENT")    ||
                                upper.Contains("INSERT OR IGNORE") ||
                                upper.Contains("INSERT OR REPLACE");

                if (isSqlite)
                {
                    _log.Info("  SQLite 문법 감지 → 자동 변환 후 실행");
                    var statements = SqliteConverter.ConvertSqlFile(filePath);
                    _log.Info("  변환된 구문: " + statements.Count + "개");
                    int ok = 0, skip = 0;
                    foreach (var stmt in statements)
                    {
                        try { _db.ExecuteSql(stmt); ok++; }
                        catch (Exception ex)
                        {
                            _log.Warn("  구문 스킵: " + ex.Message.Split('\n')[0]);
                            skip++;
                        }
                    }
                    _log.Ok("  실행: " + ok + "개  |  스킵: " + skip + "개");
                }
                else
                {
                    _db.ExecuteSqlFile(filePath);
                    _log.Ok("  완료: " + fileName);
                }
            }
            catch (Exception ex)
            {
                _log.Error("  SQL 처리 실패: " + ex.Message);
            }
        }

        private void ProcessSqliteDbFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            _log.Section("SQLite DB 변환: " + fileName);
            try
            {
                var tables = SqliteConverter.ReadFromDb(filePath);
                _log.Info("  테이블 " + tables.Count + "개 발견");

                foreach (var table in tables)
                {
                    _log.Info("  처리: " + table.TableName + " (" + table.Rows.Count + "행)");

                    if (!_db.TableExists(table.TableName))
                    {
                        _log.Warn("  테이블 없음 → CREATE TABLE: " + table.TableName);
                        _db.CreateTableFromHeaders(table.TableName, table.Columns);
                        _log.Ok("  테이블 생성 완료: " + table.TableName);
                    }
                    else
                    {
                        _log.Info("  테이블 존재: " + table.TableName);
                    }

                    foreach (var col in table.Columns)
                        if (!_db.ColumnExists(table.TableName, col))
                        {
                            _log.Warn("  컬럼 없음 → ADD: " + col);
                            _db.AddColumnByName(table.TableName, col);
                        }

                    var inserted = _db.UpsertRowsAuto(table.TableName, table.Columns, table.Rows);
                    _log.Ok("  삽입: " + inserted + "행  |  중복 스킵: " + (table.Rows.Count - inserted) + "행");
                }
            }
            catch (Exception ex)
            {
                _log.Error("  SQLite DB 처리 실패: " + ex.Message);
            }
        }

        private string AbsPath(string dir) =>
            Path.IsPathRooted(dir) ? dir : Path.GetFullPath(Path.Combine(_basePath, dir));
    }
}
