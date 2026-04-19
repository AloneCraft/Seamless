using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

[DebuggerDisplay("EntryCount={EntryCount}")]
public abstract class Collection<
    TKey,
    TValue,


    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>:IDisposable
    where TKey : IEquatable<TKey>
    where TReadFunctions : struct, ISessionFunctions<


        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions : struct, ISessionFunctions<


        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions : struct, ISessionFunctions<


        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions : IStoreFunctions



    where TAllocator : IAllocator<


        TStoreFunctions
    > {
    private protected ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadClientSession { get; private set; }
    private protected ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadMemoryClientSession { get; private set; }
    private protected ClientSession<
        FixedSpanByteKey,

        TAddInput,
        bool,
        EqualityEmpty,
        TAddFunctions,
        TStoreFunctions,
        TAllocator
    > AddClientSession { get; private set; }
    private protected ClientSession<
        FixedSpanByteKey,

        EqualityEmpty,
        bool,
        EqualityEmpty,
        TContainsDeleteFunctions,
        TStoreFunctions,
        TAllocator
    > ContainsDeleteClientSession { get; private set; }
    private readonly CollectionStore<
        TKey,
        TValue,


        TStoreFunctions,
        TAllocator
    > CollectionStore;
    public TryAddResult TryAdd(TKey Key,TValue Value) => this.PrivateTryAdd(Key,Value);
    public void Add(TKey Key,TValue Value) {
        var Result = this.TryAdd(Key,Value);
        switch(Result) {
            case TryAddResult.LogFull:
                throw this.ThrowLogFull();
            case TryAddResult.KeyAlreadyExists:
                throw new ArgumentException($"An item with the same key has already been added. Key: {Key}");
        }
        Debug.Assert(Result==TryAddResult.Success);
    }
    public sealed class CounterIterator:IScanIteratorFunctions {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord sourceLogRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }



        public long Counter;
        public bool OnStart(long beginAddress,long endAddress) {
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords) {
        }
        public void OnStop(bool completed,long numberOfRecords) {
            this.Counter=numberOfRecords;
        }
    }
    public long GetLongCount() {
        var Iterator = new CounterIterator();
        var Cursor = 0L;
        this.ReadClientSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        this.ReadClientSession.BasicContext.CompletePending(wait: true);

        return Iterator.Counter;
    }
    private protected bool PrivateProtectedContainsKey(ReadOnlySpan<byte> KeyBytes) {
        var Session = this.ContainsDeleteClientSession;
        var Output = false;
        var Status = Session.BasicContext.Read((FixedSpanByteKey)KeyBytes,ref Output);
        Status=Pending(Session,Status,ref Output);
        return Status.Found;
    }
    protected Collection(
        CollectionStore<
            TKey,
            TValue,


            TStoreFunctions,
            TAllocator
        > CollectionStore
    ) {
        this.CollectionStore=CollectionStore;
        var Store = CollectionStore.Store;
        this.ReadClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.ReadMemoryClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.AddClientSession=Store.NewSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.ContainsDeleteClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    public void Clear() {
        var Session = this.AddClientSession;
        Session.BasicContext.CompletePending(wait: true);
        Session.Dispose();
        this.ReadMemoryClientSession.Dispose();
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ReadClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.ReadMemoryClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.AddClientSession=Store.NewSession<
            FixedSpanByteKey,
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.ContainsDeleteClientSession=Store.NewSession<
            FixedSpanByteKey,
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    public bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
        var Status = this.Read(Key,out Value);
        if(Status.NotFound) {
            Value=default;
            return false;
        }
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key,TValue Value);
    private protected static Status Pending<
        TInput0,
        TOutput0,

        TSessionFunctions0
    >(
        ClientSession<
            FixedSpanByteKey,

            TInput0,
            TOutput0,
            EqualityEmpty,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status
    )
    where TSessionFunctions0 : struct, ISessionFunctions<


        TInput0,
        TOutput0,
        EqualityEmpty
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                }
            }
        }
        else {
        }
        return Status;
    }
    private protected static Status Pending<
        TInput0,
        TOutput0,

        TSessionFunctions0
    >(
        ClientSession<
            FixedSpanByteKey,

            TInput0,
            TOutput0,
            EqualityEmpty,
            TSessionFunctions0,
            TStoreFunctions,
            TAllocator
        > Session,
        Status Status,
        ref TOutput0 Output
    )
    where TSessionFunctions0 : struct, ISessionFunctions<


        TInput0,
        TOutput0,
        EqualityEmpty
    > {
        if(Status.IsPending) {
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
            using(CompletedOutputs) {
                if(!CompletedOutputs.Next()) {
                }
                else {
                    Status=CompletedOutputs.Current.Status;
                    Output=CompletedOutputs.Current.Output;
                }
            }
        }
        else {
        }
        return Status;
    }
    private long LogMemorySize => this.CollectionStore.LogMemorySize;
    private double MutableFraction => this.CollectionStore.MutableFraction;
    private protected OutOfMemoryException ThrowLogFull() {
        var Log = this.CollectionStore.Store.Log;
        return new OutOfMemoryException($"Tsavorite mutable log is full. BeginAddress: {Log.BeginAddress}, HeadAddress: {Log.HeadAddress}, TailAddress: {Log.TailAddress}");
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool WouldOverflowMutable(int KeyLength,int ValueLength) {
        Debug.Assert(this.LogMemorySize>=8192);
        var Log = this.CollectionStore.Store.Log;
        return this.CollectionStore.OverflowMutableStrategyKind switch {
            OverflowMutableStrategyKind.Memory => this.WouldOverflowMutable<MemoryOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            OverflowMutableStrategyKind.Storage => this.WouldOverflowMutable<StorageOverflowMutableStrategy>(Log.BeginAddress,Log.TailAddress,KeyLength,ValueLength),
            _ => throw new UnreachableException(),
        };
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool WouldOverflowMutable<TOverflowMutableStrategy>(long BeginAddress,long TailAddress,int KeyLength,int ValueLength)
        where TOverflowMutableStrategy : struct, IOverflowMutableStrategy<TOverflowMutableStrategy> {
        return TOverflowMutableStrategy.WouldOverflowMutable(this.LogMemorySize,this.MutableFraction,BeginAddress,TailAddress,KeyLength,ValueLength);
    }
    public int Count => (int)this.GetLongCount();
    public long EntryCount => this.CollectionStore.Store.EntryCount;
    private protected long HeadAddress => this.CollectionStore.Address.HeadAddress;
    private protected abstract Status Read(TKey Key,out TValue Output);
    public abstract bool IsDisposed { get; }
    private bool disposed;
    public void Dispose() {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if(this.disposed)
            return;
        if(disposing) {
            this.ReadMemoryClientSession.Dispose();
            this.AddClientSession.Dispose();
        }
        else {
        }
        this.disposed=true;
    }
    ~Collection() => this.Dispose(false);
    //ManagedCollection,UnamagedCollection
    private readonly struct IntersectWithScanIteratorFunctions<TCollection>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var KeyBytes = logRecord.Key;
            if(Other.PrivateProtectedContainsKey(KeyBytes)) {
            }
            else {
                if(Self.WouldOverflowMutable(KeyBytes.Length,1))
                    throw Self.ThrowLogFull();
                var Input = default(EqualityEmpty);
                var Output = default(EqualityEmpty);
                Session.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private readonly struct ExceptWithScanIteratorFunctions(
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            var Input = default(EqualityEmpty);
            var Output = false;
            var Status = Session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref Input,ref Output);
            Pending(Session,Status,ref Output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private readonly struct UnionWithScanIteratorFunctions<TCollection>(
        TCollection Self,
        ClientSession<
            FixedSpanByteKey,

            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(Self.WouldOverflowMutable(logRecord.Key.Length,logRecord.ValueSpan.Length))
                throw Self.ThrowLogFull();
            TAddInput Input = default!;
            var Output = false;
            var Status = Session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref Input,ref Output);
            Pending(Session,Status,ref Output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private readonly struct SymmetricExceptWithScanIteratorFunctions<TCollection>(
        TCollection Self,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SymmetricExceptWith,
            TStoreFunctions,
            TAllocator
        > Session
    ):IScanIteratorFunctions
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult)
            where TSourceLogRecord : ISourceLogRecord {
            if(Self.WouldOverflowMutable(logRecord.Key.Length,4))
                throw Self.ThrowLogFull();
            var Input = default(EqualityEmpty);
            var Output = default(EqualityEmpty);
            var Status = Session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref Input,ref Output);
            Pending(Session,Status,ref Output);
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    /// <summary>
    /// ManagedHashSet,UnmanagedHashSetが使う
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <param name="Self"></param>
    /// <param name="Other"></param>
    /// <param name="Session"></param>
    private protected static void IntersectWith<TCollection>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            IntersectWith,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > {
        ArgumentNullException.ThrowIfNull(Other);
        var Iterator = new IntersectWithScanIteratorFunctions<TCollection>(Self,Other,Session);
        var Cursor = 0L;
        Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected static void ExceptWith<TCollection, TOtherOutput, TOtherFunctions>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty,
            TOtherFunctions,
            TStoreFunctions,
            TAllocator
        > OtherScanSession,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TOtherFunctions : struct, ISessionFunctions<


            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(ReferenceEquals(Self,Other)) {
            Self.Clear();
            return;
        }
        var Iterator = new ExceptWithScanIteratorFunctions(Session);
        var Cursor = 0L;
        OtherScanSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected static void UnionWith<TCollection, TOtherOutput, TOtherFunctions>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty,
            TOtherFunctions,
            TStoreFunctions,
            TAllocator
        > OtherScanSession,
        ClientSession<
            FixedSpanByteKey,

            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TOtherFunctions : struct, ISessionFunctions<


            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(ReferenceEquals(Self,Other)) {
            return;
        }
        var Iterator = new UnionWithScanIteratorFunctions<TCollection>(Self,Session);
        var Cursor = 0L;
        OtherScanSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
    /// <summary>
    /// ManagedHashSet,UnmanagedHashSetが使う
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    /// <typeparam name="TOtherOutput"></typeparam>
    /// <typeparam name="TOtherFunctions"></typeparam>
    /// <param name="Self"></param>
    /// <param name="Other"></param>
    /// <param name="OtherScanSession"></param>
    /// <param name="Session"></param>
    private protected static void SymmetricExceptWith<TCollection, TOtherOutput, TOtherFunctions>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty,
            TOtherFunctions,
            TStoreFunctions,
            TAllocator
        > OtherScanSession,
        ClientSession<
            FixedSpanByteKey,

            EqualityEmpty,
            EqualityEmpty,
            EqualityEmpty,
            SymmetricExceptWith,
            TStoreFunctions,
            TAllocator
        > Session
    )
        where TCollection : Collection<
            TKey,
            TValue,
            TReadOutput,
            TAddInput,
            TReadFunctions,
            TAddFunctions,
            TContainsDeleteFunctions,
            TStoreFunctions,
            TAllocator
        >
        where TOtherFunctions : struct, ISessionFunctions<


            EqualityEmpty,
            TOtherOutput,
            EqualityEmpty
        > {
        ArgumentNullException.ThrowIfNull(Other);
        if(ReferenceEquals(Self,Other)) {
            Self.Clear();
            return;
        }
        var Iterator = new SymmetricExceptWithScanIteratorFunctions<TCollection>(Self,Session);
        var Cursor = 0L;
        OtherScanSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
}
