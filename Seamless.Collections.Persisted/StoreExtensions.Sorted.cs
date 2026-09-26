namespace Seamless.Collections.Persisted;
public static partial class StoreExtensions{
    /// <summary>明示確定する集合を作る。文字列の既定比較は Ordinal。永続カスタム比較には安定した comparisonId が必要。</summary>
    public static Work.SortedSet<T> CreatePersistedSortedSet<T>(this global::Seamless.Execution.Store store,string name,IComparer<T>? comparer=null,string? comparisonId=null,long? sortMemoryBytes=null,StorageTuning? tuning=null)
        =>new(store,name,comparer,comparisonId,sortMemoryBytes,tuning);
    /// <summary>重複行を保持する外部ソート用リストを作る。</summary>
    public static Work.SortedList<T> CreatePersistedSortedList<T>(this global::Seamless.Execution.Store store,string name,IComparer<T>? comparer=null,string? comparisonId=null,long? sortMemoryBytes=null,StorageTuning? tuning=null)
        =>new(store,name,comparer,comparisonId,sortMemoryBytes,tuning);
    /// <summary>同値キーへの最後の Add を採用する辞書を作る。検索前に CompleteAdding が必要。</summary>
    public static Work.SortedDictionary<TKey,TValue> CreatePersistedSortedDictionary<TKey,TValue>(this global::Seamless.Execution.Store store,string name,IComparer<TKey>? comparer=null,string? comparisonId=null,long? sortMemoryBytes=null,StorageTuning? tuning=null) where TKey:notnull
        =>new(store,name,comparer,comparisonId,sortMemoryBytes,tuning);
}
