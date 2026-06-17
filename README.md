# DbAutoInsert

Oracle DB에 데이터를 자동으로 삽입해주는 백그라운드 실행 도구입니다.
CSV, SQL, TXT, SQLite DB 파일을 지정 폴더에 넣으면 자동으로 처리합니다.

---

## 기능 정의

### 1. 자동 테이블/컬럼 생성
- 테이블이 없으면 자동으로 `CREATE TABLE`
- 컬럼이 없으면 자동으로 `ALTER TABLE ADD COLUMN`
- 모든 컬럼 타입은 `VARCHAR2(4000)` 으로 생성

### 2. 중복 방지
- 동일한 값이 이미 존재하면 자동으로 스킵
- 없는 데이터만 `INSERT`

### 3. 파일 형식별 처리

| 형식 | 처리 방식 |
|------|----------|
| `.csv` | 파일명 → 테이블명, 헤더 → 컬럼명으로 자동 인식 후 삽입 |
| `.sql` / `.txt` | Oracle/SQLite 문법 자동 감지 후 실행 |
| `.db` | SQLite DB 파일 직접 읽어서 Oracle에 자동 삽입 |

### 4. SQLite → Oracle 자동 변환
- `"main"."테이블명"` → `"테이블명"` (스키마 제거)
- 빈 테이블명 `""` → 파일명으로 자동 대체
- `INSERT OR IGNORE` / `INSERT OR REPLACE` → `INSERT`
- `AUTOINCREMENT` → 제거
- `CREATE TABLE` 내 타입 자동 변환
  - `TEXT` → `VARCHAR2(4000)`
  - `INTEGER` → `NUMBER(10)`
  - `REAL` → `NUMBER(18,6)`
  - `NUMERIC` → `NUMBER`

### 5. 보안 인증
- `/k CWIT_SYSTEM` 파라미터 없이 실행 시 조용히 종료
- 터미널 창 없이 백그라운드에서 실행

### 6. 로그
- `logs/dbinsert_YYYYMMDD.log` 날짜별 자동 생성
- 모든 처리 결과 기록 (성공/실패/스킵 등)

---

## 배포 파일 구조

```
DbAutoInsert.exe          ← 실행 파일
config.json               ← 데이터 폴더 경로 설정
oracle_connection.json    ← Oracle 접속 정보
dbinsert/                 ← 데이터 파일 넣는 폴더
    users.csv
    products.sql
    legacy.db
logs/                     ← 로그 자동 생성
    dbinsert_20240615.log
```

---

## 설정 파일

### config.json
```json
{
  "DataDirectory" : "dbinsert",
  "LogDirectory"  : "logs"
}
```

| 항목 | 설명 |
|------|------|
| `DataDirectory` | csv, sql, txt, db 파일을 넣는 폴더 (EXE 기준 상대경로) |
| `LogDirectory` | 로그 파일 저장 경로 |

### oracle_connection.json
```json
{
  "TnsAlias"  : "DJH",
  "UserId"    : "아이디",
  "Password"  : "비밀번호"
}
```

| 항목 | 설명 |
|------|------|
| `TnsAlias` | tnsnames.ora 에 등록된 TNS 별칭 |
| `UserId` | Oracle 계정 아이디 |
| `Password` | Oracle 계정 비밀번호 |

> TNS 파일 경로는 고객 PC의 `TNS_ADMIN` 환경변수에서 자동으로 찾습니다.

---

## 실행 방법

### 직접 실행 (테스트용)
```
DbAutoInsert.exe
```

### 인증 파라미터와 실행 (운영용)
```
DbAutoInsert.exe /k CWIT_SYSTEM
```

### 다른 프로그램에서 호출 (C#)
```csharp
var psi = new ProcessStartInfo
{
    FileName        = @"C:\CWIT\DbAutoInsert.exe",
    Arguments       = "/k CWIT_SYSTEM",
    CreateNoWindow  = true,
    UseShellExecute = false,
    WindowStyle     = ProcessWindowStyle.Hidden
};
Process.Start(psi);
```

