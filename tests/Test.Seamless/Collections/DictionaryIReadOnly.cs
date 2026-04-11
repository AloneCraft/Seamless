using Seamless.Store;
namespace Test.Seamless.Collections;

public class DictionaryIReadOnly {
    [Fact, Trait("Category","Unit")]
    public void Keys_IReadOnlyDictionary() {
        using var store = Factory.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var keys = readOnly.Keys;
        Assert.Equal(2,keys.Count());
        Assert.Contains(1,keys);
        Assert.Contains(2,keys);
    }
    [Fact, Trait("Category","Unit")]
    public void Values_IReadOnlyDictionary() {
        using var store = Factory.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        IReadOnlyDictionary<int,string> readOnly = dict;
        var values = readOnly.Values;
        Assert.Equal(2,values.Count());
        Assert.Contains("a",values);
        Assert.Contains("b",values);
    }
}
