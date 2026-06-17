// WinExe 이므로 콘솔 창은 절대 뜨지 않음
// 외부 프로그램에서 다음과 같이 호출:
//   Process.Start("DbAutoInsert.exe", "/k CWIT_SYSTEM")
//   또는 배치: DbAutoInsert.exe /k CWIT_SYSTEM

using DbAutoInsert.Core;
using DbAutoInsert.Providers;

// ── 기본 경로: EXE가 있는 디렉터리 ──────────────────────────
var basePath = AppContext.BaseDirectory;

// ── 로거 먼저 초기화 (모든 이벤트 기록) ─────────────────────
// config.json 로드 전이므로 LogDirectory 기본값 사용
var logDir = Path.Combine(basePath, "logs");
var log    = new AppLogger(logDir);

log.Info("====================================================");
log.Info("DbAutoInsert 시작");
log.Info($"EXE 경로: {basePath}");
log.Info($"실행 인자: {string.Join(" ", args)}");

// ── 파라미터 인증 (/k CWIT_SYSTEM) ──────────────────────────
if (!ArgParser.Validate(args, log))
{
    log.Warn("인증 실패 → 프로그램 종료");
    log.Info("====================================================");
    return; // 조용히 종료, 사용자에게 아무 것도 보이지 않음
}

// ── 설정 파일 로드 ────────────────────────────────────────────
try
{
    var appConfig    = ConfigLoader.LoadAppConfig(basePath, log);
    var oracleConfig = ConfigLoader.LoadOracleConfig(basePath, log);

    // LogDirectory가 config에 지정된 경우 재지정
    if (!string.IsNullOrWhiteSpace(appConfig.LogDirectory))
    {
        var customLogDir = Path.IsPathRooted(appConfig.LogDirectory)
            ? appConfig.LogDirectory
            : Path.Combine(basePath, appConfig.LogDirectory);
        log = new AppLogger(customLogDir);
    }

    // ── DB 작업 실행 ──────────────────────────────────────────
    using var provider = new OracleProvider(oracleConfig);
    var engine         = new DbEngine(provider, appConfig, log, basePath);
    engine.Run();

    log.Ok("프로그램 정상 종료");
}
catch (FileNotFoundException ex)
{
    log.Error($"설정 파일 없음: {ex.Message}");
}
catch (Exception ex)
{
    log.Error($"오류 발생: {ex.GetType().Name} - {ex.Message}");
    log.Error($"StackTrace: {ex.StackTrace}");
}
finally
{
    log.Info("====================================================");
    // WinExe이므로 별도 종료 처리 없이 자동 종료
    // 사용자에게는 아무 창도, 알림도 표시되지 않음
}
