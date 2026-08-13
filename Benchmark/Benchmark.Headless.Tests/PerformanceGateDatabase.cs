using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

namespace Test.Benchmark;

public sealed class PerformanceGateDatabase{
    private static readonly string[]全タブ表名一覧=[
        "書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List",
        "Persisted(Seamless)","Persisted(System)","Tsavorite(Seamless)","Tsavorite(System)",
    ];
    private static readonly string[]全タブID一覧=[
        global::UI.ベンチマークソースタブ一覧.最適化書き換えID,
        global::UI.ベンチマークソースタブ一覧.最適化パイプライン融合ID,
        global::UI.ベンチマークソースタブ一覧.最適化述語押し下げID,
        global::UI.ベンチマークソースタブ一覧.System配列ID,
        global::UI.ベンチマークソースタブ一覧.SystemIEnumerableID,
        global::UI.ベンチマークソースタブ一覧.System範囲ID,
        global::UI.ベンチマークソースタブ一覧.SystemリストID,
        global::UI.ベンチマークソースタブ一覧.Seamless永続化ID,
        global::UI.ベンチマークソースタブ一覧.System永続化ID,
        global::UI.ベンチマークソースタブ一覧.SeamlessTsavoriteID,
        global::UI.ベンチマークソースタブ一覧.SystemTsavoriteID,
    ];
    private static readonly IReadOnlyDictionary<string,int>Run249表別期待行数一覧=new Dictionary<string,int>(StringComparer.Ordinal){
        [global::UI.ベンチマークソースタブ一覧.最適化書き換えID]=11,
        [global::UI.ベンチマークソースタブ一覧.最適化パイプライン融合ID]=11,
        [global::UI.ベンチマークソースタブ一覧.最適化述語押し下げID]=143,
        [global::UI.ベンチマークソースタブ一覧.System配列ID]=194,
        [global::UI.ベンチマークソースタブ一覧.SystemIEnumerableID]=194,
        [global::UI.ベンチマークソースタブ一覧.System範囲ID]=81,
        [global::UI.ベンチマークソースタブ一覧.SystemリストID]=194,
        [global::UI.ベンチマークソースタブ一覧.Seamless永続化ID]=130,
        [global::UI.ベンチマークソースタブ一覧.System永続化ID]=130,
        [global::UI.ベンチマークソースタブ一覧.SeamlessTsavoriteID]=130,
        [global::UI.ベンチマークソースタブ一覧.SystemTsavoriteID]=130,
    };

    [Fact,Trait("Category","Integration")]
    public void 合格結果を保存して比較元を退役(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1348);
            var 最終結果一覧=最終結果一覧を作成(マニフェスト,2);

            global::Benchmark.Headless.PerformanceGateDatabase.比較元を検証(DBパス,マニフェスト);
            var 停止確認回数=0;
            var 保存結果=global::Benchmark.Headless.PerformanceGateDatabase.合格結果を保存して比較元を退役(
                DBパス,マニフェスト,最終結果一覧,2,Path.Combine(一時ルート,"attempt-5"),249,()=>停止確認回数++);

            Assert.True(停止確認回数>=5);
            Assert.Equal(250,保存結果.新実行ID);
            Assert.True(File.Exists(保存結果.バックアップパス));
            Assert.Equal(0,実行行数(DBパス,249));
            Assert.Equal(1,実行行数(DBパス,保存結果.新実行ID));
            Assert.Equal(0,タブ行数合計(DBパス,249));
            Assert.Equal(11,タブ行数合計(DBパス,保存結果.新実行ID));
            Assert.Equal(6,新実行を持つタブ数(DBパス,保存結果.新実行ID));
            foreach(var 表名 in 全タブ表名一覧){
                var 期待最新=新実行を持つ表か(表名) ? 保存結果.新実行ID : 248L;
                Assert.Equal(期待最新,表の最大実行ID(DBパス,表名));
            }
            Assert.Equal(250,シーケンスを読む(DBパス));
            親設定を確認(DBパス,保存結果.新実行ID,Path.Combine(一時ルート,"attempt-5"),2);
            整合性を確認(DBパス);

