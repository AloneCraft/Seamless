using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

[DebuggerDisplay("Count={Count}")]
public abstract partial class Collection<
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
        var Store = this.CollectionStore.Store;
        Store.Reset();
        this.ReadClientSession=Store.NewSession<
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
    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)] out TValue Value) {
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
                Debug.Assert(CompletedOutputs.Next());
                Status=CompletedOutputs.Current.Status;
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
                Debug.Assert(CompletedOutputs.Next());
                Status=CompletedOutputs.Current.Status;
                Output=CompletedOutputs.Current.Output;
            }
        }
        else {
        }
        return Status;
    }
    private protected readonly struct IntersectWithIterator<TCollection>(
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
            if(!Other.PrivateProtectedContainsKey(KeyBytes)) {
                if(Self.WouldOverflowMutable(KeyBytes.Length,1))
                    throw Self.ThrowLogFull();
                var Input = default(EqualityEmpty);
                var Output = default(EqualityEmpty);
                var Status = Session.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Input,ref Output);
                if(Status.IsPending) {
                    Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
                    using(CompletedOutputs) {
                        Debug.Assert(CompletedOutputs.Next());
                    }
                }
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private protected readonly struct SymmetricExceptWithIterator<TCollection>(
        TCollection Self,
        ClientSession<
            FixedSpanByteKey,

            PinnedSpanByte,
            SpanByteAndMemory,
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
            if(Self.WouldOverflowMutable(logRecord.Key.Length,1))
                throw Self.ThrowLogFull();
            var Input = default(PinnedSpanByte);
            SpanByteAndMemory Output = default;
            var Status = Session.BasicContext.RMW((FixedSpanByteKey)logRecord.Key,ref Input,ref Output);
            if(Status.IsPending) {
                Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait: true);
                using(CompletedOutputs) {
                    Debug.Assert(CompletedOutputs.Next());
                }
            }
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool OnStart(long beginAddress,long endAddress) => true;
        public void OnException(Exception exception,long numberOfRecords) { }
        public void OnStop(bool completed,long numberOfRecords) { }
    }
    private protected void IntersectWith<TCollection>(
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
        var Iterator = new IntersectWithIterator<TCollection>(Self,Other,Session);
        var Cursor = 0L;
        Session.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
    private protected void SymmetricExceptWith<TCollection>(
        TCollection Self,
        TCollection Other,
        ClientSession<
            FixedSpanByteKey,

            PinnedSpanByte,
            SpanByteAndMemory,
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
        > {
        ArgumentNullException.ThrowIfNull(Other);
        var Iterator = new SymmetricExceptWithIterator<TCollection>(Self,Session);
        var Cursor = 0L;
        Other.ReadClientSession.IterateLookup(ref Iterator,ref Cursor,resetCursor: false);
        Session.BasicContext.CompletePending(wait: true);
    }
}
