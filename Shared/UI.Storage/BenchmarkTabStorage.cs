using Microsoft.Data.Sqlite;
using System.Text;

namespace UI.Storage;

/// <summary>通常Benchmark UIのタブ別テーブル定義。識別子はすべて静的ホワイトリストから取得する。</summary>
internal static class BenchmarkTabStorage {
    private sealed record 定義(string タブID,string 表名,IReadOnlyList<string> 列見出し);

    private static readonly 定義[] 定義一覧 = [
        new("Optimization.Rewrite","書き換え",["全最適化","書き換え無効"]),
        new("Optimization.PipelineFusion","パイプライン融合",["全最適化","パイプライン融合無効"]),
        new("Optimization.PredicatePushdown","述語プッシュダウン",["全最適化","述語プッシュダウン無効"]),
        new("System.Array","Array",["FLinq","ZLinq"]),
        new("System.Collections.Generic.IEnumerable<>","IEnumerable",["FLinq","ZLinq"]),
        new("System.Range","Range",["FLinq","ZLinq"]),
        new("System.List","List",["FLinq","ZLinq"]),
        new("Seamless.Persisted.Seamless","Persisted(Seamless)",["FLinq","Persisted(Seamless)"]),
        new("Seamless.Persisted.System","Persisted(System)",["FLinq","Persisted(System)"]),
        new("Seamless.Tsavorite.Seamless","Tsavorite(Seamless)",["FLinq","Tsavorite(Seamless)"]),
        new("Seamless.Tsavorite.System","Tsavorite(System)",["FLinq","Tsavorite(System)"]),    ];

    internal static IReadOnlyList<string> 表名一覧 => 定義一覧.Select(static 定義 => 定義.表名).ToArray();

    internal static void 作成(SqliteConnection 接続,SqliteTransaction トランザクション) {
        foreach(var 定義 in 定義一覧) {
            var 列定義 = new StringBuilder("\"実行ID\" INTEGER NOT NULL,\"型\" TEXT NOT NULL,\"ベンチマーク\" TEXT NOT NULL,\"CPU番号\" INTEGER NULL,\"引数キー\" TEXT NOT NULL DEFAULT '',\"引数JSON\" TEXT NOT NULL DEFAULT '',\"行チェック\" INTEGER NOT NULL DEFAULT 0");
            foreach(var 見出し in 定義.列見出し) {
                列定義.Append(',').Append(引用(識別子(見出し,"アセンブリ名"))).Append(" TEXT NOT NULL DEFAULT ''")
                    .Append(',').Append(引用(識別子(見出し,"型完全名"))).Append(" TEXT NOT NULL DEFAULT ''")
                    .Append(',').Append(引用(識別子(見出し,"状態"))).Append(" TEXT NOT NULL")
                    .Append(',').Append(引用(識別子(見出し,"失敗理由"))).Append(" TEXT NOT NULL DEFAULT ''")
                    .Append(',').Append(引用(識別子(見出し,"日時"))).Append(" TEXT NULL")
                    .Append(',').Append(引用(識別子(見出し,"時間ns"))).Append(" REAL NULL")
                    .Append(',').Append(引用(識別子(見出し,"メモリバイト"))).Append(" INTEGER NULL")
                    .Append(',').Append(引用(識別子(見出し,"ループ回数"))).Append(" INTEGER NULL");
            }
            var 表 = 引用(定義.表名);
            実行(接続,トランザクション,$"CREATE TABLE {表}({列定義},FOREIGN KEY(\"実行ID\") REFERENCES \"実行\"(\"実行ID\") ON DELETE CASCADE);");
            実行(接続,トランザクション,$"CREATE INDEX {引用("索引_"+定義.表名+"_実行ID")} ON {表}(\"実行ID\");");
            実行(接続,トランザクション,$"CREATE UNIQUE INDEX {引用("一意索引_"+定義.表名+"_行")} ON {表}(\"実行ID\",\"型\",\"ベンチマーク\",\"引数キー\");");
        }
    }

    internal static bool 想定形状か(SqliteConnection 接続) {
        foreach(var 定義 in 定義一覧) {
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
            コマンド.Parameters.AddWithValue("$name",定義.表名);
            if((long)(コマンド.ExecuteScalar()??0L)!=1L) return false;
            var 必須列 = new List<string>{"実行ID","型","ベンチマーク","CPU番号","引数キー","引数JSON","行チェック"};
            foreach(var 見出し in 定義.列見出し) {
                必須列.AddRange([$"{見出し}_アセンブリ名",$"{見出し}_型完全名",$"{見出し}_状態",$"{見出し}_失敗理由",$"{見出し}_日時",$"{見出し}_時間ns",$"{見出し}_メモリバイト",$"{見出し}_ループ回数"]);
            }
            using var 列コマンド=接続.CreateCommand();
            列コマンド.CommandText=$"SELECT name FROM pragma_table_info({Sql文字列(定義.表名)}) ORDER BY cid;";
            using var 読取器=列コマンド.ExecuteReader();
            var 実列=new List<string>();
            while(読取器.Read()) 実列.Add(読取器.GetString(0));
            if(!実列.SequenceEqual(必須列,StringComparer.Ordinal)) return false;
        }
        return true;
    }

