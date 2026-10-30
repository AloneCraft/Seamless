using System.Buffers.Binary;
using System.IO;
using Seamless.Collections.Persisted;
using Seamless.Execution;
namespace Seamless.Linq;
internal sealed class DiskLookup<TKey,TElement>:IDisposable{
    private readonly ExecutionScope scope;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TKey> keys;
    private readonly global::Seamless.Execution.Codecs.IAdapter<TElement> elements;
    private readonly IEqualityComparer<TKey>? comparer;
    private readonly ScopedByteIndex index;
    private readonly TemporaryFile groups;
    private readonly TemporaryFile values;
    private bool disposed;
    internal DiskLookup(ExecutionScope scope,IEqualityComparer<TKey>? comparer){
        this.scope=scope;this.comparer=comparer;
        this.keys=scope.Codecs.Get<TKey>();this.elements=scope.Codecs.Get<TElement>();
        this.index=new(scope);
        this.groups=scope.CreateTemporaryFile();
        this.values=scope.CreateTemporaryFile();
        scope.Own(this);
    }
    internal void Add(TKey key,TElement value){
        this.Check();
        using var encodedKey=this.scope.Encode(key);
        var group=this.Find(key,encodedKey.Memory.Span);
        using var encodedValue=this.scope.Encode(value);
        var offset=this.values.Length;
        this.values.Position=offset;
        WriteLong(this.values,-1);WriteInt(this.values,encodedValue.Memory.Length);this.values.Write(encodedValue.Memory.Span);
        if(group<0){
            group=this.groups.Length;
            long previous=-1;
            this.TryIndex(key,encodedKey.Memory.Span,out previous);
            this.groups.Position=group;
            WriteLong(this.groups,offset);WriteLong(this.groups,offset);WriteLong(this.groups,1);WriteLong(this.groups,previous);
            WriteInt(this.groups,encodedKey.Memory.Length);this.groups.Write(encodedKey.Memory.Span);
            this.SetIndex(key,encodedKey.Memory.Span,group);
        }else{
            this.groups.Position=group+8;
            var last=ReadLong(this.groups);var count=ReadLong(this.groups);
            this.values.Position=last;WriteLong(this.values,offset);
            this.groups.Position=group+8;WriteLong(this.groups,offset);WriteLong(this.groups,checked(count+1));
        }
    }
    internal IEnumerable<IGrouping<TKey,TElement>> Groups(){
        long offset=0;
        while(offset<this.groups.Length){
            this.Check();
            this.groups.Position=offset+32;var length=ReadInt(this.groups);
            var group=offset;offset=checked(offset+36+length);
            yield return new Group(this,group);
        }
    }
    internal IEnumerable<TElement> Match(TKey key){
        this.Check();
        using var encoded=this.scope.Encode(key);
        var group=this.Find(key,encoded.Memory.Span);
        if(group<0){return System.Linq.Enumerable.Empty<TElement>();}
        return this.ReadValues(group);
    }
    private long Find(TKey key,ReadOnlySpan<byte> encoded){
        if(!this.TryIndex(key,encoded,out var group)){return -1;}
        if(this.comparer is null){return group;}
        while(group>=0){
            this.groups.Position=group+24;var next=ReadLong(this.groups);
            var stored=this.ReadKey(group);
            if(this.comparer.Equals(key,stored)){return group;}
            group=next;
        }
        return -1;
    }
    private bool TryIndex(TKey key,ReadOnlySpan<byte> encoded,out long group){
        bool found;
        if(this.comparer is null){found=this.index.TryGetValue(encoded,out group);}
        else{
            Span<byte> hash=stackalloc byte[4];
            var value=0;if(key is not null){value=this.comparer.GetHashCode(key);}
            BinaryPrimitives.WriteInt32LittleEndian(hash,value);
            found=this.index.TryGetValue(hash,out group);
        }
        if(found){return true;}
        group=-1;return false;
    }
    private void SetIndex(TKey key,ReadOnlySpan<byte> encoded,long group){
        if(this.comparer is null){this.index.Set(encoded,group);return;}
        Span<byte> hash=stackalloc byte[4];
        var value=0;if(key is not null){value=this.comparer.GetHashCode(key);}
        BinaryPrimitives.WriteInt32LittleEndian(hash,value);this.index.Set(hash,group);
    }
    private TKey ReadKey(long group){
        this.Check();
        this.groups.Position=group+32;var length=ReadInt(this.groups);
        using var buffer=this.scope.Rent(length);
        this.groups.ReadExactly(buffer.Memory.Span);
        return this.keys.Decode(buffer.Memory.Span);
    }
    private IEnumerable<TElement> ReadValues(long group){
        using var operation=this.scope.EnterDependent();
        this.Check();
        this.groups.Position=group;var next=ReadLong(this.groups);
        while(next>=0){
            this.Check();
            this.values.Position=next;
            next=ReadLong(this.values);var length=ReadInt(this.values);
            using var decoded=this.scope.Budget.ReserveMemory(checked(96+4L*length));
            TElement value;
            using(var buffer=this.scope.Rent(length)){
                this.values.ReadExactly(buffer.Memory.Span);value=this.elements.Decode(buffer.Memory.Span);
            }
            yield return value;
        }
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public void Dispose(){
        if(this.disposed){return;}
        this.disposed=true;
        try{this.index.Dispose();}finally{try{this.groups.Dispose();}finally{this.values.Dispose();this.scope.Release(this);}}
    }
    private static int ReadInt(Stream stream){Span<byte> bytes=stackalloc byte[4];stream.ReadExactly(bytes);return BinaryPrimitives.ReadInt32LittleEndian(bytes);}
    private static long ReadLong(Stream stream){Span<byte> bytes=stackalloc byte[8];stream.ReadExactly(bytes);return BinaryPrimitives.ReadInt64LittleEndian(bytes);}
    private static void WriteInt(Stream stream,int value){Span<byte> bytes=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,value);stream.Write(bytes);}
    private static void WriteLong(Stream stream,long value){Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(bytes,value);stream.Write(bytes);}
    private sealed class Group(DiskLookup<TKey,TElement> owner,long offset):IGrouping<TKey,TElement>{
        private readonly DiskLookup<TKey,TElement> owner=owner;
        private readonly long offset=offset;
        public TKey Key=>this.owner.ReadKey(this.offset);
        public IEnumerator<TElement> GetEnumerator()=>this.owner.ReadValues(this.offset).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
