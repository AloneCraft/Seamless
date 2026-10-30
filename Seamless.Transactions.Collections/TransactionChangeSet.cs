namespace Seamless.Transactions.Collections;
public readonly record struct TransactionChange<TRecord>(bool Deleted,TRecord Record);

/// <summary>
/// トランザクション変更を小さいDictionaryから一時バイト索引へ退避する変更集合。
/// 退避値はMemoryPackのバイト列で保持し、変更集合自体はトランザクション終了時に破棄する。
/// </summary>
public sealed class TransactionChangeSet<TKey,TRecord>:IDisposable
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
