using System.Text.Json;

namespace DbAutoInsert.Database
{
    public static class DatabaseManager
    {
        private static OracleHelper _helper;
        private static readonly object _lock = new object();

        public static OracleHelper GetHelper(string basePath)
        {
            if (_helper == null)
            {
                lock (_lock)
                {
                    if (_helper == null)
                    {
                        var jsonPath = Path.Combine(basePath, "oracle_connection.json");
                        var json     = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                        var cfg      = JsonSerializer.Deserialize<OracleConnectionJson>(json,
                                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                                       ?? throw new InvalidDataException("oracle_connection.json 파싱 실패");

                        _helper = new OracleHelper(cfg.UserId, cfg.Password, cfg.TnsAlias);
                    }
                }
            }
            return _helper;
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                _helper?.Dispose();
                _helper = null;
            }
        }

        private class OracleConnectionJson
        {
            public string TnsAlias  { get; set; } = "";
            public string UserId    { get; set; } = "";
            public string Password  { get; set; } = "";
        }
    }
}
