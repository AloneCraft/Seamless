using Seamless.Collections.Persisted;
using CommonStore=Seamless.Execution.Store;
namespace Test.Persisted.Collections.Work;
public class SortedDictionary{
    [Fact]public void LastAddWinsAndSearch(){
        using var owner=CommonStore.CreateTemporary();
        using var dictionary=owner.CreatePersistedSortedDictionary<string,int>("totals",StringComparer.OrdinalIgnoreCase,sortMemoryBytes:64);
        dictionary.Add("b",1);dictionary.Add("a",2);dictionary.Add("A",3);dictionary.Add("c",4);
        Assert.Throws<InvalidOperationException>(()=>dictionary.ContainsKey("a"));
        Assert.Throws<InvalidOperationException>(()=>dictionary.TryGetValue("a",out _));
        Assert.Throws<InvalidOperationException>(()=>dictionary["a"]);
        dictionary.CompleteAdding();
        Assert.Equal(3,dictionary.Count);Assert.Equal(4,dictionary.AddedCount);
        Assert.Equal(3,dictionary["a"]);
        Assert.True(dictionary.TryGetValue("b",out var value));Assert.Equal(1,value);
        Assert.False(dictionary.TryGetValue("missing",out _));Assert.Throws<KeyNotFoundException>(()=>dictionary["missing"]);
        Assert.Equal(new[]{"A","b","c"},System.Linq.Enumerable.ToArray(dictionary.Keys));
        Assert.Equal(new[]{3,1},System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(dictionary.Range("a","c"),pair=>pair.Value)));
        dictionary.Add("a",9);Assert.False(dictionary.IsReady);dictionary.CompleteAdding();Assert.Equal(9,dictionary["A"]);
    }
    [Fact]public void SaveLoadPreservesDictionaryAndDuplicateRows(){
        using var owner=CommonStore.CreateTemporary();
        using var dictionary=owner.CreatePersistedSortedDictionary<int,string>("values");
        using var rows=owner.CreatePersistedSortedList<int>("rows");
        dictionary.Add(1,"first");dictionary.Add(1,"last");rows.Add(1);rows.Add(1);
        var building=owner.Save();
        dictionary.CompleteAdding();rows.CompleteAdding();var ready=owner.Save();
        Assert.Single(dictionary);Assert.Equal("last",dictionary[1]);Assert.Equal(2,rows.Count);
        owner.Load(building);Assert.False(dictionary.IsReady);Assert.False(rows.IsReady);
        Assert.Throws<InvalidOperationException>(()=>dictionary[1]);
        dictionary.Add(1,"branch");rows.Add(2);dictionary.CompleteAdding();rows.CompleteAdding();owner.Save();
        owner.Load(ready);Assert.True(dictionary.IsReady);Assert.True(rows.IsReady);
        Assert.Equal("last",dictionary[1]);Assert.Equal(new[]{1,1},System.Linq.Enumerable.ToArray(rows));
    }
    [Fact]public void HashAndSortAgree(){
        using var owner=CommonStore.CreateTemporary();
        using var sorted=owner.CreatePersistedSortedDictionary<string,int>("sorted",StringComparer.OrdinalIgnoreCase);
        using var hashed=owner.CreatePersistedHashDictionary<string,int>("hashed",StringComparer.OrdinalIgnoreCase);
        foreach(var pair in new[]{new KeyValuePair<string,int>("Alpha",1),new("beta",2),new("ALPHA",3)}){
            sorted.Add(pair.Key,pair.Value);hashed.Add(pair.Key,pair.Value);
        }
        sorted.CompleteAdding();Assert.Equal(hashed.Count,sorted.Count);
        foreach(var pair in sorted){Assert.Equal(pair.Value,hashed[pair.Key]);}
    }
}
