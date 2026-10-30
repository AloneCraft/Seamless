-- 版2 -> 版3: クエリごとの要素型(int 以外の要素型の次元)。既存の行は int として読む。
-- 適用は Apply-BenchmarkDynamicMigration.ps1 -TargetVersion 3 -MigrationPath <このファイル>。
ALTER TABLE "DynamicQueries" ADD COLUMN "element_type" TEXT NOT NULL DEFAULT 'Int';

INSERT INTO "DynamicSchemaInfo"("version","applied_at")
VALUES(3,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
