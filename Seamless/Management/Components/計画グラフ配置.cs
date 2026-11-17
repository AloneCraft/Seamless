using Seamless.Management;

namespace Seamless.Management.Components;

internal enum 計画表示層{すべて,処理,元のクエリ,数式,判断と条件,その他,論理処理}
internal sealed record 計画配置ノード(ExecutionPlanNode 元,int X,int Y){
    internal const int 幅=248,高さ=100;
    public int 中心X=>this.X+幅/2;
    public int 下端=>this.Y+高さ;
}
internal sealed record 計画配置接続(計画配置ノード 入力,計画配置ノード 出力,string 種類,string? 入力役割,int 入力番号);
internal sealed record 計画配置領域(計画表示層 層,int X,int 幅,int 高さ);

internal sealed class 計画グラフ配置{
    private const int 横間隔=44,縦間隔=62,外余白=28,見出し高さ=64;
    internal 計画配置ノード[] ノード{get;private init;}=[];
    internal 計画配置接続[] 接続{get;private init;}=[];
    internal 計画配置ノード[] 集約元{get;private init;}=[];
    internal 計画配置領域[] 領域{get;private init;}=[];
    internal int 幅{get;private init;}=640;
    internal int 高さ{get;private init;}=320;
    internal int 結果X=>this.幅/2-計画配置ノード.幅/2;
    internal int 結果Y=>this.高さ-計画配置ノード.高さ-外余白;
    internal bool 循環を含むか{get;private init;}
    internal int 不明な入力数{get;private init;}
    internal bool 識別子重複か{get;private init;}

    internal static 計画表示層 層を取得(ExecutionPlanNode ノード){
        switch(ノード.Layer){
            case "Original":return 計画表示層.元のクエリ;
            case "Selected":return 計画表示層.処理;
            case "OriginalExpression":return 計画表示層.数式;
            case "Optimization":case "GeneratedControl":return 計画表示層.判断と条件;
        }
        return ノード.Kind switch{
        "Fragment" or "PipelineStage"=>計画表示層.処理,
        "SourceOperator"=>計画表示層.元のクエリ,
        "Expression"=>計画表示層.数式,
        "LoweringDecision" or "RuntimeCondition"=>計画表示層.判断と条件,
        _=>計画表示層.その他
        };
    }

