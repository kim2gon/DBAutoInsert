namespace DbAutoInsert.Core
{
    public static class ArgParser
    {
        private const string RequiredKey = "CWIT_SYSTEM";

        public static bool Validate(string[] args, AppLogger log)
        {
            if (args == null || args.Length < 2)
            {
                log.Warn("실행 인자 없음 → 종료");
                return false;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                var flag = args[i].TrimStart('/', '-').ToUpperInvariant();
                if (flag == "K")
                {
                    var value = args[i + 1];
                    if (value == RequiredKey)
                    {
                        log.Ok($"인증 성공 (key={MaskKey(value)})");
                        return true;
                    }
                    else
                    {
                        log.Warn($"인증 키 불일치 → 종료");
                        return false;
                    }
                }
            }

            log.Warn("/k 파라미터 없음 → 종료");
            return false;
        }

        private static string MaskKey(string key)
            => key.Length <= 4 ? "****" : key.Substring(0, 4) + new string('*', key.Length - 4);
    }
}
