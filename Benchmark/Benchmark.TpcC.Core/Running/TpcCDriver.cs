using System.Text.Json;
using Seamless.Execution;
namespace Benchmark.TpcC.Core;
/// <summary>運転の状態。取引の通し番号と、種類ごとの件数。ストアの子資源 <c>~driver</c> として保存と一緒に確定する(強制終了の検査で、復旧した位置を知るため)。</summary>
public sealed class TpcCDriverState{
    public ulong Seed{get;set;}
    /// <summary>これまでに流した取引の件数 = 最後に流した取引の通し番号(1 始まり)。</summary>
    public long TxnIndex{get;set;}
    public Dictionary<string,long> Counts{get;set;}=[];
    public TpcCDriverState DeepClone()=>new(){Seed=this.Seed,TxnIndex=this.TxnIndex,Counts=new Dictionary<string,long>(this.Counts)};
}
internal static class TpcCDriverJson{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=false};
}
public sealed class TpcCDriverStateComponent(Func<TpcCDriverState> current,Action<TpcCDriverState> restore):IStoreComponent{
    public const string ComponentName="~driver";
    public string Name=>ComponentName;
    public string Save()=>JsonSerializer.Serialize(current(),TpcCDriverJson.Options);
    public void Flush(){
    }
    public void Load(string state)=>restore(JsonSerializer.Deserialize<TpcCDriverState>(state,TpcCDriverJson.Options)!);
    public void Dispose(){
    }
    public static string Serialize(TpcCDriverState state)=>JsonSerializer.Serialize(state,TpcCDriverJson.Options);
}
/// <summary>
/// 運転側。通し番号 i の取引は、端末 i mod T の (i div T) 番目の入力で、日時は i 秒目に決まる(状態を持たず、番号だけから再現できる)。
/// 組み合わせの結果は運転に影響しない(TPC-E と違い MEE の待ち行列が無い)ので、結果は件数の集計にだけ使う。
/// </summary>
public sealed class TpcCDriver{
    private readonly TpcCInputGenerator[] terminals;
    private readonly long baseTime;
    public TpcCDriver(TpcCScale scale,TpcCMode mode,ulong seed,int terminalCount,TpcCDriverState state){
        this.State=state;
        var rules=TpcCLoadRules.For(mode);
        var load=new TpcCDataGenerator(scale,rules,seed).LastNameCLoad;
        var constants=TpcCNuRandConstants.For(scale,mode,seed,load);
        this.terminals=new TpcCInputGenerator[terminalCount];
        for(var terminal=0;terminal<terminalCount;terminal++){
            this.terminals[terminal]=new TpcCInputGenerator(scale,mode,seed,terminal,constants);
        }
        this.baseTime=rules.LoadTime+3600;
    }
    public TpcCDriverState State{get;}
    public static TpcCDriverState Initial(ulong seed)=>new(){Seed=seed};
    /// <summary>次の取引の入力を作り、通し番号を進める。</summary>
    public TpcCRequest Next(){
        var index=this.State.TxnIndex;
        this.State.TxnIndex++;
        return this.terminals[(int)(index%this.terminals.Length)].Next(index/this.terminals.Length,this.baseTime+index);
    }
    public void Completed(TpcCRequest request,TpcCResult result){
        var key=request.Kind.ToString();
        if(result.RolledBack){
            key+=":rolledback";
        }
        this.State.Counts[key]=this.State.Counts.GetValueOrDefault(key)+1;
    }
}
