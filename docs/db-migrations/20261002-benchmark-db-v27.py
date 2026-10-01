"""Benchmark.db を版26から版27へ移行する(手書き理想版の「理想」列と射影融合タブの表を追加)。

版27の変更:
  - 最適化4タブとメモリ上4タブ(Array/IEnumerable/Range/List)の表の末尾に「理想_*」の8列を足す。
  - 射影融合タブの表(版26までは欠落)を作る。
  - 比率ビューを作り直し、理想列のある表には「<左>÷理想」「<右>÷理想」を足す。
  - user_version を27にする。

UI は版や形状が合わない DB を見つけるとバックアップして作り直す。既存の計測結果を残すには、
新しい UI を起動する前にこのスクリプトを実行する。移行後に古いビルド(版26)の UI を起動すると、
今度は版27の DB が作り直されるので起動しないこと。

使い方:
  python docs/db-migrations/20261002-benchmark-db-v27.py [Benchmark.db のパス]
  既定のパスは <リポジトリ>/Work/UI/Benchmark.db。

手順:
  1. Benchmark.UI / Worker が動いていないことを確かめる。
  2. WAL を本体へ書き戻す(wal_checkpoint(TRUNCATE))。
  3. backup API で Work/UI/backup/ へ複製し、integrity_check と表ごとの行数で複製を確かめる。
  4. 1トランザクションで列追加・表作成・ビュー再作成・版更新を行う。
  5. 移行後の形状を検査する(Benchmark.Headless --verify-db-schema でも確かめられる)。
"""

import datetime
import os
import sqlite3
import subprocess
import sys

元の版 = 26
新しい版 = 27
理想見出し = "理想"
列接尾辞一覧 = ["アセンブリ名", "型完全名", "状態", "失敗理由", "日時", "時間ns", "メモリバイト", "ループ回数"]

# Shared/UI.Storage/BenchmarkTabStorage.cs の定義一覧と同じ並び。
版27の表一覧 = [
    ("書き換え", ["全最適化", "書き換え無効", 理想見出し]),
    ("パイプライン融合", ["全最適化", "パイプライン融合無効", 理想見出し]),
    ("述語プッシュダウン", ["全最適化", "述語プッシュダウン無効", 理想見出し]),
    ("射影融合", ["全最適化", "射影融合無効", 理想見出し]),
    ("Array", ["FLinq", "ZLinq", 理想見出し]),
    ("IEnumerable", ["FLinq", "ZLinq", 理想見出し]),
    ("Range", ["FLinq", "ZLinq", 理想見出し]),
    ("List", ["FLinq", "ZLinq", 理想見出し]),
    ("Persisted(Seamless)", ["FLinq", "Persisted(Seamless)"]),
    ("Persisted(System)", ["FLinq", "Persisted(System)"]),
    ("Tsavorite(Seamless)", ["FLinq", "Tsavorite(Seamless)"]),
    ("Tsavorite(System)", ["FLinq", "Tsavorite(System)"]),
]
新しい表 = "射影融合"
共通列 = ["実行ID", "型", "ベンチマーク", "CPU番号", "引数キー", "引数JSON", "行チェック"]


def 引用(識別子):
    return '"' + 識別子.replace('"', '""') + '"'


def 側の列定義(見出し, 状態の既定値):
    状態 = " TEXT NOT NULL"
    if 状態の既定値:
        # 既存行へ NOT NULL 列を足すには既定値が要る。理想側は未計測なので待機中(Queued)。
        状態 = " TEXT NOT NULL DEFAULT 'Queued'"
    return [
        引用(見出し + "_アセンブリ名") + " TEXT NOT NULL DEFAULT ''",
        引用(見出し + "_型完全名") + " TEXT NOT NULL DEFAULT ''",
        引用(見出し + "_状態") + 状態,
        引用(見出し + "_失敗理由") + " TEXT NOT NULL DEFAULT ''",
        引用(見出し + "_日時") + " TEXT NULL",
        引用(見出し + "_時間ns") + " REAL NULL",
        引用(見出し + "_メモリバイト") + " INTEGER NULL",
        引用(見出し + "_ループ回数") + " INTEGER NULL",
    ]


