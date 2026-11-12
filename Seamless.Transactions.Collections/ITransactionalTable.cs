using Seamless.Collections.Iteration;
namespace Seamless.Transactions.Collections;

public interface ITransactionalTable<TKey,TRecord>:ITransactionParticipant where TKey:notnull{
    TransactionCollection<TKey,TRecord> For(TransactionScope scope);
}
internal interface ITableChanges<TKey,TRecord>:ITransactionChanges where TKey:notnull{
    (bool Found,TRecord Row) Read(TKey key);
    void Set(TKey key,TRecord row);
    bool Add(TRecord row);
    bool Remove(TKey key);
    IEnumerable<TRecord> Rows();
    void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IStrategy<TStrategy,TRecord>;
    void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IParallelStrategy<TStrategy,TRecord>;
    global::Seamless.Execution.QueryResultBuffer<TResult> CreateResultBuffer<TResult>();
    global::Seamless.Execution.ExecutionScope CreateQueryScope();
    global::Seamless.Execution.QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull;
}

/// <summary>共通演算最適化が認識する取引行ソース。件数はsnapshotと私有変更を反映して走査する。</summary>
public sealed class TransactionRows<TKey,TRecord>(TransactionCollection<TKey,TRecord> collection):IEnumerable<TRecord> where TKey:notnull{
    /// <summary>生成器が証明したキー関数を、読取り専用取引が実際に使う索引と照合する。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public bool TryGetJoinIndex(Func<TRecord,TKey> expectedKey,out JoinIndex index){
        if(collection.MatchesReadOnlyJoinIndex(expectedKey)){
            index=new(collection);
            return true;
        }
        index=default;
        return false;
    }
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public readonly struct JoinIndex{
        private readonly TransactionCollection<TKey,TRecord> source;
        internal JoinIndex(TransactionCollection<TKey,TRecord> source)=>this.source=source;
        public bool TryGetValue(TKey key,out TRecord row)=>this.source.TryGetValue(key,out row);
    }
    public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IStrategy<TStrategy,TRecord>{collection.Iterate(ref strategy);}
    public void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IParallelStrategy<TStrategy,TRecord>{collection.IterateParallel(ref strategy);}
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public global::Seamless.Execution.QueryResultBuffer<TResult> CreateResultBuffer<TResult>()=>collection.CreateResultBuffer<TResult>();
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public global::Seamless.Execution.ExecutionScope CreateQueryScope()=>collection.CreateQueryScope();
    /// <summary>queryのproducer/worker/合成/後始末を、一つの取引operation内で完了させる。</summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public TResult ExecuteQuery<TResult>(Func<TResult> query)=>collection.ExecuteQuery(query);
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public global::Seamless.Execution.QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull{
        return collection.CreateDictionaryBuffer<TResultKey,TResultValue>(comparer);
    }
    public IEnumerator<TRecord> GetEnumerator()=>collection.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
