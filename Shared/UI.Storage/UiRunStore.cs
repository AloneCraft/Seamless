using Microsoft.Data.Sqlite;

namespace UI.Storage;

public sealed class UI実行ストア{
    private const int ベンチマークスキーマ版=26;
    private const int スケーラブルスキーマ版=20;
    private const int パラメータスイープスキーマ版=21;
    private int スキーマ版=>this.アプリ種別 switch{
        UI実行アプリ種別.ベンチマーク=>ベンチマークスキーマ版,
        UI実行アプリ種別.スケーラブルベンチマーク=>スケーラブルスキーマ版,
        UI実行アプリ種別.パラメータスイープ=>パラメータスイープスキーマ版,
        _=>throw new InvalidOperationException("対応しないUI実行アプリ種別です。"),
    };
    private readonly string DBパス;
    private readonly UI実行アプリ種別 アプリ種別;
    private readonly Lock スキーマロック=new();
    private bool スキーマ確認済みか;

    public UI実行ストア(string DBパス,UI実行アプリ種別 アプリ種別 = UI実行アプリ種別.ベンチマーク){
        ArgumentException.ThrowIfNullOrWhiteSpace(DBパス);
        this.DBパス=DBパス;
        this.アプリ種別=アプリ種別;
    }

    public string データベースパス=>this.DBパス;

    public void スキーマを確認(){
        lock(this.スキーマロック){
            if(this.スキーマ確認済みか){
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(this.DBパス)??".");
            if(File.Exists(this.DBパス)){
                var 既存スキーマ一致か=this.保存済みスキーマ版を読む()==スキーマ版&&this.想定スキーマ形状か();
                if(!既存スキーマ一致か){
                    if(this.アプリ種別==UI実行アプリ種別.ベンチマーク){
                        this.旧ベンチマークDBをバックアップして削除();
                    }else{
                        throw new InvalidOperationException("UI実行DBのスキーマが一致しません。明示的なバックアップ後にDBを再作成してください: "+this.DBパス);
                    }
                }else{
                    this.スキーマ確認済みか=true;
                    return;
                }
            }
            using var 接続=this.接続を開く();
            using var トランザクション=ロック再試行付きでトランザクションを開始(接続);
            共通テーブルを作成(接続,トランザクション,this.アプリ種別);
            アプリテーブルを作成(接続,トランザクション,this.アプリ種別);
            分析ビューを作成(接続,トランザクション,this.アプリ種別);
            非クエリを実行(接続,トランザクション,$"PRAGMA user_version={スキーマ版};");
            トランザクション.Commit();
            this.スキーマ確認済みか=true;
        }
    }

    private static void 共通テーブルを作成(SqliteConnection 接続,SqliteTransaction トランザクション,UI実行アプリ種別 アプリ種別){
        if(アプリ種別==UI実行アプリ種別.ベンチマーク){
            非クエリを実行(接続,トランザクション,"""
                CREATE TABLE "実行"(
                    "実行ID" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "表示名" TEXT NOT NULL,
                    "状態" TEXT NOT NULL,
                    "実行プロファイル" TEXT NOT NULL,
                    "タブ名" TEXT NOT NULL,
                    "更新日時" TEXT NOT NULL,
                    "実行ルート" TEXT NOT NULL DEFAULT '',
                    "要素数一覧" TEXT NOT NULL DEFAULT '',
                    "計測時間ns" REAL NOT NULL DEFAULT 0,
                    "論理CPU一覧" TEXT NOT NULL DEFAULT ''
                );
                CREATE INDEX "索引_実行_更新日時" ON "実行"("更新日時");
                """);
            return;
        }
        非クエリを実行(接続,トランザクション,"""
            CREATE TABLE "実行"(
                "実行ID" INTEGER PRIMARY KEY AUTOINCREMENT,
                "表示名" TEXT NOT NULL,
                "状態" TEXT NOT NULL,
                "実行プロファイル" TEXT NOT NULL,
                "タブ名" TEXT NOT NULL,
                "更新日時" TEXT NOT NULL,
                "実行ルート" TEXT NOT NULL DEFAULT '',
                "要素数一覧" TEXT NOT NULL DEFAULT '',
                "適応閾値ns" REAL NOT NULL DEFAULT 0,
                "論理CPU一覧" TEXT NOT NULL DEFAULT '',
                "前回実行ID" INTEGER NULL,
                FOREIGN KEY("前回実行ID") REFERENCES "実行"("実行ID") ON DELETE SET NULL
            );
            CREATE INDEX "索引_実行_更新日時" ON "実行"("更新日時");
            """);
    }

