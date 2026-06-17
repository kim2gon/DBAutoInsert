using System.Text.Json;
using DbAutoInsert.Models;

namespace DbAutoInsert.Core;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    public static AppConfig LoadAppConfig(string basePath, AppLogger log)
    {
        var path = Path.Combine(basePath, "config.json");
        log.Info($"config.json 로드: {path}");
        return Load<AppConfig>(path);
    }

    /// <summary>
    /// oracle_connection.json (TnsAlias + 계정) 로드 후
    /// TNS_ADMIN 환경변수로 tnsnames.ora 를 찾아 파싱,
    /// 최종 OracleConnectionConfig 반환
    /// </summary>
    public static OracleConnectionConfig LoadOracleConfig(string basePath, AppLogger log)
    {
        var jsonPath = Path.Combine(basePath, "oracle_connection.json");
        log.Info($"oracle_connection.json 로드: {jsonPath}");
        var file = Load<OracleConnectionFile>(jsonPath);

        // tnsnames.ora 탐색 + 파싱 (TNS_ADMIN 환경변수 우선)
        var cfg = TnsParser.Parse(file.TnsAlias, log);

        // 계정 정보 병합
        cfg.UserId            = file.UserId;
        cfg.Password          = file.Password;
        cfg.ConnectionTimeout = file.ConnectionTimeout;
        cfg.MinPoolSize       = file.MinPoolSize;
        cfg.MaxPoolSize       = file.MaxPoolSize;

        log.Ok($"Oracle 접속 준비 완료 → {cfg.Host}:{cfg.Port}/{cfg.ServiceName}  User={cfg.UserId}");
        return cfg;
    }

    private static T Load<T>(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"설정 파일을 찾을 수 없습니다: {path}");

        var json   = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var result = JsonSerializer.Deserialize<T>(json, _opts)
                     ?? throw new InvalidDataException($"역직렬화 실패: {path}");
        return result;
    }
}
