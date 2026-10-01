using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using UI;
using UI.Storage;

namespace Benchmark.Headless;

internal sealed record PerformanceGateSaveResult(long 新実行ID,string バックアップパス);

internal static class PerformanceGateDatabase{
    private const int 必須スキーマ版=27;
    private static readonly IReadOnlyDictionary<string,string>表名一覧=new Dictionary<string,string>(StringComparer.Ordinal){
        [ベンチマークソースタブ一覧.System配列ID]="Array",
        [ベンチマークソースタブ一覧.SystemIEnumerableID]="IEnumerable",
        [ベンチマークソースタブ一覧.System範囲ID]="Range",
        [ベンチマークソースタブ一覧.SystemリストID]="List",
        [ベンチマークソースタブ一覧.Seamless永続化ID]="Persisted(Seamless)",
        [ベンチマークソースタブ一覧.System永続化ID]="Persisted(System)",
    };
    private static readonly string[]全タブ表名一覧=[
        "書き換え","パイプライン融合","述語プッシュダウン","射影融合","Array","IEnumerable","Range","List",
        "Persisted(Seamless)","Persisted(System)","Tsavorite(Seamless)","Tsavorite(System)",
    ];
    private static readonly IReadOnlyDictionary<string,long>Run249期待行数一覧=new Dictionary<string,long>(StringComparer.Ordinal){
        ["書き換え"]=11,
        ["パイプライン融合"]=11,
        ["述語プッシュダウン"]=143,
        // 射影融合の表は版27で追加したので Run 249 には行が無い。
        ["射影融合"]=0,
        ["Array"]=194,
        ["IEnumerable"]=194,
        ["Range"]=81,
        ["List"]=194,
        ["Persisted(Seamless)"]=130,
        ["Persisted(System)"]=130,
        ["Tsavorite(Seamless)"]=130,
        ["Tsavorite(System)"]=130,
    };
    private static readonly IReadOnlyDictionary<string,string[]>全タブ列見出し一覧=new Dictionary<string,string[]>(StringComparer.Ordinal){
        ["書き換え"]=["全最適化","書き換え無効","理想"],
        ["パイプライン融合"]=["全最適化","パイプライン融合無効","理想"],
        ["述語プッシュダウン"]=["全最適化","述語プッシュダウン無効","理想"],
        ["射影融合"]=["全最適化","射影融合無効","理想"],
        ["Array"]=["FLinq","ZLinq","理想"],
        ["IEnumerable"]=["FLinq","ZLinq","理想"],
        ["Range"]=["FLinq","ZLinq","理想"],
        ["List"]=["FLinq","ZLinq","理想"],
        ["Persisted(Seamless)"]=["FLinq","Persisted(Seamless)"],
        ["Persisted(System)"]=["FLinq","Persisted(System)"],
        ["Tsavorite(Seamless)"]=["FLinq","Tsavorite(Seamless)"],
        ["Tsavorite(System)"]=["FLinq","Tsavorite(System)"],
    };

    public static void 比較元を検証(string DBパス,ベンチマーク性能ゲートマニフェスト マニフェスト){
        ArgumentException.ThrowIfNullOrWhiteSpace(DBパス);
        ArgumentNullException.ThrowIfNull(マニフェスト);
        if(!File.Exists(DBパス)){
            throw new FileNotFoundException("Benchmark.dbがありません。",DBパス);
        }
        using var 接続=読取専用接続を開く(DBパス);
        比較元を検証(接続,null,マニフェスト,true);
    }

