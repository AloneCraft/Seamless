using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Collections;
/// <summary>
/// Tsavorite をバックエンドに使うオンメモリ HashSet。
/// <see cref="ISet{T}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 重複を許さず、キーのみを格納する。値は EqualityEmpty 構造体を使用する。
/// </summary>
[DebuggerDisplay("Count = {Count}")]
public abstract partial class ManagedHashSet<TKey>where TKey : IEquatable<TKey>{
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        EqualityEmpty,//TInput
        EqualityEmpty,//TOutput
        EqualityEmpty,//TContext
        IntersectWith,//TFunctions


        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer
        >,//TStoreFunctions


        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer
            >
        >
    > IntersectWith_ClientSession;
    private readonly ClientSession<
        FixedSpanByteKey,//TKey

        PinnedSpanByte,//TInput
        SpanByteAndMemory,//TOutput
        EqualityEmpty,//TContext
        SymmetricExceptWith,//TFunctions
        StoreFunctions<
            SpanByteComparer,//TKeyComparer
            DefaultRecordDisposer//TRecordDisposer


        >,
        SpanByteAllocator<
            StoreFunctions<
                SpanByteComparer,//TKeyComparer
                DefaultRecordDisposer//TRecordDisposer


            >
        >
    > SymmetricExceptWith_ClientSession;
    internal ManagedHashSet(ManagedCollectionStore<TKey,EqualityEmpty> CollectionStore):base(CollectionStore){
        var Store=CollectionStore.Store;
        this.IntersectWith_ClientSession      =Store.NewSession<
            FixedSpanByteKey,//TKey
            EqualityEmpty,//TInput
            EqualityEmpty,//TOutput
            EqualityEmpty,//TContext
            IntersectWith//TFunctions
        >(new());
        this.SymmetricExceptWith_ClientSession=Store.NewSession<
            FixedSpanByteKey,//TKey
            PinnedSpanByte,//TInput
            SpanByteAndMemory,//TOutput
            EqualityEmpty,//TContext
            SymmetricExceptWith//TFunctions
        >(new());
    }
#if true
    //KEYがbyte[]の場合、ScanIteratorFunctions<byte[]>を直接実装したEnumeratorを使用する。そうでない場合は、KeyをSpan<byte>に変換してからScanIteratorFunctions<byte[]>を実装したEnumeratorを使用する。
    //これは現在上手くいく
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator()=>new (this.ReadClientSession,new());
    private Enumerator<
        SpanByteKeyIterator,
        byte[]
    > GetSpanByteEnumerator()=>new (this.ReadClientSession,new());
    private sealed class SpanByteKeyIterator:ScanIteratorFunctions<byte[]> {
        private protected override byte[] Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
            logRecord.Key.ToArray();
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var IntersectWith_ClientSession = this.IntersectWith_ClientSession;
        var Input = default(EqualityEmpty);
        var Output = default(EqualityEmpty);
        var Enumerator = this.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()) {
            var KeyBytes = Enumerator.Current;
            if(other.PrivateProtectedContainsKey(KeyBytes)) continue;
            if(this.WouldOverflowMutable(KeyBytes.Length,1)) throw this.ThrowLogFull();
            var FixedKey = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            var Status = IntersectWith_ClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        }
        IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        var Input = default(PinnedSpanByte);
        var Enumerator = other.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()){
            var KeyBytes = Enumerator.Current;
            var Key = (FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
#else
    //これは現在上手くいかない
    public Enumerator<
        KeyIterator,
        TKey
    > GetEnumerator()=>new(this.ReadClientSession,new());
    private SpanByteEnumerator GetSpanByteEnumerator()=>new(this.ReadClientSession);
    private const int KeyBufferCapacity=256;
    [InlineArray(KeyBufferCapacity)]
    private struct KeyBuffer{private byte _;}
    private struct SpanByteKeyIterator:IScanIteratorFunctions{
        private KeyBuffer _buf0,_buf1;
        private int _len0,_len1;
        public int Count;
        internal ReadOnlySpan<byte> this[int i]{
            get{
                if(i==0)
                    return MemoryMarshal.CreateReadOnlySpan(ref this._buf0[0],this._len0);
                else
                    return MemoryMarshal.CreateReadOnlySpan(ref this._buf1[0],this._len1);
            }
        }
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord:ISourceLogRecord{
            ReadOnlySpan<byte> key=logRecord.Key;
            var count=this.Count;
            if(count==0){
                key.CopyTo(MemoryMarshal.CreateSpan(ref this._buf0[0],KeyBufferCapacity));
                this._len0=key.Length;
            }else{
                key.CopyTo(MemoryMarshal.CreateSpan(ref this._buf1[0],KeyBufferCapacity));
                this._len1=key.Length;
            }
            count++;
            this.Count=count;
            cursorRecordResult=count==Const.Chunk_Length?CursorRecordResult.EndBatch:CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress){
            this.Count=0;
            return true;
        }
        public void OnStop(bool completed,long numberOfRecords){}
        public void OnException(Exception exception,long numberOfRecords){}
    }
    private ref struct SpanByteEnumerator(
        ClientSession<
            FixedSpanByteKey,
            EqualityEmpty,
            SpanByteAndMemory,
            EqualityEmpty,
            ManagedRead,
            ManagedStoreFunctions,
            ManagedAllocator
        > Session){
        private SpanByteKeyIterator _iterator;
        private long _cursor;
        private int _count,_index=-1;
        private bool _done;
        public ReadOnlySpan<byte> Current=>
            this._iterator[this._index];
        public bool MoveNext(){
            if(++this._index<this._count) return true;
            if(this._done){
                this._index=-1;return false;}
            this._iterator.Count=0;
            var r=Session.IterateLookup(ref this._iterator,ref this._cursor,resetCursor:false);
            var count=this._iterator.Count;
            this._count=count;
            if(!r)
                this._done=true;
            if(count>0){
                this._index=0;return true;}
            this._index=-1;
            return false;
        }
    }
    public partial void IntersectWith(ManagedHashSet<TKey> other){
        ArgumentNullException.ThrowIfNull(other);
        var IntersectWith_ClientSession = this.IntersectWith_ClientSession;
        var Input=default(EqualityEmpty);
        var Output=default(EqualityEmpty);
        var Enumerator = this.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()) {
            var KeyBytes = Enumerator.Current;
            if(other.PrivateProtectedContainsKey(KeyBytes)) continue;
            if(this.WouldOverflowMutable(KeyBytes.Length,1)) throw this.ThrowLogFull();
            var FixedKey = (FixedSpanByteKey)KeyBytes;
            var Status = IntersectWith_ClientSession.BasicContext.RMW(FixedKey,ref Input,ref Output);
        }
        IntersectWith_ClientSession.BasicContext.CompletePending(wait: true);
    }
    public partial void SymmetricExceptWith(ManagedHashSet<TKey> other) {
        ArgumentNullException.ThrowIfNull(other);
        var Session = this.SymmetricExceptWith_ClientSession;
        var Input = default(PinnedSpanByte);
        var Enumerator = other.GetSpanByteEnumerator();
        while(Enumerator.MoveNext()){
            var KeyBytes = Enumerator.Current;
            var Key = (FixedSpanByteKey)KeyBytes;
            SpanByteAndMemory Output = default;
            var Status=Session.BasicContext.RMW(Key,ref Input,ref Output);
        }
        Session.BasicContext.CompletePending(wait: true);
    }
#endif
}
