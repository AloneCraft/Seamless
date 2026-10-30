using Seamless.Execution;
namespace Seamless.Transactions.Collections;
/// <summary>型付き保存先の接続。codecによる値の所有権分離でmutableな参照の迂回更新を防ぐ。</summary>
public sealed class TransactionalTable<TKey,TRecord>:ITransactionParticipant where TKey:notnull{
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
    internal sealed class Changes:ITransactionChanges{
        private readonly TransactionalTable<TKey,TRecord> table;
        private readonly TransactionChangeSet<TKey,TRecord> changes;
        private readonly ResourceReservation walMemory;
        public Changes(TransactionalTable<TKey,TRecord> table){this.table=table;this.changes=new(table.resources,table.memoryLimit);this.walMemory=table.resources.Budget.ReserveMemory(0);}
        public (bool Found,TRecord Row) Read(TKey key){
            if(this.changes.TryGetValue(key,out var change)){
                if(change.Deleted){return(false,default!);}return(true,this.table.Snapshot(change.Record));
            }
            var current=this.table.read(key);if(current.Found){return(true,this.table.Snapshot(current.Row));}return current;
        }
        public void Set(TKey key,TRecord row){
            if(!EqualityComparer<TKey>.Default.Equals(key,this.table.keyOf(row))){throw new ArgumentException("Record key does not match the indexer key.",nameof(row));}
            this.changes.Set(key,new(false,this.table.Snapshot(row)));
        }
        public bool Add(TRecord row){var key=this.table.keyOf(row);if(this.Read(key).Found){return false;}this.Set(key,row);return true;}
        public bool Remove(TKey key){if(!this.Read(key).Found){return false;}this.changes.Set(key,new(true,default!));return true;}
        public IEnumerable<TRecord> Rows(){
            foreach(var pair in this.table.rows()){
                var current=this.Read(pair.Key);if(current.Found){yield return current.Row;}
            }
            foreach(var pair in this.changes.Enumerate()){
                if(!pair.Value.Deleted&&!this.table.read(pair.Key).Found){yield return this.table.Snapshot(pair.Value.Record);}
            }
        }
        public IReadOnlyList<WalEntry> Encode()=>this.EncodeBounded(int.MaxValue);
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
        public void Apply(){foreach(var pair in this.changes.Enumerate()){if(pair.Value.Deleted){this.table.remove(pair.Key);}else{this.table.write(pair.Key,pair.Value.Record);}}}
        public void Dispose(){try{this.changes.Dispose();}finally{this.walMemory.Dispose();}}
    }
}
public sealed class TransactionCollection<TKey,TRecord>:IEnumerable<TRecord> where TKey:notnull{
    private readonly TransactionalTable<TKey,TRecord> table;
    private readonly TransactionScope scope;
    internal TransactionCollection(TransactionalTable<TKey,TRecord> table,TransactionScope scope){this.table=table;this.scope=scope;}
    private T Use<T>(Func<TransactionalTable<TKey,TRecord>.Changes,T> action)=>this.scope.Use<T,TransactionalTable<TKey,TRecord>.Changes>(this.table,action);
    public TRecord this[TKey key]{
        get=>this.Use(changes=>{var result=changes.Read(key);if(!result.Found){throw new KeyNotFoundException();}return result.Row;});
        set=>this.Use(changes=>{changes.Set(key,value);return true;});
    }
    public bool TryGetValue(TKey key,out TRecord row){var result=this.Use(changes=>changes.Read(key));row=result.Row;return result.Found;}
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
