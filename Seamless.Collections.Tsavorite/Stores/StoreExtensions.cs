namespace Seamless.Collections.Tsavorite;
/// <summary>
/// 共通ストア(<see cref="global::Seamless.Execution.Store"/>)から Tsavorite のデータ本体を作る。
/// デバイス・保存フォルダ・大きさはストアの種類と上限から自動で決まり、データ本体はストアの子資源として登録される。
/// 永続ストアで同じ名前を保存済みなら、登録時にその状態へ復元される。
/// </summary>
public static class StoreExtensions{
    /// <summary>Tsavorite の辞書のデータ本体を作る。<paramref name="expectedCount"/> はプロファイルの適用条件と拡張時の推定に使う。Minimumの初期容量は増やさない。</summary>
    public static DictionaryStore<TKey,TValue> CreateTsavorite<TKey,TValue>(this global::Seamless.Execution.Store store,string name,long expectedCount=0)=>
        CreateTsavorite<TKey,TValue>(store,name,TsavoriteTuning.Default,expectedCount);
    /// <summary>Tsavorite のハッシュセットのデータ本体を作る。<paramref name="expectedCount"/> はプロファイルの適用条件と拡張時の推定に使う。Minimumの初期容量は増やさない。</summary>
    public static HashSetStore<T> CreateTsavoriteHashSet<T>(this global::Seamless.Execution.Store store,string name,long expectedCount=0)=>
        CreateTsavoriteHashSet<T>(store,name,TsavoriteTuning.Default,expectedCount);
    /// <summary>キーを持つレコードの集合(<see cref="Set{TKey,TRecord}"/>)のデータ本体を作る。</summary>
    public static SetStore<TKey,TRecord> CreateTsavoriteSet<TKey,TRecord>(this global::Seamless.Execution.Store store,string name,long expectedCount=0)
        where TRecord:IRecord<TKey>{
        return CreateTsavoriteSet<TKey,TRecord>(store,name,TsavoriteTuning.Default,expectedCount);
    }
    /// <summary>指定した詳細設定で、キーを持つレコードの集合のデータ本体を作る。</summary>
    public static SetStore<TKey,TRecord> CreateTsavoriteSet<TKey,TRecord>(this global::Seamless.Execution.Store store,string name,TsavoriteTuning tuning,long expectedCount=0)
        where TRecord:IRecord<TKey>{
        var layout=TsavoriteLayout.Resolve(store,name,expectedCount,tuning,typeof(TRecord).FullName??typeof(TRecord).Name);
        SetStore<TKey,TRecord>? data=null;
        try{
            data=new SetStore<TKey,TRecord>(layout);
            return store.Register(data);
        }catch{
            if(data is not null){
                data.Dispose();
            }else{
                layout.Dispose();
            }
            throw;
        }
    }
    /// <summary>指定した詳細設定で、Tsavorite の辞書のデータ本体を作る。</summary>
    public static DictionaryStore<TKey,TValue> CreateTsavorite<TKey,TValue>(this global::Seamless.Execution.Store store,string name,TsavoriteTuning tuning,long expectedCount=0){
        var layout=TsavoriteLayout.Resolve(store,name,expectedCount,tuning,$"{typeof(TKey).FullName}|{typeof(TValue).FullName}");
        DictionaryStore<TKey,TValue>? data=null;
        try{
            data=new DictionaryStore<TKey,TValue>(layout);
            return store.Register(data);
        }catch{
            if(data is not null){
                data.Dispose();
            }else{
                layout.Dispose();
            }
            throw;
        }
    }
    /// <summary>指定した詳細設定で、Tsavorite のハッシュセットのデータ本体を作る。</summary>
    public static HashSetStore<T> CreateTsavoriteHashSet<T>(this global::Seamless.Execution.Store store,string name,TsavoriteTuning tuning,long expectedCount=0){
        var layout=TsavoriteLayout.Resolve(store,name,expectedCount,tuning,typeof(T).FullName??typeof(T).Name);
        HashSetStore<T>? data=null;
        try{
            data=new HashSetStore<T>(layout);
            return store.Register(data);
        }catch{
            if(data is not null){
                data.Dispose();
            }else{
                layout.Dispose();
            }
            throw;
        }
    }
}
