-- SQLite 用: runs.db のアプリ側データを全件削除
-- ※SQLite はストアドプロシージャをサポートしないため、代替として実行用 SQL を用意します。

BEGIN IMMEDIATE;

DELETE FROM MetricValues;
DELETE FROM Measurements;
DELETE FROM Comparisons;
DELETE FROM FallbackDiagnostics;
DELETE FROM UiSelections;
DELETE FROM UiViewStates;
DELETE FROM RunStates;
DELETE FROM Jobs;
DELETE FROM CaseArguments;
DELETE FROM Cases;
DELETE FROM Runs;
DELETE FROM Apps;
DELETE FROM SchemaMigrations;
DELETE FROM sqlite_sequence;

COMMIT;