def 表を作成(接続, 表名, 列見出し):
    列定義 = [
        '"実行ID" INTEGER NOT NULL', '"型" TEXT NOT NULL', '"ベンチマーク" TEXT NOT NULL', '"CPU番号" INTEGER NULL',
        "\"引数キー\" TEXT NOT NULL DEFAULT ''", "\"引数JSON\" TEXT NOT NULL DEFAULT ''", '"行チェック" INTEGER NOT NULL DEFAULT 0',
    ]
    for 見出し in 列見出し:
        列定義.extend(側の列定義(見出し, False))
    表 = 引用(表名)
    接続.execute(f'CREATE TABLE {表}({",".join(列定義)},FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE);')
    接続.execute(f'CREATE INDEX {引用("索引_" + 表名 + "_実行ID")} ON {表}("実行ID");')
    接続.execute(f'CREATE UNIQUE INDEX {引用("一意索引_" + 表名 + "_行")} ON {表}("実行ID","型","ベンチマーク","引数キー");')


def 理想比(対象列, 理想列):
    return f"CASE WHEN {対象列} IS NULL OR {理想列} IS NULL OR {対象列}<0 OR {理想列}<=0 THEN NULL ELSE CAST({対象列} AS REAL)/{理想列} END"


def ビューを作成(接続, 表名, 列見出し):
    表 = 引用(表名)
    左列 = 引用(列見出し[0] + "_時間ns")
    右列 = 引用(列見出し[1] + "_時間ns")
    理想比列 = ""
    if len(列見出し) > 2:
        理想列 = 引用(列見出し[2] + "_時間ns")
        理想比列 = f",{理想比(左列, 理想列)} AS {引用(列見出し[0] + '÷理想')},{理想比(右列, 理想列)} AS {引用(列見出し[1] + '÷理想')}"
    接続.execute(f"""CREATE VIEW {引用(表名 + "View")} AS
SELECT "型","ベンチマーク",
    CASE WHEN {左列} IS NULL OR {右列} IS NULL OR {左列}<0 OR {右列}<0 THEN NULL
         WHEN {左列}=0 AND {右列}=0 THEN 0.0
         ELSE CAST({左列} AS REAL)/({左列}+{右列}) END AS "比率"{理想比列}
FROM {表}
WHERE "実行ID"=(SELECT MAX("実行ID") FROM {表})
ORDER BY "比率" DESC;""")


def 列一覧(接続, 表名):
    return [行[0] for 行 in 接続.execute("SELECT name FROM pragma_table_info(?) ORDER BY cid;", (表名,))]


def 期待列一覧(列見出し):
    列 = list(共通列)
    for 見出し in 列見出し:
        列.extend(見出し + "_" + 接尾辞 for 接尾辞 in 列接尾辞一覧)
    return 列


def 表がある(接続, 名前, 種類="table"):
    return 接続.execute("SELECT COUNT(*) FROM sqlite_master WHERE type=? AND name=?;", (種類, 名前)).fetchone()[0] == 1