    private static void アプリテーブルを作成(SqliteConnection 接続,SqliteTransaction トランザクション,UI実行アプリ種別 アプリ種別){
        if(アプリ種別==UI実行アプリ種別.ベンチマーク){ BenchmarkTabStorage.作成(接続,トランザクション); return; }
        if(アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク){
            非クエリを実行(接続,トランザクション,"""
                CREATE TABLE "ベンチマーク"(
                    "実行ID" INTEGER NOT NULL,
                    "タブ名" TEXT NOT NULL,
                    "型" TEXT NOT NULL,
                    "ベンチマーク" TEXT NOT NULL,
                    "Count" INTEGER NULL,"Initial" INTEGER NULL,"Data" INTEGER NULL,"Index" INTEGER NULL,
                    "Page" INTEGER NULL,"Segment" INTEGER NULL,"Memory" INTEGER NULL,"Fraction" REAL NULL,
                    "CPU番号" INTEGER NULL,
                    "行チェック" INTEGER NOT NULL DEFAULT 0,
                    "今回状態" TEXT NOT NULL,
                    "今回失敗理由" TEXT NOT NULL DEFAULT '',
                    "今回日時" TEXT NULL,
                    "今回時間ns" REAL NULL,
                    "今回メモリバイト" INTEGER NULL,
                    FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
                );
                CREATE INDEX "索引_ベンチマーク_実行ID" ON "ベンチマーク"("実行ID");
                CREATE UNIQUE INDEX "一意索引_ベンチマーク_行" ON "ベンチマーク"(
                    "実行ID","タブ名","型","ベンチマーク",
                    COALESCE("Count",-1),COALESCE("Initial",-1),COALESCE("Data",-1),COALESCE("Index",-1),
                    COALESCE("Page",-1),COALESCE("Segment",-1),COALESCE("Memory",-1),COALESCE("Fraction",-1)
                );
                """);
            return;
        }
        非クエリを実行(接続,トランザクション,"""
            CREATE TABLE "ベンチマーク"(
                "実行ID" INTEGER NOT NULL,
                "ケース名" TEXT NOT NULL,
                "コレクション" TEXT NOT NULL,
                "ジェネリック" TEXT NOT NULL,
                "バックエンド" TEXT NOT NULL,
                "スイープ" TEXT NOT NULL,
                "Indexサイズ" INTEGER NOT NULL,"Pageサイズ" INTEGER NOT NULL,
                "Segmentサイズ" INTEGER NOT NULL,"Memoryサイズ" INTEGER NOT NULL,
                "可変Fraction" REAL NOT NULL,"最大成功Count" INTEGER NOT NULL,"エントリCount" INTEGER NOT NULL,
                "失敗種別" TEXT NOT NULL,"失敗理由" TEXT NOT NULL,
                "追加時間ms" REAL NOT NULL,"検索時間ns" REAL NULL,"測定日時" TEXT NOT NULL,
                "索引バケット数" INTEGER NULL,"溢れバケット数" INTEGER NULL,
                "メモリログバイト数" INTEGER NULL,"ディスクログバイト数" INTEGER NULL,
                PRIMARY KEY("実行ID","ケース名"),
                FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
            );
            CREATE INDEX "索引_ベンチマーク_実行ID" ON "ベンチマーク"("実行ID");
            """);
    }

    private static void 分析ビューを作成(SqliteConnection 接続,SqliteTransaction トランザクション,UI実行アプリ種別 アプリ種別){
        if(アプリ種別==UI実行アプリ種別.ベンチマーク){ BenchmarkTabStorage.作成ビュー(接続,トランザクション); return; }
        if(アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク){
            非クエリを実行(接続,トランザクション,"""
                CREATE VIEW "スケーラブル履歴" AS
                SELECT r."実行ID",r."前回実行ID",r."更新日時",b."タブ名",b."型",b."ベンチマーク",
                    b."Count",b."Initial",b."Data",b."Index",b."Page",b."Segment",b."Memory",b."Fraction",b."今回時間ns",b."今回メモリバイト"
                FROM "ベンチマーク" b INNER JOIN "実行" r ON r."実行ID"=b."実行ID" WHERE b."今回状態"='Completed';
                CREATE VIEW "スケーラブル前回シェア" AS
                SELECT c.*,p."今回時間ns" AS "前回時間ns",
                    CASE WHEN c."今回時間ns" IS NULL OR p."今回時間ns" IS NULL OR c."今回時間ns"<0 OR p."今回時間ns"<0 THEN NULL WHEN c."今回時間ns"=0 AND p."今回時間ns"=0 THEN 0.0 ELSE CAST(c."今回時間ns" AS REAL)/(c."今回時間ns"+p."今回時間ns") END AS "今回時間シェア",
                    CASE WHEN c."今回時間ns" IS NULL OR p."今回時間ns" IS NULL OR c."今回時間ns"<0 OR p."今回時間ns"<0 THEN NULL WHEN c."今回時間ns"=0 AND p."今回時間ns"=0 THEN 0.0 ELSE CAST(p."今回時間ns" AS REAL)/(c."今回時間ns"+p."今回時間ns") END AS "前回時間シェア",
                    p."今回メモリバイト" AS "前回メモリバイト",
                    CASE WHEN c."今回メモリバイト" IS NULL OR p."今回メモリバイト" IS NULL OR c."今回メモリバイト"<0 OR p."今回メモリバイト"<0 THEN NULL WHEN c."今回メモリバイト"=0 AND p."今回メモリバイト"=0 THEN 0.0 ELSE CAST(c."今回メモリバイト" AS REAL)/(c."今回メモリバイト"+p."今回メモリバイト") END AS "今回メモリシェア",
                    CASE WHEN c."今回メモリバイト" IS NULL OR p."今回メモリバイト" IS NULL OR c."今回メモリバイト"<0 OR p."今回メモリバイト"<0 THEN NULL WHEN c."今回メモリバイト"=0 AND p."今回メモリバイト"=0 THEN 0.0 ELSE CAST(p."今回メモリバイト" AS REAL)/(c."今回メモリバイト"+p."今回メモリバイト") END AS "前回メモリシェア"
                FROM "スケーラブル履歴" c LEFT JOIN "スケーラブル履歴" p
                  ON p."実行ID"=c."前回実行ID" AND p."タブ名"=c."タブ名" AND p."型"=c."型" AND p."ベンチマーク"=c."ベンチマーク"
                 AND p."Count" IS c."Count" AND p."Initial" IS c."Initial" AND p."Data" IS c."Data" AND p."Index" IS c."Index"
                 AND p."Page" IS c."Page" AND p."Segment" IS c."Segment" AND p."Memory" IS c."Memory" AND p."Fraction" IS c."Fraction";
                """);
        }
    }

