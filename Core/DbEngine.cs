using DbAutoInsert.Models;
using DbAutoInsert.Providers;

namespace DbAutoInsert.Core;

public class DbEngine
{
    private readonly OracleProvider _db;
    private readonly AppConfig _cfg;
    private readonly AppLogger _log;
    private readonly string _basePath;

    public DbEngine(OracleProvider db, AppConfig cfg, AppLogger log, string basePath)
    {
        _db = db;
        _cfg = cfg;
        _log = log;
        _basePath = basePath;
    }

    public void Run()
    {
        // ── 1. DB 연결 ────────────────────────────────────────
        _log.Section("Oracle 연결");
        _db.Connect();
        _log.Ok("Oracle 연결 성공");

        // ── 2. CSV 폴더 스캔 → 자동 처리 ─────────────────────
        var csvDir = AbsPath(_cfg.CsvDirectory);
        if (Directory.Exists(csvDir))
        {
            var csvFiles = Directory.GetFiles(csvDir, "*.*")
                .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".csv")
                .OrderBy(f => f)
                .ToList();

            _log.Section($"CSV 폴더 스캔: {csvDir}  ({csvFiles.Count}개)");

            foreach (var file in csvFiles)
                ProcessCsvFile(file);
        }
        else
        {
            _log.Warn($"CSV 폴더 없음 → 건너뜀: {csvDir}");
        }

        // ── 3. SQL 폴더 스캔 → 순서대로 실행 ────────────────
        var sqlDir = AbsPath(_cfg.SqlDirectory);
        if (Directory.Exists(sqlDir))
        {
            var sqlFiles = Directory.GetFiles(sqlDir, "*.*")
                .Where(f => Path.GetExtension(f).ToLowerInvariant() == ".sql")
                .OrderBy(f => f)
                .ToList();

            _log.Section($"SQL 폴더 스캔: {sqlDir}  ({sqlFiles.Count}개)");

            foreach (var file in sqlFiles)
            {
                _log.Info($"  SQL 실행: {Path.GetFileName(file)}");
                _db.ExecuteSqlFile(file);
                _log.Ok($"  완료: {Path.GetFileName(file)}");
            }
        }
        else
        {
            _log.Warn($"SQL 폴더 없음 → 건너뜀: {sqlDir}");
        }

        _log.Ok("모든 작업 완료");
    }

    // ── CSV 파일 처리 ─────────────────────────────────────────────
    private void ProcessCsvFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        // 파일명(확장자 제외)을 테이블명으로 사용  예: users.csv → USERS
        var tableName = Path.GetFileNameWithoutExtension(filePath).ToUpperInvariant();

        _log.Section($"CSV 처리: {fileName} → 테이블: {tableName}");

        // CSV 읽기 (헤더 포함)
        List<Dictionary<string, string>> rows;
        List<string> headers;
        try
        {
            (rows, headers) = CsvDataReader.ReadWithHeaders(filePath);
            _log.Info($"  CSV 로드: {rows.Count}행  컬럼: {string.Join(", ", headers)}");
        }
        catch (Exception ex)
        {
            _log.Error($"  CSV 읽기 실패: {ex.Message}");
            return;
        }

        if (headers.Count == 0)
        {
            _log.Warn("  헤더 없음 → 건너뜀");
            return;
        }

        // 테이블 없으면 생성
        if (!_db.TableExists(tableName))
        {
            _log.Warn($"  테이블 없음 → CREATE TABLE: {tableName}");
            _db.CreateTableFromHeaders(tableName, headers);
            _log.Ok($"  테이블 생성 완료: {tableName}");
        }
        else
        {
            _log.Info($"  테이블 존재: {tableName}");
        }

        // 컬럼 없으면 추가
        foreach (var col in headers)
        {
            if (!_db.ColumnExists(tableName, col))
            {
                _log.Warn($"  컬럼 없음 → ALTER TABLE ADD: {col}");
                _db.AddColumnByName(tableName, col);
                _log.Ok($"  컬럼 추가 완료: {col}");
            }
        }

        // 데이터 삽입 (중복 체크: 모든 컬럼 값이 동일한 행은 스킵)
        var inserted = _db.UpsertRowsAuto(tableName, headers, rows);
        _log.Ok($"  삽입: {inserted}행  |  중복 스킵: {rows.Count - inserted}행");
    }

    private string AbsPath(string dir) =>
        Path.IsPathRooted(dir) ? dir : Path.GetFullPath(Path.Combine(_basePath, dir));
}