    internal static void 保存(SqliteConnection 接続,SqliteTransaction トランザクション,long 実行ID,IReadOnlyList<UIベンチマークスナップショット項目書込> 項目一覧) {
        foreach(var 項目 in 項目一覧) {
            var 定義=取得(項目.タブ名);
            using var コマンド=接続.CreateCommand();
            コマンド.Transaction=トランザクション;
            var 列名 = new List<string>{"実行ID","型","ベンチマーク","CPU番号","引数キー","引数JSON","行チェック"};
            var 値名 = new List<string>{"$id","$type","$benchmark","$cpu","$key","$json","$selected"};
            var 側一覧=側一覧を取得(定義,項目);
            for(var i=0;i<側一覧.Count;i++) {
                var 見出し=定義.列見出し[i]; var 側=側一覧[i].側; var ケース=側一覧[i].ケース; var p="p"+i;
                列名.AddRange([$"{見出し}_アセンブリ名",$"{見出し}_型完全名",$"{見出し}_状態",$"{見出し}_失敗理由",$"{見出し}_日時",$"{見出し}_時間ns",$"{見出し}_メモリバイト",$"{見出し}_ループ回数"]);
                値名.AddRange([$"${p}Assembly",$"${p}Type",$"${p}Status",$"${p}Failure",$"${p}At",$"${p}Mean",$"${p}Bytes",$"${p}Loops"]);
                コマンド.Parameters.AddWithValue($"${p}Assembly",ケース?.アセンブリ名??string.Empty);
                コマンド.Parameters.AddWithValue($"${p}Type",ケース?.型完全名??string.Empty);
                コマンド.Parameters.AddWithValue($"${p}Status",状態(側.状態));
                コマンド.Parameters.AddWithValue($"${p}Failure",側.失敗理由);
                コマンド.Parameters.AddWithValue($"${p}At",DB値(側.計測日時));
                コマンド.Parameters.AddWithValue($"${p}Mean",DB値(側.平均ナノ秒));
                コマンド.Parameters.AddWithValue($"${p}Bytes",DB値(側.割当バイト));
                コマンド.Parameters.AddWithValue($"${p}Loops",DB値(側.ループ回数));
            }
            var 共通=側一覧.Select(static 値=>値.ケース).FirstOrDefault(static 値=>値 is not null);
            コマンド.CommandText=$"INSERT OR REPLACE INTO {引用(定義.表名)}({string.Join(',',列名.Select(引用))}) VALUES({string.Join(',',値名)});";
            コマンド.Parameters.AddWithValue("$id",実行ID); コマンド.Parameters.AddWithValue("$type",項目.型名); コマンド.Parameters.AddWithValue("$benchmark",項目.ベンチマーク名);
            コマンド.Parameters.AddWithValue("$cpu",DB値(項目.CPU番号)); コマンド.Parameters.AddWithValue("$key",共通?.引数キー??string.Empty); コマンド.Parameters.AddWithValue("$json",共通?.引数JSON??string.Empty); コマンド.Parameters.AddWithValue("$selected",項目.選択中か ? 1 : 0);
            コマンド.ExecuteNonQuery();
        }
    }