    public UI実行記録 実行を作成(UI実行作成要求 要求){
        if(要求.アプリ種別!=this.アプリ種別){
            throw new InvalidOperationException("ストアと要求のアプリ種別が一致しません。");
        }
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        long? 前回実行ID=null;
        if(this.アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク){
            using var 前回コマンド=接続.CreateCommand();
            前回コマンド.CommandText="SELECT \"実行ID\" FROM \"実行\" WHERE \"タブ名\"=$tab AND \"状態\"='Completed' ORDER BY \"更新日時\" DESC,\"実行ID\" DESC LIMIT 1;";
            前回コマンド.Parameters.AddWithValue("$tab",要求.タブ名);
            前回実行ID=前回コマンド.ExecuteScalar() as long?;
        }
        using var コマンド=接続.CreateCommand();
        if(this.アプリ種別==UI実行アプリ種別.ベンチマーク){
            コマンド.CommandText="""
                INSERT INTO "実行"("表示名","状態","実行プロファイル","タブ名","更新日時")
                VALUES($name,'Queued',$profile,$tab,$at);SELECT last_insert_rowid();
                """;
        }else{
            コマンド.CommandText="""
                INSERT INTO "実行"("表示名","状態","実行プロファイル","タブ名","更新日時","前回実行ID")
                VALUES($name,'Queued',$profile,$tab,$at,$previous);SELECT last_insert_rowid();
                """;
            コマンド.Parameters.AddWithValue("$previous",DB値へ(前回実行ID));
        }
        コマンド.Parameters.AddWithValue("$name",要求.表示名);
        コマンド.Parameters.AddWithValue("$profile",要求.実行プロファイル);
        コマンド.Parameters.AddWithValue("$tab",要求.タブ名);
        コマンド.Parameters.AddWithValue("$at",日時を文字列へ(要求.作成日時));
        var 実行ID=(long)(ロック再試行付きでスカラーを実行(コマンド)??0L);
        return new(実行ID,要求.アプリ種別,要求.表示名,UI実行状態.待機中,要求.実行プロファイル,要求.タブ名,要求.作成日時);
    }

    public void 実行状態を設定(long 実行ID,UI実行状態 状態,string メッセージ = ""){
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="UPDATE \"実行\" SET \"状態\"=$status,\"更新日時\"=$at WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        コマンド.Parameters.AddWithValue("$status",状態を文字列へ(状態));
        コマンド.Parameters.AddWithValue("$at",日時を文字列へ(DateTimeOffset.UtcNow));
        ロック再試行付きで実行(()=>コマンド.ExecuteNonQuery());
    }

    public void 実行設定を保存(long 実行ID,string 実行ルート,IReadOnlyList<int> 要素数一覧,double 適応閾値ナノ秒,IReadOnlyList<int> 論理CPU一覧){
        this.スキーマを確認();
        var 閾値列名=this.アプリ種別==UI実行アプリ種別.ベンチマーク ? "計測時間ns" : "適応閾値ns";
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText=$"UPDATE \"実行\" SET \"実行ルート\"=$root,\"要素数一覧\"=$counts,\"{閾値列名}\"=$threshold,\"論理CPU一覧\"=$cpus,\"更新日時\"=$at WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        コマンド.Parameters.AddWithValue("$root",実行ルート);
        コマンド.Parameters.AddWithValue("$counts",string.Join(",",要素数一覧));
        コマンド.Parameters.AddWithValue("$threshold",適応閾値ナノ秒);
        コマンド.Parameters.AddWithValue("$cpus",string.Join(",",論理CPU一覧));
        コマンド.Parameters.AddWithValue("$at",日時を文字列へ(DateTimeOffset.UtcNow));
        コマンド.ExecuteNonQuery();
    }

    public UI実行設定記録? 実行設定を取得(long 実行ID){
        this.スキーマを確認();
        var 閾値列名=this.アプリ種別==UI実行アプリ種別.ベンチマーク ? "計測時間ns" : "適応閾値ns";
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText=$"SELECT \"実行ID\",\"実行ルート\",\"要素数一覧\",\"{閾値列名}\",\"論理CPU一覧\" FROM \"実行\" WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        using var 読取器=コマンド.ExecuteReader();
        return 読取器.Read() ? new(読取器.GetInt64(0),読取器.GetString(1),読取器.GetString(2),読取器.GetDouble(3),読取器.GetString(4)) : null;
    }

    public long? 前回実行IDを取得(long 実行ID){
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT \"前回実行ID\" FROM \"実行\" WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        return コマンド.ExecuteScalar() is long 値 ? 値 : null;
    }

