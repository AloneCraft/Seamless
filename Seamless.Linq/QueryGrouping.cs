using Seamless.Execution;
using System.IO;
namespace Seamless.Linq;

/// <summary>生成queryから逃避するgroupの配列を構築予算へ計上し、callerへ所有権を渡す。</summary>
[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
public static class QueryGrouping{
    internal static IGrouping<TKey,TElement> CopyLookupGroup<TKey,TElement>(Grouping<TKey,TElement> source){
        var values=new TElement[source.Count];
        source.CopyTo(values,0);
        return new OwnedGrouping<TKey,TElement>(source.Key,values);
    }
    /// <summary>同じqueryだけでcodec解決と予約を共有する。返したgroupはこの構築器を保持しない。</summary>
    public sealed class Materializer<TKey,TElement>:IDisposable{
        private readonly ExecutionScope scope;
        private ResourceReservation? memory;
        private ResourceReservation? cacheMemory;
        private Dictionary<(int Worker,long Group),IGrouping<TKey,TElement>>? sharedGroups;
        private int sharedCapacity;
        private global::Seamless.Execution.Codecs.IAdapter<TKey>? keys;
        private global::Seamless.Execution.Codecs.IAdapter<TElement>? elements;
        private int fixedKeyLength=-1,fixedElementLength=-1;
        private bool disposed;
        public Materializer(ExecutionScope scope){ArgumentNullException.ThrowIfNull(scope);scope.ThrowIfUnavailable();this.scope=scope;scope.Own(this);}
        /// <summary>同じLookupの識別子ごとに所有結果を共有する。</summary>
        public IGrouping<TKey,TElement> MaterializeShared((int Worker,long Group) identity,IGrouping<TKey,TElement> source){
            this.Check();
            if(this.sharedGroups is null){
                var reservation=this.scope.Budget.ReserveMemory(128);
                try{
                    this.sharedGroups=new();
                    this.cacheMemory=reservation;
                }catch{
                    reservation.Dispose();
                    throw;
                }
            }
            if(this.sharedGroups.TryGetValue(identity,out var owned)){
                return owned;
            }
            if(this.sharedGroups.Count==this.sharedCapacity){
                var requested=Math.Max(4,checked(this.sharedCapacity*2));
                this.cacheMemory!.Resize(checked(128L+128L*requested+64L*this.sharedCapacity));
                this.sharedCapacity=this.sharedGroups.EnsureCapacity(requested);
                this.cacheMemory.Resize(checked(128L+64L*this.sharedCapacity));
            }
            owned=this.Materialize(source);
            this.sharedGroups.Add(identity,owned);
            return owned;
        }
        public IGrouping<TKey,TElement> Materialize(IGrouping<TKey,TElement> source){
            ArgumentNullException.ThrowIfNull(source);this.Check();var key=source.Key;
            var first=this.memory is null;
            if(first){
                var reservation=this.scope.Budget.ReserveMemory(4096);
                try{
                    var codecs=this.scope.Codecs.Fork();
                    if(codecs.CanEncode<TKey>()){this.keys=codecs.Get<TKey>();}
                    if(codecs.CanEncode<TElement>()){this.elements=codecs.Get<TElement>();}
                    this.fixedKeyLength=FixedLength(codecs,this.keys);
                    this.fixedElementLength=FixedLength(codecs,this.elements);
                    this.memory=reservation;
                }catch{reservation.Dispose();throw;}
            }
            var baseline=this.memory!.Bytes;
            try{
                var metadata=checked(128L+Unsafe.SizeOf<TKey>());
                if(this.keys is not null){var keyLength=this.fixedKeyLength;if(keyLength<0){keyLength=this.keys.GetEncodedLength(key);}metadata=checked(metadata+4L*keyLength);}
                this.memory.Resize(checked(baseline+metadata));
                var values=Array.Empty<TElement>();var count=0;long payload=0;
                long known=-1;if(source is IKnownGroupingCount knownGroup){known=knownGroup.Count;}
                var upfront=known>=0&&this.fixedElementLength>=0;
                if(known>=0){
                    if(known>Array.MaxLength){throw new ResourceLimitException(ResourceLimitKind.SingleElement,checked(known*Unsafe.SizeOf<TElement>()),this.scope.Budget.MemoryLimitBytes);}
                    if(upfront){payload=checked(known*(64L+4L*this.fixedElementLength));}
                    this.memory.Resize(checked(baseline+metadata+known*Unsafe.SizeOf<TElement>()+payload));values=new TElement[(int)known];
                }
                foreach(var value in source){
                    this.Check();
                    if(known>=0&&count==values.Length){throw new InvalidDataException("The internal group count changed during materialization.");}
                    if(upfront){values[count++]=value;continue;}
                    payload=checked(payload+64L);
                    if(this.elements is not null){var length=this.fixedElementLength;if(length<0){length=this.elements.GetEncodedLength(value);}payload=checked(payload+4L*length);}
                    var capacity=values.Length;if(count==capacity){capacity=Math.Max(4,checked(capacity*2));}
                    var steady=checked(baseline+metadata+capacity*(long)Unsafe.SizeOf<TElement>()+payload);
                    if(capacity!=values.Length){
                        this.memory.Resize(checked(steady+values.Length*(long)Unsafe.SizeOf<TElement>()));
                        var next=new TElement[capacity];values.CopyTo(next,0);values=next;
                    }
                    this.memory.Resize(steady);values[count++]=value;
                }
                this.Check();if(known>=0&&count!=known){throw new InvalidDataException("The internal group count changed during materialization.");}TElement[] result;
                if(count==values.Length){result=values;}
                else{
                    this.memory.Resize(checked(this.memory.Bytes+32L+count*(long)Unsafe.SizeOf<TElement>()));result=new TElement[count];values.AsSpan(0,count).CopyTo(result);
                }
                this.Check();return new OwnedGrouping<TKey,TElement>(key,result);
            }catch{
                if(first){this.memory.Dispose();this.memory=null;this.keys=null;this.elements=null;this.fixedKeyLength=this.fixedElementLength=-1;}
                else{this.memory.Resize(baseline);}
                throw;
            }
        }
        private static int FixedLength<T>(CodecRegistry codecs,global::Seamless.Execution.Codecs.IAdapter<T>? codec){
            // 既定codec・参照なし・nullableなしのfield値だけ。custom codecの呼出しや可変payloadを省略しない。
            if(codec is null||RuntimeHelpers.IsReferenceOrContainsReferences<T>()||!codecs.CanUseParallelCodec<T>()||HasNullable(typeof(T))){return -1;}
            return codec.GetEncodedLength(default!);
        }
        private static bool HasNullable(Type type){
            if(Nullable.GetUnderlyingType(type) is not null){return true;}
            if(type.IsEnum||type.IsPrimitive||type==typeof(decimal)||type==typeof(Guid)||type==typeof(DateTime)){return false;}
            return type.GetFields(global::System.Reflection.BindingFlags.Instance|global::System.Reflection.BindingFlags.Public|global::System.Reflection.BindingFlags.NonPublic).Any(static field=>HasNullable(field.FieldType));
        }
        private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
        public void Dispose(){
            if(this.disposed){return;}
            this.disposed=true;
            this.sharedGroups=null;
            try{this.memory?.Dispose();}
            finally{
                try{this.cacheMemory?.Dispose();}
                finally{this.scope.Release(this);}
            }
        }
    }
    public static IGrouping<TKey,TElement> Materialize<TKey,TElement>(ExecutionScope scope,IGrouping<TKey,TElement> source){
        ArgumentNullException.ThrowIfNull(scope);ArgumentNullException.ThrowIfNull(source);scope.ThrowIfUnavailable();
        var key=source.Key;var bytes=checked(64L+Unsafe.SizeOf<TKey>());
        if(scope.Codecs.CanEncode<TKey>()){bytes=checked(bytes+4L*scope.Codecs.Get<TKey>().GetEncodedLength(key));}
        var metadata=scope.Budget.ReserveMemory(bytes);QueryResultBuffer<TElement>? values=null;
        try{
            values=new(scope);
            foreach(var value in source){values.Add(value);}
            var array=values.ToArray();scope.ThrowIfUnavailable();scope.Own(metadata);
            // valuesはscopeが所有する。ここで解放すると後続投影と外側の結果構築中に配列の予約が消える。
            return new OwnedGrouping<TKey,TElement>(key,array);
        }catch{values?.Dispose();metadata.Dispose();throw;}
    }
    private sealed class OwnedGrouping<TKey,TElement>(TKey key,TElement[] values):IGrouping<TKey,TElement>,IList<TElement>,IReadOnlyList<TElement>{
        public TKey Key{get;}=key;
        public int Count=>values.Length;
        public TElement this[int index]{get{if((uint)index>=(uint)values.Length){throw new ArgumentOutOfRangeException(nameof(index));}return values[index];}}
        TElement IList<TElement>.this[int index]{get=>this[index];set=>throw new NotSupportedException();}
        bool ICollection<TElement>.IsReadOnly=>true;
        bool ICollection<TElement>.Contains(TElement item)=>Array.IndexOf(values,item)>=0;
        int IList<TElement>.IndexOf(TElement item)=>Array.IndexOf(values,item);
        void ICollection<TElement>.CopyTo(TElement[] array,int arrayIndex)=>Array.Copy(values,0,array,arrayIndex,values.Length);
        void ICollection<TElement>.Add(TElement item)=>throw new NotSupportedException();
        bool ICollection<TElement>.Remove(TElement item)=>throw new NotSupportedException();
        void ICollection<TElement>.Clear()=>throw new NotSupportedException();
        void IList<TElement>.Insert(int index,TElement item)=>throw new NotSupportedException();
        void IList<TElement>.RemoveAt(int index)=>throw new NotSupportedException();
        public IEnumerator<TElement> GetEnumerator()=>((IEnumerable<TElement>)values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
// Build完了後の内部Lookupだけが実装する。未知の利用者groupのCount getterは評価しない。
internal interface IKnownGroupingCount{long Count{get;}}
