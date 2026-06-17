using DbAutoInsert.Core;
using DbAutoInsert.Database;
using DbAutoInsert.Providers;

var basePath = AppContext.BaseDirectory;
var logDir   = Path.Combine(basePath, "logs");
var log      = new AppLogger(logDir);

log.Info("====================================================");
log.Info("DbAutoInsert 시작");
log.Info($"EXE 경로: {basePath}");
log.Info($"실행 인자: {string.Join(" ", args)}");

// ── 파라미터 인증 (/k CWIT_SYSTEM) ───────────────────────────
// 테스트 시 아래 블록 주석 처리
//if (!ArgParser.Validate(args, log))
//{
//    log.Warn("인증 실패 → 프로그램 종료");
//    log.Info("====================================================");
//    return;
//}

try
{
    // ── 설정 로드 ─────────────────────────────────────────────
    var appConfig = ConfigLoader.LoadAppConfig(basePath, log);

    // ── OracleHelper 초기화 (TNS 별칭으로 연결) ───────────────
    log.Info("oracle_connection.json 로드 및 Oracle 연결 시도");
    var helper = DatabaseManager.GetHelper(basePath);
    log.Ok("Oracle 연결 성공");

    // ── DB 작업 실행 ──────────────────────────────────────────
    using var provider = new OracleProvider(helper);
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
    DatabaseManager.Dispose();
    log.Info("====================================================");
}
