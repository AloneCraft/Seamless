namespace Seamless.Transactions.Collections;
public readonly record struct TransactionChange<TRecord>(bool Deleted,TRecord Record);

/// <summary>
/// トランザクション変更を小さいDictionaryから一時バイト索引へ退避する変更集合。
/// 退避値は世代のcodecのバイト列で保持し、変更集合自体はトランザクション終了時に破棄する。
/// </summary>
public sealed partial class TransactionChangeSet<TKey,TRecord>:IDisposable
    where TKey:notnull {
    private readonly global::Seamless.Execution.ExecutionScope resources;
    private readonly long memoryLimit;
    private readonly Dictionary<TKey,TransactionChange<TRecord>> memoryChanges;
    private readonly Dictionary<TKey,long> memorySizes;
    private readonly global::Seamless.Execution.ResourceReservation memory;
    private global::Seamless.Collections.Persisted.ScopedByteIndex? spillIndex;
    private global::Seamless.Execution.TemporaryFile? spillOrder;
    private global::Seamless.Execution.TemporaryFile? spillValues;
    private bool disposed;
    private ChangeSetUndoLog? undo;
    private int historyPoints;
    public bool IsEmpty{get{this.Check();if(this.spillIndex is null){if(this.UsesEncodedRecords){return this.encodedChanges!.Count==0;}return this.memoryChanges.Count==0;}return this.spillOrder!.Length==0;}}

    public sealed class HistoryPoint:IDisposable{
        internal readonly TransactionChangeSet<TKey,TRecord> Owner;
        internal readonly long ValuesLength,OrderLength;
        private readonly global::Seamless.Execution.ResourceReservation reservation;
        public long Position{get;}
        internal bool IsDisposed;
        internal HistoryPoint(TransactionChangeSet<TKey,TRecord> owner){this.Owner=owner;this.Position=owner.undo!.Position;this.ValuesLength=owner.spillValues!.Length;this.OrderLength=owner.spillOrder!.Length;this.reservation=owner.resources.Budget.ReserveMemory(64);}
        public void Dispose(){if(this.IsDisposed){return;}this.IsDisposed=true;try{if(--this.Owner.historyPoints==0){this.Owner.undo?.Dispose();this.Owner.undo=null;}}finally{this.reservation.Dispose();}}
    }

    /// <summary>Retains an undo position. The existing row payload is spilled once and shared by all positions.</summary>
    public HistoryPoint CreateHistoryPoint(){
        this.Check();this.Spill();this.undo??=new(this.resources,this.memoryLimit);
        var point=new HistoryPoint(this);this.historyPoints++;return point;
    }
    public void RollbackToHistoryPoint(HistoryPoint point){
        this.Check();if(point.IsDisposed||!ReferenceEquals(point.Owner,this)){throw new InvalidOperationException("Invalid transaction savepoint.");}
        this.undo!.Rewind(point.Position,(key,previous)=>{
            // A failed first insertion may never have installed an index entry.
            if(previous>=0||this.spillIndex!.TryGetValue(key,out _)){this.spillIndex!.Set(key,previous);}
        });
        this.spillValues!.SetLength(point.ValuesLength);this.spillOrder!.SetLength(point.OrderLength);
    }

    public TransactionChangeSet(global::Seamless.Execution.ExecutionScope resources,long memoryLimit):this(resources,memoryLimit,null){}
    public TransactionChangeSet(global::Seamless.Execution.ExecutionScope resources,long memoryLimit,IEqualityComparer<TKey>? comparer) {
        this.resources=resources;
        this.memoryLimit=memoryLimit;
        var fixedComparer=resources.Codecs.GetEqualityComparer<TKey>();
        if(resources.Codecs.UsesRecordGraphSerialization&&comparer is not null&&!ReferenceEquals(comparer,fixedComparer)){throw new ArgumentException("Record graph keys use their fixed type contract.",nameof(comparer));}
        comparer??=fixedComparer;
        resources.Codecs.Get<TKey>();
        resources.Codecs.Get<TRecord>();
        this.UsesEncodedRecords=resources.Codecs.UsesRecordGraphSerialization;
        this.memoryChanges=null!;this.memorySizes=null!;
        if(this.UsesEncodedRecords){this.encodedChanges=new(EncodedKeyComparer.Instance);}
        else{this.memoryChanges=new(comparer);this.memorySizes=new(comparer);}
        var baseline=0L;if(this.UsesEncodedRecords){baseline=256;}
        this.memory=resources.Budget.ReserveMemory(baseline);
        try{resources.Own(this);}catch{this.memory.Dispose();throw;}
    }

    public bool TryGetValue(TKey key,out TransactionChange<TRecord> change) {
        this.Check();
        if(this.UsesEncodedRecords){return this.TryReadEncoded(key,out change);}
        if(this.spillIndex is null){return this.memoryChanges.TryGetValue(key,out change);}
        using var encoded=this.resources.Encode(key);
        if(!this.spillIndex.TryGetValue(encoded.Memory.Span,out var offset)||offset<0) {
            change=default;
            return false;
        }
        change=this.ReadChange(offset);
        return true;
    }

    public void Set(TKey key,TransactionChange<TRecord> change) {
        this.Check();
        if(this.UsesEncodedRecords){this.SetEncoded(key,change);return;}
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
        if(this.UsesEncodedRecords){foreach(var pair in this.EnumerateEncodedValues()){yield return pair;}yield break;}
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
                if(offset<0){continue;}
                yield return new(key,this.ReadChange(offset));
            }
        }
    }

    /// <summary>同じ資源予算内で、参照値も独立した保存点用の変更集合を作る。</summary>
    public TransactionChangeSet<TKey,TRecord> CreateSnapshot(){
        this.Check();
        if(this.UsesEncodedRecords){return this.SnapshotEncoded();}
        var snapshot=new TransactionChangeSet<TKey,TRecord>(this.resources,this.memoryLimit,this.memoryChanges.Comparer);
        try{
            foreach(var pair in this.Enumerate()){
                using var keyBytes=this.resources.Encode(pair.Key);
                var key=this.resources.Codecs.Get<TKey>().Decode(keyBytes.Memory.Span);
                var change=pair.Value;
                if(!change.Deleted){using var rowBytes=this.resources.Encode(change.Record);change=new(false,this.resources.Codecs.Get<TRecord>().Decode(rowBytes.Memory.Span));}
                snapshot.Set(key,change);
            }
            return snapshot;
        }catch{snapshot.Dispose();throw;}
    }

    private void Spill() {
        if(this.spillIndex is not null){return;}
        global::Seamless.Collections.Persisted.ScopedByteIndex? index=null;
        global::Seamless.Execution.TemporaryFile? order=null,values=null;
        try {
            index=new(this.resources);
            order=this.resources.CreateTemporaryFile();
            values=this.resources.CreateTemporaryFile();
            this.spillIndex=index;
            this.spillOrder=order;
            this.spillValues=values;
            if(this.UsesEncodedRecords){
                foreach(var pair in this.encodedChanges!.Values){this.WriteSpillEncoded(pair.Key.Memory.Span,pair.Change.Memory.Span);}
                this.ReleaseEncodedMemory();
            }else{
                foreach(var pair in this.memoryChanges){
                    using var encoded=this.Encode(pair.Value);
                    this.WriteSpill(pair.Key,encoded.Memory.Span);
                }
                this.memoryChanges.Clear();this.memorySizes.Clear();
            }
            this.memory.Resize(0);
        } catch {
            this.spillIndex=null;
            this.spillOrder=null;
            this.spillValues=null;
            try{index?.Dispose();}finally{try{order?.Dispose();}finally{values?.Dispose();}}
            throw;
        }
    }

    private void WriteSpill(TKey key,ReadOnlySpan<byte> encodedChange) {
        using var encodedKey=this.resources.Encode(key);
        this.WriteSpillEncoded(encodedKey.Memory.Span,encodedChange);
    }

    private void WriteSpillEncoded(ReadOnlySpan<byte> encodedKey,ReadOnlySpan<byte> encodedChange){
        var found=this.spillIndex!.TryGetValue(encodedKey,out var previous);
        var first=!found||previous<0;
        var order=this.spillOrder!;
        var values=this.spillValues!;
        var orderLength=order.Length;
        var offset=values.Length;
        var undoPosition=this.undo?.Position??0;
        try{
            if(this.historyPoints!=0){this.undo!.Append(encodedKey,found?previous:-1);}
            if(first){order.SetLength(checked(orderLength+4+encodedKey.Length));}
            values.SetLength(checked(offset+4+encodedChange.Length));
            if(first){
                order.Position=orderLength;
                WriteInt(order,encodedKey.Length);
                order.Write(encodedKey);
            }
            values.Position=offset;
            WriteInt(values,encodedChange.Length);
            values.Write(encodedChange);
            this.spillIndex.Set(encodedKey,offset);
        }catch{
            if(this.historyPoints!=0){
                this.undo!.Rewind(undoPosition,(key,prior)=>{
                    if(prior>=0||this.spillIndex.TryGetValue(key,out _)){this.spillIndex.Set(key,prior);}
                });
            }
            try{values.SetLength(offset);}finally{order.SetLength(orderLength);}
            throw;
        }
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
        try{
            buffer.Memory.Span[0]=change.Deleted?(byte)1:(byte)0;
            if(!change.Deleted){this.resources.Codecs.Get<TRecord>().Encode(change.Record,buffer.Memory.Span[1..]);}
            return buffer;
        }catch{buffer.Dispose();throw;}
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
        if(this.UsesEncodedRecords){this.ReleaseEncodedMemory();}
        else{this.memoryChanges.Clear();this.memorySizes.Clear();}
        try{this.undo?.Dispose();this.spillIndex?.Dispose();}
        finally{try{this.spillOrder?.Dispose();}finally{try{this.spillValues?.Dispose();}finally{try{this.memory.Dispose();}finally{this.resources.Release(this);}}}}
    }
}
