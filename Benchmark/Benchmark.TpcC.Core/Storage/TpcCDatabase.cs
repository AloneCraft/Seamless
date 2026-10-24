using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Core;
/// <summary>
/// TPC-C の DB 全体 = 区画の配列。倉庫 w の行は <see cref="For"/> が返す区画にある。区画の数が 1 のときも同じ形(1 区画が全倉庫を持つ)。
/// 区画ごとの Store を渡せば区画ごとに独立して保存・復旧でき、同じ Store を渡せば 1 回の保存で全区画が原子的に確定する。
/// </summary>
public class TpcCDatabase<TPartition>:IDisposable where TPartition:TpcCPartitionBase{
    private readonly TPartition[] partitions;
    private bool disposed;
    public TpcCDatabase(TpcCScale scale,Func<int,TPartition> create){
        this.Scale=scale;
        this.partitions=new TPartition[scale.PartitionCount];
        for(var partition=0;partition<this.partitions.Length;partition++){
            this.partitions[partition]=create(partition);
        }
    }
    public TpcCScale Scale{get;}
    public IReadOnlyList<TPartition> Partitions=>this.partitions;
    public TPartition Partition(int partition)=>this.partitions[partition];
    /// <summary>倉庫 w(1 始まり)を持つ区画。</summary>
    public TPartition For(int warehouse)=>this.partitions[this.Scale.PartitionOf(warehouse)];
    /// <summary>全区画の Tsavorite の操作用オブジェクトを閉じる(保存で件数を記録させる経路を通すため)。</summary>
    public void ReleaseHandles(){
        foreach(var partition in this.partitions){
            partition.ReleaseHandles();
        }
    }
    public void AcquireHandles(){
        foreach(var partition in this.partitions){
            partition.AcquireHandles();
        }
    }
    public void BeginBulkLoad(){
        foreach(var partition in this.partitions){
            partition.BeginBulkLoad();
        }
    }
    public void EndBulkLoad(){
        foreach(var partition in this.partitions){
            partition.EndBulkLoad();
        }
    }
    public void Dispose(){
        if(this.disposed){
            return;
        }
        this.disposed=true;
        foreach(var partition in this.partitions){
            partition.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
/// <summary>Tsavorite に置いた TPC-C の DB。</summary>
public sealed class TsavoriteTpcCDatabase:TpcCDatabase<TsavoriteTpcCPartition>{
    /// <summary>全区画を 1 つの Store に置く(保存が原子的)。</summary>
    public TsavoriteTpcCDatabase(Store store,TpcCScale scale):base(scale,partition=>new TsavoriteTpcCPartition(store,scale,partition)){
    }
    /// <summary>区画ごとに別の Store に置く。</summary>
    public TsavoriteTpcCDatabase(IReadOnlyList<Store> stores,TpcCScale scale):base(scale,PerPartition(stores,scale)){
    }
    private static Func<int,TsavoriteTpcCPartition> PerPartition(IReadOnlyList<Store> stores,TpcCScale scale){
        if(stores.Count!=scale.PartitionCount){
            throw new ArgumentException("one store per partition is required.",nameof(stores));
        }
        return partition=>new TsavoriteTpcCPartition(stores[partition],scale,partition);
    }
}