    private static void 比較元を検証(
        SqliteConnection 接続,
        SqliteTransaction? トランザクション,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        bool 形状も検証するか){
        if(スカラー長整数(接続,"PRAGMA user_version;",トランザクション)!=必須スキーマ版){
            throw new InvalidDataException("Benchmark.dbのスキーマ版が27ではありません。");
        }
        if(形状も検証するか){
            DB形状を検証(接続);
        }
        using(var 実行コマンド=接続.CreateCommand()){
            実行コマンド.Transaction=トランザクション;
            実行コマンド.CommandText="SELECT COUNT(*) FROM \"実行\" WHERE \"実行ID\"=$id;";
            実行コマンド.Parameters.AddWithValue("$id",マニフェスト.比較元実行ID);
            if(Convert.ToInt64(実行コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture)!=1){
                throw new InvalidDataException("比較元Runが存在しません。実行ID="+マニフェスト.比較元実行ID);
            }
        }
        Run249行数を検証(接続,トランザクション,マニフェスト.比較元実行ID);
        foreach(var 対象 in マニフェスト.対象一覧){
            var 表名=表名一覧.TryGetValue(対象.タブID,out var 値)
                ?値
                :throw new InvalidDataException("性能ゲートDB検証に未対応のタブです。タブ="+対象.タブID);
            var 右見出し=全タブ列見出し一覧[表名][1];
            using var コマンド=接続.CreateCommand();
            コマンド.Transaction=トランザクション;
            コマンド.CommandText=$"SELECT COUNT(*),MIN(\"FLinq_メモリバイト\"),MAX(\"FLinq_メモリバイト\") FROM {引用(表名)} WHERE \"実行ID\"=$id AND \"型\"=$type AND \"ベンチマーク\"=$method AND \"引数キー\"=$key AND \"FLinq_型完全名\"=$leftType AND {引用(右見出し+"_型完全名")}=$rightType;";
            コマンド.Parameters.AddWithValue("$id",マニフェスト.比較元実行ID);
            コマンド.Parameters.AddWithValue("$type",対象.表示型);
            コマンド.Parameters.AddWithValue("$method",対象.メソッド名);
            コマンド.Parameters.AddWithValue("$key",対象.引数キー);
            コマンド.Parameters.AddWithValue("$leftType",対象.左型完全名);
            コマンド.Parameters.AddWithValue("$rightType",対象.右型完全名);
            using var 読取器=コマンド.ExecuteReader();
            if(!読取器.Read()||読取器.GetInt64(0)!=1){
                throw new InvalidDataException("比較元Runの対象行が厳密に1件ではありません。ID="+対象.ID);
            }
            if(読取器.IsDBNull(1)||読取器.IsDBNull(2)){
                throw new InvalidDataException("比較元RunのFLinq割当量がありません。ID="+対象.ID);
            }
            var 最小割当量=読取器.GetInt64(1);
            var 最大割当量=読取器.GetInt64(2);
            if(最小割当量!=最大割当量||最小割当量!=対象.Run249FLinq割当上限){
                throw new InvalidDataException("マニフェストのRun 249割当上限がDBと一致しません。ID="+対象.ID+" manifest="+対象.Run249FLinq割当上限+" db="+最小割当量);
            }
        }
    }

    public static PerformanceGateSaveResult 合格結果を保存して比較元を退役(
        string DBパス,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        IReadOnlyList<PerformanceGatePairResult>最終結果一覧,
        int 論理CPU,
        string 実行ルート,
        long 退役実行ID,
        Action? 停止確認 = null){
        if(退役実行ID!=マニフェスト.比較元実行ID){
            throw new ArgumentException("退役実行IDが比較元実行IDと一致しません。",nameof(退役実行ID));
        }
        if(退役実行ID!=ベンチマーク性能ゲートロジック.必須比較元実行ID){
            throw new ArgumentException("退役できるのは承認済みのRun 249だけです。",nameof(退役実行ID));
        }
        if(最終結果一覧.Count!=ベンチマーク性能ゲートロジック.必須対象数){
            throw new InvalidDataException("保存対象は11件である必要があります。");
        }
        if(マニフェスト.要素数!=4096||マニフェスト.目標ミリ秒!=1024d){
            throw new InvalidDataException("DB保存用性能ゲートはN=4096・目標1.024秒である必要があります。");
        }
        var 実停止確認=停止確認??UIとWorkerの停止を確認;
        実停止確認();
        SqliteConnection.ClearAllPools();
        排他アクセスを確認(DBパス);
        比較元を検証(DBパス,マニフェスト);
        実停止確認();
        string? バックアップパス=null;
        var DB変更開始済みか=false;
        long 新実行ID=0;
        try{
            using var 接続=読み書き接続を開く(DBパス);
            var バックアップ前データ版=スカラー長整数(接続,"PRAGMA data_version;");
            バックアップパス=バックアップを作成(接続,DBパス);
            実停止確認();
            using var トランザクション=接続.BeginTransaction(deferred:false);
            if(バックアップ前データ版!=スカラー長整数(接続,"PRAGMA data_version;",トランザクション)){throw new InvalidDataException(nameof(バックアップ前データ版));}
            比較元を検証(接続,トランザクション,マニフェスト,false);
            バックアップと現在DBの比較元を検証(バックアップパス,接続,トランザクション,マニフェスト);
            DB変更開始済みか=true;
            実停止確認();
            var 作成日時=DateTimeOffset.UtcNow;
            新実行ID=新実行を作成(接続,トランザクション,マニフェスト,論理CPU,実行ルート,作成日時);
            BenchmarkTabStorage.保存(接続,トランザクション,新実行ID,最終結果一覧.Select(スナップショットを作成).ToArray());
            新実行を完了にする(接続,トランザクション,新実行ID,作成日時);
            新実行の親設定を検証(接続,トランザクション,新実行ID,マニフェスト,論理CPU,実行ルート);
            var 再読込=BenchmarkTabStorage.読込(接続,新実行ID,トランザクション);
            新実行を検証(再読込,最終結果一覧,論理CPU);
            var 削除前シーケンス=シーケンスを読む(接続,トランザクション);
            var 対象表別件数=マニフェスト.対象一覧
                .GroupBy(対象=>表名一覧[対象.タブID],StringComparer.Ordinal)
                .ToDictionary(static 組=>組.Key,static 組=>組.Count(),StringComparer.Ordinal);
            比較元を削除して整合性を検証(
                接続,トランザクション,マニフェスト,実停止確認,
                退役実行ID,新実行ID,削除前シーケンス,対象表別件数);
            トランザクション.Commit();
            return new(新実行ID,バックアップパス);
        }catch(Exception 例外){
            if(バックアップパス is null||!DB変更開始済みか){
                throw;
            }
            try{
                復元(DBパス,バックアップパス);
            }catch(Exception 復元例外){
                throw new IOException("性能ゲートDB更新に失敗し、バックアップからの復元にも失敗しました。",new AggregateException(例外,復元例外));
            }
            throw new IOException("性能ゲートDB更新に失敗したため、明示バックアップから復元しました。",例外);
        }
    }