            Assert.Equal(1,実行行数(保存結果.バックアップパス,249));
            Assert.Equal(0,実行行数(保存結果.バックアップパス,保存結果.新実行ID));
            Assert.Equal(1348,タブ行数合計(保存結果.バックアップパス,249));
            Assert.Equal(249,シーケンスを読む(保存結果.バックアップパス));
            整合性を確認(保存結果.バックアップパス);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void 退役前行数が不正なら明示バックアップから復元(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1348);
            var 更新前指紋=論理指紋を作成(DBパス);
            var 停止確認回数=0;
            void 停止確認(){
                停止確認回数++;
                if(停止確認回数==4){
                    throw new InvalidOperationException("バックアップ後の故障注入");
                }
            }

            var 例外=Assert.Throws<IOException>(()=>
                global::Benchmark.Headless.PerformanceGateDatabase.合格結果を保存して比較元を退役(
                    DBパス,マニフェスト,最終結果一覧を作成(マニフェスト,2),2,Path.Combine(一時ルート,"attempt-5"),249,停止確認));

            Assert.Contains("復元",例外.Message,StringComparison.Ordinal);
            Assert.Equal(4,停止確認回数);
            Assert.Equal(更新前指紋,論理指紋を作成(DBパス));
            Assert.Equal(1,実行行数(DBパス,249));
            Assert.Equal(0,実行行数(DBパス,250));
            Assert.Equal(1348,タブ行数合計(DBパス,249));
            Assert.Equal(249,シーケンスを読む(DBパス));
            整合性を確認(DBパス);
            var バックアップ一覧=Directory.GetFiles(一時ルート,"Benchmark.db.pre-performance-gate-*.bak");
            Assert.Single(バックアップ一覧);
            Assert.Equal(更新前指紋,論理指紋を作成(バックアップ一覧[0]));
            整合性を確認(バックアップ一覧[0]);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void 比較元不一致ならDBを変更しない(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1347);
            var 更新前指紋=論理指紋を作成(DBパス);

            Assert.Throws<InvalidDataException>(()=>
                global::Benchmark.Headless.PerformanceGateDatabase.合格結果を保存して比較元を退役(
                    DBパス,マニフェスト,最終結果一覧を作成(マニフェスト,2),2,Path.Combine(一時ルート,"attempt-5"),249,static()=>{}));

            Assert.Equal(更新前指紋,論理指紋を作成(DBパス));
            Assert.Empty(Directory.GetFiles(一時ルート,"Benchmark.db.pre-performance-gate-*.bak"));
            整合性を確認(DBパス);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void 比較元対象は厳密に1件で割当量が一致する(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1348);
            var 対象=マニフェスト.対象一覧[0];
            var 表名=対象.タブID switch{
                global::UI.ベンチマークソースタブ一覧.System配列ID=>"Array",
                _=>throw new InvalidDataException("先頭fixtureはArrayである必要があります。"),
            };
            using(var 接続=読み書き接続を開く(DBパス)){
                using var コマンド=接続.CreateCommand();
                コマンド.CommandText=$"UPDATE {引用(表名)} SET \"ZLinq_型完全名\"='Mismatch' WHERE \"実行ID\"=249 AND \"型\"=$type AND \"ベンチマーク\"=$method AND \"引数キー\"=$key;";
                コマンド.Parameters.AddWithValue("$type",対象.表示型);
                コマンド.Parameters.AddWithValue("$method",対象.メソッド名);
                コマンド.Parameters.AddWithValue("$key",対象.引数キー);
                Assert.Equal(1,コマンド.ExecuteNonQuery());
            }

            var 例外=Assert.Throws<InvalidDataException>(()=>
                global::Benchmark.Headless.PerformanceGateDatabase.比較元を検証(DBパス,マニフェスト));

