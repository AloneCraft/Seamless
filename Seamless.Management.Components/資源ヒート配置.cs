using Seamless.Management;

namespace Seamless.Management.Components;

public sealed record 資源レーン配置(ResourceLane 値,int Y);
public sealed record 資源サンプル配置(ResourceSample 値,ResourceLane レーン,double X,double 幅,int Y);
public sealed record 資源実行区間配置(VisualizationInterval 値,double X,double 幅,int Y);
public sealed record 資源ヒート配置(long 開始,long 終了,資源レーン配置[] レーン,資源サンプル配置[] サンプル,資源実行区間配置[] 実行区間,int 高さ,int 非対応時計区間数,int 表示省略数){
    public const int 左端=210,描画幅=1000,幅=1240,最大描画サンプル=8192;
    public double 位置(long 値)=>左端+描画幅*(double)(((decimal)値-this.開始)/Math.Max(1m,(decimal)this.終了-this.開始));
    public static 資源ヒート配置 作成(ResourceHeatTrace 記録,QueryVisualizationData? 実行,string scope="Host",string? kind=null,long? rangeStart=null,long? rangeEnd=null){
        記録.Validate();
        if(実行 is not null){
            実行.Validate();var identity=実行.Identity;
            if(identity.ExecutionId!=記録.ExecutionId||identity.DefinitionId!=記録.DefinitionId||identity.ObjectId!=記録.ObjectId||identity.Version!=記録.Version||identity.ArtifactHash!=記録.ArtifactHash||!記録.IdentityResolved){throw new InvalidDataException("資源記録と実行計画の対象・版・実行識別が一致しません。");}
            if(記録.Samples.Any(sample=>sample.Node is{} node&&(node.PlanKey!=identity.PlanKey||!実行.Plan.Nodes.Any(item=>item.Id==node.NodeId)))){throw new InvalidDataException("資源記録のノードが別の計画を参照しています。");}
        }
        var query=new List<(VisualizationInterval Value,long From,long To)>();var unknown=0;
        foreach(var interval in 実行?.Intervals??[]){
            if(interval.Evidence!="Observed"){continue;}
            var clock=実行!.Clocks.Single(value=>value.Id==interval.ClockId);
            if(clock.Id==記録.Clock.Id&&clock.TicksPerSecond==記録.Clock.TicksPerSecond){query.Add((interval,interval.StartTicks,interval.EndTicks));}
            else if(clock.Origin=="ExecutionRelativeDerived"&&記録.Clock.AnchorKind=="QueryStart"){
                try{
                    var from=記録.Clock.AnchorTimestamp+(decimal)interval.StartTicks*記録.Clock.TicksPerSecond/clock.TicksPerSecond;
                    var to=記録.Clock.AnchorTimestamp+(decimal)interval.EndTicks*記録.Clock.TicksPerSecond/clock.TicksPerSecond;
                    if(from<long.MinValue||to>long.MaxValue){unknown++;continue;}
                    query.Add((interval,(long)decimal.Round(from),(long)decimal.Round(to)));
                }catch(OverflowException){unknown++;}
            }else{unknown++;}
        }
        var start=記録.Clock.AnchorTimestamp;var end=start;
        foreach(var sample in 記録.Samples){start=Math.Min(start,sample.StartTimestamp);end=Math.Max(end,sample.EndTimestamp);}
        foreach(var interval in query){start=Math.Min(start,interval.From);end=Math.Max(end,interval.To);}
        if(rangeStart.HasValue!=rangeEnd.HasValue||rangeStart<start||rangeEnd>end||rangeEnd<rangeStart){throw new InvalidDataException("選択時間は記録の時間範囲内に指定してください。");}
        if(rangeStart is{} selectedStart&&rangeEnd is{} selectedEnd){start=selectedStart;end=selectedEnd;}
        double X(long value)=>左端+描画幅*(double)(((decimal)value-start)/Math.Max(1m,(decimal)end-start));
        var queryLanes=query.Select(interval=>interval.Value.LaneId).Distinct(StringComparer.Ordinal).ToArray();
        var queryPositions=queryLanes.Select((id,index)=>(id,Y:65+index*28)).ToDictionary(value=>value.id,value=>value.Y,StringComparer.Ordinal);
        var offset=90+queryLanes.Length*28;
        var lanes=記録.Lanes.Where(lane=>lane.Scope==scope&&(kind is null||kind==lane.Kind)).Select((lane,index)=>new 資源レーン配置(lane,offset+index*26)).ToArray();
        var positions=lanes.ToDictionary(lane=>lane.値.Id,StringComparer.Ordinal);
        bool Intersects(long from,long to){if(from==to||start==end){return to>=start&&from<=end;}return to>start&&from<end;}
        var candidates=記録.Samples.Where(sample=>positions.ContainsKey(sample.LaneId)&&Intersects(sample.StartTimestamp,sample.EndTimestamp));
        var samples=candidates.Take(最大描画サンプル).Select(sample=>new 資源サンプル配置(sample,positions[sample.LaneId].値,X(Math.Max(start,sample.StartTimestamp)),X(Math.Min(end,sample.EndTimestamp))-X(Math.Max(start,sample.StartTimestamp)),positions[sample.LaneId].Y)).ToArray();
        var total=candidates.Count();
        return new(start,end,lanes,samples,query.Where(interval=>Intersects(interval.From,interval.To)).Select(interval=>new 資源実行区間配置(interval.Value,X(Math.Max(start,interval.From)),X(Math.Min(end,interval.To))-X(Math.Max(start,interval.From)),queryPositions[interval.Value.LaneId])).ToArray(),Math.Max(180,offset+lanes.Length*26+45),unknown,Math.Max(0,total-samples.Length));
    }
    public static string 色(double percent,string palette){
        var value=Math.Clamp(percent,0,100);
        if(palette=="Mono"){var level=(int)Math.Round(245-value*2);return $"rgb({level},{level},{level})";}
        var stops=new[]{(R:36,G:87,B:165),(R:46,G:151,B:158),(R:65,G:155,B:90),(R:224,G:196,B:64),(R:224,G:124,B:42),(R:193,G:54,B:52)};
        if(palette=="Accessible"){stops=[(68,1,84),(66,65,130),(42,120,142),(34,168,132),(122,209,81),(253,231,37)];}
        var bounds=new[]{0d,20,40,60,80,95};var index=0;
        while(index<stops.Length-1&&value>=bounds[index+1]){index++;}
        if(index==stops.Length-1){return $"rgb({stops[index].R},{stops[index].G},{stops[index].B})";}
        var ratio=0d;if(palette!="Steps"){ratio=(value-bounds[index])/(bounds[index+1]-bounds[index]);}
        var first=stops[index];var second=stops[index+1];
        var r=(int)Math.Round(first.R+(second.R-first.R)*ratio);var g=(int)Math.Round(first.G+(second.G-first.G)*ratio);var b=(int)Math.Round(first.B+(second.B-first.B)*ratio);
        if(value==100){r=stops[^1].R;g=stops[^1].G;b=stops[^1].B;}
        return $"rgb({r},{g},{b})";
    }
}
