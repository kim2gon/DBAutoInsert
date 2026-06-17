# DbAutoInsert

Oracle DB에 테이블/컬럼 자동 생성 및 데이터 삽입 도구.
터미널 창 없이 백그라운드에서 조용히 실행됩니다.

---

## 배포 파일 구조

```
DbAutoInsert.exe          ← 실행 파일 (터미널 창 없음)
config.json               ← 테이블/컬럼/데이터파일 정의
oracle_connection.json    ← Oracle 접속 정보 (TNS)
│
data/
│   users.csv             ← USERS 테이블 삽입 데이터
│   products.csv          ← PRODUCTS 테이블 삽입 데이터
│
sql/
│   extra_inserts.sql     ← 추가 SQL 직접 실행
│
logs/
    dbinsert_20240115.log ← 날짜별 실행 로그 (자동 생성)
```

---

## oracle_connection.json

```json
{
  "Host"        : "192.168.0.100",   ← Oracle 서버 IP
  "Port"        : 1521,              ← 포트 (기본 1521)
  "ServiceName" : "ORCL",           ← 서비스명
  "UserId"      : "cwit_user",      ← 계정
  "Password"    : "cwit_pass",      ← 비밀번호
  "ConnectionTimeout" : 30,
  "MinPoolSize"       : 1,
  "MaxPoolSize"       : 10
}
```

내부적으로 TNS 문자열로 자동 변환됩니다:
```
(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=...)(PORT=...))(CONNECT_DATA=(SERVICE_NAME=...)))
```

---

## config.json — 테이블/컬럼 정의

```json
{
  "Tables": [
    {
      "TableName"     : "USERS",
      "UniqueColumns" : ["EMAIL"],       ← 중복 체크 기준 컬럼
      "Columns": [
        { "Name": "ID",    "Type": "NUMBER(10)",    "PrimaryKey": true, "AutoIncrement": true },
        { "Name": "NAME",  "Type": "VARCHAR2(100)", "NotNull": true },
        { "Name": "EMAIL", "Type": "VARCHAR2(200)", "NotNull": true }
      ],
      "DataFile": "data/users.csv"       ← CSV 또는 SQL 파일 (EXE 기준 상대경로)
    }
  ],
  "SqlFiles": [
    "sql/extra_inserts.sql"              ← 별도 SQL 파일 실행 목록
  ],
  "LogDirectory": "logs"                 ← 로그 저장 경로
}
```

### DataFile 지원 형식

| 형식 | 동작 |
|------|------|
| `.csv` | 헤더 행 기준으로 파싱 → UniqueColumns 중복체크 후 INSERT |
| `.sql` | 세미콜론(`;`) 구분으로 Oracle에 직접 실행 |

---

## 호출 방법

### 파라미터 규칙
- `/k CWIT_SYSTEM` 이 반드시 있어야 실행됨
- 없거나 틀리면 → 아무 반응 없이 종료 (로그에만 기록)

### 배치파일에서 호출
```bat
DbAutoInsert.exe /k CWIT_SYSTEM
```

### C# 프로그램에서 호출
```csharp
// 호출 후 백그라운드에서 알아서 실행되고 종료됨
var psi = new ProcessStartInfo
{
    FileName        = @"C:\CWIT\DbAutoInsert.exe",
    Arguments       = "/k CWIT_SYSTEM",
    CreateNoWindow  = true,
    UseShellExecute = false,
    WindowStyle     = ProcessWindowStyle.Hidden
};
Process.Start(psi);
// Wait 하지 않아도 됨 — 백그라운드에서 조용히 처리
```

---

## 로그 확인

`logs/dbinsert_YYYYMMDD.log` 파일로 날짜별 기록됩니다.

```
[2024-01-15 09:00:00.123] [INFO ] DbAutoInsert 시작
[2024-01-15 09:00:00.124] [INFO ] 실행 인자: /k CWIT_SYSTEM
[2024-01-15 09:00:00.125] [OK   ] 인증 성공 (key=CWIT*****)
[2024-01-15 09:00:00.200] [OK   ] Oracle 연결 성공
[2024-01-15 09:00:00.210] [INFO ] 테이블 존재: USERS
[2024-01-15 09:00:00.220] [WARN ] 컬럼 없음 → ALTER TABLE ADD: DEPT (VARCHAR2(100))
[2024-01-15 09:00:00.230] [OK   ] 삽입: 3행  |  중복 스킵: 1행
[2024-01-15 09:00:00.300] [OK   ] 모든 작업 완료
```

---

## 빌드

```bash
# Windows 단일 EXE
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

출력 경로: `bin/Release/net8.0-windows/win-x64/publish/`
