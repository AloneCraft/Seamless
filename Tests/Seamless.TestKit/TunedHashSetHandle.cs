using Seamless.Collections.Tsavorite;

namespace Seamless.TestKit;

/// <summary>共通メモリストアと、そのストアから作った単一スレッド用アクセサを所有する。</summary>
public sealed class TunedHashSetHandle<T>:IDisposable
    where T:IEquatable<T>{
    private readonly global::Seamless.Execution.Store owner;
    private int disposed;

    private TunedHashSetHandle(global::Seamless.Execution.Store owner,global::Seamless.Collections.Tsavorite.HashSet<T> set){
        this.owner=owner;
        this.Set=set;
    }

    public global::Seamless.Collections.Tsavorite.HashSet<T> Set{get;}

    internal static TunedHashSetHandle<T> Create(TsavoriteTuning tuning,global::Seamless.Execution.StoreLimits limits){
        var owner=global::Seamless.Execution.Store.CreateMemory(limits);
        try{
            var data=owner.CreateTsavoriteHashSet<T>("scalable-set",tuning);
            return new TunedHashSetHandle<T>(owner,data.Create());
        }catch{
            owner.Dispose();
            throw;
        }
    }

    /// <summary>アクセサを先に破棄し、その後に共通ストアとデータ本体を破棄する。</summary>
    public void Dispose(){
        if(Interlocked.Exchange(ref this.disposed,1)!=0){
            return;
        }
        try{
            this.Set.Dispose();
        }finally{
            this.owner.Dispose();
        }
    }
}
