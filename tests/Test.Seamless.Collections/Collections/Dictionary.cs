using Common;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Collections;


public class Dictionary {
    [Fact, Trait("Category",Unit)]
    public void IsReadOnly_偽を返す(){
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.IsReadOnly);
    }
    [Fact, Trait("Category",Unit)]
    public void TryAdd_新規キー_Successを返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        var result = dict.TryAdd(1,"a");
        Assert.Equal(TryAddResult.Success,result);
    }
    [Fact, Trait("Category",Unit)]
    public void TryAdd_重複キー_KeyAlreadyExistsを返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var result = dict.TryAdd(1,"b");
        Assert.Equal(TryAddResult.KeyAlreadyExists,result);
    }
    [Fact, Trait("Category",Unit)]
    public void Add_KVP_正常追加() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(new KeyValuePair<int,string>(5,"v"));
        Assert.True(dict.ContainsKey(5));
    }
    [Fact, Trait("Category",Unit)]
    public void Add_重複キー_例外を投げる() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Throws<ArgumentException>(() => dict.Add(1,"b"));
    }
    [Fact, Trait("Category",Unit)]
    public void Contains_KVP_存在する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        ICollection<KeyValuePair<int,string>> c = dict;
        Assert.True(c.Contains(new KeyValuePair<int,string>(1,"a")));
    }
    [Fact, Trait("Category",Unit)]
    public void Contains_KVP_存在しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        ICollection<KeyValuePair<int,string>> c = dict;
        Assert.False(c.Contains(new KeyValuePair<int,string>(99,"x")));
    }
    [Fact, Trait("Category",Unit)]
    public void ContainsKey_存在する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void ContainsKey_存在しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.ContainsKey(999));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_KVP_値一致_削除成功() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(new KeyValuePair<int,string>(1,"a"));
        foreach(var a in dict){

        }
        Assert.True(removed);
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_KVP_値不一致_削除しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(new KeyValuePair<int,string>(1,"wrong"));
        Assert.False(removed);
        Assert.True(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_KVP_キー不存在_偽を返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        var removed = dict.Remove(new KeyValuePair<int,string>(99,"x"));
        Assert.False(removed);
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_out_存在する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var removed = dict.Remove(1,out var value);
        Assert.True(removed);
        Assert.Equal("a",value);
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_out_存在しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        var removed = dict.Remove(99,out var value);
        Assert.False(removed);
        Assert.Null(value);
    }
    [Fact, Trait("Category",Unit)]
    public void CopyTo_正常() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var array = new KeyValuePair<int,string>[2];
        dict.CopyTo(array,0);
        Assert.Equal(2,array.Length);
    }
    [Fact, Trait("Category",Unit)]
    public void CopyTo_null配列_例外() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
    }
    [Fact, Trait("Category",Unit)]
    public void CopyTo_負のインデックス_例外() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<int,string>[2],-1));
    }
    [Fact, Trait("Category",Unit)]
    public void CopyTo_配列が小さすぎる_例外() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<int,string>[1],0));
    }
    [Fact, Trait("Category",Unit)]
    public void ToList_全エントリを返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var list = dict.ToList();
        Assert.Equal(2,list.Count);
    }
    [Fact, Trait("Category",Unit)]
    public void Item_Setter_値を設定する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict[10]="ten";
        Assert.True(dict.TryGetValue(10,out var v));
        Assert.Equal("ten",v);
    }
    [Fact, Trait("Category",Unit)]
    public void Item_Getter_存在しないキー_例外() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Throws<KeyNotFoundException>(() => { var _ = dict[999]; });
    }
    [Fact, Trait("Category",Unit)]
    public void TryGetValue_存在する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(3,"c");
        Assert.True(dict.TryGetValue(3,out var val));
        Assert.Equal("c",val);
    }
    [Fact, Trait("Category",Unit)]
    public void TryGetValue_存在しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.TryGetValue(0,out var val));
        Assert.Null(val);
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_存在する() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.Remove(1));
        Assert.False(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_存在しない() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.False(dict.Remove(99));
    }
    [Fact, Trait("Category",Unit)]
    public void Clear_後Count0() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Clear();
        Assert.Equal(0,dict.Count);
    }
    [Fact, Trait("Category",Unit)]
    public void Count_件数を返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Equal(2,dict.Count);
    }
    [Fact, Trait("Category",Unit)]
    public void GetEnumerator_IEnumerableT() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        IEnumerable<KeyValuePair<int,string>> e = dict;
        Assert.Single(e);
    }
    [Fact, Trait("Category",Unit)]
    public void Add_正常追加() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.ContainsKey(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Dispose_安全に呼び出せる() {
        var store = Store.Create<int,string>();
        var dict = store.Create();
        Assert.False(dict.IsDisposed);
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        dict.Dispose();
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void GetEnumerator_IEnumerable() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        IEnumerable e = dict;
        var count = 0;
        var en = e.GetEnumerator();
        while(en.MoveNext()) count++;
        Assert.Equal(1,count);
    }
    [Fact, Trait("Category",Unit)]
    public void Add_LogFull_例外を投げる() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for(var i = 0; ; i++) dict.Add(i,"v");
        }));
    }
    [Fact, Trait("Category",Unit)]
    public void Item_Setter_LogFull_例外を投げる() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for(var i = 0; ; i++) dict[i]="v";
        }));
    }
    [Fact, Trait("Category",Unit)]
    public void EntryCount_件数を返す() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.EntryCount>=1);
    }
    [Fact, Trait("Category",Unit)]
    public void Pending_Writeパス() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.Remove(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Pending_Readパス() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.True(dict.TryGetValue(1,out var v));
        Assert.Equal("a",v);
    }
    private static string CreateDiskFolder() =>
        Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    [Fact, Trait("Category",Unit)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void WouldOverflowMutable_MutableFractionZero() {
        var folder = CreateDiskFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        // MutableFraction=0 → MutableBytes=0 → MutableBytes=LogMemorySize 分岐を通す
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,MutableFraction:0,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Equal("a",dict[1]);
    }
    [Fact, Trait("Category",Unit)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void WouldOverflowMutable_OverflowDetected() {
        var folder = CreateDiskFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        // MemorySize=8KB, MutableFraction=0.5 → MutableBytes=4KB → 少量レコードでオーバーフロー
        using var store = Store.Create<int,string>(MemorySize:Const.LogMemorySize,MutableFraction:0.5,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        Assert.Throws<OutOfMemoryException>((Action)(() => {
            for(var i = 0;;i++) dict.Add(i,"v");
        }));
    }
    [Fact, Trait("Category",Unit)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Pending_Readパス_IsPending() {
        var folder = CreateDiskFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のReadでIsPending=true
        store.Store.Log.FlushAndEvict(true);
        Assert.True(dict.TryGetValue(0,out var v));
        Assert.Equal("target",v);
    }
    [Fact, Trait("Category",Unit)]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Pending_Writeパス_IsPending() {
        var folder = CreateDiskFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:Const.MinObjectLogCapacity,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(0,"target");
        // 全レコードをディスクに追い出す → 次のRMWでIsPending=true
        // TryAdd on existing key: AddSessionFunctionsのRMWがPENDING → Pending(Session,Status) IsPending=true 分岐を通す
        store.Store.Log.FlushAndEvict(true);
        Assert.Equal(TryAddResult.KeyAlreadyExists,dict.TryAdd(0,"other"));
    }
    [Fact, Trait("Category",Unit)]public void Keys() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Equal([1,2], dict.Keys);
    }
    [Fact, Trait("Category",Unit)]public void Values() {
        using var store = Store.Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        Assert.Equal(["a","b"], dict.Values);
    }
    [Fact, Trait("Category",Unit)]
    public void SaveLoad() {
        var folder = Path.Combine(Path.GetTempPath(),"SeamlessTest");
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var LogDevice = new ManagedLocalStorageDevice(folder);
            using var LogObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,int>(CheckpointDir:checkpointDir,LogDevice:LogDevice,ObjectLogDevice:LogObjDevice);
            using var set = store.Create();
            set[1] = 11;
            set[2] = 22;
            set[3] = 33;
            Assert.True(set.Contains(1));
            token=store.Save();
            Assert.True(set.Contains(1));
        }
        {
            using var LogDevice = new ManagedLocalStorageDevice(folder);
            using var LogObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,int>(CheckpointDir:checkpointDir,recoverDevice:true,LogDevice:LogDevice,ObjectLogDevice:LogObjDevice);
            store.Load(token);
            var (begin2,head2,ro2,tail2) = store.Address;
            using var set = store.Create();
            var l=set.ToArray();
            var r=set.Contains(1);
            Assert.True(set.Contains(1),$"Contains(1) failed. EntryCount={set.EntryCount}, Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
}