    public void ベンチマークスナップショット項目を保存(long 実行ID,IReadOnlyList<UIベンチマークスナップショット項目書込> 項目一覧){
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var トランザクション=ロック再試行付きでトランザクションを開始(接続);
        if(this.アプリ種別==UI実行アプリ種別.ベンチマーク){
            BenchmarkTabStorage.保存(接続,トランザクション,実行ID,項目一覧);
            トランザクション.Commit();
            return;
        }
        foreach(var 項目 in 項目一覧){
            using var コマンド=接続.CreateCommand();
            コマンド.Transaction=トランザクション;
            if(this.アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク){
                var 引数=スケーラブル引数を読む(項目.左ケース??項目.右ケース);
                コマンド.CommandText="""
                    INSERT OR REPLACE INTO "ベンチマーク"("実行ID","タブ名","型","ベンチマーク","Count","Initial","Data","Index","Page","Segment","Memory","Fraction","CPU番号","行チェック","今回状態","今回失敗理由","今回日時","今回時間ns","今回メモリバイト")
                    VALUES($id,$tab,$type,$benchmark,$count,$initial,$data,$index,$page,$segment,$memory,$fraction,$cpu,$selected,$leftStatus,$leftFailure,$leftAt,$leftMean,$leftBytes);
                    """;
                スケーラブル引数パラメータを追加(コマンド,引数);
            }else{
                // 左/右は同じメソッド・同じ引数の異なる最適化バリアントを指すため、
                // 行のグループ化ロジック自体が「同じメソッド名+同じ引数キー」でまとめる不変条件により、
                // メソッド名・引数キー・引数JSONは常に同一(いずれか非nullのケースから1回だけ取得すればよい)。
                // 一方、アセンブリ名・型完全名は左右で実体のクラスが異なるため、サイドごとに保存する。
                var 共通識別=項目.左ケース??項目.右ケース
                    ??new(string.Empty,string.Empty,項目.ベンチマーク名,string.Empty,string.Empty);
                コマンド.CommandText="""
                    INSERT OR REPLACE INTO "ベンチマーク"("実行ID","タブ名","型","ベンチマーク","引数キー","引数JSON",
                        "左アセンブリ名","左型完全名","右アセンブリ名","右型完全名","CPU番号","行チェック",
                        "左状態","左失敗理由","左日時","左時間ns","左メモリバイト","左ループ回数","右状態","右失敗理由","右日時","右時間ns","右メモリバイト","右ループ回数")
                    VALUES($id,$tab,$type,$benchmark,$argumentKey,$argumentsJson,
                        $leftAssembly,$leftFullType,$rightAssembly,$rightFullType,$cpu,$selected,
                        $leftStatus,$leftFailure,$leftAt,$leftMean,$leftBytes,$leftLoops,$rightStatus,$rightFailure,$rightAt,$rightMean,$rightBytes,$rightLoops);
                    """;
                コマンド.Parameters.AddWithValue("$argumentKey",共通識別.引数キー);
                コマンド.Parameters.AddWithValue("$argumentsJson",共通識別.引数JSON);
                コマンド.Parameters.AddWithValue("$leftAssembly",項目.左ケース?.アセンブリ名??string.Empty);
                コマンド.Parameters.AddWithValue("$leftFullType",項目.左ケース?.型完全名??string.Empty);
                コマンド.Parameters.AddWithValue("$rightAssembly",項目.右ケース?.アセンブリ名??string.Empty);
                コマンド.Parameters.AddWithValue("$rightFullType",項目.右ケース?.型完全名??string.Empty);
                側パラメータを追加(コマンド,"right",項目.右);
                コマンド.Parameters.AddWithValue("$rightLoops",DB値へ(項目.右.ループ回数));
            }
            コマンド.Parameters.AddWithValue("$id",実行ID);
            コマンド.Parameters.AddWithValue("$tab",項目.タブ名);
            コマンド.Parameters.AddWithValue("$type",項目.型名);
            コマンド.Parameters.AddWithValue("$benchmark",項目.ベンチマーク名);
            コマンド.Parameters.AddWithValue("$cpu",DB値へ(項目.CPU番号));
            コマンド.Parameters.AddWithValue("$selected",項目.選択中か ? 1 : 0);
            側パラメータを追加(コマンド,"left",項目.左);
            コマンド.Parameters.AddWithValue("$leftLoops",DB値へ(項目.左.ループ回数));
            コマンド.ExecuteNonQuery();
        }
        トランザクション.Commit();
    }

    public IReadOnlyList<UIベンチマークスナップショット項目記録> ベンチマークスナップショット項目一覧を取得(long 実行ID){
        this.スキーマを確認();
        if(this.アプリ種別==UI実行アプリ種別.スケーラブルベンチマーク){ return this.スケーラブル項目一覧を取得(実行ID); }
        if(this.アプリ種別!=UI実行アプリ種別.ベンチマーク){ return []; }
        using var 接続=this.接続を開く();
        return BenchmarkTabStorage.読込(接続,実行ID);
    }

    public void パラメータスイープ結果を保存(UIパラメータスイープ結果 結果){
        if(this.アプリ種別!=UI実行アプリ種別.パラメータスイープ){
            throw new InvalidOperationException("パラメータスイープ用ストアが必要です。");
        }
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="""
            INSERT OR REPLACE INTO "ベンチマーク"("実行ID","ケース名","コレクション","ジェネリック","バックエンド","スイープ","Indexサイズ","Pageサイズ","Segmentサイズ","Memoryサイズ","可変Fraction","最大成功Count","エントリCount","失敗種別","失敗理由","追加時間ms","検索時間ns","測定日時","索引バケット数","溢れバケット数","メモリログバイト数","ディスクログバイト数")
            VALUES($id,$case,$collection,$generic,$backend,$sweep,$index,$page,$segment,$memory,$fraction,$max,$entries,$failure,$reason,$add,$lookup,$at,$indexBuckets,$overflowBuckets,$memoryLogBytes,$diskLogBytes);
            """;
        コマンド.Parameters.AddWithValue("$id",結果.実行ID);コマンド.Parameters.AddWithValue("$case",結果.ケース名);
        コマンド.Parameters.AddWithValue("$collection",結果.コレクション);コマンド.Parameters.AddWithValue("$generic",結果.ジェネリック名);
        コマンド.Parameters.AddWithValue("$backend",結果.バックエンド);コマンド.Parameters.AddWithValue("$sweep",結果.スイープ);
        コマンド.Parameters.AddWithValue("$index",結果.Indexサイズ);コマンド.Parameters.AddWithValue("$page",結果.Pageサイズ);
        コマンド.Parameters.AddWithValue("$segment",結果.Segmentサイズ);コマンド.Parameters.AddWithValue("$memory",結果.Memoryサイズ);
        コマンド.Parameters.AddWithValue("$fraction",結果.可変Fraction);コマンド.Parameters.AddWithValue("$max",結果.最大成功Count);
        コマンド.Parameters.AddWithValue("$entries",結果.エントリCount);コマンド.Parameters.AddWithValue("$failure",結果.失敗種別);
        コマンド.Parameters.AddWithValue("$reason",結果.失敗理由);コマンド.Parameters.AddWithValue("$add",結果.追加経過ミリ秒);
        コマンド.Parameters.AddWithValue("$lookup",DB値へ(結果.検索平均ナノ秒));コマンド.Parameters.AddWithValue("$at",日時を文字列へ(結果.計測日時));
        コマンド.Parameters.AddWithValue("$indexBuckets",DB値へ(結果.索引バケット数));コマンド.Parameters.AddWithValue("$overflowBuckets",DB値へ(結果.溢れバケット数));
        コマンド.Parameters.AddWithValue("$memoryLogBytes",DB値へ(結果.メモリログバイト数));コマンド.Parameters.AddWithValue("$diskLogBytes",DB値へ(結果.ディスクログバイト数));
        コマンド.ExecuteNonQuery();
    }