            Assert.Contains("厳密に1件",例外.Message,StringComparison.Ordinal);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void 比較元割当量がマニフェストと一致しなければ拒否する(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1348);
            var 誤対象一覧=マニフェスト.対象一覧.ToArray();
            誤対象一覧[0]=誤対象一覧[0] with {Run249FLinq割当上限=誤対象一覧[0].Run249FLinq割当上限+1};
            var 誤マニフェスト=マニフェスト with {対象一覧=誤対象一覧};

            var 例外=Assert.Throws<InvalidDataException>(()=>
                global::Benchmark.Headless.PerformanceGateDatabase.比較元を検証(DBパス,誤マニフェスト));

            Assert.Contains("割当上限",例外.Message,StringComparison.Ordinal);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 禁止対象プロセスか(){
        Assert.True(global::Benchmark.Headless.PerformanceGateDatabase.禁止対象プロセスか("Benchmark.UI",@"G:\Work\Benchmark.UI.exe",null));
        Assert.True(global::Benchmark.Headless.PerformanceGateDatabase.禁止対象プロセスか("dotnet",@"G:\Work\Worker.exe",null));
        Assert.True(global::Benchmark.Headless.PerformanceGateDatabase.禁止対象プロセスか("dotnet",@"C:\Program Files\dotnet\dotnet.exe","dotnet \"G:\\Work\\Benchmark.Headless.dll\" --gate"));
        Assert.True(global::Benchmark.Headless.PerformanceGateDatabase.禁止対象プロセスか("dotnet",@"C:\Program Files\dotnet\dotnet.exe","dotnet \"G:\\Work\\Worker.dll\" --request x"));
        Assert.False(global::Benchmark.Headless.PerformanceGateDatabase.禁止対象プロセスか("dotnet",@"C:\Program Files\dotnet\dotnet.exe",@"dotnet build G:\GitHub\Seamless\Benchmark\Benchmark.Headless\Benchmark.Headless.csproj"));
    }