    private static UIベンチマークスナップショット項目書込 スナップショットを作成(PerformanceGatePairResult 結果){
        var 時刻=DateTimeOffset.UtcNow;
        UIベンチマークスナップショットケース書込 ケース(ベンチマークケース 値)=>new(
            値.アセンブリ名,値.型完全名,値.メソッド名,値.引数キー,JsonSerializer.Serialize(値.引数一覧));
        UIベンチマークグリッド側書込 側(ベンチマーク結果項目 値)=>new(
            UI実行状態.完了,string.Empty,時刻,値.平均ナノ秒,値.割り当てバイト数値,値.ループ回数,値.合計ナノ秒,true);
        return new(
            結果.対象.タブ.ID,
            結果.対象.定義.表示型,
            結果.対象.定義.メソッド名,
            結果.測定.FLinq論理CPU,
            true,
            側(結果.FLinq),側(結果.基準),ケース(結果.対象.FLinq),ケース(結果.対象.基準));
    }

    private static void UIとWorkerの停止を確認(){
        var 自身ID=Environment.ProcessId;
        var 全処理=Process.GetProcesses();
        var 実行中ID一覧=new List<int>();
        try{
            foreach(var 処理 in 全処理){
                if(処理.Id==自身ID){
                    continue;
                }
                try{
                    var モジュールパス=実行モジュールパスを読む(処理);
                    var コマンドライン=プロセスコマンドラインを読む(処理);
                    if(禁止対象プロセスか(処理.ProcessName,モジュールパス,コマンドライン)){
                        実行中ID一覧.Add(処理.Id);
                    }
                }catch(InvalidOperationException){
                }catch(global::System.ComponentModel.Win32Exception){
                }
            }
            if(実行中ID一覧.Count>0){
                throw new InvalidOperationException("Benchmark UI/Headless/Workerが実行中のためDBを更新できません。PID="+string.Join(",",実行中ID一覧));
            }
        }finally{
            foreach(var 処理 in 全処理){
                処理.Dispose();
            }
        }
    }

