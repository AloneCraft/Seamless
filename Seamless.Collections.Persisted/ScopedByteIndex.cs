using System;
using System.Buffers.Binary;
using System.IO;
using Seamless.Execution;
namespace Seamless.Collections.Persisted;
/// <summary>A scope-owned disk hash index. Keys are compared in full even when their hashes collide.</summary>
public sealed class ScopedByteIndex:IDisposable{
    private readonly ExecutionScope scope;
    private TemporaryFile slots;
    private readonly TemporaryFile entries;
    private long capacity=256;
    private bool disposed;
    public ScopedByteIndex(ExecutionScope scope){
        this.scope=scope;
        this.slots=scope.CreateTemporaryFile();
        try{
            this.slots.SetLength(this.capacity*8);
            this.entries=scope.CreateTemporaryFile();
            scope.Own(this);
        }catch{this.slots.Dispose();throw;}
    }
    public long LongCount{get;private set;}
    public bool TryGetValue(ReadOnlySpan<byte> key,out long value){
        this.Check();
        var slot=this.FindSlot(this.slots,this.capacity,key,out var offset);
        if(offset<0){value=default;return false;}
        this.entries.Position=offset+4+key.Length;
        value=ReadInt64(this.entries);
        return true;
    }
    public void Set(ReadOnlySpan<byte> key,long value){
        this.Check();
        var slot=this.FindSlot(this.slots,this.capacity,key,out var offset);
        if(offset>=0){
            this.entries.Position=offset+4+key.Length;
            WriteInt64(this.entries,value);return;
        }
        if(this.LongCount>=this.capacity/2){
            this.Grow();
            slot=this.FindSlot(this.slots,this.capacity,key,out _);
        }
        offset=this.entries.Length;
        this.entries.Position=offset;
        Span<byte> prefix=stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix,key.Length);
        this.entries.Write(prefix);this.entries.Write(key);WriteInt64(this.entries,value);
        this.slots.Position=slot*8;WriteInt64(this.slots,checked(offset+1));
        this.LongCount=checked(this.LongCount+1);
    }
    private long FindSlot(TemporaryFile table,long tableCapacity,ReadOnlySpan<byte> key,out long offset){
        var slot=(long)(Hash(key)%(ulong)tableCapacity);
        Span<byte> prefix=stackalloc byte[4];
        for(long probe=0;probe<tableCapacity;probe++){
            table.Position=slot*8;
            offset=ReadInt64(table)-1;
            if(offset<0){return slot;}
            this.entries.Position=offset;
            this.entries.ReadExactly(prefix);
            var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if(length==key.Length){
                using var buffer=this.scope.Rent(length);
                this.entries.ReadExactly(buffer.Memory.Span);
                if(key.SequenceEqual(buffer.Memory.Span)){return slot;}
            }
            slot=(slot+1)%tableCapacity;
        }
        throw new InvalidDataException("The disk index has no empty slot.");
    }
    private void Grow(){
        var capacity=checked(this.capacity*2);
        var table=this.scope.CreateTemporaryFile();
        try{
            table.SetLength(checked(capacity*8));
            long offset=0;
            Span<byte> prefix=stackalloc byte[4];
            while(offset<this.entries.Length){
                this.scope.ThrowIfUnavailable();
                this.entries.Position=offset;
                this.entries.ReadExactly(prefix);
                var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
                if(length<0||length>this.entries.Length-offset-12){throw new InvalidDataException("Invalid byte index record.");}
                using var buffer=this.scope.Rent(length);
                this.entries.ReadExactly(buffer.Memory.Span);
                var slot=this.FindSlot(table,capacity,buffer.Memory.Span,out _);
                table.Position=slot*8;WriteInt64(table,offset+1);
                offset=checked(offset+12+length);
            }
            this.slots.Dispose();this.slots=table;this.capacity=capacity;
        }catch{table.Dispose();throw;}
    }
    private static ulong Hash(ReadOnlySpan<byte> key){
        ulong hash=14695981039346656037;
        foreach(var value in key){hash=unchecked((hash^value)*1099511628211);}
        return hash;
    }
    private static long ReadInt64(Stream stream){
        Span<byte> bytes=stackalloc byte[8];stream.ReadExactly(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }
    private static void WriteInt64(Stream stream,long value){
        Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(bytes,value);stream.Write(bytes);
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public void Dispose(){
        if(this.disposed){return;}
        this.disposed=true;
        try{this.slots.Dispose();}
        finally{try{this.entries.Dispose();}finally{this.scope.Release(this);}}
    }
}
