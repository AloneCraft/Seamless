using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public abstract partial class UnmanagedCollection<TKey, TValue, TKeySerializer, TValueSerializer>:Collection<
    TKey,
    TValue,
    TKey,//TSessionKey
    TValue,//TSessionValue
    TValue,//TReadOutput
    TValue,//TAddInput
    UnmanagedRead<
        TKey,
        TValue
    >,
    UnmanagedAdd<
        TKey,
        TValue
    >,
    UnmanagedContainsDelete<
        TKey,
        TValue
    >,
    StoreFunctions<
        TKey,
        TValue,
        UnmanagedKeyComparer<
            TKey
        >,
        DefaultRecordDisposer<
            TKey,
            TValue
        >
    >,
    BlittableAllocator<
        TKey,
        TValue,
        StoreFunctions<
            TKey,
            TValue,
            UnmanagedKeyComparer<
                TKey
            >,
            DefaultRecordDisposer<
                TKey,
                TValue
            >
        >
    >
>
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue>
    where TKeySerializer : ISerializer<TKey>
    where TValueSerializer : ISerializer<TValue> {
    public sealed partial class KeyIterator:ScanIteratorFunctions<TKey> {
        private protected override TKey Deserialize(ref TKey key,ref TValue value) =>
            key;
    }
    internal sealed partial class ValueIterator:ScanIteratorFunctions<TValue> {
        private protected override TValue Deserialize(ref TKey key,ref TValue value) =>
            value;
    }
    public sealed partial class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>> {
        private protected override KeyValuePair<TKey,TValue> Deserialize(ref TKey key,ref TValue value) =>
            new(
                key,
                value
            );
    }
    public struct Enumerator<TIterator, T>(
        ClientSession<
            TKey,//TKey
            TValue,//TValue
            EqualityEmpty,//TInput
            TValue,//TOutput
            EqualityEmpty,//TContext
            UnmanagedRead<
                TKey,
                TValue
            >,//TReadFunctions
            StoreFunctions<
                TKey,
                TValue,
                UnmanagedKeyComparer<
                    TKey
                >,
                DefaultRecordDisposer<
                    TKey,
                    TValue
                >
            >,//TStoreFunctions
            BlittableAllocator<
                TKey,
                TValue,
                StoreFunctions<
                    TKey,
                    TValue,
                    UnmanagedKeyComparer<
                        TKey
                    >,
                    DefaultRecordDisposer<
                        TKey,
                        TValue
                    >
                >
            >//TAllocator
        > Session,
        TIterator Iterator,
        long InitialCursor = 0
    ):IEnumerator<T>
        where TIterator : ScanIteratorFunctions<
            T
        > {
        private long Cursor = InitialCursor, Count;
        private int Index = -1;
        private bool Done;
        public readonly T Current => Iterator.Chunk[this.Index];
        readonly object? IEnumerator.Current => this.Current;
        public bool MoveNext() {
            if(++this.Index<this.Count)
                return true;
            if(this.Done) {
                this.Index=-1;
                return false;
            }
            Iterator.Count=0;
            var 最後まで読み切ったか = Session.IterateLookup(ref Iterator,ref this.Cursor,resetCursor: false);
            Session.BasicContext.CompletePending(wait: true);
            var Count = Iterator.Count;
            this.Count=Count;
            if(最後まで読み切ったか) {
            }
            else {
                this.Done=true;
            }
            if(Count>0) {
                this.Index=0;
                return true;
            }
            this.Index=-1;
            return false;
        }
        public void Reset() {
            this.Cursor=0;
            this.Count=0;
            this.Index=-1;
            this.Done=false;
        }
        public readonly void Dispose() {
        }
    }
    private protected UnmanagedCollection(UnmanagedCollectionStore<TKey,TValue> CollectionStore) : base(CollectionStore) {
    }
    public bool ContainsKey(TKey Key) {
        //
        //
        return this.PrivateProtectedContainsKey(ref Key);
    }
    public bool Contains(TKey Key) => this.ContainsKey(Key);
    private protected override Status Read(TKey Key,out TValue Output) {
        var ClientSession = this.ReadSession;


        Output=default;
        var Status = ClientSession.BasicContext.Read(ref Key,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        return Status;
    }






    public bool Remove(TKey key) {
        var ClientSession = this.ContainsDeleteSession;


        var Input = default(EqualityEmpty);
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(ref key,ref Input,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.Found!=Status.NotFound);
        return Output;
    }
    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value) {

        if(this.WouldOverflowMutable(Unsafe.SizeOf<TKey>(),Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ClientSession = this.Add_ClientSession;
        var Output = false;
        var Status = ClientSession.BasicContext.RMW(ref key,ref value,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        Debug.Assert(Status.NotFound==Output);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
