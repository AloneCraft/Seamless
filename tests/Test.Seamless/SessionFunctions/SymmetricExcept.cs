using Seamless;
using Seamless.Collections;
using Seamless.Stores;
using Tsavorite.core;
namespace Test.Seamless.SessionFunctions;

public abstract class SymmetricExceptTests<TKey>
    where TKey:IEquatable<TKey>{
    protected abstract TKey Key(int seed);
    protected abstract(ManagedHashSet<TKey> Set,IDisposable Store) CreateMemory();
    protected abstract(ManagedHashSet<TKey> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice);
    protected abstract(ManagedHashSet<TKey> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice);
    protected abstract void ShiftReadOnly(IDisposable store);
    protected abstract void FlushAndEvict(IDisposable store);
    private static string CreateDiskFolder()=>Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize=Const.MinObjectLogCapacity;

    // ── In-memory (Mutable key) ──────────────────────────────────────
    private void SymmetricExceptWith(){
        var(setA,storeA)=this.CreateMemory();
        var(setB,storeB)=this.CreateMemory();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
        setA.SymmetricExceptWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.DoesNotContain(this.Key(2),setA);
        Assert.DoesNotContain(this.Key(3),setA);
        Assert.Contains(this.Key(4),setA);
    }
    [Fact,Trait("Category","Unit")] public void InPlaceUpdater()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void GetRMWModifiedFieldInfo()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void PostRMWOperation()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void NeedInitialUpdate()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void GetRMWInitialFieldInfo()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void InitialUpdater()=>this.SymmetricExceptWith();
    [Fact,Trait("Category","Unit")] public void PostInitialUpdater()=>this.SymmetricExceptWith();

    // ── ReadOnly in-memory (ShiftReadOnlyAddress) ─────────────────────
    private void SymmetricExceptWith_ReadOnly(){
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var(setA,storeA)=this.CreateDisk(logDevice,objDevice);
        var(setB,storeB)=this.CreateMemory();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3)); setB.Add(this.Key(4));
        this.ShiftReadOnly(storeA);
        setA.SymmetricExceptWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.DoesNotContain(this.Key(2),setA);
        Assert.DoesNotContain(this.Key(3),setA);
        Assert.Contains(this.Key(4),setA);
    }
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void NeedCopyUpdate()=>this.SymmetricExceptWith_ReadOnly();
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void CopyUpdater()=>this.SymmetricExceptWith_ReadOnly();

    // ── Disk (IsPending path) ─────────────────────────────────────────
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void IsPending経路でも動作する(){
        var folderA=CreateDiskFolder();
        var folderB=CreateDiskFolder();
        using var logDeviceA=Devices.CreateLogDevice(Path.Combine(folderA,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDeviceA=Devices.CreateLogDevice(Path.Combine(folderA+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var logDeviceB=Devices.CreateLogDevice(Path.Combine(folderB,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDeviceB=Devices.CreateLogDevice(Path.Combine(folderB+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var(setA,storeA)=this.CreateDiskSpill(logDeviceA,objDeviceA);
        var(setB,storeB)=this.CreateDiskSpill(logDeviceB,objDeviceB);
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        for(var a=0; a<300; a++){
            setA.Add(this.Key(a));
            if(a%3==0) setB.Add(this.Key(a));
        }
        setA.SymmetricExceptWith(setB);
        Assert.DoesNotContain(this.Key(0),setA);
        Assert.Contains(this.Key(1),setA);
    }

    // ── FlushAndEvict (ConvertOutputToHeap path) ──────────────────────
    private void FlushAndEvictPath(){
        var folder=CreateDiskFolder();
        using var logDevice=Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose:true,capacity:TestMemorySize);
        using var objDevice=Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose:true,capacity:TestMemorySize);
        var(setA,storeA)=this.CreateDisk(logDevice,objDevice);
        var(setB,storeB)=this.CreateMemory();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(this.Key(1)); setA.Add(this.Key(2)); setA.Add(this.Key(3));
        setB.Add(this.Key(2)); setB.Add(this.Key(3));
        this.FlushAndEvict(storeA);
        setA.SymmetricExceptWith(setB);
        Assert.Contains(this.Key(1),setA);
        Assert.DoesNotContain(this.Key(2),setA);
        Assert.DoesNotContain(this.Key(3),setA);
    }
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void RMWCompletionCallback()=>this.FlushAndEvictPath();
    [Fact,Trait("Category","Unit")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void ConvertOutputToHeap()=>this.FlushAndEvictPath();
}

public sealed class ManagedSymmetricExcept:SymmetricExceptTests<int>{
    protected override int Key(int seed)=>seed;
    protected override(ManagedHashSet<int> Set,IDisposable Store) CreateMemory(){
        var store=Factory.Create().Create<int>(new ManagedTag());
        return(store.Create(),store);
    }
    protected override(ManagedHashSet<int> Set,IDisposable Store) CreateDisk(IDevice logDevice,IDevice objDevice){
        var store=Factory.Create(MemorySize:Const.MinObjectLogCapacity).Create<int>(logDevice,objDevice,new ManagedTag());
        return(store.Create(),store);
    }
    protected override(ManagedHashSet<int> Set,IDisposable Store) CreateDiskSpill(IDevice logDevice,IDevice objDevice){
        var store=Factory.Create(MemorySize:Const.MinObjectLogCapacity,MutableFraction:0).Create<int>(logDevice,objDevice,new ManagedTag());
        return(store.Create(),store);
    }
    protected override void ShiftReadOnly(IDisposable store){
        var s=(ManagedHashSetStore<int>)store;
        s.Store.Log.ShiftReadOnlyAddress(s.Store.Log.TailAddress,true);
    }
    protected override void FlushAndEvict(IDisposable store)=>
        ((ManagedHashSetStore<int>)store).Store.Log.FlushAndEvict(true);

    [Fact,Trait("Category","Unit")]
    public void LogFull_例外を投げる(){
        using var store=Factory.Create(MutableFraction:0.01).Create<int>();
        using var set=store.Create();
        using var otherStore=Factory.Create().Create<int>();
        using var other=otherStore.Create();
        var exception=Record.Exception(()=>{
            for(var a=0; a<10000; a++){
                other.Clear();
                other.Add(a);
                set.SymmetricExceptWith(other);
            }
        });
        var outOfMemory=Assert.IsType<OutOfMemoryException>(exception);
        Assert.StartsWith("Tsavorite mutable log is full.",outOfMemory.Message);
    }
}

/// <summary>
/// UnmanagedHashSet.SymmetricExceptWith のテスト。メモリのみ（ストレージバリアントなし）。
/// </summary>
public sealed class UnmanagedSymmetricExcept{
    private static(UnmanagedHashSet<int> Set,IDisposable Store) CreateSet(){
        var store=Factory.Create().Create<int>();
        return(store.Create(),store);
    }

    private static(UnmanagedHashSet<int> Set,IDisposable Store) CreateSmallMutableSet(){
        var store=Factory.Create(MutableFraction:0.01).Create<int>();
        return(store.Create(),store);
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_対称差のみ残る(){
        var(setA,storeA)=CreateSet();
        var(setB,storeB)=CreateSet();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(1); setA.Add(2); setA.Add(3);
        setB.Add(2); setB.Add(3); setB.Add(4);
        setA.SymmetricExceptWith(setB);
        Assert.True(setA.Contains(1));
        Assert.False(setA.Contains(2));
        Assert.False(setA.Contains(3));
        Assert.True(setA.Contains(4));
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_空集合_変化なし(){
        var(setA,storeA)=CreateSet();
        var(setB,storeB)=CreateSet();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(1); setA.Add(2);
        setA.SymmetricExceptWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_自身が空_他方の全要素追加(){
        var(setA,storeA)=CreateSet();
        var(setB,storeB)=CreateSet();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setB.Add(1); setB.Add(2);
        setA.SymmetricExceptWith(setB);
        Assert.Equal(2,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_完全一致_空になる(){
        var(setA,storeA)=CreateSet();
        var(setB,storeB)=CreateSet();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(1); setA.Add(2);
        setB.Add(1); setB.Add(2);
        setA.SymmetricExceptWith(setB);
        Assert.Equal(0,setA.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_null_例外(){
        var(set,store)=CreateSet();
        using var _store=store;
        using var _set=set;
        Assert.Throws<ArgumentNullException>(()=>set.SymmetricExceptWith(null!));
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_共通要素なし_両方の全要素(){
        var(setA,storeA)=CreateSet();
        var(setB,storeB)=CreateSet();
        using var _storeA=storeA;
        using var _storeB=storeB;
        using var _setA=setA;
        using var _setB=setB;
        setA.Add(1); setA.Add(2);
        setB.Add(3); setB.Add(4);
        setA.SymmetricExceptWith(setB);
        Assert.Equal(4,setA.Count);
        Assert.True(setA.Contains(1));
        Assert.True(setA.Contains(2));
        Assert.True(setA.Contains(3));
        Assert.True(setA.Contains(4));
    }

    [Fact,Trait("Category","Unit")]
    public void SymmetricExceptWith_LogFull_例外を投げる(){
        var(set,store)=CreateSmallMutableSet();
        var(other,otherStore)=CreateSet();
        using var _store=store;
        using var _otherStore=otherStore;
        using var _set=set;
        using var _other=other;
        var exception=Record.Exception(()=>{
            for(var a=0; a<10000; a++){
                other.Clear();
                other.Add(a);
                set.SymmetricExceptWith(other);
            }
        });
        var outOfMemory=Assert.IsType<OutOfMemoryException>(exception);
        Assert.StartsWith("Tsavorite mutable log is full.",outOfMemory.Message);
    }
}
