using System.Numerics;
using Seamless.Management;

namespace Seamless.Management.Components;

public sealed record 実行区間配置(VisualizationInterval 値,double X,double 幅,int Y);
public sealed record 実行イベント配置(VisualizationEvent 値,double X,int Y);
public sealed record 実行レーン配置(string ID,string 種類,int Y);
public sealed record 実行時計配置(VisualizationClock 時計,long 開始Ticks,long 終了Ticks,実行レーン配置[] レーン,実行区間配置[] 区間,実行イベント配置[] イベント,int 高さ,BigInteger タスク合計Ticks){
    public long 経過Ticks=>this.終了Ticks-this.開始Ticks;
    public double 位置(long 値)=>実行時間モデル.左端+実行時間モデル.描画幅*((double)値-this.開始Ticks)/Math.Max(1,this.経過Ticks);
}

/// <summary>時計原点ごとの表示配置。包含区間を独立タスクの時間へ加算しない。</summary>
public static class 実行時間モデル{
    public const int 左端=175,描画幅=760,幅=980,最大区間=2048,最大イベント=4096;

    public static QueryVisualizationData 実測を投影(QueryVisualizationData 元,ObjectExecutionStatistics 実測,QueryExecutionObservation? 観測=null){
        if(実測.ExecutionId==Guid.Empty||実測.Elapsed<TimeSpan.Zero||実測.DatabaseGateWait<TimeSpan.Zero||実測.DatabaseGateWait>実測.Elapsed||実測.GateWaitStartedOffset<TimeSpan.Zero||実測.GateWaitStartedOffset>実測.Elapsed-実測.DatabaseGateWait||実測.TimeToFirstOutput<TimeSpan.Zero||実測.TimeToFirstOutput>実測.Elapsed){throw new InvalidDataException("実測の時間範囲が不正です。");}
        元.Validate();
        if(元.Identity.ExecutionId!=実測.ExecutionId||元.Identity.DefinitionId!=実測.DefinitionId||元.Identity.ObjectId!=実測.ObjectId||元.Identity.Version!=実測.Version||元.Identity.ArtifactHash!=実測.ArtifactHash){throw new InvalidDataException("可視化と実測の対象・版・実行識別が一致しません。");}
        if(観測 is not null&&(観測.ExecutionId!=実測.ExecutionId||観測.DefinitionId!=実測.DefinitionId||観測.ObjectId!=実測.ObjectId||観測.Version!=実測.Version||観測.ArtifactHash!=実測.ArtifactHash||!観測.IdentityResolved||観測.Elapsed!=実測.Elapsed||観測.DatabaseGateWait!=実測.DatabaseGateWait||観測.GateWaitStartedOffset!=実測.GateWaitStartedOffset)){throw new InvalidDataException("終端観測と実測が一致しません。");}
        var ID=実測.ExecutionId;
        var 時計ID="execution-relative:"+ID.ToString("N");
        var 状態=観測?.Outcome??"Succeeded";
        var 完全か=!実測.IsPartial&&状態=="Succeeded";
        var 待機開始=実測.GateWaitStartedOffset??TimeSpan.Zero;
        var 待機終了=待機開始+実測.DatabaseGateWait;
        var 時計=new VisualizationClock(時計ID,ID,TimeSpan.TicksPerSecond,"ExecutionRelativeDerived",実測.StartedAtUtc);
        var 区間=new[]{
            new VisualizationInterval("query",ID,"query",null,時計ID,"query","Query",0,実測.Elapsed.Ticks,"QueryInclusive","Observed",状態,完全か),
            new VisualizationInterval("gate",ID,"query",null,時計ID,"gate","Wait",待機開始.Ticks,待機終了.Ticks,"DatabaseGateWait","Observed",状態,true)
        };
        var 点=new List<VisualizationEvent>{new("start",ID,"query",null,時計ID,"query",0,"Started")};
        if(観測 is null||観測.Phase!="DatabaseGate"){点.Add(new("gate",ID,"query",null,時計ID,"gate",待機終了.Ticks,"GateAcquired"));}
        if(実測.TimeToFirstOutput is{} 初回){点.Add(new("first",ID,"query",null,時計ID,"query",初回.Ticks,"FirstOutput"));}
        点.Add(new("end",ID,"query",null,時計ID,"query",実測.Elapsed.Ticks,"Completed"));
        var 結果=元 with{Clocks=[時計],Intervals=区間,Events=点.ToArray()};結果.Validate();return 結果;
    }

    public static 実行時計配置[] 作成(QueryVisualizationData 値){
        値.Validate();
        var 結果=new List<実行時計配置>();
        var 区間=値.Intervals.Where(項目=>項目.Evidence=="Observed").Take(最大区間).ToArray();
        var 点=値.Events.Where(項目=>項目.Evidence=="Observed").Take(最大イベント).ToArray();
        foreach(var 時計 in 値.Clocks){
            var 時計区間=区間.Where(項目=>項目.ClockId==時計.Id).ToArray();
            var 時計点=点.Where(項目=>項目.ClockId==時計.Id).ToArray();
            if(時計区間.Length==0&&時計点.Length==0){continue;}
            var 時刻=時計区間.SelectMany(項目=>new[]{項目.StartTicks,項目.EndTicks}).Concat(時計点.Select(項目=>項目.AtTicks)).ToArray();
            var 開始=時刻.Min();var 終了=時刻.Max();
            var レーンID=時計区間.Select(項目=>項目.LaneId).Concat(時計点.Select(項目=>項目.LaneId)).Distinct(StringComparer.Ordinal).ToArray();
            var レーン=レーンID.Select((ID,番号)=>new 実行レーン配置(ID,時計区間.FirstOrDefault(項目=>項目.LaneId==ID)?.LaneKind??"Event",70+番号*44)).ToArray();
            var 行位置=レーン.ToDictionary(項目=>項目.ID,項目=>項目.Y,StringComparer.Ordinal);
            double X(long 時間)=>左端+描画幅*((double)時間-開始)/Math.Max(1,終了-開始);
            var 配置区間=時計区間.Select(項目=>new 実行区間配置(項目,X(項目.StartTicks),描画幅*(項目.EndTicks-(double)項目.StartTicks)/Math.Max(1,終了-開始),行位置[項目.LaneId])).ToArray();
            var 配置点=時計点.Select(項目=>new 実行イベント配置(項目,X(項目.AtTicks),行位置[項目.LaneId])).ToArray();
            // TaskExecutionは実際の独立タスク区間だけ。query/gateの包含時間は除外する。
            var 合計=時計区間.Where(項目=>項目.Scope=="TaskExecution").Aggregate(BigInteger.Zero,(合算,項目)=>合算+項目.EndTicks-項目.StartTicks);
            結果.Add(new(時計,開始,終了,レーン,配置区間,配置点,110+レーン.Length*44,合計));
        }
        return 結果.ToArray();
    }
}