    public IReadOnlyList<UIパラメータスイープ結果> パラメータスイープ結果一覧を取得(long 実行ID){
        if(this.アプリ種別!=UI実行アプリ種別.パラメータスイープ){
            throw new InvalidOperationException("パラメータスイープ用ストアが必要です。");
        }
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT \"実行ID\",\"ケース名\",\"コレクション\",\"ジェネリック\",\"バックエンド\",\"スイープ\",\"Indexサイズ\",\"Pageサイズ\",\"Segmentサイズ\",\"Memoryサイズ\",\"可変Fraction\",\"最大成功Count\",\"エントリCount\",\"失敗種別\",\"失敗理由\",\"追加時間ms\",\"検索時間ns\",\"測定日時\",\"索引バケット数\",\"溢れバケット数\",\"メモリログバイト数\",\"ディスクログバイト数\" FROM \"ベンチマーク\" WHERE \"実行ID\"=$id ORDER BY \"ケース名\";";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        using var 読取器=コマンド.ExecuteReader();
        var 結果一覧=new List<UIパラメータスイープ結果>();
        while(読取器.Read()){
            結果一覧.Add(new(読取器.GetInt64(0),読取器.GetString(1),読取器.GetString(2),読取器.GetString(3),読取器.GetString(4),読取器.GetString(5),読取器.GetInt64(6),読取器.GetInt64(7),読取器.GetInt64(8),読取器.GetInt64(9),読取器.GetDouble(10),読取器.GetInt64(11),読取器.GetInt64(12),読取器.GetString(13),読取器.GetString(14),読取器.GetDouble(15),読取器.IsDBNull(16) ? null : 読取器.GetDouble(16),文字列を日時へ(読取器.GetString(17)),空値許容長整数を読む(読取器,18),空値許容長整数を読む(読取器,19),空値許容長整数を読む(読取器,20),空値許容長整数を読む(読取器,21)));
        }
        return 結果一覧;
    }

    public IReadOnlyList<UI実行記録> 実行一覧を取得(UI実行アプリ種別? アプリ種別 = null){
        if(アプリ種別 is { } 要求アプリ種別&&要求アプリ種別!=this.アプリ種別){return [];}
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT \"実行ID\",\"表示名\",\"状態\",\"実行プロファイル\",\"タブ名\",\"更新日時\" FROM \"実行\" ORDER BY \"更新日時\" DESC,\"実行ID\" DESC;";
        using var 読取器=コマンド.ExecuteReader();
        var 実行一覧=new List<UI実行記録>();
        while(読取器.Read()){
            実行一覧.Add(new(読取器.GetInt64(0),this.アプリ種別,読取器.GetString(1),文字列を状態へ(読取器.GetString(2)),読取器.GetString(3),読取器.GetString(4),文字列を日時へ(読取器.GetString(5))));
        }
        return 実行一覧;
    }

    public IReadOnlyList<UI指標値記録> 指標一覧を取得(long 実行ID){
        var 一覧=new List<UI指標値記録>();var ジョブID=1L;
        foreach(var 項目 in this.ベンチマークスナップショット項目一覧を取得(実行ID)){側指標を追加(一覧,ジョブID++,項目.左);側指標を追加(一覧,ジョブID++,項目.右);}
        return 一覧;
    }

    public void 実行を削除(long 実行ID){
        this.スキーマを確認();using var 接続=this.接続を開く();using var コマンド=接続.CreateCommand();コマンド.CommandText="DELETE FROM \"実行\" WHERE \"実行ID\"=$id;";コマンド.Parameters.AddWithValue("$id",実行ID);コマンド.ExecuteNonQuery();
    }

    public void 全記録を削除(long? 保持実行ID = null){
        this.スキーマを確認();
        using var 接続=this.接続を開く();
        using var トランザクション=ロック再試行付きでトランザクションを開始(接続);
        if(this.アプリ種別==UI実行アプリ種別.ベンチマーク){ BenchmarkTabStorage.全削除(接続,トランザクション,保持実行ID); }
        else {
            using var コマンド=接続.CreateCommand();
            コマンド.Transaction=トランザクション;
            コマンド.CommandText=保持実行ID is null ? "DELETE FROM \"ベンチマーク\";" : "DELETE FROM \"ベンチマーク\" WHERE \"実行ID\"!=$keep;";
            if(保持実行ID is { } 除外対象) { コマンド.Parameters.AddWithValue("$keep",除外対象); }
            コマンド.ExecuteNonQuery();
        }
        using var 実行削除=接続.CreateCommand();
        実行削除.Transaction=トランザクション;
        実行削除.CommandText=保持実行ID is null ? "DELETE FROM \"実行\";" : "DELETE FROM \"実行\" WHERE \"実行ID\"!=$keep;";
        if(保持実行ID is { } 除外実行ID) { 実行削除.Parameters.AddWithValue("$keep",除外実行ID); }
        実行削除.ExecuteNonQuery();
        トランザクション.Commit();
    }

