using Seamless.Execution;
namespace Seamless.Linq;
internal sealed class ScopedLookup<TKey,TElement>:IDisposable{
    private readonly ExecutionScope scope;
    private readonly IEqualityComparer<TKey>? comparer;
    private readonly ResourceReservation memory;
    private readonly bool canSpill;
    private readonly long threshold;
    private Dictionary<Key,MemoryGroup>? index;
    private List<MemoryGroup>? groups=[];
    private DiskLookup<TKey,TElement>? disk;
    private bool disposed;
    internal ScopedLookup(ExecutionScope scope,IEqualityComparer<TKey>? comparer){
        this.scope=scope;this.comparer=comparer;
        if(comparer is null){scope.Codecs.Get<TKey>();}
        this.canSpill=scope.Codecs.CanEncode<TKey>()&&scope.Codecs.CanEncode<TElement>();
        this.threshold=Math.Max(256,(scope.Budget.MemoryLimitBytes-scope.Budget.MemoryBytes)/3);
        this.memory=scope.Budget.ReserveMemory(256);
        this.index=new(new KeyComparer(comparer));
        scope.Own(this);
    }
    internal void Add(TKey key,TElement element){
        this.Check();
        if(this.disk is not null){this.disk.Add(key,element);return;}
        long size=128L+2L*Unsafe.SizeOf<TElement>();
        if(this.scope.Codecs.CanEncode<TElement>()){size=checked(size+4L*this.scope.Codecs.Get<TElement>().GetEncodedLength(element));}
        if(this.scope.Codecs.CanEncode<TKey>()){size=checked(size+4L*this.scope.Codecs.Get<TKey>().GetEncodedLength(key));}
        if(this.canSpill&&this.memory.Bytes+size>this.threshold){
            this.Spill();this.disk!.Add(key,element);return;
        }
        if(!this.canSpill&&size>this.scope.Budget.MemoryLimitBytes-this.scope.Budget.MemoryBytes){
            if(!this.scope.Codecs.CanEncode<TKey>()){throw new CodecUnavailableException(typeof(TKey));}
            throw new CodecUnavailableException(typeof(TElement));
        }
        this.memory.Resize(checked(this.memory.Bytes+size));
        using var buffer=this.EncodeKey(key);
        var token=this.Token(key,buffer);
        if(!this.index!.TryGetValue(token,out var group)){
            if(buffer is not null){token=token with{Encoded=buffer.Memory.ToArray()};}
            group=new(this,key);
            this.groups!.Add(group);this.index.Add(token,group);
        }
        group.Values.Add(element);
    }
    internal IEnumerable<IGrouping<TKey,TElement>> Groups(){
        this.Check();
        if(this.disk is not null){return this.disk.Groups();}
        return this.groups!;
    }
    internal IEnumerable<TElement> Match(TKey key){
        this.Check();
        if(this.disk is not null){return this.disk.Match(key);}
        using var buffer=this.EncodeKey(key);
        if(this.index!.TryGetValue(this.Token(key,buffer),out var group)){return group;}
        return System.Linq.Enumerable.Empty<TElement>();
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
                foreach(var value in group.Values){disk.Add(group.Key,value);}
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
    private sealed class KeyComparer(IEqualityComparer<TKey>? comparer):IEqualityComparer<Key>{
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
            uint hash=2166136261;
            foreach(var value in key.Encoded.Span){hash=unchecked((hash^value)*16777619);}
            return unchecked((int)hash);
        }
    }
    private sealed class MemoryGroup(ScopedLookup<TKey,TElement> owner,TKey key):IGrouping<TKey,TElement>{
        private readonly ScopedLookup<TKey,TElement> owner=owner;
        public TKey Key{get;}=key;
        internal List<TElement> Values{get;}=[];
        public IEnumerator<TElement> GetEnumerator()=>this.Read().GetEnumerator();
        private IEnumerable<TElement> Read(){
            using var operation=this.owner.scope.EnterDependent();
            this.owner.Check();
            foreach(var value in this.Values){this.owner.Check();yield return value;}
        }
        IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
