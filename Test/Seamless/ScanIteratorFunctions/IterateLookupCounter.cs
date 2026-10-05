namespace Test.Seamless.ScanIteratorFunctions;

public class IterateLookupCounter {
    [Fact, Trait("Category","Unit")]
    public void CountsAllAddedElements() {
        const int Count = 300;
        using var s = Store.Create().Create<int>();
        using var h = s.Create();
        for(var a = 0;a<Count;a++) {
            h.Add(a);
        }
        Assert.Equal(Count,h.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void OnExceptionDoesNotThrow() {
        var iter = new global::Seamless.Collections.Tsavorite.IterateLookupCounter();
        iter.OnException(new Exception("test"),0);
    }
}