    [Fact,Trait("Category","Integration")]
    public void バックアップ後の競合書込を復元で消さない(){
        var 一時ルート=一時ルートを作成();
        try{
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            var マニフェスト=マニフェストを読む();
            比較元DBを作成(DBパス,マニフェスト,1348);
            var 停止確認回数=0;
            void 停止確認(){
                停止確認回数++;
                if(停止確認回数!=3){return;}
                using var 接続=読み書き接続を開く(DBパス);
                using var コマンド=接続.CreateCommand();
                コマンド.CommandText="""
                    INSERT INTO "実行"("表示名","状態","実行プロファイル","タブ名","更新日時")
                    VALUES('Concurrent','Completed','Accurate','Concurrent','2026-08-12T01:00:00.0000000+00:00');
                    """;
                Assert.Equal(1,コマンド.ExecuteNonQuery());
            }

            var 例外=Assert.Throws<InvalidDataException>(()=>
                global::Benchmark.Headless.PerformanceGateDatabase.合格結果を保存して比較元を退役(
                    DBパス,マニフェスト,最終結果一覧を作成(マニフェスト,2),2,Path.Combine(一時ルート,"attempt-5"),249,停止確認));

            Assert.Contains("バックアップ前データ版",例外.Message,StringComparison.Ordinal);
            Assert.Equal(3,停止確認回数);
            Assert.Equal(1,実行行数(DBパス,249));
            using(var 接続=読取専用接続を開く(DBパス)){
                using var コマンド=接続.CreateCommand();
                コマンド.CommandText="SELECT COUNT(*) FROM \"実行\" WHERE \"表示名\"='Concurrent';";
                Assert.Equal(1,Convert.ToInt64(コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.Single(Directory.GetFiles(一時ルート,"Benchmark.db.pre-performance-gate-*.bak"));
            整合性を確認(DBパス);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","LocalIntegration")]
    public void 実BenchmarkDBのSQLiteコピーだけを更新(){
        if(!string.Equals(
            Environment.GetEnvironmentVariable("SEAMLESS_RUN_LOCAL_DB_COPY_TEST"),
            "1",
            StringComparison.Ordinal)){
            return;
        }
        var リポジトリルート=リポジトリルートを取得();
        var 元DBパス=Path.Combine(リポジトリルート,"Work","UI","Benchmark.db");
        if(!File.Exists(元DBパス)){
            return;
        }
        var 一時ルート=一時ルートを作成();
        try{
            var 元コピールート=Path.Combine(一時ルート,"source-copy");
            Directory.CreateDirectory(元コピールート);
            var 元コピーパス=Path.Combine(元コピールート,"Benchmark.db");
            rawコピー(元DBパス,元コピーパス);
            foreach(var 接尾辞 in new[]{"-wal","-shm"}){
                var 元付属パス=元DBパス+接尾辞;
                if(File.Exists(元付属パス)){
                    rawコピー(元付属パス,元コピーパス+接尾辞);
                }
            }
            if(実行行数(元コピーパス,249)!=1){
                return;
            }
            var DBパス=Path.Combine(一時ルート,"Benchmark.db");
            SQLiteバックアップでコピー(元コピーパス,DBパス);
            var マニフェスト=マニフェストを読む();

            global::Benchmark.Headless.PerformanceGateDatabase.比較元を検証(DBパス,マニフェスト);
            var 保存結果=global::Benchmark.Headless.PerformanceGateDatabase.合格結果を保存して比較元を退役(
                DBパス,マニフェスト,最終結果一覧を作成(マニフェスト,2),2,Path.Combine(一時ルート,"attempt-5"),249,static()=>{});

            Assert.Equal(0,実行行数(DBパス,249));
            Assert.Equal(11,タブ行数合計(DBパス,保存結果.新実行ID));
            整合性を確認(DBパス);
        }finally{
            一時ルートを削除(一時ルート);
        }
    }

    private static void 比較元DBを作成(
        string DBパス,
        global::UI.ベンチマーク性能ゲートマニフェスト マニフェスト,
        int 比較元行数){
        var ストア=new global::UI.Storage.UI実行ストア(DBパス,global::UI.Storage.UI実行アプリ種別.ベンチマーク);
        ストア.スキーマを確認();
        using(var 接続=読み書き接続を開く(DBパス)){
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="""
                INSERT INTO "実行"("実行ID","表示名","状態","実行プロファイル","タブ名","更新日時","実行ルート","要素数一覧","計測時間ns","論理CPU一覧")
                VALUES(248,'Run 248','Completed','Accurate','All','2026-08-11T00:00:00.0000000+00:00','fixture-previous','4096',1024000000,'2');
                INSERT INTO "実行"("実行ID","表示名","状態","実行プロファイル","タブ名","更新日時","実行ルート","要素数一覧","計測時間ns","論理CPU一覧")
                VALUES(249,'Run 249','Completed','Accurate','All','2026-08-12T00:00:00.0000000+00:00','fixture','4096',1024000000,'2');
                """;
            コマンド.ExecuteNonQuery();
        }

        var 以前項目一覧=new List<global::UI.Storage.UIベンチマークスナップショット項目書込>(全タブID一覧.Length);
        foreach(var タブID in 全タブID一覧){
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(タブID)!;
            以前項目一覧.Add(項目を作成(
                タブID,"PreviousType","PreviousMethod",string.Empty,
                タブ.左アセンブリ名,"Previous.Left."+タブID,3,
                タブ.右アセンブリ名,"Previous.Right."+タブID,4));
        }
        ストア.ベンチマークスナップショット項目を保存(248,以前項目一覧);

        var 項目一覧=new List<global::UI.Storage.UIベンチマークスナップショット項目書込>(比較元行数);
        foreach(var 対象 in マニフェスト.対象一覧){
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(対象.タブID)!;
            項目一覧.Add(項目を作成(
                対象.タブID,対象.表示型,対象.メソッド名,対象.引数キー,
                タブ.左アセンブリ名,対象.左型完全名,対象.Run249FLinq割当上限,
                タブ.右アセンブリ名,対象.右型完全名,対象.Run249FLinq割当上限+32));
        }
        foreach(var タブ期待 in Run249表別期待行数一覧){
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(タブ期待.Key)!;
            var 現在件数=項目一覧.Count(項目=>string.Equals(項目.タブ名,タブ期待.Key,StringComparison.Ordinal));
            var 期待件数=タブ期待.Value;
            if(比較元行数<Run249表別期待行数一覧.Values.Sum()
                &&string.Equals(タブ期待.Key,global::UI.ベンチマークソースタブ一覧.System配列ID,StringComparison.Ordinal)){
                期待件数--;
            }
            for(var 番号=現在件数;番号<期待件数;番号++){
                項目一覧.Add(項目を作成(
                    タブ.ID,"FixtureType"+番号,"FixtureMethod",string.Empty,
                    タブ.左アセンブリ名,"Fixture.Left."+タブ.ID+"."+番号,1,
                    タブ.右アセンブリ名,"Fixture.Right."+タブ.ID+"."+番号,2));
            }
        }
        ストア.ベンチマークスナップショット項目を保存(249,項目一覧);
        SqliteConnection.ClearAllPools();
        Assert.Equal(比較元行数,タブ行数合計(DBパス,249));
        Assert.Equal(249,シーケンスを読む(DBパス));
    }

    private static global::UI.Storage.UIベンチマークスナップショット項目書込 項目を作成(
        string タブID,string 型,string メソッド,string 引数キー,
        string 左アセンブリ,string 左型,long 左割当,
        string 右アセンブリ,string 右型,long 右割当){
        var 時刻=DateTimeOffset.Parse("2026-08-12T00:00:00+00:00",global::System.Globalization.CultureInfo.InvariantCulture);
        global::UI.Storage.UIベンチマークグリッド側書込 側(double 時間,long 割当)=>new(
            global::UI.Storage.UI実行状態.完了,string.Empty,時刻,時間,割当,8,時間*8,true);
        global::UI.Storage.UIベンチマークスナップショットケース書込 ケース(string アセンブリ,string 型完全名)=>new(
            アセンブリ,型完全名,メソッド,引数キー,"[]");
        return new(タブID,型,メソッド,2,true,側(8d,左割当),側(10d,右割当),ケース(左アセンブリ,左型),ケース(右アセンブリ,右型));
    }

    private static IReadOnlyList<global::Benchmark.Headless.PerformanceGatePairResult>最終結果一覧を作成(
        global::UI.ベンチマーク性能ゲートマニフェスト マニフェスト,
        int 論理CPU){
        return マニフェスト.対象一覧.Select(対象=>{
            var タブ=global::UI.ベンチマークソースタブ一覧.IDから取得を試みる(対象.タブID)!;
            var 左ケース=new global::UI.ベンチマークケース(
                タブ.左プロジェクト,タブ.左アセンブリ名,対象.左型完全名,対象.メソッド名,対象.引数キー,[]);
            var 右ケース=new global::UI.ベンチマークケース(
                タブ.右プロジェクト,タブ.右アセンブリ名,対象.右型完全名,対象.メソッド名,対象.引数キー,[]);
            var 左割当=対象.Run249FLinq割当上限;
            var 右割当=左割当+32;
            var 測定=new global::UI.ベンチマーク性能ゲート測定(
                対象.ID,5,8d,左割当,"digest",論理CPU,8,true,10d,右割当,"digest",論理CPU,8,true);
            var 解決対象=new global::UI.ベンチマーク性能ゲート解決対象(対象,タブ,左ケース,右ケース);
            return new global::Benchmark.Headless.PerformanceGatePairResult(
                解決対象,測定,
                結果を作成(左ケース,タブ.タブ,論理CPU,8d,左割当),
                結果を作成(右ケース,タブ.タブ,論理CPU,10d,右割当));
        }).ToArray();
    }

    private static global::UI.ベンチマーク結果項目 結果を作成(
        global::UI.ベンチマークケース ケース,
        global::UI.ベンチマークソースタブ ソースタブ,
        int 論理CPU,
        double 平均ナノ秒,
        long 割当バイト){
        return new(new global::UI.ベンチマークジョブ{
            プロジェクト=ケース.プロジェクト,
            アセンブリ名=ケース.アセンブリ名,
            型完全名=ケース.型完全名,
            メソッド名=ケース.メソッド名,
            引数キー=ケース.引数キー,
            引数一覧=ケース.引数一覧.ToList(),
            ソースタブ=ソースタブ,
            要素数=4096,
            ループ回数=8,
            状態=global::UI.ジョブ状態.完了,
            割り当て論理CPU=論理CPU,
            平均ナノ秒=平均ナノ秒*8,
            割り当てバイト数=割当バイト*8,
            結果ダイジェスト="digest",
            終了コード=0,
        });
    }

    private static global::UI.ベンチマーク性能ゲートマニフェスト マニフェストを読む(){
        var パス=Path.Combine(リポジトリルートを取得(),"Benchmark","Benchmark.Headless","Manifests","run249-performance-gate.json");
        return JsonSerializer.Deserialize<global::UI.ベンチマーク性能ゲートマニフェスト>(File.ReadAllText(パス))
            ??throw new InvalidDataException("性能ゲートマニフェストを読み込めませんでした。");
    }

    private static string リポジトリルートを取得(){
        foreach(var 開始 in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory}){
            for(var 候補=new DirectoryInfo(開始);候補 is not null;候補=候補.Parent){
                if(File.Exists(Path.Combine(候補.FullName,"Seamless.slnx"))){
                    return 候補.FullName;
                }
            }
        }
        throw new DirectoryNotFoundException("Seamless.slnxを持つリポジトリルートを解決できませんでした。");
    }

    private static string 一時ルートを作成(){
        var 基準=Path.Combine(Path.GetTempPath(),"Seamless.PerformanceGateDatabase.Tests");
        var ルート=Path.Combine(基準,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    private static void 一時ルートを削除(string 一時ルート){
        SqliteConnection.ClearAllPools();
        var 基準=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"Seamless.PerformanceGateDatabase.Tests"))
            .TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var 対象=Path.GetFullPath(一時ルート);
        if(!対象.StartsWith(基準,StringComparison.OrdinalIgnoreCase)){
            throw new InvalidOperationException("性能ゲートDB試験の一時ルート外は削除できません。対象="+対象);
        }
        if(Directory.Exists(対象)){
            Directory.Delete(対象,true);
        }
    }

    private static SqliteConnection 読取専用接続を開く(string DBパス){
        var 接続=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=DBパス,
            Mode=SqliteOpenMode.ReadOnly,
            Pooling=false,
        }.ToString());
        接続.Open();
        return 接続;
    }

    private static SqliteConnection 読み書き接続を開く(string DBパス){
        var 接続=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=DBパス,
            Mode=SqliteOpenMode.ReadWrite,
            Pooling=false,
        }.ToString());
        接続.Open();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="PRAGMA foreign_keys=ON;";
        コマンド.ExecuteNonQuery();
        return 接続;
    }

