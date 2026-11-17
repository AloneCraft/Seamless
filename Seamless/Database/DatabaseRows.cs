using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using Seamless.Collections.Iteration;
using Seamless.Execution;
using Seamless.Transactions.Collections;
namespace Seamless.Database;

/// <summary>生成query用の行ソース。版付きGatewayは所有取引へ、従来方式は既存の読取保護へ委譲する。</summary>
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public sealed class DatabaseRows<TKey,TRecord>:IEnumerable<TRecord> where TKey:struct where TRecord:struct{
    private readonly DatabaseContainer database;
    private readonly IEnumerable<TRecord> source;
    private readonly TransactionRows<TKey,TRecord>? versioned;
    internal DatabaseRows(DatabaseContainer database,IEnumerable<TRecord> source,TransactionRows<TKey,TRecord>? versioned){this.database=database;this.source=source;this.versioned=versioned;}
    public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IStrategy<TStrategy,TRecord>{
        if(this.versioned is{} rows){rows.Iterate(ref strategy);return;}
        using var read=this.database.BeginReadSession();using var iterator=this.source.GetEnumerator();
        while(iterator.MoveNext()){
            this.database.ValidateQueryAccess();var more=TStrategy.Invoke(ref strategy,iterator.Current);this.database.ValidateQueryAccess();if(!more){break;}
        }
    }
    public void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,IParallelStrategy<TStrategy,TRecord>{
        if(this.versioned is{} rows){rows.IterateParallel(ref strategy);return;}
        // 従来方式の列挙子とoverlayはworkerへ共有しない。
        this.Iterate(ref strategy);
    }
    public long GetLongCount(){
        if(this.versioned is{} rows){var count=new CountStrategy();rows.IterateParallel(ref count);return count.Value;}
        // 従来方式だけは、読取保護下で自己overlayを反映する既存の論理件数を利用できる。
        return this.database.WithReadGate(()=>{
            if(this.source is StoredTable<TKey,TRecord> table){return table.LongCount;}
            if(this.source is MaterializedView<TKey,TRecord> view){return view.LongCount;}
            long count=0;foreach(var row in this.source){count=checked(count+1);}return count;
        });
    }
    private struct CountStrategy:IParallelStrategy<CountStrategy,TRecord>{
        internal long Value;
        public static bool Invoke(ref CountStrategy self,TRecord row){self.Value=checked(self.Value+1);return true;}
        public static CountStrategy CreatePartial(in CountStrategy seed)=>new();
        public static void Merge(ref CountStrategy result,in CountStrategy partial)=>result.Value=checked(result.Value+partial.Value);
    }
    public TResult ExecuteQuery<TResult>(Func<TResult> query){
        ArgumentNullException.ThrowIfNull(query);if(this.versioned is{} rows){return rows.ExecuteQuery(query);}return this.database.WithReadGate(query);
    }
    public ExecutionScope CreateQueryScope(){
        if(this.versioned is{} rows){return rows.CreateQueryScope();}this.database.ValidateQueryAccess();return this.database.ExecutionResources.CreateChild(this.database.ValidateQueryAccess);
    }
    public QueryResultBuffer<TResult> CreateResultBuffer<TResult>(){
        if(this.versioned is{} rows){return rows.CreateResultBuffer<TResult>();}return new(this.database.ExecutionResources,this.database.ValidateQueryAccess);
    }
    public QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull{
        if(this.versioned is{} rows){return rows.CreateDictionaryBuffer<TResultKey,TResultValue>(comparer);}return new(this.database.ExecutionResources,comparer,this.database.ValidateQueryAccess);
    }
    public IEnumerator<TRecord> GetEnumerator(){if(this.versioned is{} rows){return rows.GetEnumerator();}return this.source.GetEnumerator();}
    global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
