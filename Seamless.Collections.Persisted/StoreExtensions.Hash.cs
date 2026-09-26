namespace Seamless.Collections.Persisted;

public static partial class StoreExtensions{
    /// <summary>完了操作を必要としない作業用集合を作る。comparerId は比較器の設定を区別する、再 open 時も同じ識別子。</summary>
    public static Work.HashSet<T> CreatePersistedHashSet<T>(this global::Seamless.Execution.Store store,string name,IEqualityComparer<T>? comparer=null,string? comparerId=null,StorageTuning? tuning=null){
        ArgumentNullException.ThrowIfNull(store);
        comparer??=EqualityComparer<T>.Default;
        var identity=HashComparerIdentity(typeof(T),null,comparer,comparerId);
        return new(store,name,comparer,identity,tuning);
    }
    /// <summary>同じキーへの Add が後勝ちになる作業用辞書を作る。比較器の設定を変える場合は comparerId も変える。</summary>
    public static Work.HashDictionary<TKey,TValue> CreatePersistedHashDictionary<TKey,TValue>(this global::Seamless.Execution.Store store,string name,IEqualityComparer<TKey>? comparer=null,string? comparerId=null,StorageTuning? tuning=null) where TKey:notnull{
        ArgumentNullException.ThrowIfNull(store);
        comparer??=EqualityComparer<TKey>.Default;
        var identity=HashComparerIdentity(typeof(TKey),typeof(TValue),comparer,comparerId);
        return new(store,name,comparer,identity,tuning);
    }
    private static string HashComparerIdentity(Type key,Type? value,object comparer,string? id){
        return string.Join("|",key.AssemblyQualifiedName,value?.AssemblyQualifiedName,comparer.GetType().AssemblyQualifiedName,id??string.Empty);
    }
}
