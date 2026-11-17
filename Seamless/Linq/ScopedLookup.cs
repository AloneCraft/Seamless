using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
using Seamless.Execution;
namespace Seamless.Linq;
internal sealed class ScopedLookup<TKey,TElement>:IDisposable{
    private static readonly bool inlineValues=!RuntimeHelpers.IsReferenceOrContainsReferences<TKey>()&&!RuntimeHelpers.IsReferenceOrContainsReferences<TElement>();
    private readonly ExecutionScope scope;
    private readonly IEqualityComparer<TKey>? comparer;
    private readonly ResourceReservation memory;
    private readonly bool canSpill;
    private readonly long intKeyReservationBytes;
    private readonly bool spillAllowed;
    private readonly long threshold;
    private readonly bool usesPool;
    private Dictionary<Key,MemoryGroup>? index;
    private List<MemoryGroup>? groups=[];
    private DiskLookup<TKey,TElement>? disk;
    private bool disposed;
    internal ScopedLookup(ExecutionScope scope,IEqualityComparer<TKey>? comparer,bool allowSpill=true,ResourceBudget.MemoryPool? retentionPool=null,long maxRetention=0){
        this.scope=scope;this.comparer=comparer;
        if(comparer is null){scope.Codecs.Get<TKey>();}
        this.spillAllowed=allowSpill;this.canSpill=allowSpill&&scope.Codecs.CanEncode<TKey>()&&scope.Codecs.CanEncode<TElement>();
        this.usesPool=retentionPool is not null;
        this.threshold=this.usesPool?maxRetention:Math.Max(256,(scope.Budget.MemoryLimitBytes-scope.Budget.MemoryBytes)/3);
        this.memory=retentionPool is null?scope.Budget.ReserveMemory(256):scope.Budget.ReserveMemory(256,retentionPool);
        this.intKeyReservationBytes=typeof(TKey)==typeof(int)&&comparer is null&&scope.Codecs.UsesDefaultInt32Codec
            ?scope.Codecs.Get<int>().GetEncodedLength(0)+64L:0;
        try{this.index=new(new KeyComparer(this.intKeyReservationBytes!=0?EqualityComparer<TKey>.Default:comparer));scope.Own(this);}
        catch{this.memory.Dispose();throw;}
    }
    internal void Add(TKey key,TElement element){
        this.Check();
        if(this.disk is not null){this.disk.Add(key,element);return;}
        if(inlineValues&&this.comparer is not null){this.AddInline(key,element);return;}
        long size=128L+2L*Unsafe.SizeOf<TElement>();
        if(this.scope.Codecs.CanEncode<TElement>()){size=checked(size+4L*this.scope.Codecs.Get<TElement>().GetEncodedLength(element));}
        if(this.intKeyReservationBytes!=0){size=checked(size+4L*(this.intKeyReservationBytes-64));}
        else if(this.scope.Codecs.CanEncode<TKey>()){size=checked(size+4L*this.scope.Codecs.Get<TKey>().GetEncodedLength(key));}
        if(this.canSpill&&this.memory.Bytes+size>this.threshold){
            this.Spill();this.disk!.Add(key,element);return;
        }
        if(this.spillAllowed&&!this.canSpill&&size>this.scope.Budget.MemoryLimitBytes-this.scope.Budget.MemoryBytes){
            if(!this.scope.Codecs.CanEncode<TKey>()){throw new CodecUnavailableException(typeof(TKey));}
            throw new CodecUnavailableException(typeof(TElement));
        }
        this.memory.Resize(checked(this.memory.Bytes+size));
        if(this.intKeyReservationBytes!=0){
            using var reservation=this.ReserveIntKey();
            AddOwned(null);
            return;
        }
        if(typeof(TKey)==typeof(int)&&this.comparer is null){
            var codec=this.scope.Codecs.Get<TKey>();
            var length=codec.GetEncodedLength(key);
            if(length is >=0 and <=64){
                this.scope.ThrowIfUnavailable();
                var bytes=length+64L;
                if(bytes>this.scope.Budget.MemoryLimitBytes){
                    throw new ResourceLimitException(ResourceLimitKind.SingleElement,bytes,this.scope.Budget.MemoryLimitBytes);
                }
                using var reservation=this.scope.Budget.ReserveMemory(bytes);
                Span<byte> encoded=stackalloc byte[length];
                encoded.Clear();
                this.scope.ThrowIfUnavailable();
                codec.Encode(key,encoded);
                this.Check();
                var alternate=this.index!.GetAlternateLookup<ReadOnlySpan<byte>>();
                if(!alternate.TryGetValue(encoded,out var existing)){
                    existing=new(this,key,this.groups!.Count);
                    this.groups!.Add(existing);
                    this.index.Add(new(key,encoded.ToArray()),existing);
                }
                existing.Add(element);
                return;
            }
            using var owned=this.scope.Rent(length);
            codec.Encode(key,owned.Memory.Span);
            AddOwned(owned);
            return;
        }
        using var buffer=this.EncodeKey(key);
        AddOwned(buffer);
        void AddOwned(OwnedBuffer? owned){
            var token=this.Token(key,owned);
            if(!this.index!.TryGetValue(token,out var group)){
                if(owned is not null){token=token with{Encoded=owned.Memory.ToArray()};}
                group=new(this,key,this.groups!.Count);
                this.groups!.Add(group);this.index.Add(token,group);
            }
            group.Add(element);
        }
    }
    private void AddInline(TKey key,TElement element){
        // 比較器付きのunmanaged値は既に配列内にある。符号化したobject graphを毎行重ねて予約しない。
        // group管理費と実capacityを計上し、拡大時は新旧配列の重なりも割当前に予約する。
        var token=new Key(key,ReadOnlyMemory<byte>.Empty);
        var found=this.index!.TryGetValue(token,out var group);
        var previousCapacity=group?.Values.Capacity??0;
        var nextCapacity=previousCapacity;
        if(!found||group!.Values.Count==previousCapacity){if(previousCapacity==0){nextCapacity=4;}else{nextCapacity=checked(previousCapacity*2);}}
        long additional=0;
        if(!found){additional=checked(256L+8L*Unsafe.SizeOf<TKey>());}
        if(nextCapacity!=previousCapacity){additional=checked(additional+24L+(long)nextCapacity*Unsafe.SizeOf<TElement>());}
        var required=checked(this.memory.Bytes+additional);
        if(this.canSpill&&(required>this.threshold||(this.usesPool&&additional!=0&&!this.memory.TryResize(required)))){
            this.Spill();this.disk!.Add(key,element);return;
        }
        if(this.spillAllowed&&!this.canSpill&&additional>this.scope.Budget.AvailableMemoryBytes){
            if(!this.scope.Codecs.CanEncode<TKey>()){throw new CodecUnavailableException(typeof(TKey));}
            throw new CodecUnavailableException(typeof(TElement));
        }
        if(additional!=0&&!this.usesPool){this.memory.Resize(required);}
        if(!found){group=new(this,key,this.groups!.Count);this.groups.Add(group);this.index.Add(token,group);}
        if(nextCapacity!=previousCapacity){
            group!.Values.Capacity=nextCapacity;
            if(previousCapacity!=0){this.memory.Resize(checked(this.memory.Bytes-24L-(long)previousCapacity*Unsafe.SizeOf<TElement>()));}
        }
        group!.Values.Add(element);
    }
    internal IEnumerable<IGrouping<TKey,TElement>> Groups(){
        this.Check();
        if(this.disk is not null){return this.disk.Groups();}
        return this.groups!;
    }
    internal IEnumerable<TElement> Match(TKey key){
        this.Check();
        if(this.disk is not null){return this.disk.Match(key);}
        if(this.intKeyReservationBytes!=0){
            using var reservation=this.ReserveIntKey();
            return this.index!.TryGetValue(new(key,ReadOnlyMemory<byte>.Empty),out var match)?match:System.Linq.Enumerable.Empty<TElement>();
        }
        if(typeof(TKey)==typeof(int)&&this.comparer is null){
            var codec=this.scope.Codecs.Get<TKey>();
            var length=codec.GetEncodedLength(key);
            if(length is >=0 and <=64){
                this.scope.ThrowIfUnavailable();
                var size=length+64L;
                if(size>this.scope.Budget.MemoryLimitBytes){
                    throw new ResourceLimitException(ResourceLimitKind.SingleElement,size,this.scope.Budget.MemoryLimitBytes);
                }
                using var reservation=this.scope.Budget.ReserveMemory(size);
                Span<byte> bytes=stackalloc byte[length];
                bytes.Clear();
                this.scope.ThrowIfUnavailable();
                codec.Encode(key,bytes);
                this.Check();
                var alternate=this.index!.GetAlternateLookup<ReadOnlySpan<byte>>();
                return alternate.TryGetValue(bytes,out var match)?match:System.Linq.Enumerable.Empty<TElement>();
            }
            using var encoded=this.scope.Rent(length);
            codec.Encode(key,encoded.Memory.Span);
            return this.index!.TryGetValue(this.Token(key,encoded),out var fallback)?fallback:System.Linq.Enumerable.Empty<TElement>();
        }
        using var buffer=this.EncodeKey(key);
        if(this.index!.TryGetValue(this.Token(key,buffer),out var group)){return group;}
        return System.Linq.Enumerable.Empty<TElement>();
    }
    internal IGrouping<TKey,TElement>? FindGroup(TKey key,out long identity){
        this.Check();
        if(this.disk is not null){
            return this.disk.FindGroup(key,out identity);
        }
        using var buffer=this.EncodeKey(key);
        if(this.index!.TryGetValue(this.Token(key,buffer),out var group)){
            identity=group.Identity;
            return group;
        }
        identity=-1;
        return null;
    }
    private ResourceReservation ReserveIntKey(){
        this.scope.ThrowIfUnavailable();
        var size=this.intKeyReservationBytes;
        if(size>this.scope.Budget.MemoryLimitBytes){
            throw new ResourceLimitException(ResourceLimitKind.SingleElement,size,this.scope.Budget.MemoryLimitBytes);
        }
        return this.scope.Budget.ReserveMemory(size);
    }
    private OwnedBuffer? EncodeKey(TKey key){
        if(this.comparer is not null){return null;}
        return this.scope.Encode(key);
    }
    private Key Token(TKey key,OwnedBuffer? buffer){
        if(buffer is not null){return new(key,buffer.Memory);}
        return new(key,ReadOnlyMemory<byte>.Empty);
    }
    private void Spill(){
        var disk=new DiskLookup<TKey,TElement>(this.scope,this.comparer);
        try{
            foreach(var group in this.groups!){
                group.WriteTo(disk);
            }
            this.disk=disk;this.groups=null;this.index=null;this.memory.Resize(0);
        }catch{disk.Dispose();throw;}
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public void Dispose(){
        if(this.disposed){return;}this.disposed=true;
        try{this.disk?.Dispose();}finally{this.groups=null;this.index=null;this.memory.Dispose();this.scope.Release(this);}
    }
    private readonly record struct Key(TKey Value,ReadOnlyMemory<byte> Encoded);
    private sealed class KeyComparer(IEqualityComparer<TKey>? comparer):IEqualityComparer<Key>,IAlternateEqualityComparer<ReadOnlySpan<byte>,Key>{
        private readonly IEqualityComparer<TKey>? comparer=comparer;
        public bool Equals(Key x,Key y){
            if(this.comparer is not null){return this.comparer.Equals(x.Value,y.Value);}
            return x.Encoded.Span.SequenceEqual(y.Encoded.Span);
        }
        public int GetHashCode(Key key){
            if(this.comparer is not null){
                if(key.Value is null){return 0;}
                return this.comparer.GetHashCode(key.Value);
            }
            return this.GetHashCode(key.Encoded.Span);
        }
        public bool Equals(ReadOnlySpan<byte> bytes,Key key)=>bytes.SequenceEqual(key.Encoded.Span);
        public Key Create(ReadOnlySpan<byte> bytes)=>throw new NotSupportedException();
        public int GetHashCode(ReadOnlySpan<byte> bytes){
            uint hash=2166136261;
            foreach(var value in bytes){hash=unchecked((hash^value)*16777619);}
            return unchecked((int)hash);
        }
    }
    private sealed class MemoryGroup(ScopedLookup<TKey,TElement> owner,TKey key,int identity):IGrouping<TKey,TElement>,IKnownGroupingCount{
        private readonly ScopedLookup<TKey,TElement> owner=owner;
        internal int Identity{get;}=identity;
        public TKey Key{get;}=key;
        private bool hasFirst;
        private TElement first=default!;
        private List<TElement>? remaining;
        internal void Add(TElement value){
            if(this.hasFirst){
                (this.remaining??=[]).Add(value);
            }else{
                this.first=value;
                this.hasFirst=true;
            }
        }
        internal void WriteTo(DiskLookup<TKey,TElement> disk){
            if(this.hasFirst){disk.Add(this.Key,this.first);}
            if(this.remaining is not null){
                foreach(var value in this.remaining){disk.Add(this.Key,value);}
            }
        }
        internal List<TElement> Values=>this.remaining??=[];
        long IKnownGroupingCount.Count{get{this.owner.Check();return (this.hasFirst?1:0)+(this.remaining?.Count??0);}}
        public IEnumerator<TElement> GetEnumerator()=>this.Read().GetEnumerator();
        private IEnumerable<TElement> Read(){
            using var operation=this.owner.scope.EnterDependent();
            this.owner.Check();
            if(this.hasFirst){yield return this.first;}
            if(this.remaining is not null){
                foreach(var value in this.remaining){this.owner.Check();yield return value;}
            }
        }
        IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
