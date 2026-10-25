using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Seamless.Execution;
namespace Seamless.Collections.Persisted;
/// <summary>A sequential result stored in the scope's temporary storage, without materializing all rows in memory.</summary>
public sealed class ScopedStoredSequence<T>:IEnumerable<T>,IDisposable{
    private readonly ExecutionScope scope;
    private readonly global::Seamless.Execution.Codecs.IAdapter<T> codec;
    private readonly TemporaryFile file;
    private bool sealedForReading;
    private bool disposed;
    public ScopedStoredSequence(ExecutionScope scope){
        this.scope=scope;this.codec=scope.Codecs.Get<T>();
        this.file=scope.CreateTemporaryFile();
        scope.Own(this);
    }
    public long LongCount{get;private set;}
    public int Count=>checked((int)this.LongCount);
    public void Append(T value){
        this.Check();
        if(this.sealedForReading){throw new InvalidOperationException("This stored sequence is sealed for reading.");}
        using var encoded=this.scope.Encode(value);
        this.file.Position=this.file.Length;
        Span<byte> prefix=stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(prefix,encoded.Memory.Length);
        this.file.Write(prefix);this.file.Write(encoded.Memory.Span);
        this.LongCount=checked(this.LongCount+1);
    }
    public void Seal(){this.Check();this.file.Flush();this.sealedForReading=true;}
    public IEnumerator<T> GetEnumerator()=>this.Read().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    private IEnumerable<T> Read(){
        using var operation=this.scope.EnterDependent();
        this.Check();
        if(!this.sealedForReading){throw new InvalidOperationException("Seal the stored sequence before reading.");}
        long position=0;
        while(position<this.file.Length){
            this.Check();
            this.file.Position=position;
            Span<byte> prefix=stackalloc byte[4];this.file.ReadExactly(prefix);
            var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if(length<0||length>this.file.Length-this.file.Position){throw new InvalidDataException("Invalid stored sequence record.");}
            using var decoded=this.scope.Budget.ReserveMemory(checked(96+4L*length));
            T value;
            using(var buffer=this.scope.Rent(length)){
                this.file.ReadExactly(buffer.Memory.Span);
                value=this.codec.Decode(buffer.Memory.Span);
                position=this.file.Position;
            }
            yield return value;
        }
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;
        try{this.file.Dispose();}finally{this.scope.Release(this);}
    }
}