    internal static 計画グラフ配置 作成(ExecutionPlanDocument 計画,計画表示層 表示層){
        var 全識別子=new HashSet<string>(StringComparer.Ordinal);
        foreach(var ノード in 計画.Nodes){
            if(!全識別子.Add(ノード.Id)){return new(){識別子重複か=true};}
        }
        var 記録接続=計画.Edges;
        if(計画.SchemaVersion<2){
            記録接続=計画.Nodes.SelectMany(ノード=>ノード.Inputs.Select(入力=>new ExecutionPlanEdge(入力,ノード.Id,"Unknown",null))).ToArray();
        }
        var 対象=計画.Nodes.Where(ノード=>表示層==計画表示層.すべて||層を取得(ノード)==表示層||表示層==計画表示層.論理処理&&層を取得(ノード) is 計画表示層.処理 or 計画表示層.数式).ToArray();
        var 配置=new List<計画配置ノード>();
        var 領域=new List<計画配置領域>();
        var 次X=外余白;
        var 最大下端=見出し高さ;
        var 循環か=false;
        foreach(var 層 in new[]{計画表示層.元のクエリ,計画表示層.処理,計画表示層.数式,計画表示層.判断と条件,計画表示層.その他}){
            var 一覧=対象.Where(ノード=>層を取得(ノード)==層).ToArray();
            if(一覧.Length==0){continue;}
            var 識別子=一覧.Select(ノード=>ノード.Id).ToHashSet(StringComparer.Ordinal);
            var 層接続=記録接続.Where(接続=>接続.Kind!="SourceMapping"&&識別子.Contains(接続.From)&&識別子.Contains(接続.To)).ToArray();
            var 入力一覧=層接続.GroupBy(接続=>接続.To).ToDictionary(組=>組.Key,組=>組.Select(接続=>接続.From).Distinct(StringComparer.Ordinal).ToArray(),StringComparer.Ordinal);
            var 残入力=一覧.ToDictionary(ノード=>ノード.Id,ノード=>入力一覧.GetValueOrDefault(ノード.Id)?.Length??0,StringComparer.Ordinal);
            var 後続=一覧.ToDictionary(ノード=>ノード.Id,_=>new List<string>(),StringComparer.Ordinal);
            var 段=一覧.ToDictionary(ノード=>ノード.Id,_=>0,StringComparer.Ordinal);
            foreach(var ノード in 一覧){
                foreach(var 入力 in 入力一覧.GetValueOrDefault(ノード.Id)??[]){後続[入力].Add(ノード.Id);}
            }
            var 待ち=new Queue<string>(一覧.Where(ノード=>残入力[ノード.Id]==0).Select(ノード=>ノード.Id));
            while(待ち.TryDequeue(out var ID)){
                foreach(var 次 in 後続[ID]){
                    段[次]=Math.Max(段[次],段[ID]+1);
                    if(--残入力[次]==0){待ち.Enqueue(次);}
                }
            }
            var 未確定=一覧.Where(ノード=>残入力[ノード.Id]>0).ToArray();
            if(未確定.Length>0){
                循環か=true;
                var 保留段=段.Values.Max()+1;
                foreach(var ノード in 未確定){段[ノード.Id]=保留段;}
            }
            var 段別=一覧.GroupBy(ノード=>段[ノード.Id]).OrderBy(組=>組.Key).ToArray();
            var 領域幅=段別.Max(組=>組.Count())*(計画配置ノード.幅+横間隔)-横間隔+外余白*2;
            var 領域高さ=見出し高さ+(段.Values.Max()+1)*(計画配置ノード.高さ+縦間隔)-縦間隔+外余白;
            foreach(var 組 in 段別){
                var 段幅=組.Count()*(計画配置ノード.幅+横間隔)-横間隔;
                var X=次X+(領域幅-段幅)/2;
                foreach(var ノード in 組){
                    配置.Add(new(ノード,X,見出し高さ+組.Key*(計画配置ノード.高さ+縦間隔)));
                    X+=計画配置ノード.幅+横間隔;
                }
            }
            領域.Add(new(層,次X,領域幅,領域高さ));
            最大下端=Math.Max(最大下端,領域高さ);
            次X+=領域幅+横間隔;
        }
        var 位置=配置.ToDictionary(ノード=>ノード.元.Id,StringComparer.Ordinal);
        var 接続=new List<計画配置接続>();
        var 入力番号=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var 記録 in 記録接続){
            if(位置.TryGetValue(記録.From,out var 入力)&&位置.TryGetValue(記録.To,out var 出力)){
                var 番号=入力番号.GetValueOrDefault(記録.To)+1;入力番号[記録.To]=番号;
                接続.Add(new(入力,出力,記録.Kind,記録.InputRole,番号));
            }
        }
        // 層間の意味を推定せず、処理、元クエリ、数式の順で表示中の参照終端をまとめる。
        var 主層=計画表示層.処理;
        if(!配置.Any(ノード=>層を取得(ノード.元)==主層)){主層=計画表示層.元のクエリ;}
        if(!配置.Any(ノード=>層を取得(ノード.元)==主層)){主層=計画表示層.数式;}
        var 主ノード=配置.Where(ノード=>層を取得(ノード.元)==主層).ToArray();
        var 主識別子=主ノード.Select(ノード=>ノード.元.Id).ToHashSet(StringComparer.Ordinal);
        var 被参照=記録接続.Where(接続=>接続.Kind!="SourceMapping"&&主識別子.Contains(接続.From)&&主識別子.Contains(接続.To)).Select(接続=>接続.From).ToHashSet(StringComparer.Ordinal);
        var 集約元=主ノード.Where(ノード=>!被参照.Contains(ノード.元.Id)).ToArray();
        return new(){
            ノード=配置.ToArray(),接続=接続.ToArray(),集約元=集約元,領域=領域.ToArray(),
            幅=Math.Max(640,次X-横間隔+外余白),高さ=最大下端+縦間隔+計画配置ノード.高さ+外余白,
            循環を含むか=循環か,不明な入力数=記録接続.Count(接続=>!全識別子.Contains(接続.From)||!全識別子.Contains(接続.To))
        };
    }
}
