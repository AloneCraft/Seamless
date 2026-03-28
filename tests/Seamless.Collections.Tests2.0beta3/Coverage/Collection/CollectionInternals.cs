using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class CollectionInternals {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    // WouldOverflowMutable: LogMemorySize = 0 → if(LogMemorySize<=0) return false の分岐
    [Fact, Trait("Category","Coverage")]
    public void WouldOverflowMutable_LogMemorySizeZero_早期リターンFalse() {
        using var store = new HashSetStore<int>();
        // 内部コンストラクタで LogMemorySize=0 を指定 → WouldOverflowMutable は常に false を返す
        using var set = new Seamless.Collections.HashSet<int>(store.Store,LogMemorySize: 0);
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
        using var set = new Seamless.Collections.HashSet<int>(store.Store,LogMemorySize: 0);
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
}
