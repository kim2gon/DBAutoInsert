-- extra_inserts.sql
-- 이 파일은 세미콜론(;)으로 구문을 구분합니다
-- Oracle MERGE 구문으로 중복 방지 삽입 예시

MERGE INTO "USERS" tgt
USING (SELECT 'admin@example.com' AS EMAIL FROM dual) src
ON (tgt."EMAIL" = src.EMAIL)
WHEN NOT MATCHED THEN
  INSERT ("NAME","EMAIL","DEPT","CREATED_AT")
  VALUES ('관리자','admin@example.com','시스템팀',SYSTIMESTAMP)