    public IReadOnlyList<IReadOnlyDictionary<string,object?>> 分析ビューを照会(string ビュー名,int 上限 = 1000){
        var 比率ビューか=BenchmarkTabStorage.ビュー名一覧.Contains(ビュー名);
        if(ビュー名 is not ("スケーラブル履歴" or "スケーラブル前回シェア")&&!比率ビューか){return [];}
        this.スキーマを確認();using var 接続=this.接続を開く();using var コマンド=接続.CreateCommand();コマンド.CommandText=比率ビューか ? $"SELECT * FROM \"{ビュー名}\" LIMIT $limit;" : $"SELECT * FROM \"{ビュー名}\" ORDER BY \"実行ID\" DESC LIMIT $limit;";コマンド.Parameters.AddWithValue("$limit",上限);using var 読取器=コマンド.ExecuteReader();var 行一覧=new List<IReadOnlyDictionary<string,object?>>();
        while(読取器.Read()){var 値一覧=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);for(var 位置=0;位置<読取器.FieldCount;位置++){値一覧[読取器.GetName(位置)]=読取器.IsDBNull(位置) ? null : 読取器.GetValue(位置);}行一覧.Add(値一覧);}return 行一覧;
    }


    private IReadOnlyList<UIベンチマークスナップショット項目記録> スケーラブル項目一覧を取得(long 実行ID){
        using var 接続=this.接続を開く();using var コマンド=接続.CreateCommand();
        コマンド.CommandText="""
            SELECT c."実行ID",c."タブ名",c."型",c."ベンチマーク",c."CPU番号",c."行チェック",c."Count",c."Initial",c."Data",c."Index",c."Page",c."Segment",c."Memory",c."Fraction",c."今回状態",c."今回失敗理由",c."今回日時",c."今回時間ns",c."今回メモリバイト",p."今回状態",p."今回失敗理由",p."今回日時",p."今回時間ns",p."今回メモリバイト"
            FROM "ベンチマーク" c INNER JOIN "実行" r ON r."実行ID"=c."実行ID" LEFT JOIN "ベンチマーク" p ON p."実行ID"=r."前回実行ID" AND p."タブ名"=c."タブ名" AND p."型"=c."型" AND p."ベンチマーク"=c."ベンチマーク" AND p."Count" IS c."Count" AND p."Initial" IS c."Initial" AND p."Data" IS c."Data" AND p."Index" IS c."Index" AND p."Page" IS c."Page" AND p."Segment" IS c."Segment" AND p."Memory" IS c."Memory" AND p."Fraction" IS c."Fraction" WHERE c."実行ID"=$id ORDER BY c."タブ名",c."型",c."ベンチマーク",c."Count";
            """;
        コマンド.Parameters.AddWithValue("$id",実行ID);using var 読取器=コマンド.ExecuteReader();var 一覧=new List<UIベンチマークスナップショット項目記録>();while(読取器.Read()){var 引数=new スケーラブル引数(空値許容長整数を読む(読取器,6),空値許容長整数を読む(読取器,7),空値許容長整数を読む(読取器,8),空値許容長整数を読む(読取器,9),空値許容長整数を読む(読取器,10),空値許容長整数を読む(読取器,11),空値許容長整数を読む(読取器,12),読取器.IsDBNull(13) ? null : 読取器.GetDouble(13));var 識別=スケーラブル識別情報を作成(読取器.GetString(3),引数);var 今回=側を読む(読取器,14,null);var 前回=読取器.IsDBNull(19) ? new UIベンチマークグリッド側記録(UI実行状態.待機中,string.Empty,null,null,null) : 側を読む(読取器,19,null);一覧.Add(new(読取器.GetInt64(0),読取器.GetString(1),読取器.GetString(2),読取器.GetString(3),読取器.IsDBNull(4) ? null : 読取器.GetInt32(4),読取器.GetInt32(5)!=0,今回,前回,識別,識別));}return 一覧;
    }

    private        long                  保存済みスキーマ版を読む()                                                                {try{using var 接続=this.作成せず接続を開く();using var コマンド=接続.CreateCommand();コマンド.CommandText="PRAGMA user_version;";return (long)(コマンド.ExecuteScalar()??0L);}catch(SqliteException){return 0;}}
    private        bool                  想定スキーマ形状か(){
        try{
            using var 接続=this.作成せず接続を開く();using var コマンド=接続.CreateCommand();
            if(this.アプリ種別==UI実行アプリ種別.ベンチマーク){
                コマンド.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行';";
                return (long)(コマンド.ExecuteScalar()??0L)==1L&&BenchmarkTabStorage.想定形状か(接続);
            }
            コマンド.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','ベンチマーク');";
            if((long)(コマンド.ExecuteScalar()??0L)!=2L){return false;}
            コマンド.CommandText="SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name IN('要素数一覧','適応閾値ns');";
            if((long)(コマンド.ExecuteScalar()??0L)!=2L){return false;}
            if(this.アプリ種別==UI実行アプリ種別.パラメータスイープ){
                コマンド.CommandText="SELECT COUNT(*) FROM pragma_table_info('ベンチマーク') WHERE name IN('コレクション','ジェネリック','バックエンド','スイープ','Indexサイズ','Pageサイズ','Segmentサイズ','Memoryサイズ','可変Fraction','最大成功Count','エントリCount','索引バケット数','溢れバケット数','メモリログバイト数','ディスクログバイト数');";
                return (long)(コマンド.ExecuteScalar()??0L)==15L;
            }
            コマンド.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('スケーラブル履歴','スケーラブル前回シェア');";
            return (long)(コマンド.ExecuteScalar()??0L)==2L;
        }catch(SqliteException){return false;}
    }
    private        SqliteConnection      接続を開く()                                                                       => this.接続を開く(SqliteOpenMode.ReadWriteCreate);
    private        SqliteConnection      作成せず接続を開く()                                                                   => this.接続を開く(SqliteOpenMode.ReadWrite);
    private        SqliteConnection      接続を開く(SqliteOpenMode                  モード)                                    {var       構築器 =new SqliteConnectionStringBuilder{DataSource=this.DBパス,Mode=モード,DefaultTimeout=30};var 接続=new SqliteConnection(構築器.ToString());接続.Open();using var コマンド=接続.CreateCommand();コマンド.CommandText="PRAGMA busy_timeout=30000;PRAGMA journal_mode=WAL;PRAGMA foreign_keys=ON;";コマンド.ExecuteNonQuery();return 接続;}
    private static void                  非クエリを実行(SqliteConnection              接続,SqliteTransaction トランザクション,string 文) {using var コマンド=接続.CreateCommand();コマンド.Transaction                                                                                                                                                        =トランザクション;コマンド.CommandText=文;コマンド.ExecuteNonQuery();}
    private static SqliteTransaction     ロック再試行付きでトランザクションを開始(SqliteConnection 接続)                                         {const int 試行回数=5;for(var 試行=1;試行<=試行回数;試行++){try{return 接続.BeginTransaction();}catch(SqliteException 例外)when(使用中またはロック中か(例外)&&試行<試行回数){Thread.Sleep(TimeSpan.FromMilliseconds(200*試行));}}return 接続.BeginTransaction();}
    private static void                  ロック再試行付きで実行(Action                    処理)                                         {const int 試行回数=5;for(var 試行=1;試行<=試行回数;試行++){try{処理();return;}catch(SqliteException 例外)when(使用中またはロック中か(例外)                 &&試行<試行回数){Thread.Sleep(TimeSpan.FromMilliseconds(200*試行));}}}
    private static object?               ロック再試行付きでスカラーを実行(SqliteCommand        コマンド)                                       {const int 試行回数=5;for(var 試行=1;試行<=試行回数;試行++){try{return コマンド.ExecuteScalar();}catch(SqliteException 例外)when(使用中またはロック中か(例外) &&試行<試行回数){Thread.Sleep(TimeSpan.FromMilliseconds(200*試行));}}return コマンド.ExecuteScalar();}
    private void                          旧ベンチマークDBをバックアップして削除(){
        this.WALを確定する();
        SqliteConnection.ClearAllPools();
        var 時刻=DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff",System.Globalization.CultureInfo.InvariantCulture);
        var バックアップ=this.DBパス+"."+時刻+".bak";
        var 連番=0;
        while(File.Exists(バックアップ)){ 連番++; バックアップ=this.DBパス+"."+時刻+"_"+連番+".bak"; }
        File.Copy(this.DBパス,バックアップ,false);
        try{
            foreach(var パス in new[]{this.DBパス,this.DBパス+"-wal",this.DBパス+"-shm"}){
                if(File.Exists(パス)){ File.Delete(パス); }
            }
        }catch(Exception 例外){
            try{
                if(!File.Exists(this.DBパス)){ File.Copy(バックアップ,this.DBパス,true); }
            }catch(Exception 復元例外){
                throw new IOException("旧Benchmark DBの削除に失敗し、元DBの復元にも失敗しました: "+this.DBパス, new AggregateException(例外,復元例外));
            }
            throw new IOException("旧Benchmark DBの削除に失敗したため再作成を中止しました: "+this.DBパス,例外);
        }
    }
    private void                          WALを確定する(){
        using var 接続=this.作成せず接続を開く();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="PRAGMA wal_checkpoint(TRUNCATE);";
        コマンド.ExecuteNonQuery();
    }
    private static bool                  使用中またはロック中か(SqliteException           例外)                                         =>例外.SqliteErrorCode is 5 or 6;
    // アセンブリ名・型完全名が両方空ならその側にはケースが無かったとみなし null を返す。
    // メソッド名・引数キー・引数JSONは左/右で共通(行のグループ化ロジックにより保証される)。
    private static UIベンチマークスナップショットケース書込? 側識別を読む(SqliteDataReader 読取器,int 位置,string メソッド名,string 引数キー,string 引数JSON){
        var アセンブリ名=読取器.GetString(位置);
        var 型完全名=読取器.GetString(位置+1);
        return string.IsNullOrWhiteSpace(アセンブリ名)&&string.IsNullOrWhiteSpace(型完全名)
            ? null
            : new(アセンブリ名,型完全名,メソッド名,引数キー,引数JSON);
    }
    private static void                  側パラメータを追加(SqliteCommand               コマンド, string   接頭辞,  UIベンチマークグリッド側書込 側)     {コマンド.Parameters.AddWithValue("$"+接頭辞+"Status",状態を文字列へ(側.状態));コマンド.Parameters.AddWithValue("$"+接頭辞+"Failure",側.失敗理由);コマンド.Parameters.AddWithValue("$"+接頭辞+"At",DB値へ(側.計測日時));コマンド.Parameters.AddWithValue("$"+接頭辞+"Mean",DB値へ(側.平均ナノ秒));コマンド.Parameters.AddWithValue("$"+接頭辞+"Bytes",DB値へ(側.割当バイト));}
    private static UIベンチマークグリッド側記録       側を読む(SqliteDataReader                 読取器,  int      位置,   int?            ループ回数) =>new(文字列を状態へ(読取器.GetString(位置)),読取器.GetString(位置+1),読取器.IsDBNull(位置+2) ? null : 文字列を日時へ(読取器.GetString(位置+2)),読取器.IsDBNull(位置+3) ? null : 読取器.GetDouble(位置+3),読取器.IsDBNull(位置+4) ? null : 読取器.GetInt64(位置+4),ループ回数);
    private static void                  側指標を追加(List<UI指標値記録>                  一覧,   long     ジョブID,UIベンチマークグリッド側記録 側)     {var 日時=側.計測日時??DateTimeOffset.MinValue;if(側.平均ナノ秒 is { } 時間){一覧.Add(new(ジョブID,"平均ナノ秒",時間,"ns",日時));}if(側.割当バイト is { } メモリ){一覧.Add(new(ジョブID,"割当バイト",メモリ,"bytes",日時));}}
    private static スケーラブル引数              スケーラブル引数を読む(UIベンチマークスナップショットケース書込?    識別,   bool     厳密か = false) {var 値一覧=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);if(識別 is not null&&!string.IsNullOrWhiteSpace(識別.引数JSON)){try{using var 文書=System.Text.Json.JsonDocument.Parse(識別.引数JSON);if(文書.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Array){throw new System.Text.Json.JsonException();}foreach(var 項目 in 文書.RootElement.EnumerateArray()){if(項目.TryGetProperty("名前",out var 名前)&&項目.TryGetProperty("値",out var 値)){値一覧[名前.GetString()??string.Empty]=値.GetString()??値.ToString();}}}catch(System.Text.Json.JsonException 例外){if(厳密か){throw new InvalidDataException("スケーラブル引数JSONが不正です。",例外);}}}else if(厳密か){throw new InvalidDataException("スケーラブル引数がありません。");}long? 長整数を読む(string 名前)=>値一覧.TryGetValue(名前,out var 文字列)&&long.TryParse(文字列,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var 値) ? 値 : null;double? 実数を読む(string 名前)=>値一覧.TryGetValue(名前,out var 文字列)&&double.TryParse(文字列,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var 値) ? 値 : null;return new(長整数を読む("Count"),長整数を読む("Initial"),長整数を読む("Data"),長整数を読む("Index"),長整数を読む("Page"),長整数を読む("Segment"),長整数を読む("Memory"),実数を読む("Fraction"));}
    private static void                  スケーラブル引数パラメータを追加(SqliteCommand        コマンド, スケーラブル引数 引数)          {コマンド.Parameters.AddWithValue("$count",DB値へ(引数.Count));コマンド.Parameters.AddWithValue("$initial",DB値へ(引数.Initial));コマンド.Parameters.AddWithValue("$data",DB値へ(引数.Data));コマンド.Parameters.AddWithValue("$index",DB値へ(引数.Index));コマンド.Parameters.AddWithValue("$page",DB値へ(引数.Page));コマンド.Parameters.AddWithValue("$segment",DB値へ(引数.Segment));コマンド.Parameters.AddWithValue("$memory",DB値へ(引数.Memory));コマンド.Parameters.AddWithValue("$fraction",DB値へ(引数.Fraction));}
    private static UIベンチマークスナップショットケース書込 スケーラブル識別情報を作成(string                  メソッド名,スケーラブル引数 引数)          {var 組一覧=new List<Dictionary<string,string>>();void 追加(string 名前,object? 値){if(値 is not null){組一覧.Add(new(){{"名前",名前},{"値",Convert.ToString(値,System.Globalization.CultureInfo.InvariantCulture)??string.Empty}});}}追加("Count",引数.Count);追加("Initial",引数.Initial);追加("Data",引数.Data);追加("Index",引数.Index);追加("Page",引数.Page);追加("Segment",引数.Segment);追加("Memory",引数.Memory);追加("Fraction",引数.Fraction);var キー=string.Join(",",組一覧.Select(static 組=>組["名前"]+"="+組["値"]));return new(string.Empty,string.Empty,メソッド名,キー,System.Text.Json.JsonSerializer.Serialize(組一覧));}
    private sealed record スケーラブル引数(long? Count,long? Initial,long? Data,long? Index,long? Page,long? Segment,long? Memory,double? Fraction);
    private static long? 空値許容長整数を読む(SqliteDataReader 読取器,int 位置)=>読取器.IsDBNull(位置) ? null : 読取器.GetInt64(位置);
    private static object DB値へ(int? 値)=>値 is { } 実値 ? 実値 : DBNull.Value;
    private static object DB値へ(long? 値)=>値 is { } 実値 ? 実値 : DBNull.Value;
    private static object DB値へ(double? 値)=>値 is { } 実値&&double.IsFinite(実値) ? 実値 : DBNull.Value;
    private static object DB値へ(DateTimeOffset? 値)=>値 is { } 実値 ? 日時を文字列へ(実値) : DBNull.Value;
    private static string 日時を文字列へ(DateTimeOffset 値)=>値.ToString("O",System.Globalization.CultureInfo.InvariantCulture);
    private static DateTimeOffset 文字列を日時へ(string 値)=>DateTimeOffset.Parse(値,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
    private static string 状態を文字列へ(UI実行状態 値)=>値 switch{UI実行状態.待機中=>"Queued",UI実行状態.実行中=>"Running",UI実行状態.一時停止中=>"Paused",UI実行状態.停止済み=>"Stopped",UI実行状態.完了=>"Completed",UI実行状態.失敗=>"Failed",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static UI実行状態 文字列を状態へ(string 値)=>値 switch{"Queued"=>UI実行状態.待機中,"Running"=>UI実行状態.実行中,"Paused"=>UI実行状態.一時停止中,"Stopped"=>UI実行状態.停止済み,"Completed"=>UI実行状態.完了,"Failed"=>UI実行状態.失敗,_=>throw new InvalidDataException("不明なUI実行状態です: "+値)};
}