### 배치파일에서 호출
```bat
DbAutoInsert.exe /k CWIT_SYSTEM
```

---

## 데이터 파일 작성 방법

### CSV 파일
- 첫 번째 행이 헤더 (컬럼명)
- 파일명이 테이블명으로 사용됨
- 인코딩: UTF-8 (BOM 없음 권장)

```
NAME,EMAIL,DEPT
홍길동,hong@example.com,개발팀
김영희,kim@example.com,인사팀
```
→ `USERS` 테이블에 삽입 (`users.csv` 기준)

### SQL/TXT 파일 (Oracle 문법)
```sql
INSERT INTO "USERS" ("NAME","EMAIL") VALUES ('홍길동','hong@example.com');
INSERT INTO "PRODUCTS" ("CODE","NAME") VALUES ('P001','노트북');
```

### SQL/TXT 파일 (SQLite 문법 — 자동 변환)
```sql
INSERT INTO "main"."" ("name","email") VALUES ('홍길동','hong@example.com');
```
→ 자동으로 Oracle 문법으로 변환 후 실행
→ 빈 테이블명은 파일명으로 자동 대체

### SQLite DB 파일
- `.db` 파일을 그대로 `dbinsert` 폴더에 복사
- DB 내 모든 테이블 자동 추출 후 Oracle에 삽입

---

## 처리 순서

같은 폴더 안에서 아래 순서로 처리됩니다.

```
1. CSV 파일 (.csv)
2. SQL/TXT 파일 (.sql, .txt)
3. SQLite DB 파일 (.db)
```

같은 확장자 내에서는 파일명 알파벳 순서로 처리됩니다.

---

## 빌드 방법

```bash
dotnet publish -c Release -r win-x64 --self-contained false
```

> .NET Framework 4.8 기반으로 빌드됩니다.
> 고객 PC에 .NET Framework 4.8이 설치되어 있어야 합니다.

---

## 주의사항

1. **TNS_ADMIN 환경변수** — 고객 PC에 `TNS_ADMIN` 환경변수가 설정되어 있어야 Oracle 접속이 가능합니다.

2. **파일명 = 테이블명** — CSV 파일명이 Oracle 테이블명으로 사용됩니다. 파일명에 특수문자 사용을 피해주세요.

3. **컬럼 타입** — 자동 생성되는 컬럼은 모두 `VARCHAR2(4000)` 타입입니다. 특정 타입이 필요한 경우 Oracle에서 직접 테이블을 먼저 생성해두면 기존 테이블을 그대로 사용합니다.

4. **중복 체크** — 모든 컬럼 값이 완전히 동일한 행만 중복으로 판단합니다. 일부 컬럼만 같은 경우는 중복으로 처리되지 않습니다.

5. **SQL 파일 세미콜론** — SQL/TXT 파일은 세미콜론(`;`)으로 구문을 구분합니다. 마지막 구문에도 `;`를 붙여주세요.

6. **인증 파라미터** — 운영 배포 시 `Program.cs`의 인증 주석을 해제하고 빌드해야 합니다.

7. **로그 확인** — 오류 발생 시 `logs/` 폴더의 날짜별 로그 파일을 확인하세요.

---

## 로그 예시

```
[2024-06-15 09:00:00.100] [INFO ] DbAutoInsert 시작
[2024-06-15 09:00:00.500] [OK   ] Oracle 연결 성공
[2024-06-15 09:00:00.510] [----] ──── CSV 처리: users.csv → 테이블: USERS
[2024-06-15 09:00:00.520] [INFO ]   CSV 로드: 10행  컬럼: NAME, EMAIL, DEPT
[2024-06-15 09:00:00.530] [WARN ]   테이블 없음 → CREATE TABLE: USERS
[2024-06-15 09:00:00.600] [OK   ]   테이블 생성 완료: USERS
[2024-06-15 09:00:00.800] [OK   ]   삽입: 10행  |  중복 스킵: 0행
[2024-06-15 09:00:01.000] [OK   ] 모든 작업 완료
```