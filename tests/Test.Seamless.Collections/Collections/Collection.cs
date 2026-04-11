using Seamless.Collections;
using Seamless.Store;
using Tsavorite.core;
using EqualityEmpty = global::Seamless.Collections.EqualityEmpty;
namespace Test.Seamless.Collections;


public class Collection {
    internal sealed class TestCollection<TKey,TValue>(ManagedCollectionStore<TKey,TValue> CollectionStore)
        :ManagedCollection<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>(CollectionStore)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue>{
        private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength)=>false;
        private bool disposed;
        public override bool IsDisposed=>this.disposed;
        protected override void Dispose(bool disposing){
            base.Dispose(disposing);
            this.disposed=true;
        }
    }
    [Fact, Trait("Category",Unit)]
    public void EntryCount_追加後に増加する() {
        using var store = Factory.Create().Create<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        writer.Add(1);
        Assert.True(subject.EntryCount>=1);
    }
    [Fact, Trait("Category",Unit)]
    public void GetLongCount_空の場合ゼロを返す() {
        using var store = Factory.Create().Create<int>();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        Assert.Equal(0L,subject.GetLongCount());
    }
    [Fact, Trait("Category",Unit)]
    public void Count_GetLongCountと一致する() {
        using var store = Factory.Create().Create<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        writer.Add(1);
        writer.Add(2);
        Assert.Equal((int)subject.GetLongCount(),subject.Count);
    }
    [Fact, Trait("Category",Unit)]
    public void Contains_存在するキー_真を返す() {
        using var store = Factory.Create().Create<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        writer.Add(1);
        Assert.True(subject.Contains(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Contains_存在しないキー_偽を返す() {
        using var store = Factory.Create().Create<int>();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        Assert.False(subject.Contains(99));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_存在するキー_真を返す() {
        using var store = Factory.Create().Create<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        writer.Add(1);
        Assert.True(subject.Remove(1));
    }
    [Fact, Trait("Category",Unit)]
    public void Remove_存在しないキー_偽を返す() {
        using var store = Factory.Create().Create<int>();
        using var subject = new TestCollection<int,EqualityEmpty>(store);
        Assert.False(subject.Remove(99));
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    // WouldOverflowMutable: MutableFraction=0 → MutableBytes=0 → if(MutableBytes<=0) MutableBytes=LogMemorySize の分岐
    [Fact, Trait("Category",Unit)]
    public void WouldOverflowMutable_MutableFractionZero_MutableBytesはLogMemorySize() {
        using var store = Factory.Create(MutableFraction: 0.0).Create<int>();
        using var set = store.Create();
        // MutableBytes = (long)(LogMemorySize * 0.0) = 0 → LogMemorySize で上書き → 溢れずに追加できる
        Assert.True(set.Add(1));
    }

    // Pending(Session, Status, ref Output) の IsPending 分岐 → Contains(Read) がディスク上レコードを参照
    // Pending(Session, Status)             の IsPending 分岐 → Remove/Add(RMW) がディスク上レコードを参照
    [Fact, Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Pending_IsPending分岐_ディスクスピル経由() {
        var folder = CreateFolder();
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        // MutableFraction=0 でTsavorite内部が即時ReadOnly化しディスクへスピルさせる
        using var store = Factory.Create(MutableFraction:0).Create<int>(logDevice,objDevice);
        // LogMemorySize=0 でWouldOverflowMutableを無効化し、ディスクスピルを妨げない
        using var set =store.Create();
        // デフォルト MemorySize(8192 bytes) を超えるデータを追加して先頭レコードをディスクへ追い出す
        const int count = 200;
        for(var i = 0;i<count;i++) set.Add(i);
        // Contains → Read → Pending(Session, Status, ref Output) の IsPending 分岐
        Assert.True(set.Contains(0));
        // Remove → DeleteSession.RMW → Pending(Session, Status) の IsPending 分岐
        Assert.True(set.Remove(0));
        // Add → Session.RMW → Pending(Session, Status) の IsPending 分岐
        Assert.True(set.Add(0));
    }
    [Fact, Trait("Category",Unit)]
    public void IsDisposed_Dictionary_Dispose前後() {
        var store = Factory.Create().Create<int,string>();
        var dict = store.Create();
        Assert.False(dict.IsDisposed);
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void IsDisposed_HashSet_Dispose前後() {
        var store = Factory.Create().Create<int>();
        var set = store.Create();
        Assert.False(set.IsDisposed);
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void Dispose_Dictionary_二重呼び出し_冪等() {
        var store = Factory.Create().Create<int,string>();
        var dict = store.Create();
        dict.Dispose();
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void Dispose_HashSet_二重呼び出し_冪等() {
        var store = Factory.Create().Create<int>();
        var set = store.Create();
        set.Dispose();
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void Dispose_TestCollection_二重呼び出し_Collection5早期リターン() {
        var store = Factory.Create().Create<int>();
        var subject = new TestCollection<int,EqualityEmpty>(store);
        subject.Dispose();
        subject.Dispose();
        Assert.True(subject.IsDisposed);
        store.Dispose();
    }
}
