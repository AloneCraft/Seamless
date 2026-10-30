using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.UI;

/// <summary>外部 run の設定。実行 ID はアプリ側で発行し、保存履歴の再利用を受け付けない。</summary>
public sealed record 動的外部実行設定{
    public required int Query数{get;init;}
    public required int N{get;init;}
    public required int 入力要素数{get;init;}
    public required int 最大深度{get;init;}
    public required string Seed{get;init;}
    public required OperatorSetMode 演算子集合モード{get;init;}
    public required SourceTarget[] 対象一覧{get;init;}
    public required QueryElementType 要素型{get;init;}
    public required int 最適化フラグ{get;init;}
    public required WorkerMeasurementProfile 計測設定{get;init;}
    public required WorkerStorageSettings ストレージ設定{get;init;}
    public 動的実行設定 実行設定を作る(){
        if(!Seed256.TryParse(this.Seed,out var シード)||this.最適化フラグ is <0 or >15||this.計測設定 is null||this.ストレージ設定 is null){
            throw new ArgumentException("Seed・最適化フラグ・計測設定・ストレージ設定を明示してください。");
        }
        var 設定=new 動的実行設定(Guid.NewGuid().ToString("N"),this.Query数,this.N,this.入力要素数,this.最大深度,シード,this.演算子集合モード,this.対象一覧,計測ProfileOverride:this.計測設定,要素型:this.要素型,最適化フラグ:this.最適化フラグ,ストレージ設定:this.ストレージ設定);
        _=動的実行調停サービス.実行設定を検証(設定);
        return 設定;
    }
}

public sealed class 動的外部検証集計(int 予定数){
    private int 完了数;
    private int 失敗数;
    public void 結果を追加(WorkerExecutionStatus 状態,bool 意味一致か,bool 固定点到達か){
        Interlocked.Increment(ref this.完了数);
        if(状態!=WorkerExecutionStatus.Succeeded||!意味一致か||!固定点到達か){Interlocked.Increment(ref this.失敗数);}
    }
    public string 検証結果を取得(string 状態){
        if(状態 is "Canceled" or "Cancelled"){return "Unavailable";}
        if(this.失敗数!=0){return "Failed";}
        if(状態!="Completed"||this.完了数==0){return "Unavailable";}
        if(this.完了数!=予定数){return "Inconclusive";}
        return "Passed";
    }
}

public sealed class 動的アプリ操作サービス(動的ベンチマーク画面モデル 画面モデル):Iアプリ操作サービス{
    public static readonly JsonSerializerOptions 設定JSON=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter()}};
    public IReadOnlyList<string> 操作一覧=>["run"];
    public Task<アプリ操作準備?> 準備Async(string 操作,JsonElement 設定)=>MainThread.InvokeOnMainThreadAsync<アプリ操作準備?>(()=>{
        if(操作!="run"){throw new InvalidDataException("未対応の操作です。");}
        var 要求=設定.Deserialize<動的外部実行設定>(設定JSON)??throw new InvalidDataException("実行設定が必要です。");
        var 実行設定=要求.実行設定を作る();
        var 予約=画面モデル.実行排他.予約する();
        if(予約 is null){return null;}
        return new アプリ操作準備(予約,取消=>MainThread.InvokeOnMainThreadAsync(()=>画面モデル.外部実行Async(実行設定,取消)));
    });
}
