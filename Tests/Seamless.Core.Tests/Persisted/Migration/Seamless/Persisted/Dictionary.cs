using PersistedStore=global::Seamless.Collections.Persisted.Store;
namespace Test.Seamless.Persisted;
/// <summary>Persisted Dictionary の列挙が、全件をメモリに載せずに「キーごとの最新の値」を 1 回ずつ返すことの仕様。</summary>
public class Dictionary{
    [Fact,Trait("Category","Unit")]public void Constructor(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-persisted-dictionary-tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            {
                //キーと値が固定長: 従来の BlittableDictionary で書いたファイルを、そのまま開いて読める
                var path=Path.Combine(directory,"blittable.dat");
                using(var old=new global::Seamless.Collections.Persisted.BlittableDictionary<int,long>(path)){
                    old.Add(1,10);
                    old.Add(2,20);
                }
                using var dictionary=new global::Seamless.Collections.Persisted.Dictionary<int,long>(path);
                Assert.True(dictionary.TryGetValue(2,out var value));
                Assert.Equal(20,value);
                Assert.Equal(2,dictionary.Count);
            }
            {
                //片方が参照を含む: 従来どおり両方 MemoryPack の形式で読める
                var path=Path.Combine(directory,"mixed.dat");
                using(var old=new global::Seamless.Collections.Persisted.SerializedDictionary<int,string,global::Seamless.Collections.Serializers.MemoryPack<int>,global::Seamless.Collections.Serializers.MemoryPack<string>>(path)){
                    old.Add(1,"one");
                }
                using var dictionary=new global::Seamless.Collections.Persisted.Dictionary<int,string>(path);
                Assert.Equal("one",dictionary[1]);
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }
    [Fact,Trait("Category","Unit")]public void GetEnumerator(){
        {
            //同じキーを後から追加すると後勝ちで、列挙・Keys・Values には最新の値だけが 1 回ずつ現れる
            using var dictionary=PersistedStore.CreateTempDictionary<string,int>();
            dictionary.Add("a",1);
            dictionary.Add("b",2);
            dictionary.Add("a",3);
            dictionary.Add("c",4);
            dictionary.Add("b",5);
            var pairs=dictionary.OrderBy(static pair=>pair.Key,StringComparer.Ordinal).Select(static pair=>$"{pair.Key}={pair.Value}").ToArray();
            Assert.Equal(["a=3","b=5","c=4"],pairs);
            Assert.Equal(["a","b","c"],dictionary.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.Equal([3,4,5],dictionary.Values.Order().ToArray());
            Assert.Equal(3,dictionary.Count);
        }
        {
            //固定長のキーと値でも同じ
            using var dictionary=PersistedStore.CreateTempDictionary<int,long>();
            for(var round=0;round<3;round++){
                for(var key=0;key<100;key++){
                    dictionary.Add(key,round*1000L+key);
                }
            }
            var pairs=dictionary.ToArray();
            Assert.Equal(100,pairs.Length);
            Assert.All(pairs,static pair=>Assert.Equal(2000L+pair.Key,pair.Value));
        }
        {
            //空の辞書
            using var dictionary=PersistedStore.CreateTempDictionary<int,int>();
            Assert.Empty(dictionary);
        }
    }
}
