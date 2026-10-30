using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>書き込みの記録(L2・L3 用)。<see cref="Partition"/> は書いた区画。</summary>
public readonly record struct TpcCWriteRecord(int Partition,int TableId,WriteKind Kind,object Key,object? Row);
/// <summary>区画ごとの取り消し記録に付けて、書き込みを組み合わせごとの 1 本の列に積む(計測の内側では積むだけ)。</summary>
internal sealed class TpcCPartitionRecorder(int partition,List<TpcCWriteRecord> sink):IWriteObserver{
    public void OnWrite(int tableId,WriteKind kind,object key,object? row)=>sink.Add(new(partition,tableId,kind,key,row));
}
/// <summary>1 つの組み合わせ(または区画 1 つのシャドー)の実行中の資源と計測。</summary>
public sealed class TpcCVariantSession(ITpcCVariant variant,string name,string shortName,TpcCScale scale,string directory){
    public ITpcCVariant Variant{get;}=variant;
    public string Name{get;}=name;
    public string ShortName{get;}=shortName;
    public TpcCScale Scale{get;}=scale;
    public string Directory{get;}=directory;
    internal bool ReopenTampered{get;set;}
    public Store? Store{get;set;}
    public ITpcCEngine? Engine{get;set;}
    public List<TpcCWriteRecord> Records{get;}=[];
    public Dictionary<TpcCTxnKind,LatencyHistogram> Timings{get;}=[];
    public Dictionary<string,double> PhaseSeconds{get;}=new(StringComparer.Ordinal);
    public ITpcCEngine RequireEngine=>this.Engine??throw new InvalidOperationException($"{this.Name} is not open.");
    public void AddPhase(string phase,double seconds){
        this.PhaseSeconds[phase]=this.PhaseSeconds.GetValueOrDefault(phase)+seconds;
    }
    /// <summary>全区画の取り消し記録に、書き込みを <see cref="Records"/> へ積む観察者を付ける。</summary>
    public void AttachRecorders(){
        var partitions=this.RequireEngine.Database.PartitionBases;
        for(var index=0;index<partitions.Count;index++){
            partitions[index].Log.Observer=new TpcCPartitionRecorder(index,this.Records);
        }
    }
}
