using Seamless;
using Seamless.Stores;

using Tsavorite.core;

namespace Test.Seamless.Collections;

public interface IDictTypeProvider<T>
    where T:IEquatable<T>{
    static abstract T Create(int i);
    static abstract T Wrong();
}

public readonly struct IntDictTypeProvider:IDictTypeProvider<int>{
    public static int Create(int i)=>i;
    public static int Wrong()=>-1;
}

public readonly struct StringDictTypeProvider:IDictTypeProvider<string>{
    public static string Create(int i)=>i.ToString();
    public static string Wrong()=>"!!";
}

public readonly struct UnmanagedStructDictTypeProvider:IDictTypeProvider<UnmanagedStruct>{
    public static UnmanagedStruct Create(int i)=>new(){Value=i};
    public static UnmanagedStruct Wrong()=>new(){Value=-1};
}

public readonly struct ManagedStructDictTypeProvider:IDictTypeProvider<ManagedStruct>{
    public static ManagedStruct Create(int i)=>new(){Name=$"N{i}"};
    public static ManagedStruct Wrong()=>new(){Name="!!"};
}

public readonly struct ManagedClassDictTypeProvider:IDictTypeProvider<ManagedClass>{
    public static ManagedClass Create(int i)=>new(){Name=$"C{i}"};
    public static ManagedClass Wrong()=>new(){Name="!!"};
}

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

public readonly struct ManagedStoreDictionaryStrategy<TKeyProvider,TValueProvider,TKey,TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider:IDictTypeProvider<TKey>
    where TValueProvider:IDictTypeProvider<TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    public static TKey K(int i)=>TKeyProvider.Create(i);
    public static TValue V(int i)=>TValueProvider.Create(i);
    public static TValue VWrong()=>TValueProvider.Wrong();

    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<TKey,TValue>(new ManagedTag(),new ManagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new ManagedTag(),new ManagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

public readonly struct UnmanagedKeyDictionaryStrategy<TKeyProvider,TValueProvider,TKey,TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider:IDictTypeProvider<TKey>
    where TValueProvider:IDictTypeProvider<TValue>
    where TKey:unmanaged, IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    public static TKey K(int i)=>TKeyProvider.Create(i);
    public static TValue V(int i)=>TValueProvider.Create(i);
    public static TValue VWrong()=>TValueProvider.Wrong();

    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<TKey,TValue>(new UnmanagedTag(),new ManagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new UnmanagedTag(),new ManagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

public readonly struct UnmanagedValueDictionaryStrategy<TKeyProvider,TValueProvider,TKey,TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider:IDictTypeProvider<TKey>
    where TValueProvider:IDictTypeProvider<TValue>
    where TKey:IEquatable<TKey>
    where TValue:unmanaged, IEquatable<TValue>{
    public static TKey K(int i)=>TKeyProvider.Create(i);
    public static TValue V(int i)=>TValueProvider.Create(i);
    public static TValue VWrong()=>TValueProvider.Wrong();

    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<TKey,TValue>(new ManagedTag(),new UnmanagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new ManagedTag(),new UnmanagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

public readonly struct UnmanagedStoreDictionaryStrategy<TKeyProvider,TValueProvider,TKey,TValue>:IDictTestStrategy<TKey,TValue>
    where TKeyProvider:IDictTypeProvider<TKey>
    where TValueProvider:IDictTypeProvider<TValue>
    where TKey:unmanaged, IEquatable<TKey>
    where TValue:unmanaged, IEquatable<TValue>{
    public static TKey K(int i)=>TKeyProvider.Create(i);
    public static TValue V(int i)=>TValueProvider.Create(i);
    public static TValue VWrong()=>TValueProvider.Wrong();

    public static StoreHandle<TKey,TValue> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<TKey,TValue>(new UnmanagedTag(),new UnmanagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }

    public static StoreHandle<TKey,TValue> CreateStorageStore(
        IDevice logDevice,IDevice objDevice,
        long memorySize=Const.MinObjectLogCapacity,
        double mutableFraction=1.0,
        string? checkpointDir=null,
        bool recoverDevice=false){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<TKey,TValue>(logDevice,objDevice,new UnmanagedTag(),new UnmanagedTag());
        return new StoreHandle<TKey,TValue>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

/// <summary>TKey=string, TValue=string, Factory.Create (両マネージド)</summary>
public struct ManagedDictStrategy:IDictTestStrategy<string,string>{
    public static string K(int i)=>i.ToString();
    public static string V(int i)=>i.ToString();
    public static string VWrong()=>"!!";

    public static StoreHandle<string,string> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<string,string>(new ManagedTag(),new ManagedTag());
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
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<string,string>(logDevice,objDevice,new ManagedTag(),new ManagedTag());
        return new StoreHandle<string,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

    /// <summary>TKey=int, TValue=int, Factory.Create</summary>
public struct UnmanagedDictStrategy:IDictTestStrategy<int,int>{
    public static int K(int i)=>i;
    public static int V(int i)=>i*10;
    public static int VWrong()=>-1;

    public static StoreHandle<int,int> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<int,int>(new UnmanagedTag(),new UnmanagedTag());
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
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<int,int>(logDevice,objDevice,new UnmanagedTag(),new UnmanagedTag());
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
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<int,string>(new UnmanagedTag(),new ManagedTag());
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
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<int,string>(logDevice,objDevice,new UnmanagedTag(),new ManagedTag());
        return new StoreHandle<int,string>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}

    /// <summary>TKey=string (managed), TValue=int (unmanaged), Factory.Create</summary>
public struct UnmanagedValueDictStrategy:IDictTestStrategy<string,int>{
    public static string K(int i)=>i.ToString();
    public static int V(int i)=>i*10;
    public static int VWrong()=>-1;

    public static StoreHandle<string,int> CreateMemoryStore(
        long memorySize=Const.LogMemorySize,
        double mutableFraction=1.0){
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction).Create<string,int>(new ManagedTag(),new UnmanagedTag());
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
        var store=Factory.Create(MemorySize:memorySize,MutableFraction:mutableFraction,CheckpointDir:checkpointDir).Create<string,int>(logDevice,objDevice,new ManagedTag(),new UnmanagedTag());
        return new StoreHandle<string,int>(store,()=>store.Create(),
            wait=>store.Store.Log.FlushAndEvict(wait),
            ()=>store.Save(),token=>store.Load(token),()=>store.Address);
    }
}
