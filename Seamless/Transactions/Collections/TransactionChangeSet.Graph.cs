using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Seamless.Execution;

namespace Seamless.Transactions.Collections;

public sealed partial class TransactionChangeSet<TKey,TRecord> where TKey:notnull{
    private Dictionary<ReadOnlyMemory<byte>,EncodedEntry>? encodedChanges;
    private long encodedPayloadBytes;
    internal bool UsesEncodedRecords{get;}
    private readonly record struct EncodedEntry(OwnedBuffer Key,OwnedBuffer Change);

    private sealed class EncodedKeyComparer:IEqualityComparer<ReadOnlyMemory<byte>>{
        internal static readonly EncodedKeyComparer Instance=new();
        public bool Equals(ReadOnlyMemory<byte> first,ReadOnlyMemory<byte> second)=>first.Span.SequenceEqual(second.Span);
        public int GetHashCode(ReadOnlyMemory<byte> value){var hash=new HashCode();hash.AddBytes(value.Span);return hash.ToHashCode();}
    }

    private bool TryReadEncoded(TKey key,out TransactionChange<TRecord> change){
        using var encodedKey=this.resources.Encode(key);
        if(this.spillIndex is not null){
            if(!this.spillIndex.TryGetValue(encodedKey.Memory.Span,out var offset)||offset<0){change=default;return false;}
            change=this.ReadChange(offset);return true;
        }
        if(!this.encodedChanges!.TryGetValue(encodedKey.Memory,out var entry)){change=default;return false;}
        change=this.Decode(entry.Change.Memory.Span);return true;
    }

    private void SetEncoded(TKey key,TransactionChange<TRecord> change){
        var encodedKey=this.resources.Encode(key);OwnedBuffer? encodedChange=null;var retained=false;
        try{encodedChange=this.Encode(change);retained=this.StoreEncoded(encodedKey,encodedChange);}
        finally{if(!retained){encodedChange?.Dispose();encodedKey.Dispose();}}
    }

    /// <summary>true の場合はバッファの所有を引き取る。上書きでは保存済みのキーを維持する。</summary>
    private bool StoreEncoded(OwnedBuffer key,OwnedBuffer change){
        if(this.spillIndex is not null){this.WriteSpillEncoded(key.Memory.Span,change.Memory.Span);return false;}
        var found=this.encodedChanges!.TryGetValue(key.Memory,out var previous);
        var count=this.encodedChanges.Count;if(!found){count=checked(count+1);}
        var metadata=checked(256L+count*384L);
        var payload=checked(this.encodedPayloadBytes+change.MemoryBytes);
        if(found){payload=checked(payload-previous.Change.MemoryBytes);}else{payload=checked(payload+key.MemoryBytes);}
        if(checked(metadata+payload)>this.memoryLimit){this.Spill();this.WriteSpillEncoded(key.Memory.Span,change.Memory.Span);return false;}
        var before=this.memory.Bytes;
        try{this.memory.Resize(metadata);}
        catch(ResourceLimitException){this.Spill();this.WriteSpillEncoded(key.Memory.Span,change.Memory.Span);return false;}
        var storedKey=key;if(found){storedKey=previous.Key;}
        try{this.encodedChanges[key.Memory]=new(storedKey,change);}
        catch{this.memory.Resize(before);throw;}
        this.encodedPayloadBytes=payload;
        if(found){key.Dispose();previous.Change.Dispose();}
        return true;
    }

    private TransactionChangeSet<TKey,TRecord> SnapshotEncoded(){
        var snapshot=new TransactionChangeSet<TKey,TRecord>(this.resources,this.memoryLimit);
        try{
            foreach(var entry in this.EncodedEntries()){
                var key=this.resources.Rent(entry.Key.Memory.Length);OwnedBuffer? change=null;var retained=false;
                try{
                    entry.Key.Memory.Span.CopyTo(key.Memory.Span);change=this.resources.Rent(entry.Change.Memory.Length);
                    entry.Change.Memory.Span.CopyTo(change.Memory.Span);retained=snapshot.StoreEncoded(key,change);
                }finally{if(!retained){change?.Dispose();key.Dispose();}}
            }
            return snapshot;
        }catch{snapshot.Dispose();throw;}
    }

    private IEnumerable<EncodedEntry> EncodedEntries(){
        if(this.spillIndex is null){foreach(var entry in this.encodedChanges!.Values){this.Check();yield return entry;}yield break;}
        var order=this.spillOrder!;long position=0;
        while(position<order.Length){
            this.Check();order.Position=position;var length=ReadInt(order);
            if(length<0||length>order.Length-order.Position){throw new InvalidDataException("Invalid transaction key record.");}
            using var key=this.resources.Rent(length);order.ReadExactly(key.Memory.Span);position=order.Position;
            if(!this.spillIndex.TryGetValue(key.Memory.Span,out var offset)){throw new InvalidDataException("Transaction change index is missing a key.");}
            if(offset<0){continue;}
            using var change=this.ReadEncodedChange(offset);yield return new(key,change);
        }
    }

    private OwnedBuffer ReadEncodedChange(long offset){
        var values=this.spillValues!;values.Position=offset;var length=ReadInt(values);
        if(length<=0||length>values.Length-values.Position){throw new InvalidDataException("Invalid transaction change record.");}
        var buffer=this.resources.Rent(length);
        try{values.ReadExactly(buffer.Memory.Span);return buffer;}catch{buffer.Dispose();throw;}
    }

    private IEnumerable<KeyValuePair<TKey,TransactionChange<TRecord>>> EnumerateEncodedValues(){
        foreach(var entry in this.EncodedEntries()){
            using var key=this.resources.DecodeOwned<TKey>(entry.Key.Memory.Span);
            using var row=this.DecodeOwnedChange(entry.Change);
            var change=new TransactionChange<TRecord>(true,default!);if(row is not null){change=new(false,row.Value);}
            yield return new(key.Value,change);
        }
    }

    private OwnedValue<TRecord>? DecodeOwnedChange(OwnedBuffer buffer){
        var bytes=buffer.Memory.Span;
        if(bytes.Length==0){throw new InvalidDataException("The transaction spill store contains an empty change.");}
        if(bytes[0]!=0){return null;}
        return this.resources.DecodeOwned<TRecord>(bytes[1..]);
    }

    private void ReleaseEncodedMemory(){
        if(this.encodedChanges is null){return;}
        foreach(var entry in this.encodedChanges.Values){try{entry.Change.Dispose();}finally{entry.Key.Dispose();}}
        this.encodedChanges.Clear();this.encodedChanges=null;this.encodedPayloadBytes=0;
    }
}
