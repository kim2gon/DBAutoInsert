using System.Text.Json;
using DbAutoInsert.Models;

namespace DbAutoInsert.Core
{
    public static class ConfigLoader
    {
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling         = JsonCommentHandling.Skip,
            AllowTrailingCommas         = true
        };

        public static AppConfig LoadAppConfig(string basePath, AppLogger log)
        {
            var path = Path.Combine(basePath, "config.json");
            log.Info($"config.json 로드: {path}");
            var json   = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var result = JsonSerializer.Deserialize<AppConfig>(json, _opts)
                         ?? throw new InvalidDataException("config.json 파싱 실패");
            return result;
        }
    }
}