    internal static bool 禁止対象プロセスか(string プロセス名,string? モジュールパス,string? コマンドライン){
        string[]禁止名一覧=["Benchmark.UI","Benchmark.Headless","Worker"];
        if(禁止名一覧.Contains(プロセス名,StringComparer.OrdinalIgnoreCase)
            ||(モジュールパス is not null
                &&禁止名一覧.Contains(Path.GetFileNameWithoutExtension(モジュールパス),StringComparer.OrdinalIgnoreCase))){
            return true;
        }
        if(string.IsNullOrWhiteSpace(コマンドライン)){
            return false;
        }
        foreach(var 引数 in コマンドラインを分割(コマンドライン)){
            var 拡張子=Path.GetExtension(引数);
            if((string.Equals(拡張子,".dll",StringComparison.OrdinalIgnoreCase)
                ||string.Equals(拡張子,".exe",StringComparison.OrdinalIgnoreCase))
                &&禁止名一覧.Contains(Path.GetFileNameWithoutExtension(引数),StringComparer.OrdinalIgnoreCase)){
                return true;
            }
        }
        return false;
    }

    private static string? 実行モジュールパスを読む(Process 処理){
        try{
            return 処理.MainModule?.FileName;
        }catch(InvalidOperationException){
            return null;
        }catch(global::System.ComponentModel.Win32Exception){
            return null;
        }
    }

    private static string? プロセスコマンドラインを読む(Process 処理){
        if(!OperatingSystem.IsWindows()){
            return null;
        }
        try{
            _=NtQueryInformationProcess(処理.Handle,60,IntPtr.Zero,0,out var 必要長);
            if(必要長==0||必要長>1024*1024){
                return null;
            }
            var バッファ=Marshal.AllocHGlobal(checked((int)必要長));
            try{
                if(NtQueryInformationProcess(処理.Handle,60,バッファ,必要長,out _)!=0){
                    return null;
                }
                var 文字列=Marshal.PtrToStructure<UnicodeString>(バッファ);
                return 文字列.Buffer==IntPtr.Zero||文字列.Length==0
                    ?null
                    :Marshal.PtrToStringUni(文字列.Buffer,文字列.Length/2);
            }finally{
                Marshal.FreeHGlobal(バッファ);
            }
        }catch(InvalidOperationException){
            return null;
        }catch(global::System.ComponentModel.Win32Exception){
            return null;
        }
    }

