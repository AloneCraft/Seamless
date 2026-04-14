namespace Test.Seamless.Collections;

/// <summary>
/// <see cref="Seamless.Collections.Collection{TKey,TValue,TReadOutput,TAddInput,TReadFunctions,TAddFunctions,TDeleteFunctions,TStoreFunctions,TAllocator}"/>
/// のテスト。テストに使うクラスはインナークラスとする。
/// </summary>
public class Collection{
    [Fact,Trait("Category","Unit")]
    public void PrivateProtectedWouldOverflowMutable(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void LogMemorySize(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void MutableFraction(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void ThrowLogFull(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void WouldOverflowMutable(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Count(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void EntryCount(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Read(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Remove(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Contains(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void IsDisposed(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Dispose(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Dispose_Boolean(){
        //ここにテストコードを書いて
    }

    [Fact,Trait("Category","Unit")]
    public void Destructor(){
        //ここにテストコードを書いて
    }
    //↓のコードが今までのテストコードの例。必要に応じて参考にしてください。

    //// ── テスト用インナークラス ─────────────────────────────────────
    //private sealed class TestCollection<TKey,TValue>(
    //    ManagedCollectionStore<TKey,TValue> CollectionStore
    //)
    //    :ManagedCollection<
    //        TKey,
    //        TValue,
    //        ManagedSerializer<TKey>,
    //        ManagedSerializer<TValue>
    //    >(CollectionStore)
    //    where TKey:IEquatable<TKey>
    //    where TValue:IEquatable<TValue>{
    //    private protected override bool WouldOverflowMutable(
    //        int KeyLength,
    //        int ValueLength
    //    )=>false;
    //    private bool disposed;
    //    public override bool IsDisposed=>this.disposed;
    //    protected override void Dispose(bool disposing){
    //        base.Dispose(disposing);
    //        this.disposed=true;
    //    }
    //}

    //private static string CreateFolder()=>
    //    Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    //// ── PrivateProtectedWouldOverflowMutable ──────────────────────
    //// MutableFraction=0 → MutableBytes=0 → if(MutableBytes<=0) MutableBytes=LogMemorySize の分岐
    //[Fact, Trait("Category","Unit")]
    //public void WouldOverflowMutable_MutableFractionZero_MutableBytesはLogMemorySize() {
    //    using var store = Factory.Create(MutableFraction: 0.0).Create<int>();
    //    using var set = store.Create();
    //    // MutableBytes = (long)(LogMemorySize * 0.0) = 0 → LogMemorySize で上書き → 溢れずに追加できる
    //    Assert.True(set.Add(1));
    //}

    //// ── Count / GetLongCount ─────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void Count_GetLongCountと一致する() {
    //    using var store = Factory.Create().Create<int>();
    //    using var writer = store.Create();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    writer.Add(1);
    //    writer.Add(2);
    //    Assert.Equal((int)subject.GetLongCount(),subject.Count);
    //}

    //[Fact, Trait("Category","Unit")]
    //public void GetLongCount_空の場合ゼロを返す() {
    //    using var store = Factory.Create().Create<int>();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    Assert.Equal(0L,subject.GetLongCount());
    //}

    //// ── EntryCount ───────────────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void EntryCount_追加後に増加する() {
    //    using var store = Factory.Create().Create<int>();
    //    using var writer = store.Create();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    writer.Add(1);
    //    Assert.True(subject.EntryCount>=1);
    //}

    //// ── Contains (Read 経由) ─────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void Contains_存在するキー_真を返す() {
    //    using var store = Factory.Create().Create<int>();
    //    using var writer = store.Create();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    writer.Add(1);
    //    Assert.True(subject.Contains(1));
    //}

    //[Fact, Trait("Category","Unit")]
    //public void Contains_存在しないキー_偽を返す() {
    //    using var store = Factory.Create().Create<int>();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    Assert.False(subject.Contains(99));
    //}

    //// ── Remove ──────────────────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void Remove_存在するキー_真を返す() {
    //    using var store = Factory.Create().Create<int>();
    //    using var writer = store.Create();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    writer.Add(1);
    //    Assert.True(subject.Remove(1));
    //}

    //[Fact, Trait("Category","Unit")]
    //public void Remove_存在しないキー_偽を返す() {
    //    using var store = Factory.Create().Create<int>();
    //    using var subject = new TestCollection<int,EqualityEmpty>(store);
    //    Assert.False(subject.Remove(99));
    //}

    //// ── Pending (IsPending 分岐: Read / RMW) ─────────────────────
    //[Fact, Trait("Category","Unit")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    //public void Pending_IsPending分岐_ディスクスピル経由() {
    //    var folder = CreateFolder();
    //    using var logDevice = new LocalStorageDevice(folder);
    //    using var objDevice = new LocalStorageDevice(folder+"_obj");
    //    using var store = Factory.Create(MutableFraction:0).Create<int>(logDevice,objDevice);
    //    using var set = store.Create();
    //    const int count = 200;
    //    for(var i = 0;i<count;i++) set.Add(i);
    //    // Contains → Read → Pending(Session, Status, ref Output) の IsPending 分岐
    //    Assert.True(set.Contains(0));
    //    // Remove → DeleteSession.RMW → Pending(Session, Status) の IsPending 分岐
    //    Assert.True(set.Remove(0));
    //    // Add → Session.RMW → Pending(Session, Status) の IsPending 分岐
    //    Assert.True(set.Add(0));
    //}

    //// ── IsDisposed ──────────────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void IsDisposed_Dictionary_Dispose前後() {
    //    var store = Factory.Create().Create<int,string>();
    //    var dict = store.Create();
    //    Assert.False(dict.IsDisposed);
    //    dict.Dispose();
    //    Assert.True(dict.IsDisposed);
    //    store.Dispose();
    //}

    //[Fact, Trait("Category","Unit")]
    //public void IsDisposed_HashSet_Dispose前後() {
    //    var store = Factory.Create().Create<int>();
    //    var set = store.Create();
    //    Assert.False(set.IsDisposed);
    //    set.Dispose();
    //    Assert.True(set.IsDisposed);
    //    store.Dispose();
    //}

    //// ── Dispose（冪等性）────────────────────────────────────────
    //[Fact, Trait("Category","Unit")]
    //public void Dispose_Dictionary_二重呼び出し_冪等() {
    //    var store = Factory.Create().Create<int,string>();
    //    var dict = store.Create();
    //    dict.Dispose();
    //    dict.Dispose();
    //    Assert.True(dict.IsDisposed);
    //    store.Dispose();
    //}

    //[Fact, Trait("Category","Unit")]
    //public void Dispose_HashSet_二重呼び出し_冪等() {
    //    var store = Factory.Create().Create<int>();
    //    var set = store.Create();
    //    set.Dispose();
    //    set.Dispose();
    //    Assert.True(set.IsDisposed);
    //    store.Dispose();
    //}

    //[Fact, Trait("Category","Unit")]
    //public void Dispose_TestCollection_二重呼び出し_Collection基底早期リターン() {
    //    var store = Factory.Create().Create<int>();
    //    var subject = new TestCollection<int,EqualityEmpty>(store);
    //    subject.Dispose();
    //    subject.Dispose();
    //    Assert.True(subject.IsDisposed);
    //    store.Dispose();
    //}
}
