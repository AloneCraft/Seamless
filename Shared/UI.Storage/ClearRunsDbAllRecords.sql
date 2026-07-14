-- SQLite 用: UI DB のアプリ側データを全件削除
-- ※SQLite はストアドプロシージャをサポートしないため、代替として実行用 SQL を用意します。

BEGIN IMMEDIATE;

DELETE FROM "診断カウンター";
DELETE FROM "ベンチマーク";
DELETE FROM "実行";
DELETE FROM sqlite_sequence;

COMMIT;
