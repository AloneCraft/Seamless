using Seamless.Transactions.Collections;
namespace Seamless.Database;

/// <summary>データベース全体の直列化可能なトランザクションスコープ。</summary>
public sealed class DatabaseTransaction:IDisposable {
    private readonly DatabaseContainer database;
    private readonly DatabaseTransactionState state;
    private bool commitRequested;
    private bool disposed;

    internal DatabaseTransaction(DatabaseContainer database,DatabaseTransactionState state) {
        this.database=database;
        this.state=state;
    }

    public void Commit() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.commitRequested=true;
    }

    public void Rollback() {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        this.state.Aborted=true;
        this.commitRequested=false;
    }

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        this.database.EndTransaction(this.state,this.commitRequested);
    }
}

internal sealed class DatabaseTransactionState(long transactionMemoryLimit,IDisposable writeLease):IDisposable {
    internal global::Seamless.Transactions.TransactionScope? SharedScope{get;init;}
    public int Depth { get; set; }
    public bool Aborted { get; set; }
    public Dictionary<object,ITransactionOverlay> Overlays { get; }=
        new(global::System.Collections.Generic.ReferenceEqualityComparer.Instance);

    public void Dispose() {
        List<Exception>? exceptions=null;
        try {
            foreach(var overlay in this.Overlays.Values) {
                try {
                    overlay.Dispose();
                } catch(Exception exception) {
                    exceptions??=[];
                    exceptions.Add(exception);
                }
            }
            this.Overlays.Clear();
            if(exceptions is null) {
                return;
            }
            if(exceptions.Count==1) {
                global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }
            throw new AggregateException(exceptions);
        } finally {
            writeLease.Dispose();
        }
    }

    public long TransactionMemoryLimit { get; }=transactionMemoryLimit;
}

internal interface ITransactionOverlay:IDisposable {
    IEnumerable<DatabaseWalOperation> CreateWalOperations();
    void Apply();
}

internal static class DatabaseWalOperationFactory {
    public static DatabaseWalOperation Create<TKey,TRecord>(
        global::Seamless.Execution.ExecutionScope resources,
        string tableName,
        bool deleted,
        TKey key,
        TRecord record) {
        var keyLength=resources.Codecs.Get<TKey>().GetEncodedLength(key);
        var recordLength=0;
        if(!deleted){recordLength=resources.Codecs.Get<TRecord>().GetEncodedLength(record);}
        var keyCharacters=Base64Length(keyLength);
        var recordCharacters=deleted?0:Base64Length(recordLength);
        var reservation=resources.Budget.ReserveMemory(checked(256L+2L*keyCharacters+2L*recordCharacters));
        try {
            using var encodedKey=resources.Rent(keyLength);
            resources.Codecs.Get<TKey>().Encode(key,encodedKey.Memory.Span);
            string? recordData=null;
            if(!deleted) {
                using var encodedRecord=resources.Rent(recordLength);
                resources.Codecs.Get<TRecord>().Encode(record,encodedRecord.Memory.Span);
                recordData=Convert.ToBase64String(encodedRecord.Memory.Span);
            }
            return new(2,tableName,deleted,Convert.ToBase64String(encodedKey.Memory.Span),recordData,reservation);
        } catch {
            reservation.Dispose();
            throw;
        }
    }

    private static long Base64Length(int byteLength)=>checked(4L*((byteLength+2L)/3L));
}

