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
