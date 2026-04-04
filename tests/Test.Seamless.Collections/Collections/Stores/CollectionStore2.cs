using Common;
using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Collections;
/// <summary>
/// src/Seamless.Collections/Store/CollectionStore`2.cs の全分岐をカバーする。
/// 既存テスト(CoverInternals等)でカバーしていない分岐に注力する。
/// </summary>

public class CollectionStore2 {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize = Const.MinObjectLogCapacity;
    //CollectionStore コンストラクタ: ObjectLogDevice.Capacity<0 の分岐
    //Capacity=-1のデバイスを渡すとObjectLogSegmentSize=Const.MinObjectLogCapacityになる
    [Fact, Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Constructor_ObjectLogCapacity_Negative() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        //Capacityを指定しないとデフォルト=-1になる(Capacity<0分岐を通す)
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true);
        //Capacity<0 → ObjectLogSegmentSize=Const.MinObjectLogCapacity として正常に構築されること
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Equal("a",dict[1]);
    }
    //CollectionStore.CompactWithLookup: CompactUntil<=BeginAddress の早期return分岐
    //returnの後は2.0だとカバーできる
    //データなしまたは直後の2回目呼び出しでSafeReadOnlyAddress<CompactUntilとなる場合
    [Fact, Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void CompactWithLookup_EarlyReturn() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0.1,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        //データを書き込んでCompactWithLookupを実行
        for(var a=0;a<1000;a++){
            dict.Add(a,$"v{a}");
        }
        {
            var before=store.Address;
            store.CompactWithLookup();
            var after=store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        //2回目呼び出し: BeginAddressが進んだ後にさらに呼び出す
        //SafeReadOnlyAddressがBeginAddress以下になるとCompactUntil<=BeginAddressの早期returnが発生する
        {
            var beforeSecond=store.Address;
            store.CompactWithLookup();
            var afterSecond=store.Address;
            //例外が発生せず正常完了することを確認
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
        //連続呼び出し: 早期returnのelse経路をカバー (BeginAddress=CompactUntilになる場合)
        store.CompactWithLookup();
        store.CompactWithLookup();
    }
    //1.1.1CollectionStore.CompactWithLookup: CompactUntil>SafeReadOnlyAddress でクリップされる分岐
    [Fact, Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void CompactWithLookup() {
        //1.1.1の
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0.1,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        //データを書き込んでCompactWithLookupを実行
        for(var a = 0; a<1000; a++) {
            dict.Add(a,$"v{a}");
        }
        //1.1.1ではReadOnlyAddressの進行がバッファ容量ベースのため10000件ではSafeReadOnlyAddressが
        //BeginAddressを超えず早期returnになる。ShiftReadOnlyAddressで強制的にTailAddressまで
        //読み取り専用にしてSafeReadOnlyAddressを進め、CompactWithLookupが実際にコンパクションを
        //実行できる状態にする。2.0beta3では既にReadOnlyAddressが進んでいるため冪等で無害。
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress, true);
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        //2回目呼び出し: BeginAddressが進んだ後にさらに呼び出す
        //SafeReadOnlyAddressがBeginAddress以下になるとCompactUntil<=BeginAddressの早期returnが発生する
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            //例外が発生せず正常完了することを確認
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
    }
    //2.0CollectionStore.CompactWithLookup: CompactUntil>SafeReadOnlyAddress でクリップされる分岐
    [Fact, Trait("Category",Unit)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void CompactWithLookup_ClipToSafeReadOnly() {
        var folder = CreateFolder();
        using var LogDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
        //MutableFraction=0で全データを即時読み取り専用にする
        using var store = Store.Create<int,string>(MemorySize:TestMemorySize,MutableFraction:0,LogDevice:LogDevice,ObjectLogDevice:ObjDevice);
        using var dict = store.Create();
        //少量のデータを追加してSafeReadOnlyAddressがSegmentSize以下になる状況を作る
        dict.Add(1,"a");
        dict.Add(2,"b");
        var address = store.Address;
        //SafeReadOnlyAddressが小さい状態でCompactWithLookup → CompactUntil>SafeReadOnlyAddressのクリップ発生
        store.CompactWithLookup();
        //例外なく完了すること
        Assert.True(store.Address.BeginAddress>=address.BeginAddress);
    }
    [Fact, Trait("Category",Unit)]
    public void Dispose_二重呼び出し_冪等() {
        var store = Store.Create<int>();
        store.Dispose();
        store.Dispose();
    }
    [Fact, Trait("Category",Unit)]
    public void Finalize_GCによる非同期破棄() {
        static void CreateOrphan() {
            _ = Store.Create<int,string>();
        }
        CreateOrphan();
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
    }
}
