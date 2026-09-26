using Seamless.Collections.Tsavorite;

namespace Seamless.TestKit;

/// <summary>共通メモリストアと辞書のデータ本体を所有する。NewDict で作ったアクセサは呼び出し側が先に破棄する。</summary>
public sealed class TunedDictionaryHandle<TKey,TValue>:IDisposable
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    private readonly global::Seamless.Execution.Store owner;
    private readonly DictionaryStore<TKey,TValue> data;
    private int disposed;

    private TunedDictionaryHandle(global::Seamless.Execution.Store owner,DictionaryStore<TKey,TValue> data){
        this.owner=owner;
        this.data=data;
    }

    internal static TunedDictionaryHandle<TKey,TValue> Create(TsavoriteTuning tuning,global::Seamless.Execution.StoreLimits limits){
        var owner=global::Seamless.Execution.Store.CreateMemory(limits);
        try{
            var data=owner.CreateTsavorite<TKey,TValue>("scalable-dictionary",tuning);
            return new TunedDictionaryHandle<TKey,TValue>(owner,data);
        }catch{
            owner.Dispose();
            throw;
        }
    }

    /// <summary>呼び出したスレッド専用のアクセサを作る。共通ストアより先に破棄すること。</summary>
    public global::Seamless.Collections.Tsavorite.IDictionary<TKey,TValue> NewDict(){
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed)!=0,this);
        return this.data.Create();
    }

    public void Dispose(){
        if(Interlocked.Exchange(ref this.disposed,1)==0){
            this.owner.Dispose();
        }
    }
}
