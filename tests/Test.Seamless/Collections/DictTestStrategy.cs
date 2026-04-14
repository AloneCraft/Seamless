using Seamless;
using Seamless.Stores;

using Tsavorite.core;

namespace Test.Seamless.Collections;

public interface IDictTestStrategy<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    static abstract TKey K(int i);
    static abstract TValue V(int i);
    /// <summary>どの V(i) とも一致しない値。値不一致テスト用。</summary>
    static abstract TValue VWrong();
    static abstract StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0);
    static abstract StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,
        IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false);
}

/// <summary>TKey=int, TValue=string, Factory.Create (両マネージド)</summary>
public struct ManagedDictStrategy:IDictTestStrategy<string,string>{
    public static string K(int i)=>i.ToString();
    public static string V(int i)=>i.ToString();
    public static string VWrong()=>"!!";

    public static StoreHandle<string,string> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<string,string>();
        return new StoreHandle<string,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<string,string> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<string,string>(logDevice,objDevice);
        return new StoreHandle<string,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

/// <summary>TKey=int, TValue=int, Factory.Create
public struct UnmanagedDictStrategy:IDictTestStrategy<int,int>{
    public static int K(int i)=>i;
    public static int V(int i)=>i*10;
    public static int VWrong()=>-1;

    public static StoreHandle<int,int> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<int,int>();
        return new StoreHandle<int,int>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<int,int> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        // Factory.Create
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<int,int>(logDevice,objDevice);
        return new StoreHandle<int,int>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

/// <summary>TKey=int (unmanaged), TValue=string (managed), Factory.Create</summary>
public struct UnmanagedKeyDictStrategy:IDictTestStrategy<int,string>{
    public static int K(int i)=>i;
    public static string V(int i)=>i.ToString();
    public static string VWrong()=>"!!";

    public static StoreHandle<int,string> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<int,string>();
        return new StoreHandle<int,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<int,string> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<int,string>(logDevice,objDevice);
        return new StoreHandle<int,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

/// <summary>TKey=string (managed), TValue=int (unmanaged), Factory.Create
public struct UnmanagedValueDictStrategy:IDictTestStrategy<string,int>{
    public static string K(int i)=>i.ToString();
    public static int V(int i)=>i*10;
    public static int VWrong()=>-1;

    public static StoreHandle<string,int> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<string,int>();
        return new StoreHandle<string,int>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<string,int> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<string,int>(logDevice,objDevice);
        return new StoreHandle<string,int>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}