def 行数一覧(接続):
    名前一覧 = [行[0] for 行 in 接続.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;")]
    return {名前: 接続.execute(f"SELECT COUNT(*) FROM {引用(名前)};").fetchone()[0] for 名前 in 名前一覧}


def UIとWorkerの停止を確認():
    if os.name != "nt":
        return
    出力 = subprocess.run(["tasklist", "/fo", "csv", "/nh"], capture_output=True, text=True, encoding="mbcs", errors="replace").stdout
    動作中 = sorted({行.split(",")[0].strip('"') for 行 in 出力.splitlines()
                    if 行.split(",")[0].strip('"').lower() in ("benchmark.ui.exe", "worker.exe")})
    if 動作中:
        raise SystemExit("Benchmark.UI / Worker が動いています。停止してから実行してください: " + ", ".join(動作中))


def 形状を検査(接続):
    if 接続.execute("PRAGMA user_version;").fetchone()[0] != 新しい版:
        raise RuntimeError("user_version が27になっていません。")
    for 表名, 列見出し in 版27の表一覧:
        if not 表がある(接続, 表名):
            raise RuntimeError("表がありません: " + 表名)
        if not 表がある(接続, 表名 + "View", "view"):
            raise RuntimeError("ビューがありません: " + 表名 + "View")
        実際 = 列一覧(接続, 表名)
        if 実際 != 期待列一覧(列見出し):
            raise RuntimeError("列の並びが版27と一致しません: " + 表名 + " 実際=" + ",".join(実際))
        接続.execute(f"SELECT * FROM {引用(表名 + 'View')} LIMIT 1;").fetchall()


def main():
    リポジトリ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    DBパス = sys.argv[1] if len(sys.argv) > 1 else os.path.join(リポジトリ, "Work", "UI", "Benchmark.db")
    if not os.path.exists(DBパス):
        raise SystemExit("DB がありません: " + DBパス)
    既定DBパス = os.path.join(リポジトリ, "Work", "UI", "Benchmark.db")
    if os.path.normcase(os.path.abspath(DBパス)) == os.path.normcase(既定DBパス):
        # UI が使う DB だけ確かめる(複製で試すときは計測中の Worker があってもよい)。
        UIとWorkerの停止を確認()

    接続 = sqlite3.connect(DBパス, isolation_level=None)
    try:
        版 = 接続.execute("PRAGMA user_version;").fetchone()[0]
        if 版 == 新しい版:
            形状を検査(接続)
            print("すでに版27です。形状も一致しました: " + DBパス)
            return
        if 版 != 元の版:
            raise SystemExit(f"版{元の版}の DB だけを移行できます。実際の版={版}")
        if 接続.execute("PRAGMA wal_checkpoint(TRUNCATE);").fetchone()[0] != 0:
            raise SystemExit("WAL の書き戻しが他の接続に妨げられました。UI / Worker を止めてから再実行してください。")

        時刻 = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
        バックアップ先 = os.path.join(os.path.dirname(DBパス), "backup", f"Benchmark.v{元の版}.{時刻}.db")
        os.makedirs(os.path.dirname(バックアップ先), exist_ok=True)
        バックアップ = sqlite3.connect(バックアップ先)
        try:
            接続.backup(バックアップ)
            if バックアップ.execute("PRAGMA integrity_check;").fetchone()[0] != "ok":
                raise SystemExit("バックアップの integrity_check に失敗しました: " + バックアップ先)
            元の行数 = 行数一覧(接続)
            if 行数一覧(バックアップ) != 元の行数:
                raise SystemExit("バックアップの行数が元と一致しません: " + バックアップ先)
        finally:
            バックアップ.close()
        print("バックアップ: " + バックアップ先)

        接続.execute("BEGIN IMMEDIATE;")
        try:
            for 表名, 列見出し in 版27の表一覧:
                if 表名 == 新しい表:
                    continue
                if 列一覧(接続, 表名) != 期待列一覧(列見出し[:2]):
                    raise RuntimeError("版26の列の並びと一致しません: " + 表名)
            for 表名, 列見出し in 版27の表一覧:
                if 表がある(接続, 表名 + "View", "view"):
                    接続.execute(f"DROP VIEW {引用(表名 + 'View')};")
            for 表名, 列見出し in 版27の表一覧:
                if 表名 == 新しい表:
                    if 表がある(接続, 表名):
                        raise RuntimeError("版26に無いはずの表があります: " + 表名)
                    表を作成(接続, 表名, 列見出し)
                    continue
                if len(列見出し) > 2:
                    for 列定義 in 側の列定義(列見出し[2], True):
                        接続.execute(f"ALTER TABLE {引用(表名)} ADD COLUMN {列定義};")
            for 表名, 列見出し in 版27の表一覧:
                ビューを作成(接続, 表名, 列見出し)
            接続.execute(f"PRAGMA user_version={新しい版};")
            形状を検査(接続)
            for 表名, 件数 in 元の行数.items():
                if 接続.execute(f"SELECT COUNT(*) FROM {引用(表名)};").fetchone()[0] != 件数:
                    raise RuntimeError("移行中に行数が変わりました: " + 表名)
            接続.execute("COMMIT;")
        except BaseException:
            接続.execute("ROLLBACK;")
            raise
        if 接続.execute("PRAGMA integrity_check;").fetchone()[0] != "ok":
            raise SystemExit("移行後の integrity_check に失敗しました。バックアップから戻してください: " + バックアップ先)
        print("版27へ移行しました: " + DBパス)
    finally:
        接続.close()


if __name__ == "__main__":
    main()
