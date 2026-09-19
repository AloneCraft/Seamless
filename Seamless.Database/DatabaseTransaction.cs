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

internal readonly record struct TransactionChange<TRecord>(bool Deleted,TRecord Record);

/// <summary>
/// トランザクション変更を小さいDictionaryからTsavorite一時辞書へ退避する変更集合。
/// 退避値はMemoryPackのバイト列で保持し、変更集合自体はトランザクション終了時に破棄する。
/// </summary>
internal sealed class TransactionChangeSet<TKey,TRecord>:IDisposable
    where TKey:notnull {
    private readonly global::Seamless.Execution.ExecutionScope resources;
    private readonly long memoryLimit;
    private readonly Dictionary<TKey,TransactionChange<TRecord>> memoryChanges=[];
    private readonly Dictionary<TKey,long> memorySizes=[];
    private readonly global::Seamless.Execution.ResourceReservation memory;
    private global::Seamless.Collections.Persisted.ScopedByteIndex? spillIndex;
    private global::Seamless.Execution.TemporaryFile? spillOrder;
    private global::Seamless.Execution.TemporaryFile? spillValues;
    private bool disposed;

    public TransactionChangeSet(global::Seamless.Execution.ExecutionScope resources,long memoryLimit) {
        this.resources=resources;
        this.memoryLimit=memoryLimit;
        this.memory=resources.Budget.ReserveMemory(0);
        resources.Codecs.Get<TKey>();
        resources.Codecs.Get<TRecord>();
    }

    public bool TryGetValue(TKey key,out TransactionChange<TRecord> change) {
        this.Check();
        if(this.spillIndex is null){return this.memoryChanges.TryGetValue(key,out change);}
        using var encoded=this.resources.Encode(key);
        if(!this.spillIndex.TryGetValue(encoded.Memory.Span,out var offset)) {
            change=default;
            return false;
        }
        change=this.ReadChange(offset);
        return true;
    }

    public void Set(TKey key,TransactionChange<TRecord> change) {
        this.Check();
        using var encodedChange=this.Encode(change);
        if(this.spillIndex is not null) {
            this.WriteSpill(key,encodedChange.Memory.Span);
            return;
        }
        using var encodedKey=this.resources.Encode(key);
        var size=checked(96L+4L*encodedKey.Memory.Length+4L*encodedChange.Memory.Length);
        var previous=0L;
        this.memorySizes.TryGetValue(key,out previous);
        var next=checked(this.memory.Bytes-previous+size);
        if(next>this.memoryLimit) {
            this.Spill();
            this.WriteSpill(key,encodedChange.Memory.Span);
            return;
        }
        this.memory.Resize(next);
        this.memoryChanges[key]=change;
        this.memorySizes[key]=size;
    }

    public IEnumerable<KeyValuePair<TKey,TransactionChange<TRecord>>> Enumerate() {
        this.Check();
        if(this.spillIndex is null) {
            foreach(var change in this.memoryChanges){yield return change;}
            yield break;
        }
        var order=this.spillOrder!;
        long position=0;
        while(position<order.Length) {
            this.Check();
            order.Position=position;
            var length=ReadInt(order);
            if(length<0||length>order.Length-order.Position){throw new InvalidDataException("Invalid transaction key record.");}
            TKey key;
            using(var buffer=this.resources.Rent(length)) {
                order.ReadExactly(buffer.Memory.Span);
                key=this.resources.Codecs.Get<TKey>().Decode(buffer.Memory.Span);
                position=order.Position;
                if(!this.spillIndex.TryGetValue(buffer.Memory.Span,out var offset)){throw new InvalidDataException("Transaction change index is missing a key.");}
                yield return new(key,this.ReadChange(offset));
            }
        }
    }

    private void Spill() {
        if(this.spillIndex is not null){return;}
        var index=new global::Seamless.Collections.Persisted.ScopedByteIndex(this.resources);
        var order=this.resources.CreateTemporaryFile();
        var values=this.resources.CreateTemporaryFile();
        this.spillIndex=index;
        this.spillOrder=order;
        this.spillValues=values;
        try {
            foreach(var pair in this.memoryChanges){
                using var encoded=this.Encode(pair.Value);
                this.WriteSpill(pair.Key,encoded.Memory.Span);
            }
            this.memoryChanges.Clear();
            this.memorySizes.Clear();
            this.memory.Resize(0);
        } catch {
            this.spillIndex=null;
            this.spillOrder=null;
            this.spillValues=null;
            try{index.Dispose();}finally{try{order.Dispose();}finally{values.Dispose();}}
            throw;
        }
    }

    private void WriteSpill(TKey key,ReadOnlySpan<byte> encodedChange) {
        using var encodedKey=this.resources.Encode(key);
        var first=!this.spillIndex!.TryGetValue(encodedKey.Memory.Span,out _);
        if(first) {
            this.spillOrder!.Position=this.spillOrder.Length;
            WriteInt(this.spillOrder,encodedKey.Memory.Length);
            this.spillOrder.Write(encodedKey.Memory.Span);
        }
        var offset=this.spillValues!.Length;
        this.spillValues.Position=offset;
        WriteInt(this.spillValues,encodedChange.Length);
        this.spillValues.Write(encodedChange);
        this.spillIndex.Set(encodedKey.Memory.Span,offset);
    }

    private TransactionChange<TRecord> ReadChange(long offset) {
        var values=this.spillValues!;
        values.Position=offset;
        var length=ReadInt(values);
        if(length<=0||length>values.Length-values.Position){throw new InvalidDataException("Invalid transaction change record.");}
        using var buffer=this.resources.Rent(length);
        values.ReadExactly(buffer.Memory.Span);
        return Decode(buffer.Memory.Span);
    }

    private void ValidateSingleElement(int length) {
        var requested=checked((long)length+96);
        if(requested>this.resources.Budget.MemoryLimitBytes) {
            throw new global::Seamless.Execution.ResourceLimitException(global::Seamless.Execution.ResourceLimitKind.SingleElement,requested,this.resources.Budget.MemoryLimitBytes);
        }
    }

    private global::Seamless.Execution.OwnedBuffer Encode(TransactionChange<TRecord> change) {
        var length=1;
        if(!change.Deleted){length=checked(this.resources.Codecs.Get<TRecord>().GetEncodedLength(change.Record)+1);}
        this.ValidateSingleElement(length);
        var buffer=this.resources.Rent(length);
        buffer.Memory.Span[0]=change.Deleted?(byte)1:(byte)0;
        if(!change.Deleted){this.resources.Codecs.Get<TRecord>().Encode(change.Record,buffer.Memory.Span[1..]);}
        return buffer;
    }

    private TransactionChange<TRecord> Decode(ReadOnlySpan<byte> bytes) {
        if(bytes.Length==0){throw new InvalidDataException("The transaction spill store contains an empty change.");}
        if(bytes[0]!=0){return new(true,default!);}
        return new(false,this.resources.Codecs.Get<TRecord>().Decode(bytes[1..])!);
    }

    private static int ReadInt(Stream stream) {
        Span<byte> bytes=stackalloc byte[4];
        stream.ReadExactly(bytes);
        return global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static void WriteInt(Stream stream,int value) {
        Span<byte> bytes=stackalloc byte[4];
        global::System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes,value);
        stream.Write(bytes);
    }

    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.resources.ThrowIfUnavailable();}

    public void Dispose() {
        if(this.disposed){return;}
        this.disposed=true;
        this.memoryChanges.Clear();
        this.memorySizes.Clear();
        try{this.spillIndex?.Dispose();}
        finally{try{this.spillOrder?.Dispose();}finally{try{this.spillValues?.Dispose();}finally{this.memory.Dispose();}}}
    }
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
