namespace Test.Seamless.Persisted;
/// <summary>PersistedList の末尾削除(Remove)が、キー索引を持つ派生(Set)でも検索結果と整合することの仕様(BUG-0003)。</summary>
public class PersistedList{
    [Fact,Trait("Category","Unit")]public void Constructor(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-invalid-persisted-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            foreach(var mode in new[]{StorageBufferingMode.Buffered,StorageBufferingMode.Unbuffered}){
                if(mode==StorageBufferingMode.Unbuffered&&!OperatingSystem.IsWindows()){
                    continue;
                }
                var tuning=new StorageTuning(FileAccess.ReadWrite,StorageAccessPattern.Random,mode);
                var path=Path.Combine(directory,mode+".dat");
                //不正な固定長ファイルを拒否した後も、同じファイルを排他的に開ける。
                File.WriteAllBytes(path,new byte[1]);
                Assert.Throws<InvalidDataException>(()=>new global::Seamless.Collections.BlittableList<long>(path,tuning:tuning));
                using(var exclusive=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                    Assert.Equal(1,exclusive.Length);
                }
                //可変長の索引検証で失敗しても、データと索引の両方を解放する。
                File.WriteAllBytes(path+".index",new byte[1]);
                Assert.Throws<InvalidDataException>(()=>new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning));
                using(var data=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                    using var index=File.Open(path+".index",FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                    Assert.Equal(1,data.Length);Assert.Equal(1,index.Length);
                }
                //AppendOnly の不完全な長さプレフィックスも、ハンドルを残さない。
                Assert.Throws<InvalidDataException>(()=>new global::Seamless.Collections.Persisted.AppendOnlyList<string>(path,tuning:tuning));
                using var append=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                Assert.Equal(1,append.Length);
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }

    [Fact,Trait("Category","Unit")]public void RollbackTo(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-rollback-persisted-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            foreach(var mode in new[]{StorageBufferingMode.Buffered,StorageBufferingMode.Unbuffered}){
                if(mode==StorageBufferingMode.Unbuffered&&!OperatingSystem.IsWindows()){
                    continue;
                }
                var tuning=new StorageTuning(FileAccess.ReadWrite,StorageAccessPattern.Random,mode);
                {
                    //強制終了で、保存(Commit)後に追記した未確定部分の索引が歯抜けになっても開ける。保存済みの状態へ戻せて、その後の追記も正しい
                    var path=Path.Combine(directory,mode+".gap.dat");
                    PersistedCommitState state;
                    using(var list=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning)){
                        for(var i=0;i<10;i++){
                            list.Add("committed-"+i);
                        }
                        state=list.Commit();
                        for(var i=10;i<20;i++){
                            list.Add("uncommitted-"+i);
                        }
                    }
                    TearIndexEntry(path+".index",15);
                    using(var reopened=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning)){
                        reopened.RollbackTo(state);
                        Assert.Equal(10,reopened.Count);
                        for(var i=0;i<10;i++){
                            Assert.Equal("committed-"+i,reopened[i]);
                        }
                        reopened.Add("after");
                        Assert.Equal(11,reopened.Count);
                        Assert.Equal("after",reopened[10]);
                    }
                }
                {
                    //保存後に追記した未確定の記録の長さが壊れていても(強制終了で本体が書き戻される前後が食い違う)、開けて、保存済みの状態へ戻せる
                    var path=Path.Combine(directory,mode+".torn-record.dat");
                    PersistedCommitState state;
                    using(var list=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning)){
                        for(var i=0;i<10;i++){
                            list.Add("committed-"+i);
                        }
                        state=list.Commit();
                        for(var i=10;i<20;i++){
                            list.Add("uncommitted-"+i);
                        }
                    }
                    TearRecordLength(path,path+".index",13);
                    using var reopened=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning);
                    reopened.RollbackTo(state);
                    Assert.Equal(10,reopened.Count);
                    Assert.Equal("committed-9",reopened[9]);
                }
                {
                    //歯抜けが保存済みの部分にあるときは、保存済みの状態へ戻せないので RollbackTo が拒否する
                    var path=Path.Combine(directory,mode+".committed-gap.dat");
                    PersistedCommitState state;
                    using(var list=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning)){
                        for(var i=0;i<10;i++){
                            list.Add("committed-"+i);
                        }
                        state=list.Commit();
                    }
                    TearIndexEntry(path+".index",5);
                    using var reopened=new global::Seamless.Collections.SerializedList<string>(path,tuning:tuning);
                    Assert.Throws<InvalidDataException>(()=>reopened.RollbackTo(state));
                }
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }
    //entry 番目の記録の長さを、ファイルの範囲を超える値に書き換える。
    private static void TearRecordLength(string dataPath,string indexPath,int entry){
        long recordOffset;
        using(var index=File.Open(indexPath,FileMode.Open,FileAccess.Read,FileShare.None)){
            index.Position=(long)entry*sizeof(long);
            var bytes=new byte[sizeof(long)];
            index.ReadExactly(bytes);
            recordOffset=BitConverter.ToInt64(bytes);
        }
        using var data=File.Open(dataPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        data.Position=recordOffset;
        data.Write(BitConverter.GetBytes(int.MaxValue));
    }
    //索引ファイルの entry 番目を未使用(-1)にして、前後に有効なエントリがある歯抜けにする。
    private static void TearIndexEntry(string indexPath,int entry){
        using var stream=File.Open(indexPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        stream.Position=(long)entry*sizeof(long);
        stream.Write(BitConverter.GetBytes(-1L));
    }

    [Fact,Trait("Category","Unit")]public void Remove(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-persistedlist-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"items.dat");
        try{
            {
                //末尾を削除した要素は、同じ間に Contains で見つからない。残りは見つかる
                using var set=new global::Seamless.Collections.Persisted.BlittableSet<int>(path);
                set.Add(1);
                set.Add(2);
                set.Add(3);
                Assert.True(set.Remove());
                Assert.False(set.Contains(3));
                Assert.True(set.Contains(2));
                //削除したあとに同じ値を入れ直すと見つかる
                set.Add(3);
                Assert.True(set.Contains(3));
                Assert.True(set.Remove());
            }
            {
                //開き直しても、削除した要素は見つからない
                using var set=new global::Seamless.Collections.Persisted.BlittableSet<int>(path);
                Assert.Equal(2,set.Count);
                Assert.False(set.Contains(3));
                Assert.True(set.Contains(1));
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }
}
