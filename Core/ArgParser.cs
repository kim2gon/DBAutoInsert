namespace DbAutoInsert.Core;

/// <summary>
/// 실행 인자 파싱 및 인증 키 검증
/// 
/// 유효한 호출 예시:
///   DbAutoInsert.exe /k CWIT_SYSTEM
///   DbAutoInsert.exe -k CWIT_SYSTEM
/// </summary>
public static class ArgParser
{
    private const string RequiredKey = "CWIT_SYSTEM";

    /// <summary>
    /// 파라미터 검증.
    /// 올바르면 true, 아니면 false (프로그램은 조용히 종료해야 함)
    /// </summary>
    public static bool Validate(string[] args, AppLogger log)
    {
        // args 자체가 없는 경우
        if (args == null || args.Length < 2)
        {
            log.Warn("실행 인자 없음 → 종료");
            return false;
        }

        // /k 또는 -k 위치 탐색
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
                    log.Warn($"인증 키 불일치 (입력값={MaskKey(value)}) → 종료");
                    return false;
                }
            }
        }

        log.Warn("/k 파라미터 없음 → 종료");
        return false;
    }

    /// <summary>로그에 키 전체 노출 방지 — 앞 4자만 표시</summary>
    private static string MaskKey(string key)
        => key.Length <= 4 ? "****" : key[..4] + new string('*', key.Length - 4);
}
