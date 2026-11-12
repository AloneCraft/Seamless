namespace Test.Seamless.ScanIteratorFunctions;

public class ValueIterator {
    [Fact, Trait("Category","Unit")]
    public void EnumeratesAllValues() {
        const int Count = 300;
        using var s = Store.Create().Create<int,int>();
        using var h = s.Create();
        for(var a = 0;a<Count;a++) {
            h.Add(a,a);
        }
        var seen = 0;
        foreach(var _ in h.Values) {
            seen++;
        }
        Assert.Equal(Count,seen);
    }
}
