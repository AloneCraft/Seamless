namespace Test.Seamless.Persisted;
/// <summary>PersistedList の末尾削除(Remove)が、キー索引を持つ派生(Set)でも検索結果と整合することの仕様(BUG-0003)。</summary>
public class PersistedList{
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
