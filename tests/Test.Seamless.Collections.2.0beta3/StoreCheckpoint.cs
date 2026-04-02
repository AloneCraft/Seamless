using Garnet.common;

using Seamless.Collections;

using Tsavorite.core;

using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace Test.Seamless.Collections;
public class StoreCheckpoint {
    private static string CreateFolder() => Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
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
    public void Save_Load_HashSet_データが永続化され復元される() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var objDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,int>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var set = store.Create();
            {
                int Key=1;
                var KeyBytes=MemoryPack.MemoryPackSerializer.Serialize(Key);
                var Value=11;
                var ValueBytes=MemoryPack.MemoryPackSerializer.Serialize(Value);
                // ④ SpanByte → FixedSpanByteKey、PinnedSpanByte → SpanByteAndMemory に修正
                var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
                var Session=set.ClientSession;
                var status=Session.BasicContext.Upsert(FixedKey,ValueBytes);
                if(status.IsPending){
                    Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                } else {
                }
            }
            //set[1]=11;
            //set[2]=22;
            //set[3]=33;
            Assert.True(set.Contains(1));
            token=store.Save();
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var objDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,int>(CheckpointDir:checkpointDir,recoverDevice:true,LogDevice:logDevice,ObjectLogDevice:objDevice);
            store.Load(token);
            var (begin2,head2,ro2,tail2) = store.Address;
            using var set = store.Create();
            {
                int Key=1;
                var KeyBytes=MemoryPack.MemoryPackSerializer.Serialize(Key);
                // ④ SpanByte → FixedSpanByteKey、PinnedSpanByte → SpanByteAndMemory に修正
                var FixedKey=(FixedSpanByteKey)(ReadOnlySpan<byte>)KeyBytes;
                SpanByteAndMemory Value0=default;
                var Session=set.ClientSession;
                var status=Session.BasicContext.Read(FixedKey,ref Value0);
                if(status.IsPending){
                    Session.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    using(completedOutputs){
                        Assert.True(completedOutputs.Next(),$"キー {Key} の非同期Readが完了しませんでした");
                        Assert.True(completedOutputs.Current.Status.Found,$"キー {Key} はリカバリー後に見つかるはずです");
                    }
                } else {
                    Assert.True(status.Found,$"キー {Key} はリカバリー後に見つかるはずです");
                }
            }
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
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var set = store.Create();
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.True(set.Contains(1));
            await store.SaveAsync();
            Assert.True(set.Contains(1));
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync();
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
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var dict = store.Create();
            dict.Add(1,"one");
            dict.Add(2,"two");
            await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync();
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
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            using var set = store.Create();
            for(var i=0;i<100;i++) set.Add(i);
            await store.SaveAsync();
        }
        {
            using var logDevice = new LocalStorageDevice(folder);
            using var objDevice = new LocalStorageDevice(folder+"_obj");
            using var store = Store.Create<int>(logDevice,objDevice,CheckpointDir: checkpointDir);
            await store.LoadAsync();
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
        {
            using var logDevice=new LocalStorageDevice(folder);
            using var objDevice=new LocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            using var dict=store.Create();
            for(var i=0;i<50;i++) dict.Add(i,$"val_{i}");
            await store.SaveAsync();
        }
        {
            using var logDevice=new LocalStorageDevice(folder);
            using var objDevice=new LocalStorageDevice(folder+"_obj");
            using var store=Store.Create<int,string>(CheckpointDir:checkpointDir,LogDevice:logDevice,ObjectLogDevice:objDevice);
            await store.LoadAsync();
            using var dict=store.Create();
            for(var i=0;i<50;i++){
                Assert.True(dict.ContainsKey(i),$"ContainsKey({i}) failed after recovery");
                Assert.Equal($"val_{i}",dict[i]);
            }
            Assert.False(dict.ContainsKey(999));
        }
    }
}
