using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Seamless.Collections;
public abstract partial class Collection<
    TKey,
    TValue,
    TSessionKey,
    TSessionValue,
    TReadOutput,
    TAddInput,
    TReadFunctions,
    TAddFunctions,
    TContainsDeleteFunctions,
    TStoreFunctions,
    TAllocator
>
    where TKey:IEquatable<TKey>
    where TReadFunctions:struct,ISessionFunctions<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty
    >,
    ICreate<TReadFunctions>
    where TAddFunctions   :struct,ISessionFunctions<
        TSessionKey,
        TSessionValue,
        TAddInput,
        bool,
        EqualityEmpty
    >,
    ICreate<TAddFunctions>
    where TContainsDeleteFunctions:struct,ISessionFunctions<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        bool,
        EqualityEmpty
    >,
    ICreate<TContainsDeleteFunctions>
    where TStoreFunctions :IStoreFunctions<
        TSessionKey,
        TSessionValue
    >
    where TAllocator:IAllocator<
        TSessionKey,
        TSessionValue
    ,TStoreFunctions>
{
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        TReadOutput,
        EqualityEmpty,
        TReadFunctions,
        TStoreFunctions,
        TAllocator
    > ReadClientSession{get;private set;}
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        TAddInput,
        bool,
        EqualityEmpty,
        TAddFunctions,
        TStoreFunctions,
        TAllocator
    > AddClientSession{get;private set;}
    private protected ClientSession<
        TSessionKey,
        TSessionValue,
        EqualityEmpty,
        bool,
        EqualityEmpty,
        TContainsDeleteFunctions,
        TStoreFunctions,
        TAllocator
    > DeleteClientSession{get;private set;}
    private readonly CollectionStore<
        TKey,
        TValue,
        TSessionKey,
        TSessionValue,
        TStoreFunctions,
        TAllocator
    > CollectionStore;
    //todo ストラテジパターンでCollectionを分割して、CountをCollectionStoreに移すこともできるかも
    public abstract long GetLongCount();
    
    
    
    
    
    
    protected Collection(CollectionStore<
        TKey,
        TValue,
        TSessionKey,
        TSessionValue,
        TStoreFunctions,
        TAllocator
    > CollectionStore){
        this.CollectionStore=CollectionStore;
        var Store=CollectionStore.Store;
        this.ReadClientSession  =Store.NewSession<
            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.AddClientSession   =Store.NewSession<
            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.DeleteClientSession=Store.NewSession<
            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    public sealed partial class CounterIterator:IScanIteratorFunctions<TSessionKey,TSessionValue>{
        public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult){
            cursorRecordResult=CursorRecordResult.Accept;
            return true;
        }
        public long Counter;
        public bool OnStart(long beginAddress,long endAddress){
            return true;
        }
        public void OnException(Exception exception,long numberOfRecords){
        }
        public void OnStop(bool completed,long numberOfRecords) {
            this.Counter=numberOfRecords;
        }
    }
    public void Clear(){
        var Session=this.AddClientSession;
        Session.BasicContext.CompletePending(wait:true);
        Session.Dispose();
        var Store=this.CollectionStore.Store;
        Store.Reset();
        this.ReadClientSession=Store.NewSession<

            EqualityEmpty,
            TReadOutput,
            EqualityEmpty,
            TReadFunctions
        >(TReadFunctions.Create());
        this.AddClientSession=Store.NewSession<

            TAddInput,
            bool,
            EqualityEmpty,
            TAddFunctions
        >(TAddFunctions.Create());
        this.DeleteClientSession=Store.NewSession<

            EqualityEmpty,
            bool,
            EqualityEmpty,
            TContainsDeleteFunctions
        >(TContainsDeleteFunctions.Create());
    }
    private protected static Status Pending<
        TInput0,
        TOutput0,
        TContext0,
        TSessionFunctions0
    >(ClientSession<
        TSessionKey,
        TSessionValue,
        TInput0,
        TOutput0,
        TContext0,
        TSessionFunctions0,
        TStoreFunctions,
        TAllocator
    > Session,Status Status)
    where TSessionFunctions0:struct,ISessionFunctions<
    	TSessionKey,
    	TSessionValue,
    	TInput0,
    	TOutput0,
    	TContext0
    >{
        if(Status.IsPending){
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait:true);
            using(CompletedOutputs){
                Debug.Assert(CompletedOutputs.Next());
                Status=CompletedOutputs.Current.Status;
            }
        }
        return Status;
    }

    private protected static Status Pending<
        TInput0,
        TOutput0,
        TContext0,
        TSessionFunctions0
    >(ClientSession<
        TSessionKey,
        TSessionValue,
        TInput0,
        TOutput0,
        TContext0,
        TSessionFunctions0,
        TStoreFunctions,
        TAllocator
    > Session,Status Status,ref TOutput0 Output)
    where TSessionFunctions0:struct,ISessionFunctions<
    	TSessionKey,
    	TSessionValue,
        TInput0,
        TOutput0,
        TContext0
    >{
        if(Status.IsPending){
            Session.BasicContext.CompletePendingWithOutputs(out var CompletedOutputs,wait:true);
            using(CompletedOutputs){
                Debug.Assert(CompletedOutputs.Next());
                Status=CompletedOutputs.Current.Status;
                Output=CompletedOutputs.Current.Output;
            }
        }
        return Status;
    }
    public virtual bool TryGetValue(TKey Key,[MaybeNullWhen(false)]out TValue Value){
        var Status=this.Read(Key,out Value);
        if(Status.NotFound){
            Value=default;
            return false;
        }
        return true;
    }
    private protected abstract TryAddResult PrivateTryAdd(TKey Key,TValue Value);
}
