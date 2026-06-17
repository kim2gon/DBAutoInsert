using DbAutoInsert.Models;

namespace DbAutoInsert.Core;

/// <summary>
/// TNS_ADMIN 환경변수로 tnsnames.ora 위치를 찾고 파싱합니다.
///
/// 탐색 순서:
///   1. 환경변수 TNS_ADMIN
///   2. ORACLE_HOME\network\admin
///   3. EXE 실행 디렉터리 (fallback)
/// </summary>
public static class TnsParser
{
    private const string TnsFileName = "tnsnames.ora";

    public static OracleConnectionConfig Parse(string alias, AppLogger log)
    {
        var tnsPath = FindTnsFile(log);
        log.Info($"tnsnames.ora 경로: {tnsPath}");

        var content = File.ReadAllText(tnsPath, System.Text.Encoding.UTF8);
        var block   = ExtractAliasBlock(content, alias);

        if (string.IsNullOrEmpty(block))
            throw new InvalidDataException(
                $"tnsnames.ora 에서 별칭을 찾을 수 없습니다: '{alias}'  (파일: {tnsPath})");

        log.Info($"TNS 블록 추출 성공 (alias={alias})");

        var host        = ExtractValue(block, "HOST")         ?? "";
        var port        = ExtractValue(block, "PORT")         ?? "1521";
        var serviceName = ExtractValue(block, "SERVICE_NAME") ?? "";
        var protocol    = ExtractValue(block, "PROTOCOL")     ?? "TCP";

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidDataException($"TNS 파싱 실패 — HOST 없음 (alias={alias})");
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidDataException($"TNS 파싱 실패 — SERVICE_NAME 없음 (alias={alias})");

        log.Ok($"TNS 파싱 완료 → HOST={host}  PORT={port}  SERVICE_NAME={serviceName}  PROTOCOL={protocol}");

        return new OracleConnectionConfig
        {
            TnsAlias    = alias,
            Host        = host,
            Port        = int.TryParse(port, out var p) ? p : 1521,
            ServiceName = serviceName,
            Protocol    = protocol
        };
    }

    // ── tnsnames.ora 탐색 ────────────────────────────────────────
    private static string FindTnsFile(AppLogger log)
    {
        // 1순위: TNS_ADMIN 환경변수
        var tnsAdmin = Environment.GetEnvironmentVariable("TNS_ADMIN");
        if (!string.IsNullOrWhiteSpace(tnsAdmin))
        {
            var path = Path.Combine(tnsAdmin.Trim(), TnsFileName);
            if (File.Exists(path))
            {
                log.Info($"TNS_ADMIN 환경변수 사용: {tnsAdmin}");
                return path;
            }
            log.Warn($"TNS_ADMIN 경로에 tnsnames.ora 없음: {path}");
        }
        else
        {
            log.Warn("TNS_ADMIN 환경변수가 설정되지 않음");
        }

        // 2순위: ORACLE_HOME\network\admin
        var oracleHome = Environment.GetEnvironmentVariable("ORACLE_HOME");
        if (!string.IsNullOrWhiteSpace(oracleHome))
        {
            var path = Path.Combine(oracleHome.Trim(), "network", "admin", TnsFileName);
            if (File.Exists(path))
            {
                log.Info($"ORACLE_HOME 경로 사용: {oracleHome}");
                return path;
            }
            log.Warn($"ORACLE_HOME 경로에 tnsnames.ora 없음: {path}");
        }

        // 3순위: 일반적인 Oracle 클라이언트 기본 설치 경로
        var defaultPaths = new[]
        {
            @"C:\oracle\network\admin",
            @"C:\Oracle\product\19c\client_1\network\admin",
            @"C:\Oracle\product\12.2.0\client_1\network\admin",
            @"C:\Oracle\product\11.2.0\client_1\network\admin",
            @"C:\app\oracle\product\19.0.0\client_1\network\admin",
        };

        foreach (var dir in defaultPaths)
        {
            var path = Path.Combine(dir, TnsFileName);
            if (File.Exists(path))
            {
                log.Info($"Oracle 기본 경로 사용: {dir}");
                return path;
            }
        }

        // 4순위: EXE 실행 디렉터리 (배포 시 함께 넣을 수 있도록 fallback)
        var exePath = Path.Combine(AppContext.BaseDirectory, TnsFileName);
        if (File.Exists(exePath))
        {
            log.Info($"EXE 디렉터리 fallback 사용: {AppContext.BaseDirectory}");
            return exePath;
        }

        throw new FileNotFoundException(
            $"tnsnames.ora 파일을 찾을 수 없습니다.\n" +
            $"TNS_ADMIN 환경변수를 확인하거나 EXE 폴더에 tnsnames.ora 를 복사해 주세요.");
    }

    // ── 별칭 블록 추출 ───────────────────────────────────────────
    private static string? ExtractAliasBlock(string content, string alias)
    {
        // 주석(#) 제거
        var lines  = content.Split('\n')
                            .Select(l => l.Contains('#') ? l[..l.IndexOf('#')] : l)
                            .Select(l => l.TrimEnd());
        var clean  = string.Join("\n", lines);
        var upper  = clean.ToUpperInvariant();
        var search = alias.Trim().ToUpperInvariant();

        int pos = 0;
        while (pos < upper.Length)
        {
            var idx = upper.IndexOf(search, pos, StringComparison.Ordinal);
            if (idx < 0) break;

            // 별칭 앞이 줄 시작인지 확인
            bool atLineStart = idx == 0 || clean[idx - 1] == '\n' || clean[idx - 1] == '\r';
            if (!atLineStart) { pos = idx + 1; continue; }

            // 별칭 뒤(공백 제거 후) '=' 확인
            var after = clean[(idx + search.Length)..].TrimStart();
            if (!after.StartsWith('=')) { pos = idx + 1; continue; }

            // '=' 이후 괄호 깊이 추적으로 블록 범위 확정
            var eqIdx = clean.IndexOf('=', idx + search.Length);
            if (eqIdx < 0) break;

            int depth = 0, blockStart = -1, blockEnd = -1;
            for (int i = eqIdx + 1; i < clean.Length; i++)
            {
                if      (clean[i] == '(') { if (depth++ == 0) blockStart = i; }
                else if (clean[i] == ')') { if (--depth == 0) { blockEnd = i; break; } }
            }

            if (blockStart >= 0 && blockEnd >= 0)
                return clean[blockStart..(blockEnd + 1)];

            break;
        }
        return null;
    }

    // ── KEY = VALUE 추출 ─────────────────────────────────────────
    private static string? ExtractValue(string block, string key)
    {
        var upper  = block.ToUpperInvariant();
        var search = key.ToUpperInvariant();

        int idx = upper.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;

        int eqIdx  = block.IndexOf('=', idx + search.Length);
        if (eqIdx < 0) return null;

        int endIdx = block.IndexOf(')', eqIdx);
        if (endIdx < 0) return null;

        return block[(eqIdx + 1)..endIdx].Trim();
    }
}
