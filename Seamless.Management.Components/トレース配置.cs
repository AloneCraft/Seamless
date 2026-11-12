using Seamless.Management;

namespace Seamless.Management.Components;

internal sealed record トレース配置点(QueryTraceFrame 値,int X,double Y);
internal sealed record トレース説明配置(int X,double Y,トレース配置点[] 観測);
internal sealed record トレース目盛(double Y,long 値);
internal sealed class トレース配置{
    internal const int 最大観測数=1024,レーン幅=240,説明幅=208,行間隔=24,最大退避=72;
    internal QueryTraceFrame[] 記録{get;private init;}=[];
    internal Guid[] レーン{get;private init;}=[];
    internal トレース配置点[] 点{get;private init;}=[];
    internal トレース説明配置[] 説明{get;private init;}=[];
    internal トレース目盛[] 目盛{get;private init;}=[];
    internal int 幅{get;private init;}=640;
    internal int 高さ{get;private init;}=360;
    internal int 範囲外数{get;private init;}
    internal int 表示省略数{get;private init;}
    internal int 集約観測数=>this.説明.Sum(項目=>項目.観測.Length-1);

    internal static トレース配置 作成(QueryTraceFrame[] 全記録,bool 観測順か){
        var 記録=全記録.Take(最大観測数).ToArray();
        var 有効=記録.Where(項目=>項目.Sequence>0&&項目.Elapsed>=TimeSpan.Zero&&項目.Elapsed.Ticks<=DateTimeOffset.MaxValue.UtcTicks-項目.StartedAtUtc.UtcTicks).ToArray();
        var レーン=有効.Select(項目=>項目.ExecutionId).Distinct().ToArray();
        var 列=レーン.Select((ID,位置)=>(ID,位置)).ToDictionary(項目=>項目.ID,項目=>項目.位置);
        var 図高さ=Math.Clamp(有効.Length*28+120,360,7300);
        if(有効.Length==0){return new(){記録=記録,表示省略数=全記録.Length-記録.Length,範囲外数=記録.Length};}
        long 座標(QueryTraceFrame 項目){if(観測順か){return 項目.Sequence;}return 項目.StartedAtUtc.Add(項目.Elapsed).UtcTicks;}
        var 最小=有効.Min(座標);var 最大=有効.Max(座標);var 範囲=最大-最小;
        if(範囲==0){図高さ=360;}
        double Y(long 値){var 比率=0d;if(範囲>0){比率=(値-最小)/(double)範囲;}return 72+比率*(図高さ-120);}
        var 点=有効.Select(項目=>new トレース配置点(項目,260+列[項目.ExecutionId]*レーン幅,Y(座標(項目)))).ToArray();
        var 説明=new List<トレース説明配置>();
        foreach(var 組 in 点.GroupBy(項目=>項目.値.ExecutionId)){
            var 行=new List<(double Y,List<トレース配置点> 点)>();
            foreach(var 項目 in 組.OrderBy(項目=>項目.Y).ThenBy(項目=>項目.値.Sequence)){
                var 位置=項目.Y;if(行.Count>0){位置=Math.Max(項目.Y,行[^1].Y+行間隔);}
                if(行.Count>0&&位置>項目.Y+最大退避){行[^1].点.Add(項目);}
                else{行.Add((位置,[項目]));}
            }
            説明.AddRange(行.Select(項目=>new トレース説明配置(組.First().X+12,項目.Y,項目.点.ToArray())));
        }
        var 目盛数=(int)Math.Min(6L,範囲);
        var 目盛=Enumerable.Range(0,目盛数+1).Select(番号=>{
            var 値=最小;if(目盛数>0){値=最小+範囲/目盛数*番号+範囲%目盛数*番号/目盛数;}
            return new トレース目盛(Y(値),値);
        }).ToArray();
        return new(){記録=記録,レーン=レーン,点=点,説明=説明.ToArray(),目盛=目盛,幅=Math.Max(640,260+(レーン.Length-1)*レーン幅+12+説明幅+24),高さ=Math.Max(図高さ,(int)Math.Ceiling(説明.Max(項目=>項目.Y))+36),範囲外数=記録.Length-有効.Length,表示省略数=全記録.Length-記録.Length};
    }
}
