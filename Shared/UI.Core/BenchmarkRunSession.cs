using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal sealed class ベンチマーク実行セッション{
    private readonly object ゲート=new();
    private readonly Dictionary<ベンチマーク実行結果キー,ベンチマーク結果項目>結果一覧=[];

    public bool 結果ありか{
        get{
            lock(this.ゲート){
                return this.結果一覧.Count>0;
            }
        }
    }

    public int 件数{
        get{
            lock(this.ゲート){
                return this.結果一覧.Count;
            }
        }
    }

    /// <summary>件数チェック専用。結果取得はソートと配列化を伴うため、存在確認に使わないこと。</summary>
    public bool ソースタブ結果があるか(ベンチマークソースタブ ソースタブ){
        lock(this.ゲート){
            foreach(var キー in this.結果一覧.Keys){
                if(キー.ソースタブ==ソースタブ){
                    return true;
                }
            }
        }
        return false;
    }

    public void 結果を更新または追加(ベンチマーク結果項目 結果){
        lock(this.ゲート){
            this.結果一覧[ベンチマーク実行結果キー.作成(結果)]=結果;
        }
    }

    public void クリア(){
        lock(this.ゲート){
            this.結果一覧.Clear();
        }
    }

    public void 実行中結果をクリア(){
        lock(this.ゲート){
            foreach(var キー in this.結果一覧
                .Where(static 項目=>string.Equals(項目.Value.状態,ジョブ状態名.保存値に変換(ジョブ状態.実行中),StringComparison.OrdinalIgnoreCase))
                .Select(static 項目=>項目.Key)
                .ToArray()){
                this.結果一覧.Remove(キー);
            }
        }
    }

    public IReadOnlyList<ベンチマーク結果項目>結果を取得(ベンチマークソースタブ ソースタブ){
        lock(this.ゲート){
            return this.結果一覧
                .Where(項目=>項目.Key.ソースタブ==ソースタブ)
                .Select(static 項目=>項目.Value)
                .OrderBy(static 項目=>項目.開始日時)
                .ThenBy(static 項目=>項目.軸数)
                .ToArray();
        }
    }

    public IReadOnlyList<ベンチマーク結果項目>全結果を取得(){
        lock(this.ゲート){
            return this.結果一覧.Values
                .OrderBy(static 項目=>項目.ソースタブ)
                .ThenBy(static 項目=>項目.開始日時)
                .ThenBy(static 項目=>項目.軸数)
                .ToArray();
        }
    }

    public IReadOnlyList<int>要素数一覧を取得(ベンチマークソースタブ ソースタブ)=>
        this.結果を取得(ソースタブ)
            .Select(static 項目=>項目.軸数)
            .Where(static 項目=>項目>0)
            .Distinct()
            .Order()
            .ToArray();

    public 進行状態 進行状態に変換(
        string 実行ルート,
        ベンチマーク実行プロファイル 実行プロファイル,
        ベンチマークソースタブ定義 選択ソースタブ,
        TimeSpan 適応しきい値,
        IReadOnlyDictionary<string,bool>行選択,
        Func<ベンチマーク結果項目,ベンチマークケース?>ベンチマーク解決器){
        var 現在日時=DateTimeOffset.UtcNow;
        IReadOnlyList<ベンチマーク結果項目>元結果一覧=[];
        if(this.結果ありか){
            元結果一覧=this.全結果を取得();
        }
        return 進行状態を作成(
            実行ルート,
            実行プロファイル,
            選択ソースタブ,
            適応しきい値,
            行選択,
            元結果一覧,
            ベンチマーク解決器,
            現在日時);
    }

    public static 進行状態 進行状態を作成(
        string 実行ルート,
        ベンチマーク実行プロファイル 実行プロファイル,
        ベンチマークソースタブ定義 選択ソースタブ,
        TimeSpan 適応しきい値,
        IReadOnlyDictionary<string,bool>行選択,
        IEnumerable<ベンチマーク結果項目>元結果一覧,
        Func<ベンチマーク結果項目,ベンチマークケース?>ベンチマーク解決器,
        DateTimeOffset?日時=null){
        var 現在日時=日時??DateTimeOffset.UtcNow;
        var 結果一覧=元結果一覧
            .Where(static 結果=>結果.軸数>0)
            .GroupBy(static 結果=>結果識別キーを作成(結果.プロジェクト,結果.型完全名,結果.メソッド,結果.軸数,結果.引数キー),StringComparer.OrdinalIgnoreCase)
            .Select(static グループ=>グループ.Last())
            .ToArray();
        var 要素数一覧=結果一覧
            .Select(static 結果=>結果.軸数)
            .Where(static 項目=>項目>0)
            .Distinct()
            .Order()
            .ToArray();
        var 状態=new 進行状態{
            実行ID=Guid.NewGuid().ToString("N",CultureInfo.InvariantCulture),
            実行プロファイル=実行プロファイル,
            実行ルート=実行ルート,
            実行番号=ベンチマーク状態マネージャー.実行番号の解析を試みる(実行ルート),
            ソースタブ=選択ソースタブ.タブ,
            要素数一覧=要素数一覧.ToList(),
            適応しきい値ナノ秒=適応しきい値.TotalMilliseconds*1_000_000,
            ジョブ一覧=結果一覧.Select(結果=>解決済み結果から進行ジョブを作成(
                    結果,
                    選択ソースタブ,
                    要素数一覧,
                    ベンチマーク解決器))
                .Where(static ジョブ=>!string.IsNullOrWhiteSpace(ジョブ.型完全名))
                .ToList(),
            ベンチマーク選択=new 進行ベンチマーク選択状態{
                行選択=new Dictionary<string,bool>(行選択,StringComparer.OrdinalIgnoreCase),
            },
            作成日時=現在日時,
            更新日時=現在日時,
        };
        状態.適応状態一覧=状態.ジョブ一覧
            .Select(static ジョブ=>ベンチマーク行グループ化ロジック.ケースキーを作成(ジョブ.プロジェクト,ジョブ.型完全名,ジョブ.メソッド名,ジョブ.引数キー))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static キー=>キー,StringComparer.OrdinalIgnoreCase)
            .Select(static キー=>new 適応ベンチマーク状態{
                メソッド名=キー,
                完了済みか=true,
            })
            .ToList();
        return 状態;
    }

    private static ベンチマークジョブ 解決済み結果から進行ジョブを作成(
        ベンチマーク結果項目 結果,
        ベンチマークソースタブ定義 選択ソースタブ,
        IReadOnlyList<int>準備済み要素数一覧,
        Func<ベンチマーク結果項目,ベンチマークケース?>ベンチマーク解決器){
        var ベンチマーク=ベンチマーク解決器(結果)
            ??throw new InvalidDataException(
                "ベンチマーク結果をカタログケースに照合できません。プロジェクト="+結果.プロジェクト
                +" メソッド="+結果.メソッド
                +" 型="+結果.型完全名
                +" 引数キー="+結果.引数キー);
        var ソースタブ=ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得(結果.プロジェクト,ベンチマーク.アセンブリ名);
        if(!ソースタブ.ケースを含むか(ベンチマーク)){
            throw new InvalidDataException(
                "ベンチマークケースが解決したソースタブに属していません。ソースタブ="+ソースタブ.ID
                +" プロジェクト="+ベンチマーク.プロジェクト
                +" アセンブリ="+ベンチマーク.アセンブリ名
                +" 型="+ベンチマーク.型完全名);
        }
        if(ソースタブ.タブ!=選択ソースタブ.タブ||結果.ソースタブ!=ソースタブ.タブ){
            throw new InvalidDataException(
                "ベンチマーク結果のソースタブが一致しません。プロジェクト="+結果.プロジェクト
                +" アセンブリ="+ベンチマーク.アセンブリ名
                +" 選択ソースタブ="+選択ソースタブ.ID
                +" 結果ソースタブ="+ベンチマークソースタブ一覧.取得(結果.ソースタブ).ID
                +" 解決ソースタブ="+ソースタブ.ID);
        }
        return 結果から進行ジョブを作成(結果,ソースタブ,準備済み要素数一覧,ベンチマーク);
    }

    private static ベンチマークジョブ 結果から進行ジョブを作成(
        ベンチマーク結果項目 結果,
        ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyList<int>準備済み要素数一覧,
        ベンチマークケース?ベンチマーク){
        var 引数キー=最初の文字列(結果.引数キー,ベンチマーク?.引数キー??string.Empty);
        List<ベンチマーク引数>引数一覧;
        if(結果.引数一覧.Count>0){
            引数一覧=結果.引数一覧.ToList();
        }else{
            引数一覧=ベンチマーク?.引数一覧.ToList()??[];
        }
        var 状態=ジョブ状態を解析(結果.状態);
        var 終了日時=結果.開始日時??DateTimeOffset.UtcNow;
        DateTimeOffset?完了日時=null;
        if(状態 is ジョブ状態.完了 or ジョブ状態.失敗){
            完了日時=終了日時;
        }
        return new ベンチマークジョブ{
            ジョブID=string.Join("|",結果.プロジェクト,ベンチマーク?.アセンブリ名??string.Empty,ベンチマーク?.型完全名??string.Empty,結果.メソッド,引数キー,結果.軸数),
            プロジェクト=結果.プロジェクト,
            アセンブリ名=ベンチマーク?.アセンブリ名??string.Empty,
            型完全名=ベンチマーク?.型完全名??string.Empty,
            メソッド名=結果.メソッド,
            引数キー=引数キー,
            引数一覧=引数一覧,
            ソースタブ=ソースタブ.タブ,
            要素数=結果.要素数,
            ループ回数=結果.ループ回数,
            準備済み要素数一覧=ベンチマーク作業軸.準備済み要素数を解決(ソースタブ.タブ,準備済み要素数一覧).ToList(),
            状態=状態,
            割り当て論理CPU=結果.割り当て論理CPU,
            平均=結果.平均,
            誤差=結果.誤差ナノ秒?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            標準偏差=結果.標準偏差ナノ秒?.ToString(CultureInfo.InvariantCulture)??string.Empty,
            平均ナノ秒=結果.平均ナノ秒,
            誤差ナノ秒=結果.誤差ナノ秒,
            標準偏差ナノ秒=結果.標準偏差ナノ秒,
            割り当てバイト数=結果.割り当てバイト数値,
            乱数シード=結果.乱数シード,
            結果ダイジェスト=結果.結果ダイジェスト,
            結果確認文字列=結果.結果確認文字列,
            終了コード=結果.終了コード,
            成果物フォルダー=結果.成果物フォルダー,
            失敗理由=結果.失敗理由,
            開始日時=結果.開始日時??終了日時,
            終了日時=完了日時,
        };
    }

    private static ジョブ状態 ジョブ状態を解析(string 状態){
        if(string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.実行中),StringComparison.OrdinalIgnoreCase)){
            return ジョブ状態.実行中;
        }
        if(string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.一時停止),StringComparison.OrdinalIgnoreCase)){
            return ジョブ状態.一時停止;
        }
        if(string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)){
            return ジョブ状態.完了;
        }
        if(string.Equals(状態,ジョブ状態名.保存値に変換(ジョブ状態.失敗),StringComparison.OrdinalIgnoreCase)){
            return ジョブ状態.失敗;
        }
        return ジョブ状態.保留;
    }

    private static string 最初の文字列(string 左,string 右){
        if(!string.IsNullOrWhiteSpace(左)){
            return 左;
        }
        return 右;
    }

    private static string 結果識別キーを作成(string プロジェクト,string 型完全名,string メソッド,int 要素数,string 引数キー)=>
        string.Join("|",プロジェクト,型完全名,メソッド,要素数.ToString(CultureInfo.InvariantCulture),引数キー);
}

internal readonly record struct ベンチマーク実行結果キー(
    ベンチマークソースタブ ソースタブ,
    string プロジェクト,
    string 型完全名,
    string メソッド,
    string 引数キー,
    int 要素数){
    public static ベンチマーク実行結果キー 作成(ベンチマーク結果項目 結果)=>
        new(
            結果.ソースタブ,
            結果.プロジェクト,
            結果.型完全名,
            結果.メソッド,
            結果.引数キー,
            結果.軸数);
}
