using Seamless.Collections;

using Tsavorite.core;
namespace Test.Seamless.Collections;
public class StoreCheckpoint {
    //private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest");
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
    public async Task SaveLogOnlyAsync_CheckpointDir未設定_InvalidOperationExceptionをスロー() {
        using var store = Store.Create<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.SaveLogOnlyAsync());
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public void Save_Load_HashSet_データが永続化され復元される() {
        var folder = CreateFolder();
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
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_HashSet_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        bool success;
        Guid token;
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token=await store.SaveAsync();
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync(token);
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
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_Dictionary_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        bool success;
        Guid token;
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token=await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync(token);
            using var dict = store.Create();
            Assert.True(dict.ContainsKey(1));
            Assert.Equal("one",dict[1]);
            Assert.True(dict.ContainsKey(2));
            Assert.Equal("two",dict[2]);
            Assert.False(dict.ContainsKey(99));
        }
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_LoadAsync_HashSet_ハッシュインデックスが復元されContainsが動作する() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        bool success;
        Guid token;
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var set = store.Create();
            for(var i=0;i<100;i++) set.Add(i);
            token=await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync(token);
            using var set = store.Create();
            for(var i=0;i<100;i++) Assert.True(set.Contains(i),$"Contains({i}) failed after recovery");
            Assert.False(set.Contains(999));
        }
    }
    [Fact,Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public async Task SaveAsync_LoadAsync_Dictionary_ハッシュインデックスが復元されReadが動作する(){
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        bool success;
        Guid token;
        {
            using var logDevice=new LocalStorageDevice(folder);
            using var objDevice=new LocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var dict=store.Create();
            for(var i=0;i<50;i++) dict.Add(i,$"val_{i}");
            token=await store.SaveAsync();
        }
        {
            using var logDevice=new LocalStorageDevice(folder);
            using var objDevice=new LocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync(token);
            using var dict=store.Create();
            for(var i=0;i<50;i++){
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
            Assert.False(dict.ContainsKey(999));
        }
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_CheckpointFailed_InvalidOperationExceptionをスロー() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
        using var set = store.Create();
        set.Add(1);
        // SaveAsync が同期的にロックを取得してから非同期 I/O を待機するため、
        // await せずタスクを保持しておくことでロックが取得された状態を維持できる
        var ongoingCheckpoint = store.SaveAsync().AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync().AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveLogOnlyAsync_CheckpointFailed_InvalidOperationExceptionをスロー() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
        using var set = store.Create();
        set.Add(1);
        // SaveLogOnlyAsync が同期的にロックを取得してから非同期 I/O を待機するため、
        // await せずタスクを保持しておくことでロックが取得された状態を維持できる
        var ongoingCheckpoint = store.SaveLogOnlyAsync().AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveLogOnlyAsync().AsTask());
        await ongoingCheckpoint;
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveAsync_CompleteCheckpointAsyncキャンセル_OperationCanceledExceptionをスロー() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
        using var set = store.Create();
        set.Add(1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(ct:cts.Token).AsTask());
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveLogOnlyAsync_CompleteCheckpointAsyncキャンセル_OperationCanceledExceptionをスロー() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        using var logDevice = new LocalStorageDevice(folder);
        using var objDevice = new LocalStorageDevice(folder+"_obj");
        using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
        using var set = store.Create();
        set.Add(1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveLogOnlyAsync(ct:cts.Token).AsTask());
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveLogOnlyAsync_LoadAsync_HashSet_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            token = await store.SaveLogOnlyAsync();
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync(token);
            using var set = store.Create();
            Assert.True(set.Contains(1));
            Assert.True(set.Contains(2));
            Assert.True(set.Contains(3));
            Assert.False(set.Contains(99));
        }
    }
    [Fact, Trait("Category","Coverage")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    public async Task SaveLogOnlyAsync_LoadAsync_Dictionary_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            token = await store.SaveLogOnlyAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync(token);
            using var dict = store.Create();
            Assert.True(dict.ContainsKey(1));
            Assert.Equal("one",dict[1]);
            Assert.True(dict.ContainsKey(2));
            Assert.Equal("two",dict[2]);
            Assert.False(dict.ContainsKey(99));
        }
    }
}
