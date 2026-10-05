using Seamless.Execution;
using StoreType=Seamless.Execution.Store;
namespace Execution.Tests;
public sealed class Store{
    private static string NewRoot(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-store-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    [Fact]public void CreateTemporary(){
        var root=NewRoot();
        try{
            {
                //一時ストアは指定ルートの下に専用フォルダを作り、破棄で全削除する
                var store=StoreType.CreateTemporary(temporaryDirectory:root);
                Assert.Equal(StoreKind.Temporary,store.Kind);
                var directory=store.DirectoryPath!;
                Assert.StartsWith(root,directory);
                Assert.True(Directory.Exists(directory));
                File.WriteAllText(Path.Combine(store.GetComponentDirectory("a"),"x.dat"),"x");
                store.Dispose();
                Assert.False(Directory.Exists(directory));
            }
            {
                //上限を省略すると ExecutionOptions と同じ既定値
                using var store=StoreType.CreateTemporary(temporaryDirectory:root);
                var defaults=new ExecutionOptions();
                Assert.Equal(defaults.MemoryLimitBytes,store.Limits.MemoryBytes);
                Assert.Equal(defaults.TemporaryStorageLimitBytes,store.Limits.DiskBytes);
            }
            {
                //異常終了で残った一時ストア(子フォルダを含む)は RecoverAbandoned で回収できる。使用中のものは回収しない
                var abandoned=Path.Combine(root,"store-"+Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.Combine(abandoned,"data","orders","checkpoints"));
                File.WriteAllText(Path.Combine(abandoned,"data","orders","checkpoints","c.bin"),"c");
                File.WriteAllText(Path.Combine(abandoned,"owner.json"),global::System.Text.Json.JsonSerializer.Serialize(new{Format="seamless-store-v1",Id=Path.GetFileName(abandoned)}));
                File.WriteAllText(Path.Combine(abandoned,"owner.lock"),string.Empty);
                using var live=StoreType.CreateTemporary(temporaryDirectory:root);
                var recovered=TemporaryResourceRecovery.RecoverAbandoned(root);
                Assert.Equal([abandoned],recovered);
                Assert.False(Directory.Exists(abandoned));
                Assert.True(Directory.Exists(live.DirectoryPath));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Open(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"app");
            {
                //永続ストアはフォルダが無ければ作り、破棄してもフォルダは残る
                var store=StoreType.Open(directory);
                Assert.Equal(StoreKind.Persistent,store.Kind);
                Assert.Equal(Path.GetFullPath(directory),store.DirectoryPath);
                store.Dispose();
                Assert.True(Directory.Exists(directory));
            }
            {
                //開いている間は同じフォルダを二重に開けない
                using var first=StoreType.Open(directory);
                Assert.Throws<IOException>(()=>StoreType.Open(directory));
            }
            {
                //破棄後は開き直せる
                using var again=StoreType.Open(directory);
                Assert.Equal(StoreKind.Persistent,again.Kind);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void CreateMemory(){
        using var store=StoreType.CreateMemory(new(){MemoryBytes=1024,DiskBytes=0});
        {
            //メモリストアはフォルダを持たない
            Assert.Equal(StoreKind.Memory,store.Kind);
            Assert.Null(store.DirectoryPath);
            Assert.Throws<InvalidOperationException>(()=>store.GetComponentDirectory("a"));
        }
        {
            //メモリストアは保存できない
            Assert.Throws<NotSupportedException>(()=>store.Save());
        }
    }
    [Fact]public void Budget(){
        using var store=StoreType.CreateMemory(new(){MemoryBytes=1000,DiskBytes=10});
        {
            //予算の上限はストアの上限と同じ
            Assert.Equal(1000,store.Budget.MemoryLimitBytes);
            Assert.Equal(10,store.Budget.TemporaryStorageLimitBytes);
        }
        {
            //上限を超える予約は ResourceLimitException
            using var reserved=store.Budget.ReserveMemory(700);
            Assert.Throws<ResourceLimitException>(()=>store.Budget.ReserveMemory(400));
            Assert.Throws<ResourceLimitException>(()=>store.Budget.ReserveTemporaryStorage(11));
        }
    }
    [Fact]public void GetComponentDirectory(){
        var root=NewRoot();
        try{
            using var store=StoreType.CreateTemporary(temporaryDirectory:root);
            {
                //子資源のフォルダはストアのフォルダの下に作られ、同じ名前なら同じ場所
                var a=store.GetComponentDirectory("orders");
                Assert.True(Directory.Exists(a));
                Assert.StartsWith(store.DirectoryPath!,a);
                Assert.Equal(a,store.GetComponentDirectory("orders"));
                Assert.NotEqual(a,store.GetComponentDirectory("items"));
            }
            {
                //パスとして危険な名前は拒否する
                Assert.Throws<ArgumentException>(()=>store.GetComponentDirectory(".."));
                Assert.Throws<ArgumentException>(()=>store.GetComponentDirectory("a/b"));
                Assert.Throws<ArgumentException>(()=>store.GetComponentDirectory(""));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Register(){
        var root=NewRoot();
        try{
            var store=StoreType.CreateTemporary(temporaryDirectory:root);
            var a=store.Register(new RecordingComponent("a"));
            {
                //同じ名前の二重登録は拒否する
                Assert.Throws<ArgumentException>(()=>store.Register(new RecordingComponent("a")));
            }
            {
                //ストアの破棄で登録済みの子資源も破棄される。保存はしない
                store.Dispose();
                Assert.True(a.Disposed);
                Assert.Equal(0,a.SaveCount);
            }
            {
                //破棄後の登録は ObjectDisposedException
                Assert.Throws<ObjectDisposedException>(()=>store.Register(new RecordingComponent("b")));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Unregister(){
        var root=NewRoot();
        try{
            using var store=StoreType.CreateTemporary(temporaryDirectory:root);
            var a=store.Register(new RecordingComponent("a"));
            var b=store.Register(new RecordingComponent("b"));
            {
                //登録を外した子資源は以後の保存に含まれず、破棄もされない
                Assert.True(store.Unregister(a));
                Assert.False(store.Unregister(a));
                store.Save();
                Assert.Equal(0,a.SaveCount);
                Assert.Equal(1,b.SaveCount);
                store.Dispose();
                Assert.False(a.Disposed);
                Assert.True(b.Disposed);
            }
            {
                //外した名前は再び登録できる
                using var other=StoreType.CreateTemporary(temporaryDirectory:root);
                var first=other.Register(new RecordingComponent("x"));
                other.Unregister(first);
                other.Register(new RecordingComponent("x"));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void TryGetSavedState(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"app");
            using(var store=StoreType.Open(directory)){
                //保存前は状態が無い
                Assert.False(store.TryGetSavedState("a",out _));
                store.Register(new RecordingComponent("a"){State="a1"});
                store.Save();
            }
            {
                //開き直すと、登録前でも現在の保存番号の状態を読める
                using var store=StoreType.Open(directory);
                Assert.True(store.TryGetSavedState("a",out var state));
                Assert.Equal("a1",state);
                Assert.False(store.TryGetSavedState("b",out _));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Save(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"app");
            Guid first,second;
            {
                //保存は全子資源の状態を記録し、保存番号を返す
                using var store=StoreType.Open(directory);
                var a=store.Register(new RecordingComponent("a"){State="a1"});
                var b=store.Register(new RecordingComponent("b"){State="b1"});
                first=store.Save();
                Assert.Equal(1,a.SaveCount);
                Assert.Equal(1,b.SaveCount);
                Assert.Equal(first,store.CurrentCheckpoint);
                a.State="a2";
                second=store.Save();
                Assert.NotEqual(first,second);
                Assert.Equal([first,second],store.Checkpoints);
            }
            {
                //開き直すと最新の保存状態が、あとから登録した子資源へ渡される
                using var store=StoreType.Open(directory);
                Assert.Equal(second,store.CurrentCheckpoint);
                var a=store.Register(new RecordingComponent("a"));
                var b=store.Register(new RecordingComponent("b"));
                var c=store.Register(new RecordingComponent("c"));
                Assert.Equal(["a2"],a.Loaded);
                Assert.Equal(["b1"],b.Loaded);
                Assert.Empty(c.Loaded);
            }
            {
                //保存しないまま破棄した変更は確定しない(保存番号は増えない)
                using(var store=StoreType.Open(directory)){
                    store.Register(new RecordingComponent("a"){State="a3"});
                }
                using var reopened=StoreType.Open(directory);
                Assert.Equal([first,second],reopened.Checkpoints);
            }
            {
                //永続ストアは全子資源の保存が終わってから、マニフェストを書く前に、全子資源を確実に書き出す
                var events=new List<string>();
                using var store=StoreType.Open(Path.Combine(root,"ordered"));
                var manifest=Path.Combine(store.DirectoryPath!,"store.json");
                store.Register(new RecordingComponent("a"){OnSave=()=>events.Add("save a"),OnFlush=()=>events.Add("flush a manifest="+File.Exists(manifest))});
                store.Register(new RecordingComponent("b"){OnSave=()=>events.Add("save b"),OnFlush=()=>events.Add("flush b manifest="+File.Exists(manifest))});
                store.Save();
                Assert.Equal(["save a","save b","flush a manifest=False","flush b manifest=False"],events);
                Assert.True(File.Exists(manifest));
            }
            {
                //一時ストアは開き直せないので、確実な書き出しはしない
                using var store=StoreType.CreateTemporary(temporaryDirectory:root);
                var a=store.Register(new RecordingComponent("a"));
                store.Save();
                Assert.Equal(1,a.SaveCount);
                Assert.Equal(0,a.FlushCount);
            }
            {
                //書き出しに失敗したら保存も失敗し、マニフェストは書かれず保存番号も増えない。直せば保存できる
                using var store=StoreType.Open(Path.Combine(root,"failing"));
                var failing=true;
                store.Register(new RecordingComponent("a"){OnFlush=()=>{
                    if(failing){
                        throw new IOException("disk full");
                    }
                }});
                Assert.Throws<IOException>(()=>store.Save());
                Assert.Empty(store.Checkpoints);
                Assert.False(File.Exists(Path.Combine(store.DirectoryPath!,"store.json")));
                failing=false;
                var token=store.Save();
                Assert.Equal([token],store.Checkpoints);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Save_DropsOlderSaves(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"app");
            using var store=StoreType.Open(directory);
            var manifest=Path.Combine(directory,"store.json");
            string? manifestAtCommit=null;
            var a=store.Register(new RecordingComponent("a"){State="a1",OnSaveCommitted=()=>manifestAtCommit=File.ReadAllText(manifest)});
            var b=store.Register(new RecordingComponent("b"){State="b1"});
            var first=store.Save();
            {
                //保存が確定する(マニフェストが書かれる)前には、確定の通知は呼ばれない。確定した保存の通知は 1 回ずつ
                Assert.Equal(1,a.SaveCommittedCount);
                Assert.Equal(1,b.SaveCommittedCount);
                Assert.Contains(first.ToString(),manifestAtCommit);
            }
            {
                //どれか 1 つの子資源が前の保存へ戻れなくなる変更をしていたら、新しい保存だけを残す(記録を、確定の通知より先に書き換える)
                b.InvalidatesOlderSaves=true;
                var second=store.Save();
                Assert.Equal([second],store.Checkpoints);
                Assert.DoesNotContain(first.ToString(),manifestAtCommit);
                Assert.Contains(second.ToString(),manifestAtCommit);
                Assert.Throws<ArgumentException>(()=>store.Load(first));
                Assert.Equal(2,a.SaveCommittedCount);
                Assert.Equal(2,b.SaveCommittedCount);
                //外した結果は開き直しても保たれる
                store.Dispose();
                using var reopened=StoreType.Open(directory);
                Assert.Equal([second],reopened.Checkpoints);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Save_KeepsOlderSavesUnlessInvalidated(){
        var root=NewRoot();
        try{
            //前の保存へ戻れなくなる変更が無ければ、保存を重ねて記録を増やす。外す必要が無くなったら、また増える
            using var store=StoreType.Open(Path.Combine(root,"app"));
            var a=store.Register(new RecordingComponent("a"));
            var first=store.Save();
            a.InvalidatesOlderSaves=true;
            var second=store.Save();
            a.InvalidatesOlderSaves=false;
            var third=store.Save();
            Assert.Equal([second,third],store.Checkpoints);
            Assert.NotEqual(first,second);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Save_FailureCommitsNothing(){
        var root=NewRoot();
        try{
            //書き出しに失敗した保存は、前の保存を外さず、確定の通知もしない
            using var store=StoreType.Open(Path.Combine(root,"app"));
            var failing=false;
            var a=store.Register(new RecordingComponent("a"){OnFlush=()=>{
                if(failing){
                    throw new IOException("disk full");
                }
            }});
            var first=store.Save();
            a.InvalidatesOlderSaves=true;
            failing=true;
            Assert.Throws<IOException>(()=>store.Save());
            Assert.Equal([first],store.Checkpoints);
            Assert.Equal(1,a.SaveCommittedCount);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Load_ValidatesBeforeDroppingNewerSaves(){
        var root=NewRoot();
        try{
            //戻り先の保存が読み込めないと分かったときは、新しい保存を記録から外さず、子資源へも渡さない
            var directory=Path.Combine(root,"app");
            using var store=StoreType.Open(directory);
            var a=store.Register(new RecordingComponent("a"){State="a1"});
            var first=store.Save();
            a.State="a2";
            var second=store.Save();
            a.RejectedState="a1";
            Assert.Throws<InvalidOperationException>(()=>store.Load(first));
            Assert.Equal([first,second],store.Checkpoints);
            Assert.Equal(second,store.CurrentCheckpoint);
            Assert.Empty(a.Loaded);
            store.Dispose();
            using var reopened=StoreType.Open(directory);
            Assert.Equal([first,second],reopened.Checkpoints);
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Load(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"app");
            using var store=StoreType.Open(directory);
            var a=store.Register(new RecordingComponent("a"){State="a1"});
            var first=store.Save();
            a.State="a2";
            var second=store.Save();
            {
                //番号を指定すると、その時点の状態を登録済みの子資源へ渡す
                store.Load(first);
                Assert.Equal(["a1"],a.Loaded);
                Assert.Equal(first,store.CurrentCheckpoint);
            }
            {
                //古い保存を読むと、それより新しい保存は記録から外れる(子資源の復元でデータが無効になるため)
                Assert.Equal([first],store.Checkpoints);
                Assert.Throws<ArgumentException>(()=>store.Load(second));
            }
            {
                //番号を省略すると最新(いま残っている最新)
                store.Load();
                Assert.Equal(["a1","a1"],a.Loaded);
                Assert.Equal(first,store.CurrentCheckpoint);
            }
            {
                //存在しない番号は ArgumentException
                Assert.Throws<ArgumentException>(()=>store.Load(Guid.NewGuid()));
            }
            {
                //外した結果は開き直しても保存されている: 最新の保存へ戻ると、外した新しい保存へは戻らない
                store.Dispose();
                using var reopened=StoreType.Open(directory);
                Assert.Equal([first],reopened.Checkpoints);
                Assert.Equal(first,reopened.CurrentCheckpoint);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact]public void Dispose(){
        var root=NewRoot();
        try{
            var store=StoreType.CreateTemporary(temporaryDirectory:root);
            store.Register(new RecordingComponent("a"));
            {
                //二重の破棄は何もしない
                store.Dispose();
                store.Dispose();
            }
            {
                //破棄後の保存は ObjectDisposedException
                Assert.Throws<ObjectDisposedException>(()=>store.Save());
            }
        }finally{Directory.Delete(root,true);}
    }
}
file sealed class RecordingComponent(string name):IStoreComponent{
    public string Name{get;}=name;
    public string State{get;set;}=string.Empty;
    public int SaveCount{get;private set;}
    public int FlushCount{get;private set;}
    // Save / Flush が呼ばれた時点の様子を記録するために、テストが渡す処理。
    public Action? OnSave{get;init;}
    public Action? OnFlush{get;init;}
    // 保存が確定した通知(SaveCommitted)の時点の様子を記録するために、テストが渡す処理。
    public Action? OnSaveCommitted{get;init;}
    public int SaveCommittedCount{get;private set;}
    // 前の保存へ戻れなくなる変更をしたことにする。
    public bool InvalidatesOlderSaves{get;set;}
    // この状態は読み込めないことにする(ValidateLoad が例外にする)。
    public string? RejectedState{get;set;}
    public List<string> Loaded{get;}=[];
    public bool Disposed{get;private set;}
    public string Save(){
        this.SaveCount++;
        this.OnSave?.Invoke();
        return this.State;
    }
    public void Flush(){
        this.FlushCount++;
        this.OnFlush?.Invoke();
    }
    public void Load(string state)=>this.Loaded.Add(state);
    public void SaveCommitted(){
        this.SaveCommittedCount++;
        this.OnSaveCommitted?.Invoke();
    }
    public void ValidateLoad(string state){
        if(state==this.RejectedState){
            throw new InvalidOperationException("The state cannot be loaded.");
        }
    }
    public void Dispose()=>this.Disposed=true;
}
