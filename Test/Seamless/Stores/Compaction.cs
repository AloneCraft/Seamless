using System.Text.Json;
using CommonStore=global::Seamless.Execution.Store;
using IStoreComponent=global::Seamless.Execution.IStoreComponent;
namespace Test.Seamless.Stores;
/// <summary>データ本体の詰め直し(Compact)と、詰め直したあとの保存・読み込みの仕様。</summary>
public class Compaction{
    private static string NewRoot(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-tsavorite-compaction-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
    // ログを小さくして、少ない件数でセグメントのファイルが何本もできるようにする。
    private static readonly TsavoriteTuning Small=new(){PageSize=1L<<12,SegmentSize=1L<<16,LogMemorySize=1L<<16,IndexSize=1L<<14};
    private const int Count=20_000;
    private static string LogDirectory(string storeDirectory,string name)=>Path.Combine(storeDirectory,"data",name);
    private static int LogFileCount(string storeDirectory,string name)=>Directory.GetFiles(LogDirectory(storeDirectory,name),"hlog.*").Length;
    private static void AddRange(HashSetStore<long> data,long from,long to){
        using var set=data.Create();
        for(var i=from;i<to;i++){
            set.Add(i);
        }
    }
    private static void RemoveRange(HashSetStore<long> data,long from,long to){
        using var set=data.Create();
        for(var i=from;i<to;i++){
            set.Remove(i);
        }
    }
    private static void AssertContents(HashSetStore<long> data,long liveFrom,long liveTo,long total){
        using var set=data.Create();
        Assert.Equal(liveTo-liveFrom,set.GetLongCount());
        var missing=0;
        var resurrected=0;
        for(var i=0L;i<total;i++){
            var expected=i>=liveFrom&&i<liveTo;
            if(set.Contains(i)!=expected){
                if(expected){
                    missing++;
                }else{
                    resurrected++;
                }
            }
        }
        Assert.Equal(0,missing);
        Assert.Equal(0,resurrected);
    }
    // 保存番号を、マニフェスト(store.json)に記録された子資源の状態から取り出す。
    private static Guid[] SavedTokens(string storeDirectory,string name){
        using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(storeDirectory,"store.json")));
        var tokens=new List<Guid>();
        foreach(var checkpoint in manifest.RootElement.GetProperty("Checkpoints").EnumerateArray()){
            using var state=JsonDocument.Parse(checkpoint.GetProperty("Components").GetProperty(name).GetString()!);
            tokens.Add(state.RootElement.GetProperty("Token").GetGuid());
        }
        return [..tokens];
    }
    [Fact,Trait("Category","Unit")]public void Compact_Memory(){
        {
            //半分まで入れて 6 割を削除し、詰め直すと、中身と件数は変わらず、空いた分だけ追加できる。予算の予約も減る
            var tuning=new TsavoriteTuning{PageSize=1L<<12,SegmentSize=1L<<16,LogMemorySize=1L<<16,IndexSize=1L<<14,MutableFraction=0.9};
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids",tuning);
            long inserted=0;
            using(var set=data.Create()){
                while(data.Address.TailAddress<0x8000){
                    set.Add(inserted);
                    inserted++;
                }
            }
            var removed=inserted*6/10;
            RemoveRange(data,0,removed);
            var reservedBefore=store.Budget.MemoryBytes;
            var result=data.Compact();
            Assert.True(result.BeginAddressAfter>result.BeginAddressBefore);
            Assert.True(result.ReclaimedBytes>0);
            Assert.True(store.Budget.MemoryBytes<reservedBefore);
            AssertContents(data,removed,inserted,inserted);
            using var set2=data.Create();
            var added=0;
            try{
                for(var i=inserted;;i++){
                    set2.Add(i);
                    added++;
                }
            }catch(OutOfMemoryException){
            }
            //詰め直さなければ、残りの空きで入るのは削除した件数より少ない
            Assert.True(added>removed,$"added={added} removed={removed}");
        }
        {
            //満杯のログは、写しを書くページが無い。写しを書くために先頭のページが追い出されて生きているレコードを失うので、何も移さずに例外にする
            using var store=CommonStore.CreateMemory();
            using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
            long inserted=0;
            using(var set=data.Create()){
                try{
                    for(long i=0;;i++){
                        set.Add(i);
                        inserted++;
                    }
                }catch(OutOfMemoryException){
                }
            }
            Assert.True(inserted>1000);
            RemoveRange(data,0,inserted/2);
            Assert.Throws<global::Seamless.Execution.ResourceLimitException>(()=>data.Compact());
            AssertContents(data,inserted/2,inserted,inserted);
        }
    }
    [Fact,Trait("Category","Unit")]public void Compact_Dictionary(){
        //何度も上書きして無駄を作った辞書でも、詰め直したあとは最新の値だけが見え、件数が保たれる
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavorite<int,string>("names",Small);
        using(var dictionary=data.Create()){
            for(var round=0;round<8;round++){
                for(var i=0;i<400;i++){
                    dictionary[i]="v"+round+"-"+i;
                }
            }
            for(var i=0;i<400;i+=2){
                dictionary.Remove(i);
            }
        }
        //無駄の量: 半分を削除した直後は無駄が多く、詰め直すと減る。統計には件数とログの大きさが入る
        var usageBefore=data.MeasureLogUsage();
        Assert.Equal(200,usageBefore.LiveRecords);
        Assert.True(usageBefore.WasteFraction>0.3,$"waste={usageBefore.WasteFraction}");
        var statistics=data.GetStatistics();
        Assert.Equal(200,statistics.Count);
        Assert.Equal(statistics.TailAddress-statistics.BeginAddress,statistics.LogBytes);
        var result=data.Compact();
        Assert.True(result.ReclaimedBytes>0);
        var usageAfter=data.MeasureLogUsage();
        Assert.Equal(200,usageAfter.LiveRecords);
        Assert.True(usageAfter.LogBytes<usageBefore.LogBytes);
        Assert.True(usageAfter.WasteFraction<0.1,$"waste={usageAfter.WasteFraction}");
        using var check=data.Create();
        Assert.Equal(200,check.GetLongCount());
        for(var i=0;i<400;i++){
            if(i%2==0){
                Assert.False(check.ContainsKey(i));
            }else{
                Assert.Equal("v7-"+i,check[i]);
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void Compact_Rejected(){
        using var store=CommonStore.CreateMemory();
        using var data=store.CreateTsavoriteHashSet<long>("ids");
        {
            //操作用オブジェクトがあるあいだは詰め直せない
            using var set=data.Create();
            set.Add(1);
            Assert.Throws<InvalidOperationException>(()=>data.Compact());
        }
        {
            //並列区間があるあいだも詰め直せない
            using var region=data.BeginParallel();
            Assert.Throws<InvalidOperationException>(()=>data.Compact());
        }
        data.Compact();
        data.Dispose();
        Assert.Throws<ObjectDisposedException>(()=>data.Compact());
    }
    [Fact,Trait("Category","Unit")]public void Compact_Persistent_SaveAndReopen(){
        var root=NewRoot();
        try{
            var directory=Path.Combine(root,"store");
            long diskBefore;
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                var firstToken=store.CurrentCheckpoint!.Value;
                RemoveRange(data,0,Count*9/10);
                var filesBefore=LogFileCount(directory,"ids");
                diskBefore=store.Budget.TemporaryStorageBytes;
                var result=data.Compact();
                Assert.True(result.ReclaimedBytes>0);
                //保存が確定するまで、ファイルも前の保存も残っている(強制終了しても前の保存へ戻れる)
                Assert.True(LogFileCount(directory,"ids")>=filesBefore);
                Assert.Single(store.Checkpoints);
                store.Save();
                //前の保存は記録から外れ、ファイルが消え、ディスクの予約が減る
                Assert.Single(store.Checkpoints);
                Assert.DoesNotContain(firstToken,store.Checkpoints);
                Assert.True(LogFileCount(directory,"ids")<filesBefore,$"files={LogFileCount(directory,"ids")} before={filesBefore}");
                Assert.True(store.Budget.TemporaryStorageBytes<diskBefore);
                Assert.Throws<ArgumentException>(()=>store.Load(firstToken));
                AssertContents(data,Count*9/10,Count,Count);
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,Count*9/10,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Compact_Persistent_WithoutSave(){
        var root=NewRoot();
        try{
            //詰め直したあと保存せずに閉じると、ファイルは消えておらず、開き直すと前の保存へ戻る
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                RemoveRange(data,0,Count*9/10);
                data.Compact();
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,0,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Compact_Persistent_CrashBetweenCheckpointAndManifest(){
        var root=NewRoot();
        try{
            //データ本体のチェックポイントを取ったあと、マニフェストを書く前に強制終了した状態(EXP-0005)。
            //Tsavorite はチェックポイントのあとでログの古いセグメントを消すが、マニフェストが指す前の保存のファイルを消してはいけない
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                RemoveRange(data,0,Count*9/10);
                data.Compact();
                var component=(IStoreComponent)data;
                component.Save();
                //Tsavorite の後始末が走る時間を与える
                Thread.Sleep(500);
                Assert.True(File.Exists(Path.Combine(LogDirectory(directory,"ids"),"hlog.0")));
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,0,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Compact_Persistent_CrashAfterManifestBeforeTruncation(){
        var root=NewRoot();
        try{
            //マニフェストを書いたあと、古いファイルを消す前に強制終了した状態。保存の確定の通知(古いファイルを消す前)で、その時点のフォルダを複製して開き直す
            var directory=Path.Combine(root,"store");
            var snapshot=Path.Combine(root,"snapshot");
            using(var store=CommonStore.Open(directory)){
                //登録順に通知されるので、データ本体より先に登録して、データ本体が古いファイルを消す前に複製する
                store.Register(new SnapshotOnCommit(()=>{
                    if(Directory.Exists(snapshot)){
                        Directory.Delete(snapshot,true);
                    }
                    CopyDirectory(directory,snapshot);
                }));
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                RemoveRange(data,0,Count*9/10);
                data.Compact();
                store.Save();
                //通常の保存は、確定の通知でファイルを消す
                Assert.True(File.Exists(Path.Combine(snapshot,"data","ids","hlog.0")));
                Assert.False(File.Exists(Path.Combine(LogDirectory(directory,"ids"),"hlog.0")));
            }
            using(var store=CommonStore.Open(snapshot)){
                Assert.Single(store.Checkpoints);
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,Count*9/10,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    // 開いたままのファイルも複製する(デバイスは読み書きの共有で開いている)。排他ロックのファイルは複製しない。
    private static void CopyDirectory(string source,string destination){
        Directory.CreateDirectory(destination);
        foreach(var file in Directory.GetFiles(source)){
            if(Path.GetFileName(file)=="store.lock"){
                continue;
            }
            using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var output=new FileStream(Path.Combine(destination,Path.GetFileName(file)),FileMode.Create,FileAccess.Write);
            input.CopyTo(output);
        }
        foreach(var directory in Directory.GetDirectories(source)){
            CopyDirectory(directory,Path.Combine(destination,Path.GetFileName(directory)));
        }
    }
    [Fact,Trait("Category","Unit")]public void Save_KeepsOlderSavesWithoutCompaction(){
        var root=NewRoot();
        try{
            //詰め直していなければ、保存を重ねても前の保存へ戻れる
            using var store=CommonStore.Open(Path.Combine(root,"store"));
            using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
            AddRange(data,0,1000);
            var first=store.Save();
            AddRange(data,1000,2000);
            var second=store.Save();
            Assert.Equal(new[]{first,second},store.Checkpoints);
            store.Load(first);
            AssertContents(data,0,1000,2000);
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Save_AfterClear_DropsOlderSaves(){
        var root=NewRoot();
        try{
            //全削除はログを作り直す(前の保存のファイルも消える)ので、次の保存で前の保存は記録から外れる
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,1000);
                store.Save();
                using(var set=data.Create()){
                    set.Clear();
                    set.Add(7);
                }
                store.Save();
                Assert.Single(store.Checkpoints);
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                using var set=data.Create();
                Assert.Equal(1,set.GetLongCount());
                Assert.True(set.Contains(7));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Clear_BeforeSave_KeepsPreviousSave(){
        var root=NewRoot();
        try{
            //全削除のあと保存する前に十分書き込んでも、前の保存のログを上書きしない(BUG-0023)。保存しないで開き直すと前の保存へ戻る
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                using(var set=data.Create()){
                    set.Clear();
                    for(long i=100_000;i<105_000;i++){
                        set.Add(i);
                    }
                }
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,0,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Clear_Save_RetiresOldGeneration(){
        var root=NewRoot();
        try{
            //全削除のあとの保存が確定すると、前の保存は記録から外れ、古いログのファイルが消える。開き直すと新しい状態
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                Assert.True(LogFileCount(directory,"ids")>0);
                using(var set=data.Create()){
                    set.Clear();
                    for(long i=100_000;i<105_000;i++){
                        set.Add(i);
                    }
                }
                //保存が確定するまで、古いログは残っている
                Assert.True(LogFileCount(directory,"ids")>0);
                store.Save();
                Assert.Single(store.Checkpoints);
                Assert.Equal(0,LogFileCount(directory,"ids"));
                Assert.NotEmpty(Directory.GetFiles(LogDirectory(directory,"ids"),"gen1-hlog.*"));
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                using var set=data.Create();
                Assert.Equal(5000,set.GetLongCount());
                Assert.True(set.Contains(104_999));
                Assert.False(set.Contains(5));
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Clear_ThenLoadPreviousSave(){
        var root=NewRoot();
        try{
            //全削除のあと保存せずに前の保存を読み込むことはできない。何も変えずに例外にし、ストアを開き直せば前の保存へ戻る
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                var first=store.Save();
                using(var set=data.Create()){
                    set.Clear();
                    for(long i=100_000;i<105_000;i++){
                        set.Add(i);
                    }
                }
                Assert.Throws<InvalidOperationException>(()=>store.Load(first));
                Assert.Equal(new[]{first},store.Checkpoints);
                //新しい世代のログと、そこへ書いた中身はそのまま
                AssertContents(data,100_000,105_000,105_000);
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AssertContents(data,0,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Load_InReopenedSession(){
        var root=NewRoot();
        try{
            //ストアを開き直した同じセッションで、保存番号を指定した Load ができる(BUG-0024 として疑った。全削除が前の保存を壊していた実験の汚染で、再現しなかった)
            var directory=Path.Combine(root,"store");
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                AddRange(data,0,Count);
                store.Save();
                using(var set=data.Create()){
                    set.Clear();
                    set.Add(5);
                }
            }
            using(var store=CommonStore.Open(directory)){
                using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
                var first=store.Save();
                AddRange(data,100_000,105_000);
                store.Load(first);
                AssertContents(data,0,Count,Count);
            }
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Load_ValidatesBeforeDroppingNewerSaves(){
        var root=NewRoot();
        try{
            //戻り先の保存が読み込めないとき(BUG-0019)、新しい保存を記録から外してはいけない
            var directory=Path.Combine(root,"store");
            using var store=CommonStore.Open(directory);
            using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
            AddRange(data,0,1000);
            var first=store.Save();
            AddRange(data,1000,2000);
            var second=store.Save();
            var firstDirectory=Path.Combine(LogDirectory(directory,"ids"),"checkpoints","cpr-checkpoints",SavedTokens(directory,"ids")[0].ToString());
            Directory.Delete(firstDirectory,true);
            Assert.Throws<InvalidOperationException>(()=>store.Load(first));
            Assert.Equal(new[]{first,second},store.Checkpoints);
            Assert.Equal(second,store.CurrentCheckpoint);
        }finally{Directory.Delete(root,true);}
    }
    [Fact,Trait("Category","Unit")]public void Compact_Temporary(){
        //一時ストアでも詰め直せる(保存すればファイルが消える)
        using var store=CommonStore.CreateTemporary();
        using var data=store.CreateTsavoriteHashSet<long>("ids",Small);
        AddRange(data,0,Count);
        RemoveRange(data,0,Count*9/10);
        data.Compact();
        store.Save();
        AssertContents(data,Count*9/10,Count,Count);
    }
}
// 保存の確定の通知で、テストが渡した処理を呼ぶだけの子資源。
file sealed class SnapshotOnCommit(Action onCommit):IStoreComponent{
    public string Name=>"snapshot-spy";
    public string Save()=>string.Empty;
    public void Flush(){}
    public void Load(string state){}
    public void SaveCommitted()=>onCommit();
    public void Dispose(){}
}
