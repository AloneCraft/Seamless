using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Collections;


public class Collection {
    internal sealed class TestCollection<TKey,TValue>(CollectionStore<TKey,TValue> store)
        :Collection<TKey,TValue>(store.Store,store.LogMemorySize,store.MutableFraction)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue>{
        private bool disposed;
        public override bool IsDisposed=>this.disposed;
        protected override void Dispose(bool disposing){
            base.Dispose(disposing);
            this.disposed=true;
        }
    }
    [Fact, Trait("Category","Coverage")]
    public void EntryCount_追加後に増加する() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.EntryCount>=1);
    }
    [Fact, Trait("Category","Coverage")]
    public void GetLongCount_空の場合ゼロを返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        Assert.Equal(0L,subject.GetLongCount());
    }
    [Fact, Trait("Category","Coverage")]
    public void Count_GetLongCountと一致する() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        writer.Add(1);
        writer.Add(2);
        Assert.Equal((int)subject.GetLongCount(),subject.Count);
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_存在するキー_真を返す() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.Contains(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Contains_存在しないキー_偽を返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        Assert.False(subject.Contains(99));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在するキー_真を返す() {
        using var store = new HashSetStore<int>();
        using var writer = store.Create();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        writer.Add(1);
        Assert.True(subject.Remove(1));
    }
    [Fact, Trait("Category","Coverage")]
    public void Remove_存在しないキー_偽を返す() {
        using var store = new HashSetStore<int>();
        using var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        Assert.False(subject.Remove(99));
    }
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    // WouldOverflowMutable: LogMemorySize = 0 → if(LogMemorySize<=0) return false の分岐
    [Fact, Trait("Category","Coverage")]
    public void WouldOverflowMutable_LogMemorySizeZero_早期リターンFalse() {
        using var store = new HashSetStore<int>();
        // 内部コンストラクタで LogMemorySize=0 を指定 → WouldOverflowMutable は常に false を返す
        using var set = new global::Seamless.Collections.HashSet<int>(store.Store,LogMemorySize: 0);
        Assert.True(set.Add(1));
    }

    // WouldOverflowMutable: MutableFraction=0 → MutableBytes=0 → if(MutableBytes<=0) MutableBytes=LogMemorySize の分岐
    [Fact, Trait("Category","Coverage")]
    public void WouldOverflowMutable_MutableFractionZero_MutableBytesはLogMemorySize() {
        using var store = new HashSetStore<int>(MutableFraction: 0.0);
        using var set = store.Create();
        // MutableBytes = (long)(LogMemorySize * 0.0) = 0 → LogMemorySize で上書き → 溢れずに追加できる
        Assert.True(set.Add(1));
    }

    // Pending(Session, Status, ref Output) の IsPending 分岐 → Contains(Read) がディスク上レコードを参照
    // Pending(Session, Status)             の IsPending 分岐 → Remove/Add(RMW) がディスク上レコードを参照
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Pending_IsPending分岐_ディスクスピル経由() {
        var folder = CreateFolder();
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        // MutableFraction=0 でTsavorite内部が即時ReadOnly化しディスクへスピルさせる
        using var store = new HashSetStore<int>(logDevice,objDevice,MutableFraction: 0);
        // LogMemorySize=0 でWouldOverflowMutableを無効化し、ディスクスピルを妨げない
        using var set = new global::Seamless.Collections.HashSet<int>(store.Store,LogMemorySize: 0);
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
    [Fact, Trait("Category","Coverage")]
    public void IsDisposed_Dictionary_Dispose前後() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        Assert.False(dict.IsDisposed);
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void IsDisposed_HashSet_Dispose前後() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        Assert.False(set.IsDisposed);
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_Dictionary_二重呼び出し_冪等() {
        var store = new DictionaryStore<int,string>();
        var dict = store.Create();
        dict.Dispose();
        dict.Dispose();
        Assert.True(dict.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_HashSet_二重呼び出し_冪等() {
        var store = new HashSetStore<int>();
        var set = store.Create();
        set.Dispose();
        set.Dispose();
        Assert.True(set.IsDisposed);
        store.Dispose();
    }
    [Fact, Trait("Category","Coverage")]
    public void Dispose_TestCollection_二重呼び出し_Collection5早期リターン() {
        var store = new HashSetStore<int>();
        var subject = new TestCollection<int,global::Seamless.Collections.Empty>(store);
        subject.Dispose();
        subject.Dispose();
        Assert.True(subject.IsDisposed);
        store.Dispose();
    }
}
