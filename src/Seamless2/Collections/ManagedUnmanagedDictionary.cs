using System.Diagnostics;
using System.Runtime.CompilerServices;
using Seamless.SessionFunctions;
using Seamless.Stores;
namespace Seamless.Collections;
/// <summary>
/// todo 2.0 TKeyとTValueの片方が可変長でも可変長の扱いになっている。将来、TKeyの固定長が実装できた場合は書き直す
/// </summary>
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

    private protected override TryAddResult PrivateTryAdd(TKey key,TValue value){
        var KeyBytes=TKeySerializer.Serialize(ref key);
        if(this.WouldOverflowMutable(KeyBytes.Length,Unsafe.SizeOf<TValue>()))
            return TryAddResult.LogFull;
        var ClientSession=this.UnmanagedValueAddClientSession;
        var Output=false;
        var Status=ClientSession.BasicContext.RMW((FixedSpanByteKey)KeyBytes,ref value,ref Output);
        Status=Pending(ClientSession,Status,ref Output);
        Debug.Assert(Status.IsCompleted);
        if(Output)
            return TryAddResult.Success;
        return TryAddResult.KeyAlreadyExists;
    }
}