    private static IReadOnlyList<string>コマンドラインを分割(string コマンドライン){
        var 配列ポインタ=CommandLineToArgvW(コマンドライン,out var 件数);
        if(配列ポインタ==IntPtr.Zero){
            return [];
        }
        try{
            var 結果=new string[件数];
            for(var 位置=0;位置<件数;位置++){
                結果[位置]=Marshal.PtrToStringUni(Marshal.ReadIntPtr(配列ポインタ,位置*IntPtr.Size))??string.Empty;
            }
            return 結果;
        }finally{
            _=LocalFree(配列ポインタ);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UnicodeString{
        public readonly ushort Length;
        public readonly ushort MaximumLength;
        public readonly IntPtr Buffer;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr プロセスハンドル,int 情報クラス,IntPtr 情報,uint 情報長,out uint 戻り長);

    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr CommandLineToArgvW(string コマンドライン,out int 引数数);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr メモリ);

    private static void 排他アクセスを確認(string DBパス){
        try{
            using var ストリーム=new FileStream(DBパス,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        }catch(IOException 例外){
            throw new InvalidOperationException("Benchmark.dbが別プロセスで使用中のため更新できません。",例外);
        }
    }

    private static long 新実行を作成(
        SqliteConnection 接続,
        SqliteTransaction トランザクション,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        int 論理CPU,
        string 実行ルート,
        DateTimeOffset 作成日時){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText="""
            INSERT INTO "実行"("表示名","状態","実行プロファイル","タブ名","更新日時","実行ルート","要素数一覧","計測時間ns","論理CPU一覧")
            VALUES($name,'Queued',$profile,'PerformanceGate.Run249',$at,$root,$count,$target,$cpu);
            SELECT last_insert_rowid();
            """;
        コマンド.Parameters.AddWithValue("$name",$"Performance gate {作成日時:O}");
        コマンド.Parameters.AddWithValue("$profile",ベンチマーク実行プロファイル.正確.ToString());
        コマンド.Parameters.AddWithValue("$at",作成日時.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture));
        コマンド.Parameters.AddWithValue("$root",実行ルート);
        コマンド.Parameters.AddWithValue("$count",マニフェスト.要素数.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        コマンド.Parameters.AddWithValue("$target",マニフェスト.目標ミリ秒*1_000_000d);
        コマンド.Parameters.AddWithValue("$cpu",論理CPU.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        return Convert.ToInt64(コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void 新実行を完了にする(
        SqliteConnection 接続,
        SqliteTransaction トランザクション,
        long 実行ID,
        DateTimeOffset 更新日時){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText="UPDATE \"実行\" SET \"状態\"='Completed',\"更新日時\"=$at WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        コマンド.Parameters.AddWithValue("$at",更新日時.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture));
        if(コマンド.ExecuteNonQuery()!=1){
            throw new InvalidDataException("新しい性能ゲートRunを完了状態へ更新できませんでした。");
        }
    }

    private static void 新実行の親設定を検証(
        SqliteConnection 接続,
        SqliteTransaction トランザクション,
        long 実行ID,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        int 論理CPU,
        string 実行ルート){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText="SELECT \"状態\",\"実行プロファイル\",\"タブ名\",\"実行ルート\",\"要素数一覧\",\"計測時間ns\",\"論理CPU一覧\" FROM \"実行\" WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        using var 読取器=コマンド.ExecuteReader();
        if(!読取器.Read()
            ||!string.Equals(読取器.GetString(0),"Completed",StringComparison.Ordinal)
            ||!string.Equals(読取器.GetString(1),ベンチマーク実行プロファイル.正確.ToString(),StringComparison.Ordinal)
            ||!string.Equals(読取器.GetString(2),"PerformanceGate.Run249",StringComparison.Ordinal)
            ||!string.Equals(読取器.GetString(3),実行ルート,StringComparison.Ordinal)
            ||!string.Equals(読取器.GetString(4),マニフェスト.要素数.ToString(global::System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal)
            ||読取器.GetDouble(5)!=マニフェスト.目標ミリ秒*1_000_000d
            ||!string.Equals(読取器.GetString(6),論理CPU.ToString(global::System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal)
            ||読取器.Read()){
            throw new InvalidDataException("新しい性能ゲートRunの親設定が正確/N=4096/目標1.024秒/指定CPUと一致しません。");
        }
    }

    private static void 新実行を検証(
        IReadOnlyList<UIベンチマークスナップショット項目記録>再読込,
        IReadOnlyList<PerformanceGatePairResult>元結果一覧,
        int 論理CPU){
        var 期待分布=元結果一覧.GroupBy(static 結果=>結果.対象.タブ.ID,StringComparer.Ordinal).ToDictionary(static 組=>組.Key,static 組=>組.Count(),StringComparer.Ordinal);
        var 実分布=再読込.GroupBy(static 項目=>項目.タブ名,StringComparer.Ordinal).ToDictionary(static 組=>組.Key,static 組=>組.Count(),StringComparer.Ordinal);
        if(再読込.Count!=ベンチマーク性能ゲートロジック.必須対象数
            ||期待分布.Count!=6
            ||実分布.Count!=6
            ||期待分布.Any(組=>!実分布.TryGetValue(組.Key,out var 件数)||件数!=組.Value)){
            throw new InvalidDataException("新しい性能ゲートRunの6タブ11件分布が一致しません。");
        }
        foreach(var 元結果 in 元結果一覧){
            var 項目=再読込.SingleOrDefault(候補=>
                string.Equals(候補.タブ名,元結果.対象.タブ.ID,StringComparison.Ordinal)
                &&string.Equals(候補.型名,元結果.対象.定義.表示型,StringComparison.Ordinal)
                &&string.Equals(候補.ベンチマーク名,元結果.対象.定義.メソッド名,StringComparison.Ordinal)
                &&string.Equals(候補.左ケース?.引数キー,元結果.対象.定義.引数キー,StringComparison.Ordinal)
                &&string.Equals(候補.左ケース?.型完全名,元結果.対象.FLinq.型完全名,StringComparison.Ordinal)
                &&string.Equals(候補.右ケース?.型完全名,元結果.対象.基準.型完全名,StringComparison.Ordinal));
            if(項目 is null
                ||項目.CPU番号!=論理CPU
                ||項目.左ケース is null
                ||項目.右ケース is null
                ||!string.Equals(項目.左ケース.アセンブリ名,元結果.対象.FLinq.アセンブリ名,StringComparison.OrdinalIgnoreCase)
                ||!string.Equals(項目.右ケース.アセンブリ名,元結果.対象.基準.アセンブリ名,StringComparison.OrdinalIgnoreCase)
                ||!string.Equals(項目.左ケース.メソッド名,元結果.対象.FLinq.メソッド名,StringComparison.Ordinal)
                ||!string.Equals(項目.右ケース.メソッド名,元結果.対象.基準.メソッド名,StringComparison.Ordinal)
                ||!string.Equals(項目.右ケース.引数キー,元結果.対象.定義.引数キー,StringComparison.Ordinal)
                ||項目.左.状態!=UI実行状態.完了
                ||項目.右.状態!=UI実行状態.完了
                ||項目.左.平均ナノ秒!=元結果.FLinq.平均ナノ秒
                ||項目.右.平均ナノ秒!=元結果.基準.平均ナノ秒
                ||項目.左.割当バイト!=元結果.FLinq.割り当てバイト数値
                ||項目.右.割当バイト!=元結果.基準.割り当てバイト数値
                ||項目.左.ループ回数!=元結果.FLinq.ループ回数
                ||項目.右.ループ回数!=元結果.基準.ループ回数
                ||項目.左.平均ナノ秒 is not { }左時間
                ||項目.右.平均ナノ秒 is not { }右時間
                ||左時間/(左時間+右時間)>0.5d){
                throw new InvalidDataException("新しい性能ゲートRunの保存値が元結果と一致しません。ID="+元結果.対象.定義.ID);
            }
        }
    }

    private static string バックアップを作成(SqliteConnection 元,string DBパス){
        var 時刻=DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff",global::System.Globalization.CultureInfo.InvariantCulture);
        var 基本パス=DBパス+".pre-performance-gate-"+時刻;
        var バックアップパス=基本パス+".bak";
        for(var 連番=1;File.Exists(バックアップパス);連番++){
            バックアップパス=基本パス+"-"+連番.ToString(global::System.Globalization.CultureInfo.InvariantCulture)+".bak";
        }
        using var 先=new SqliteConnection(new SqliteConnectionStringBuilder{
            DataSource=バックアップパス,
            Mode=SqliteOpenMode.ReadWriteCreate,
            Pooling=false,
        }.ToString());
        先.Open();
        元.BackupDatabase(先);
        using var 確認=先.CreateCommand();
        確認.CommandText="PRAGMA integrity_check;";
        if(!string.Equals(Convert.ToString(確認.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture),"ok",StringComparison.OrdinalIgnoreCase)){
            throw new InvalidDataException("性能ゲートDBバックアップのintegrity_checkに失敗しました。");
        }
        確認.CommandText="PRAGMA foreign_key_check;";
        using var 読取器=確認.ExecuteReader();
        if(読取器.Read()){
            throw new InvalidDataException("性能ゲートDBバックアップのforeign_key_checkに失敗しました。");
        }
        return バックアップパス;
    }

    private static void バックアップと現在DBの比較元を検証(
        string バックアップパス,
        SqliteConnection 現在接続,
        SqliteTransaction 現在トランザクション,
        ベンチマーク性能ゲートマニフェスト マニフェスト){
        using var バックアップ接続=読取専用接続を開く(バックアップパス);
        比較元を検証(バックアップ接続,null,マニフェスト,false);
        foreach(var 表名 in 全タブ表名一覧.Prepend("実行")){
            var バックアップ件数=表の実行行数(バックアップ接続,null,表名,マニフェスト.比較元実行ID);
            var 現在件数=表の実行行数(現在接続,現在トランザクション,表名,マニフェスト.比較元実行ID);
            if(バックアップ件数!=現在件数){
                throw new InvalidDataException(nameof(バックアップ件数)+表名);
            }
        }
    }

    private static void 比較元を削除して整合性を検証(
        SqliteConnection 接続,
        SqliteTransaction トランザクション,
        ベンチマーク性能ゲートマニフェスト マニフェスト,
        Action 停止確認,
        long 退役実行ID,
        long 新実行ID,
        long 変更前シーケンス,
        IReadOnlyDictionary<string,int>対象表別件数){
        停止確認();
        比較元を検証(接続,トランザクション,マニフェスト,false);
        if(対象表別件数.Count!=6||対象表別件数.Values.Sum()!=ベンチマーク性能ゲートロジック.必須対象数){
            throw new InvalidDataException("新しい性能ゲートRunの対象表分布が6タブ11件ではありません。");
        }
        var 非対象表の以前最新一覧=全タブ表名一覧
            .Where(表名=>!対象表別件数.ContainsKey(表名))
            .ToDictionary(
                static 表名=>表名,
                表名=>表の最大実行ID(接続,トランザクション,表名,退役実行ID),
                StringComparer.Ordinal);
        using(var 削除=接続.CreateCommand()){
            削除.Transaction=トランザクション;
            削除.CommandText="DELETE FROM \"実行\" WHERE \"実行ID\"=$id;";
            削除.Parameters.AddWithValue("$id",退役実行ID);
            if(削除.ExecuteNonQuery()!=1){
                throw new InvalidDataException("比較元Runを1件削除できませんでした。");
            }
        }
        if(実行行数(接続,トランザクション,退役実行ID)!=0){
            throw new InvalidDataException("退役Runが残っています。");
        }
        foreach(var 表名 in 全タブ表名一覧){
            if(表の実行行数(接続,トランザクション,表名,退役実行ID)!=0){
                throw new InvalidDataException("退役RunのCASCADE削除に失敗しました。表="+表名);
            }
        }
        if(実行行数(接続,トランザクション,新実行ID)!=1){
            throw new InvalidDataException("新しい性能ゲートRunがありません。実行ID="+新実行ID);
        }
        foreach(var 対象表 in 対象表別件数){
            if(表の実行行数(接続,トランザクション,対象表.Key,新実行ID)!=対象表.Value
                ||表の最大実行ID(接続,トランザクション,対象表.Key,null)!=新実行ID){
                throw new InvalidDataException("新Runを持つタブの最新表示が新Run対象だけではありません。表="+対象表.Key);
            }
        }
        foreach(var 非対象表 in 非対象表の以前最新一覧){
            if(表の最大実行ID(接続,トランザクション,非対象表.Key,null)!=非対象表.Value){
                throw new InvalidDataException("新Runを持たないタブが以前の最新Runへ戻りませんでした。表="+非対象表.Key);
            }
        }
        if(シーケンスを読む(接続,トランザクション)!=変更前シーケンス){
            throw new InvalidDataException("Run退役によってsqlite_sequenceが変更されました。");
        }
        using(var 外部キー=接続.CreateCommand()){
            外部キー.Transaction=トランザクション;
            外部キー.CommandText="PRAGMA foreign_key_check;";
            using var 読取器=外部キー.ExecuteReader();
            if(読取器.Read()){
                throw new InvalidDataException("foreign_key_checkに失敗しました。");
            }
        }
        using(var 完全性=接続.CreateCommand()){
            完全性.Transaction=トランザクション;
            完全性.CommandText="PRAGMA integrity_check;";
            if(!string.Equals(Convert.ToString(完全性.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture),"ok",StringComparison.OrdinalIgnoreCase)){
                throw new InvalidDataException("integrity_checkに失敗しました。");
            }
        }
    }

    private static void 復元(string DBパス,string バックアップパス){
        SqliteConnection.ClearAllPools();
        foreach(var パス in new[]{DBパス+"-wal",DBパス+"-shm"}){
            if(File.Exists(パス)){
                File.Delete(パス);
            }
        }
        using var 元=読取専用接続を開く(バックアップパス);
        using var 先=読み書き接続を開く(DBパス);
        元.BackupDatabase(先);
    }

    private static SqliteConnection 読取専用接続を開く(string パス){
        var 接続=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=パス,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
        接続.Open();
        return 接続;
    }

    private static SqliteConnection 読み書き接続を開く(string パス){
        var 接続=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=パス,Mode=SqliteOpenMode.ReadWrite,DefaultTimeout=30,Pooling=false}.ToString());
        接続.Open();
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="PRAGMA busy_timeout=30000;PRAGMA foreign_keys=ON;";
        コマンド.ExecuteNonQuery();
        return 接続;
    }

    private static long シーケンスを読む(string DBパス){
        using var 接続=読取専用接続を開く(DBパス);
        return シーケンスを読む(接続,null);
    }

    private static long シーケンスを読む(SqliteConnection 接続,SqliteTransaction?トランザクション){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText="SELECT seq FROM sqlite_sequence WHERE name='実行';";
        return Convert.ToInt64(コマンド.ExecuteScalar()??0L,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Run249行数を検証(SqliteConnection 接続,SqliteTransaction? トランザクション,long 実行ID){
        var 表別行数=全タブ表名一覧.ToDictionary(
            static 表名=>表名,
            表名=>表の実行行数(接続,トランザクション,表名,実行ID),
            StringComparer.Ordinal);
        var 合計=表別行数.Values.Sum();
        var 不一致一覧=Run249期待行数一覧
            .Where(期待=>!表別行数.TryGetValue(期待.Key,out var 実際)||実際!=期待.Value)
            .Select(期待=>期待.Key+" expected="+期待.Value+" actual="+表別行数.GetValueOrDefault(期待.Key))
            .ToArray();
        var 期待合計=Run249期待行数一覧.Values.Sum();
        if(合計!=期待合計||不一致一覧.Length>0){
            throw new InvalidDataException(
                "Run 249の退役前行数が承認済みの"+期待合計.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
                +"件・表別内訳と一致しません。件数="+合計
                +" 内訳="+string.Join(",",表別行数.Select(static 組=>組.Key+"="+組.Value))
                +(不一致一覧.Length==0 ? string.Empty : " 不一致="+string.Join("|",不一致一覧)));
        }
    }

    private static long 実行行数(SqliteConnection 接続,SqliteTransaction? トランザクション,long 実行ID)=>表の実行行数(接続,トランザクション,"実行",実行ID);

    private static long 表の実行行数(SqliteConnection 接続,SqliteTransaction? トランザクション,string 表名,long 実行ID){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText=$"SELECT COUNT(*) FROM {引用(表名)} WHERE \"実行ID\"=$id;";
        コマンド.Parameters.AddWithValue("$id",実行ID);
        return Convert.ToInt64(コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static long?表の最大実行ID(
        SqliteConnection 接続,
        SqliteTransaction トランザクション,
        string 表名,
        long?除外実行ID){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText=$"SELECT MAX([実行ID]) FROM {引用(表名)}"
            +(除外実行ID is null ? ";" : " WHERE [実行ID]<>$id;");
        if(除外実行ID is { }実行ID){
            コマンド.Parameters.AddWithValue("$id",実行ID);
        }
        var 結果=コマンド.ExecuteScalar();
        return 結果 is null or DBNull
            ?null
            :Convert.ToInt64(結果,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void DB形状を検証(SqliteConnection 接続){
        string[]実行必須列=[
            "実行ID","表示名","状態","実行プロファイル","タブ名","更新日時","実行ルート","要素数一覧","計測時間ns","論理CPU一覧",
        ];
        if(!表列一覧を読む(接続,"実行").SequenceEqual(実行必須列,StringComparer.Ordinal)){
            throw new InvalidDataException("Benchmark.dbの実行表形状が現行版27と一致しません。");
        }
        foreach(var 定義 in 全タブ列見出し一覧){
            using(var ビュー確認=接続.CreateCommand()){
                ビュー確認.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name=$name;";
                ビュー確認.Parameters.AddWithValue("$name",定義.Key+"View");
                if(Convert.ToInt64(ビュー確認.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture)!=1){
                    throw new InvalidDataException("Benchmark.dbの現行ビューがありません。ビュー="+定義.Key+"View");
                }
            }
            var 必須列=new List<string>{"実行ID","型","ベンチマーク","CPU番号","引数キー","引数JSON","行チェック"};
            foreach(var 見出し in 定義.Value){
                必須列.AddRange([
                    見出し+"_アセンブリ名",見出し+"_型完全名",見出し+"_状態",見出し+"_失敗理由",
                    見出し+"_日時",見出し+"_時間ns",見出し+"_メモリバイト",見出し+"_ループ回数",
                ]);
            }
            if(!表列一覧を読む(接続,定義.Key).SequenceEqual(必須列,StringComparer.Ordinal)){
                throw new InvalidDataException("Benchmark.dbのタブ表形状が現行版27と一致しません。表="+定義.Key);
            }
        }
    }

    private static IReadOnlyList<string>表列一覧を読む(SqliteConnection 接続,string 表名){
        using var コマンド=接続.CreateCommand();
        コマンド.CommandText="SELECT name FROM pragma_table_info($table) ORDER BY cid;";
        コマンド.Parameters.AddWithValue("$table",表名);
        using var 読取器=コマンド.ExecuteReader();
        var 結果一覧=new List<string>();
        while(読取器.Read()){
            結果一覧.Add(読取器.GetString(0));
        }
        return 結果一覧;
    }

    private static long スカラー長整数(SqliteConnection 接続,string SQL,SqliteTransaction? トランザクション = null){
        using var コマンド=接続.CreateCommand();
        コマンド.Transaction=トランザクション;
        コマンド.CommandText=SQL;
        return Convert.ToInt64(コマンド.ExecuteScalar(),global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string 引用(string 識別子)=>"\""+識別子.Replace("\"","\"\"")+"\"";
}