    internal static IReadOnlyList<UIベンチマークスナップショット項目記録> 読込(SqliteConnection 接続,long 実行ID) {
        var 結果=new List<UIベンチマークスナップショット項目記録>();
        foreach(var 定義 in 定義一覧) {
            using var コマンド=接続.CreateCommand();
            var 列名=new List<string>{"実行ID","型","ベンチマーク","CPU番号","行チェック","引数キー","引数JSON"};
            foreach(var 見出し in 定義.列見出し) 列名.AddRange([$"{見出し}_アセンブリ名",$"{見出し}_型完全名",$"{見出し}_状態",$"{見出し}_失敗理由",$"{見出し}_日時",$"{見出し}_時間ns",$"{見出し}_メモリバイト",$"{見出し}_ループ回数"]);
            コマンド.CommandText=$"SELECT {string.Join(',',列名.Select(引用))} FROM {引用(定義.表名)} WHERE \"実行ID\"=$id ORDER BY \"型\",\"ベンチマーク\",\"引数キー\";";
            コマンド.Parameters.AddWithValue("$id",実行ID);
            using var 読取器=コマンド.ExecuteReader();
            while(読取器.Read()) {
                var ベンチマーク=読取器.GetString(2); var キー=読取器.GetString(5); var JSON=読取器.GetString(6); var 側一覧=new List<(UIベンチマークグリッド側記録 側,UIベンチマークスナップショットケース書込? ケース)>();
                var 位置=7;
                for(var i=0;i<定義.列見出し.Count;i++) {
                    var ケース=読取器.GetString(位置).Length==0&&読取器.GetString(位置+1).Length==0 ? null : new UIベンチマークスナップショットケース書込(読取器.GetString(位置),読取器.GetString(位置+1),ベンチマーク,キー,JSON);
                    var 側=new UIベンチマークグリッド側記録(状態を読む(読取器.GetString(位置+2)),読取器.GetString(位置+3),読取器.IsDBNull(位置+4)?null:日時を読む(読取器.GetString(位置+4)),読取器.IsDBNull(位置+5)?null:読取器.GetDouble(位置+5),読取器.IsDBNull(位置+6)?null:読取器.GetInt64(位置+6),読取器.IsDBNull(位置+7)?null:読取器.GetInt32(位置+7));
                    側一覧.Add((側,ケース)); 位置+=8;
                }
                var 左=側一覧[0]; var 右=側一覧[1];
                結果.Add(new(読取器.GetInt64(0),定義.タブID,読取器.GetString(1),ベンチマーク,読取器.IsDBNull(3)?null:読取器.GetInt32(3),読取器.GetInt32(4)!=0,左.側,右.側,左.ケース,右.ケース));
            }
        }
        return 結果;
    }

    internal static void 全削除(SqliteConnection 接続,SqliteTransaction トランザクション) { foreach(var 表名 in 表名一覧) 実行(接続,トランザクション,$"DELETE FROM {引用(表名)};"); }

    private static 定義 取得(string タブID) => 定義一覧.FirstOrDefault(定義=>string.Equals(定義.タブID,タブID,StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidDataException("不明なBenchmarkタブIDです: "+タブID);
    private static IReadOnlyList<(UIベンチマークグリッド側書込 側,UIベンチマークスナップショットケース書込? ケース)> 側一覧を取得(定義 定義,UIベンチマークスナップショット項目書込 項目) =>
        [(項目.左,項目.左ケース),(項目.右,項目.右ケース)];
    private static UIベンチマークグリッド側書込 待機側()=>new(UI実行状態.待機中,string.Empty,null,null,null);
    private static string 識別子(string 見出し,string 接尾辞)=>$"{見出し}_{接尾辞}";
    private static string 引用(string 識別子)=>"\""+識別子.Replace("\"","\"\"")+"\"";
    private static string Sql文字列(string 値)=>"'"+値.Replace("'","''")+"'";
    private static void 実行(SqliteConnection 接続,SqliteTransaction トランザクション,string SQL){using var コマンド=接続.CreateCommand();コマンド.Transaction=トランザクション;コマンド.CommandText=SQL;コマンド.ExecuteNonQuery();}
    private static object DB値(int? 値)=>値 is { } 実値 ? 実値 : DBNull.Value; private static object DB値(long? 値)=>値 is { } 実値 ? 実値 : DBNull.Value; private static object DB値(double? 値)=>値 is { } 実値&&double.IsFinite(実値) ? 実値 : DBNull.Value; private static object DB値(DateTimeOffset? 値)=>値 is { } 実値 ? 実値.ToString("O",System.Globalization.CultureInfo.InvariantCulture) : DBNull.Value;
    private static string 状態(UI実行状態 値)=>値 switch{UI実行状態.待機中=>"Queued",UI実行状態.実行中=>"Running",UI実行状態.一時停止中=>"Paused",UI実行状態.停止済み=>"Stopped",UI実行状態.完了=>"Completed",UI実行状態.失敗=>"Failed",_=>throw new ArgumentOutOfRangeException(nameof(値),値,null)};
    private static UI実行状態 状態を読む(string 値)=>値 switch{"Queued"=>UI実行状態.待機中,"Running"=>UI実行状態.実行中,"Paused"=>UI実行状態.一時停止中,"Stopped"=>UI実行状態.停止済み,"Completed"=>UI実行状態.完了,"Failed"=>UI実行状態.失敗,_=>throw new InvalidDataException("不明なUI実行状態です: "+値)};
    private static DateTimeOffset 日時を読む(string 値)=>DateTimeOffset.Parse(値,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
}