internal sealed class UnkeyedTableTransactionOverlay<TRecord>(
    Table<TRecord> table,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TRecord:notnull {
    private TransactionChangeSet<TRecord,TRecord> changes=new(resources,memoryLimit);
    private bool cleared;

    public long LongCount {
        get {
            var count=this.cleared?0:table.DirectLongCount;
            foreach(var pair in this.changes.Enumerate()) {
                var exists=!this.cleared&&table.DirectContains(pair.Key);
                if(pair.Value.Deleted&&exists){count--;}
                if(!pair.Value.Deleted&&!exists){count++;}
            }
            return count;
        }
    }

    public IEnumerable<TRecord> Rows=>this.EnumerateRows();

    public bool Contains(TRecord item) {
        if(this.changes.TryGetValue(item,out var change)){return !change.Deleted;}
        if(this.cleared){return false;}
        return table.DirectContains(item);
    }

    public bool Add(TRecord item) {
        if(this.Contains(item)){return false;}
        this.changes.Set(item,new(false,item));
        return true;
    }

    public bool Remove(TRecord item) {
        if(!this.Contains(item)){return false;}
        this.changes.Set(item,new(true,default!));
        return true;
    }

    public void Clear() {
        this.changes.Dispose();
        this.changes=new(resources,memoryLimit);
        this.cleared=true;
    }

    public void Apply() {
        if(this.cleared){table.DirectClear();}
        foreach(var pair in this.changes.Enumerate()) {
            if(pair.Value.Deleted){_ = table.DirectRemove(pair.Key);}
            else{_ = table.DirectAdd(pair.Value.Record);}
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        if(this.cleared) {
            foreach(var item in table.DirectRows) {
                yield return DatabaseWalOperationFactory.Create(resources,table.Name,true,item,default(TRecord)!);
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                table.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<TRecord> EnumerateRows() {
        if(!this.cleared) {
            foreach(var item in table.DirectRows) {
                if(!this.changes.TryGetValue(item,out var change)){yield return item;}
                else if(!change.Deleted){yield return change.Record;}
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            if(!pair.Value.Deleted&&(this.cleared||!table.DirectContains(pair.Key))){yield return pair.Value.Record;}
        }
    }

    public void Dispose()=>this.changes.Dispose();
}

internal sealed class TableTransactionOverlay<TKey,TRecord>(
    Table<TKey,TRecord> table,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TKey:struct
    where TRecord:struct {
    private readonly TransactionChangeSet<TKey,TRecord> changes=new(resources,memoryLimit);

    public long LongCount {
        get {
            var count=table.DirectLongCount;
            foreach(var pair in this.changes.Enumerate()) {
                var exists=table.DirectTryGetValue(pair.Key,out _);
                if(pair.Value.Deleted&&exists){count--;}
                if(!pair.Value.Deleted&&!exists){count++;}
            }
            return count;
        }
    }
    public int Count=>checked((int)this.LongCount);
    public IEnumerable<TKey> Keys {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Key;}
        }
    }
    public IEnumerable<TRecord> Rows {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Value;}
        }
    }

    public bool TryGetValue(TKey key,out TRecord record) {
        if(this.changes.TryGetValue(key,out var change)) {
            record=change.Record;
            return !change.Deleted;
        }
        return table.DirectTryGetValue(key,out record);
    }

    public bool Add(TKey key,TRecord record) {
        if(this.TryGetValue(key,out _)) {
            return false;
        }
        this.changes.Set(key,new(false,record));
        return true;
    }

    public void Set(TKey key,TRecord record)=>this.changes.Set(key,new(false,record));

    public bool Remove(TKey key) {
        if(!this.TryGetValue(key,out _)) {
            return false;
        }
        this.changes.Set(key,new(true,default));
        return true;
    }

    public void Apply() {
        foreach(var pair in this.changes.Enumerate()) {
            if(pair.Value.Deleted) {
                _=table.DirectRemove(pair.Key);
            } else {
                table.DirectSet(pair.Key,pair.Value.Record);
            }
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        foreach(var pair in this.changes.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                table.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<KeyValuePair<TKey,TRecord>> EnumeratePairs() {
        foreach(var pair in table.DirectRows) {
            if(!this.changes.TryGetValue(pair.Key,out var change)) {
                yield return pair;
            } else if(!change.Deleted) {
                yield return new(pair.Key,change.Record);
            }
        }
        foreach(var pair in this.changes.Enumerate()) {
            if(!pair.Value.Deleted&&!table.DirectTryGetValue(pair.Key,out _)) {
                yield return new(pair.Key,pair.Value.Record);
            }
        }
    }

    public void Dispose()=>this.changes.Dispose();
}

internal sealed class MaterializedViewTransactionOverlay<TKey,TRecord>(
    MaterializedView<TKey,TRecord> view,
    global::Seamless.Execution.ExecutionScope resources,
    long memoryLimit):ITransactionOverlay
    where TKey:struct
    where TRecord:struct {
    private readonly TransactionChangeSet<TKey,TRecord> additions=new(resources,memoryLimit);

    public long LongCount {
        get {
            var count=view.DirectLongCount;
            foreach(var pair in this.additions.Enumerate()) {
                if(!pair.Value.Deleted&&!view.DirectTryGetValue(pair.Key,out _)){count++;}
            }
            return count;
        }
    }
    public int Count=>checked((int)this.LongCount);
    public IEnumerable<TKey> Keys {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Key;}
        }
    }
    public IEnumerable<TRecord> Rows {
        get {
            foreach(var pair in this.EnumeratePairs()){yield return pair.Value;}
        }
    }

    public bool TryGetValue(TKey key,out TRecord record) {
        if(this.additions.TryGetValue(key,out var change)) {
            record=change.Record;
            return !change.Deleted;
        }
        return view.DirectTryGetValue(key,out record);
    }

    public bool Add(TKey key,TRecord record) {
        if(this.TryGetValue(key,out _)) {
            return false;
        }
        this.additions.Set(key,new(false,record));
        return true;
    }

    public void Apply() {
        foreach(var pair in this.additions.Enumerate()) {
            if(!pair.Value.Deleted) {
                view.DirectSet(pair.Key,pair.Value.Record);
            }
        }
    }

    public IEnumerable<DatabaseWalOperation> CreateWalOperations() {
        foreach(var pair in this.additions.Enumerate()) {
            yield return DatabaseWalOperationFactory.Create(
                resources,
                view.Name,
                pair.Value.Deleted,
                pair.Key,
                pair.Value.Record);
        }
    }

    private IEnumerable<KeyValuePair<TKey,TRecord>> EnumeratePairs() {
        foreach(var pair in view.DirectRows){yield return pair;}
        foreach(var pair in this.additions.Enumerate()) {
            if(!pair.Value.Deleted&&!view.DirectTryGetValue(pair.Key,out _)) {
                yield return new(pair.Key,pair.Value.Record);
            }
        }
    }

    public void Dispose()=>this.additions.Dispose();
}
