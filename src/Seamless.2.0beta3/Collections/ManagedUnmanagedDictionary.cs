using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;

public sealed partial class ManagedUnmanagedDictionary<TKey,TValue,TKeySerializer,TValueSerializer> {
    private ClientSession<
        FixedSpanByteKey,
        TValue,
        bool,
        EqualityEmpty,
        ManagedUnmanagedAdd<TValue>,
        ManagedStoreFunctions,
        ManagedAllocator
    > UnmanagedValueAddClientSession = null!;

    partial void InitializeKind(ManagedCollectionStore<TKey,TValue> collectionStore){
        this.UnmanagedValueAddClientSession=collectionStore.Store.NewSession<
            FixedSpanByteKey,
            TValue,
            bool,
            EqualityEmpty,
            ManagedUnmanagedAdd<TValue>
        >(new());
    }

    partial void DisposeKind(bool disposing){
        if(disposing) this.UnmanagedValueAddClientSession.Dispose();
    }

    private protected override TryAddResult PrivateTryAdd(TKey Key,TValue Value){
        var KeyBytes=TKeySerializer.Serialize(ref Key);
        if(this.WouldOverflowMutable(KeyBytes.Length,Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var Session=this.UnmanagedValueAddClientSession;
        var Output=false;
        var Status=Session.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref Value,ref Output);
        Status=Pending(Session,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
