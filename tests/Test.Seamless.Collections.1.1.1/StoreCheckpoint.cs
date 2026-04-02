using Seamless.Collections;
using Tsavorite.core;
namespace Test.Seamless.Collections111;
public class StoreCheckpoint {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest111_"+Guid.NewGuid().ToString("N"));
    [Fact, Trait("Category","Coverage")]
    public async Task SaveAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        using var store = Store.Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveAsync());
    }
    [Fact, Trait("Category","Coverage")]
    public async Task LoadAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        using var store = Store.Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.LoadAsync());
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_HashSet_ハッシュインデックスが復元されContainsが動作する() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var set = store.Create();
            for(var i=0;i<100;i++) set.Add(i);
            await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync();
            using var set = store.Create();
            for(var i=0;i<100;i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_Dictionary_ハッシュインデックスが復元されReadが動作する() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var dict = store.Create();
            for(var i=0;i<50;i++) dict.Add(i,$"val_{i}");
            await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync();
            using var dict = store.Create();
            for(var i=0;i<50;i++) {
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
            Assert.False(dict.ContainsKey(999));
        }
    }
}