    private static long 実行行数(string DBパス,long 実行ID){
        using var 接続=読取専用接続を開く(DBパス);
        return 実行行数(接続,"実行",実行ID);
    }

    private static long タブ行数合計(string DBパス,long 実行ID){
        using var 接続=読取専用接続を開く(DBパス);
        return 全タブ表名一覧.Sum(表名=>実行行数(接続,表名,実行ID));
    }

    private static int 新実行を持つタブ数(string DBパス,long 実行ID){
        using var 接続=読取専用接続を開く(DBパス);
        return 全タブ表名一覧.Count(表名=>実行行数(接続,表名,実行ID)>0);
    }

    private static bool 新実行を持つ表か(string 表名)=>表名 is
        "Array" or "IEnumerable" or "Range" or "List" or "Persisted(Seamless)" or "Persisted(System)";

    private static long?表の最大実行ID(string DBパス,string 表名){
        using var 接続=読取専用接続を開く(DBパス);
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText=$"SELECT MAX([実行ID]) FROM {引用(表名)};";
        var 結果=コマンド.ExecuteScalar();
        return 結果 is null or DBNull
            ?null
            :Convert.ToInt64(結果,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long 実行行数(SqliteConnection 接続,string 表名,long 実行ID){
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText=$"SELECT COUNT(*) FROM {引用(表名)} WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        return Convert.ToInt64(コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long シーケンスを読む(string DBパス){
        using var 接続=読取専用接続を開く(DBパス);
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT seq FROM sqlite_sequence WHERE name='実行';";
        return Convert.ToInt64(コマンド.ExecuteScalar()??0L,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void 親設定を確認(string DBパス,long 実行ID,string 実行ルート,int 論理CPU){
        using var 接続=読取専用接続を開く(DBパス);
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT \"状態\",\"実行プロファイル\",\"タブ名\",\"実行ルート\",\"要素数一覧\",\"計測時間ns\",\"論理CPU一覧\" FROM \"実行\" WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        using var 読取器=コマンド.ExecuteReader();
        Assert.True(読取器.Read());
        Assert.Equal("Completed",読取器.GetString(0));
        Assert.Equal(global::UI.ベンチマーク実行プロファイル.正確.ToString(),読取器.GetString(1));
        Assert.Equal("PerformanceGate.Run249",読取器.GetString(2));
        Assert.Equal(実行ルート,読取器.GetString(3));
        Assert.Equal("4096",読取器.GetString(4));
        Assert.Equal(1_024_000_000d,読取器.GetDouble(5));
        Assert.Equal(論理CPU.ToString(global::System.Globalization.CultureInfo.InvariantCulture),読取器.GetString(6));
        Assert.False(読取器.Read());
    }

    private static void 整合性を確認(string DBパス){
        using var 接続=読取専用接続を開く(DBパス);
        using(var 外部キー=接続.CreateCommand()){
            外部キー.CommandText="PRAGMA foreign_key_check;";
            using var 読取器=外部キー.ExecuteReader();
            Assert.False(読取器.Read());
        }
        using(var 完全性=接続.CreateCommand()){
            完全性.CommandText="PRAGMA integrity_check;";
            Assert.Equal("ok",Convert.ToString(完全性.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static string 論理指紋を作成(string DBパス){
        using var 接続=読取専用接続を開く(DBパス);
        var 部分一覧=new List<string>{
            "version="+スカラー長整数(接続,"PRAGMA user_version;"),
            "sequence="+スカラー長整数(接続,"SELECT seq FROM sqlite_sequence WHERE name='実行';"),
            "runs="+スカラー長整数(接続,"SELECT COUNT(*) FROM \"実行\";"),
            "run249="+実行行数(接続,"実行",249),
            "run250="+実行行数(接続,"実行",250),
        };
        foreach(var 表名 in 全タブ表名一覧){
            部分一覧.Add(表名+"="+実行行数(接続,表名,249));
        }
        return string.Join("|",部分一覧);
    }

    private static long スカラー長整数(SqliteConnection 接続,string SQL){
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText=SQL;
        return Convert.ToInt64(コマンド.ExecuteScalar()??0L,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SQLiteバックアップでコピー(string 元DBパス,string 先DBパス){
        using var 元=読取専用接続を開く(元DBパス);
        using var 先=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=先DBパス,
            Mode=SqliteOpenMode.ReadWriteCreate,
            Pooling=false,
        }.ToString());
        先.Open();
        元.BackupDatabase(先);
    }

    private static string ファイルハッシュを作成(string パス){
        using var ストリーム=File.OpenRead(パス);
        return Convert.ToHexString(SHA256.HashData(ストリーム));
    }

    private static void rawコピー(string 元パス,string 先パス){
        using var 元=new FileStream(
            元パス,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var 先=new FileStream(先パス,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        元.CopyTo(先);
    }

    private static string 引用(string 識別子)=>"\""+識別子.Replace("\"","\"\"")+"\"";
}
