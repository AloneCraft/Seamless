using Seamless.Execution;
namespace Seamless.Transactions.Collections;
/// <summary>型付き保存先の接続。codecによる値の所有権分離でmutableな参照の迂回更新を防ぐ。</summary>
public sealed class TransactionalTable<TKey,TRecord>:IContextualTransactionParticipant,ITransactionalTable<TKey,TRecord> where TKey:notnull{
    private readonly Func<TKey,(bool Found,TRecord Row)> read;
    private readonly Action<TKey,TRecord> write;
    private readonly Action<TKey> remove;
    private readonly Func<TRecord,TKey> keyOf;
    private readonly Func<IEnumerable<KeyValuePair<TKey,TRecord>>> rows;
    private readonly ExecutionScope resources;
    private readonly long memoryLimit;
    public TransactionalTable(int partition,int tableId,string codecIdentity,ExecutionScope resources,long memoryLimit,
        Func<TKey,(bool Found,TRecord Row)> read,Action<TKey,TRecord> write,Action<TKey> remove,Func<TRecord,TKey> keyOf,Func<IEnumerable<KeyValuePair<TKey,TRecord>>> rows){
        this.Partition=partition;this.TableId=tableId;this.CodecIdentity=codecIdentity;this.resources=resources;this.memoryLimit=memoryLimit;
        this.read=read;this.write=write;this.remove=remove;this.keyOf=keyOf;this.rows=rows;
    }
    public int Partition{get;}
    public int TableId{get;}
    public string CodecIdentity{get;}
    public TransactionCollection<TKey,TRecord> For(TransactionScope scope)=>new(this,scope);
    public ITransactionChanges CreateChanges()=>new Changes(this);
    public ITransactionChanges CreateChanges(TransactionContext context)=>new Changes(this,context);
    public TransactionParticipantSnapshot GetCommittedSnapshot(){
        // 順序の異なる復元でも比較できる。キーを含む各行の hash を和・xor・件数で束ねる。
        using var rowHasher=global::System.Security.Cryptography.IncrementalHash.CreateHash(global::System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> sum=stackalloc byte[32];sum.Clear();Span<byte> xor=stackalloc byte[32];xor.Clear();Span<byte> rowHash=stackalloc byte[32];Span<byte> lengths=stackalloc byte[8];long count=0;
        foreach(var pair in this.rows()){
            this.resources.ThrowIfUnavailable();using var key=this.resources.Encode(pair.Key);using var row=this.resources.Encode(pair.Value);
            global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengths,key.Memory.Length);global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengths[4..],row.Memory.Length);
            rowHasher.AppendData(lengths);rowHasher.AppendData(key.Memory.Span);rowHasher.AppendData(row.Memory.Span);if(!rowHasher.TryGetHashAndReset(rowHash,out _)){throw new InvalidOperationException("Snapshot hash failed.");}
            var carry=0;for(var index=0;index<32;index++){xor[index]^=rowHash[index];var value=sum[index]+rowHash[index]+carry;sum[index]=unchecked((byte)value);carry=value>>8;}count=checked(count+1);
        }
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(lengths,count);rowHasher.AppendData(lengths);rowHasher.AppendData(global::System.Text.Encoding.UTF8.GetBytes(this.CodecIdentity));rowHasher.AppendData(sum);rowHasher.AppendData(xor);
        return new(count,Convert.ToHexString(rowHasher.GetHashAndReset()));
    }
    public void Replay(WalEntry entry){
        if(entry.Partition!=this.Partition||entry.TableId!=this.TableId||entry.Codec!=this.CodecIdentity){throw new InvalidDataException("Collection identity mismatch.");}
        var key=this.resources.Codecs.Get<TKey>().Decode(entry.Key);
        if(entry.Mutation==WalMutation.Delete){this.remove(key);return;}
        if(entry.Row is null){throw new InvalidDataException("Missing row.");}
        var row=this.resources.Codecs.Get<TRecord>().Decode(entry.Row);
        if(!EqualityComparer<TKey>.Default.Equals(key,this.keyOf(row))){throw new InvalidDataException("WAL row/key mismatch.");}
        this.write(key,row);
    }
    private TRecord Snapshot(TRecord value){using var bytes=this.resources.Encode(value);return this.resources.Codecs.Get<TRecord>().Decode(bytes.Memory.Span);}
    internal sealed class Changes:ITableChanges<TKey,TRecord>{
        private readonly TransactionalTable<TKey,TRecord> table;
        private readonly TransactionContext? context;
        private TransactionChangeSet<TKey,TRecord> changes;
        private readonly ResourceReservation walMemory;
        public Changes(TransactionalTable<TKey,TRecord> table,TransactionContext? context=null){this.table=table;this.context=context;this.changes=new(table.resources,table.memoryLimit);this.walMemory=table.resources.Budget.ReserveMemory(0);}
        public ITransactionChanges CreateSnapshot(){
            this.context?.ThrowIfUnavailable();
            var snapshot=new Changes(this.table,this.context);
            try{var changes=this.changes.CreateSnapshot();snapshot.changes.Dispose();snapshot.changes=changes;return snapshot;}catch{snapshot.Dispose();throw;}
        }
        public (bool Found,TRecord Row) Read(TKey key){
            this.context?.ThrowIfUnavailable();
            if(this.changes.TryGetValue(key,out var change)){
                if(change.Deleted){return(false,default!);}return(true,this.table.Snapshot(change.Record));
            }
            var current=this.table.read(key);if(current.Found){return(true,this.table.Snapshot(current.Row));}return current;
        }
        public void Set(TKey key,TRecord row){
            this.context?.EnsureWritable();
            if(!EqualityComparer<TKey>.Default.Equals(key,this.table.keyOf(row))){throw new ArgumentException("Record key does not match the indexer key.",nameof(row));}
            this.changes.Set(key,new(false,this.table.Snapshot(row)));
        }
        public bool Add(TRecord row){this.context?.EnsureWritable();var key=this.table.keyOf(row);if(this.Read(key).Found){return false;}this.Set(key,row);return true;}
        public bool Remove(TKey key){this.context?.EnsureWritable();if(!this.Read(key).Found){return false;}this.changes.Set(key,new(true,default!));return true;}
        public IEnumerable<TRecord> Rows(){
            this.context?.ThrowIfUnavailable();
            foreach(var pair in this.table.rows()){
                var current=this.Read(pair.Key);if(current.Found){yield return current.Row;}
            }
            foreach(var pair in this.changes.Enumerate()){
                if(!pair.Value.Deleted&&!this.table.read(pair.Key).Found){yield return this.table.Snapshot(pair.Value.Record);}
            }
        }
        public void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IStrategy<TStrategy,TRecord>{
            foreach(var row in this.Rows()){this.context?.ThrowIfUnavailable();if(!TStrategy.Invoke(ref strategy,row)){return;}this.context?.ThrowIfUnavailable();}
        }
        public void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IParallelStrategy<TStrategy,TRecord>{this.Iterate(ref strategy);}
        public QueryResultBuffer<TResult> CreateResultBuffer<TResult>(){
            Action? validate=null;if(this.context is not null){validate=this.context.ThrowIfUnavailable;}return new(this.table.resources,validate);
        }
        public ExecutionScope CreateQueryScope(){Action? validate=null;if(this.context is not null){validate=this.context.ThrowIfUnavailable;}return this.table.resources.CreateChild(validate);}
        public QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull{
            Action? validate=null;if(this.context is not null){validate=this.context.ThrowIfUnavailable;}return new(this.table.resources,comparer,validate);
        }
        public IReadOnlyList<WalEntry> Encode()=>this.EncodeBounded(int.MaxValue);
        public IEnumerable<WalEntry> EnumerateEncoded(){
            try{
                foreach(var pair in this.changes.Enumerate()){
                    using var key=this.table.resources.Encode(pair.Key);byte[]? row=null;
                    if(!pair.Value.Deleted){using var encoded=this.table.resources.Encode(pair.Value.Record);this.walMemory.Resize(checked(key.Memory.Length+encoded.Memory.Length+128L));row=encoded.Memory.ToArray();}
                    else{this.walMemory.Resize(checked(key.Memory.Length+128L));}
                    yield return new(this.table.Partition,this.table.TableId,pair.Value.Deleted?WalMutation.Delete:WalMutation.Upsert,this.table.CodecIdentity,key.Memory.ToArray(),row);
                }
            }finally{this.walMemory.Resize(0);}
        }
        public IReadOnlyList<WalEntry> EncodeBounded(int maximumBytes){
            var entries=new List<WalEntry>();long bytes=0;this.walMemory.Resize(0);
            foreach(var pair in this.changes.Enumerate()){
                using var key=this.table.resources.Encode(pair.Key);byte[]? row=null;
                void Reserve(int rowBytes){
                    bytes=checked(bytes+WalFormat.EntryPayloadBytes(this.table.CodecIdentity,key.Memory.Length,rowBytes));
                    if(bytes>maximumBytes){throw new InvalidDataException("Transaction exceeds the WAL frame bound.");}
                    this.walMemory.Resize(checked(this.walMemory.Bytes+key.Memory.Length+rowBytes+128L));
                }
                if(!pair.Value.Deleted){using var encoded=this.table.resources.Encode(pair.Value.Record);Reserve(encoded.Memory.Length);row=encoded.Memory.ToArray();}else{Reserve(0);}
                var mutation=WalMutation.Upsert;if(pair.Value.Deleted){mutation=WalMutation.Delete;}
                entries.Add(new(this.table.Partition,this.table.TableId,mutation,this.table.CodecIdentity,key.Memory.ToArray(),row));
            }
            return entries;
        }
        public void Apply(){this.context?.EnsureWritable();foreach(var pair in this.changes.Enumerate()){if(pair.Value.Deleted){this.table.remove(pair.Key);}else{this.table.write(pair.Key,pair.Value.Record);}}}
        public void Dispose(){try{this.changes.Dispose();}finally{this.walMemory.Dispose();}}
    }
}
public sealed class TransactionCollection<TKey,TRecord>:IEnumerable<TRecord> where TKey:notnull{
    private readonly ITransactionalTable<TKey,TRecord> table;
    private readonly TransactionScope scope;
    internal TransactionCollection(ITransactionalTable<TKey,TRecord> table,TransactionScope scope){this.table=table;this.scope=scope;}
    private T Use<T>(Func<ITableChanges<TKey,TRecord>,T> action)=>this.scope.Use<T,ITableChanges<TKey,TRecord>>(this.table,action);
    public TransactionRows<TKey,TRecord> Rows=>new(this);
    internal bool MatchesReadOnlyJoinIndex(Func<TRecord,TKey> expectedKey)=>
        this.scope.IsReadOnly&&this.table is VersionedTransactionalTable<TKey,TRecord> versioned&&versioned.MatchesJoinKey(expectedKey);
    internal void Iterate<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IStrategy<TStrategy,TRecord>{
        this.scope.Use<ITableChanges<TKey,TRecord>,TStrategy>(this.table,ref strategy,static(ITableChanges<TKey,TRecord> changes,ref TStrategy state)=>changes.Iterate(ref state));
    }
    internal void IterateParallel<TStrategy>(ref TStrategy strategy) where TStrategy:struct,global::Seamless.Collections.Iteration.IParallelStrategy<TStrategy,TRecord>{
        this.scope.Use<ITableChanges<TKey,TRecord>,TStrategy>(this.table,ref strategy,static(ITableChanges<TKey,TRecord> changes,ref TStrategy state)=>changes.IterateParallel(ref state));
    }
    internal QueryResultBuffer<TResult> CreateResultBuffer<TResult>()=>this.Use(static changes=>changes.CreateResultBuffer<TResult>());
    internal ExecutionScope CreateQueryScope()=>this.Use(static changes=>changes.CreateQueryScope());
    internal TResult ExecuteQuery<TResult>(Func<TResult> query){
        ArgumentNullException.ThrowIfNull(query);var state=new QueryCall<TResult>(query);
        this.scope.Use<ITableChanges<TKey,TRecord>,QueryCall<TResult>>(this.table,ref state,static(ITableChanges<TKey,TRecord> changes,ref QueryCall<TResult> call)=>{call.Result=call.Query();});return state.Result;
    }
    private struct QueryCall<TResult>(Func<TResult> query){internal readonly Func<TResult> Query=query;internal TResult Result=default!;}
    internal QueryDictionaryBuffer<TResultKey,TResultValue> CreateDictionaryBuffer<TResultKey,TResultValue>(IEqualityComparer<TResultKey>? comparer) where TResultKey:notnull{
        return this.Use(changes=>changes.CreateDictionaryBuffer<TResultKey,TResultValue>(comparer));
    }
    public TRecord this[TKey key]{
        get=>this.Use(changes=>{var result=changes.Read(key);if(!result.Found){throw new KeyNotFoundException();}return result.Row;});
        set=>this.Use(changes=>{changes.Set(key,value);return true;});
    }
    public bool TryGetValue(TKey key,out TRecord row){
        var result=this.scope.Use(this.table,key,static(ITableChanges<TKey,TRecord> changes,TKey value)=>changes.Read(value));
        row=result.Row;
        return result.Found;
    }
    public bool ContainsKey(TKey key)=>this.Use(changes=>changes.Read(key).Found);
    public bool Add(TRecord row)=>this.Use(changes=>changes.Add(row));
    public bool Remove(TKey key)=>this.Use(changes=>changes.Remove(key));
    public IEnumerator<TRecord> GetEnumerator()=>this.Enumerate().GetEnumerator();
    private IEnumerable<TRecord> Enumerate(){
        using var iterator=this.Use(static changes=>changes.Rows().GetEnumerator());
        while(this.Use(_=>iterator.MoveNext())){yield return iterator.Current;}
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
}
