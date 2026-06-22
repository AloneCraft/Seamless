using Seamless.Collections.Tsavorite.Stores;
namespace Test.Seamless.ScanIteratorFunctions;

public class CounterIterator{
    [Fact,Trait("Category","Unit")]
    public void CountsAllAddedElements(){
        const int Count=300;
        using var s=Store.Create().Create<int>();
        using var h=s.Create();
        for(var a=0; a<Count; a++){
            h.Add(a);
        }
        Assert.Equal(Count,h.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void OnExceptionDoesNotThrow(){
#if TSAVORITE2_0BETA3
        var iter=new global::Seamless.Collections.Tsavorite.HashSet<int>.CounterIterator();
#elif TSAVORITE1_1_1
        var iter=new global::Seamless.Collections.Tsavorite.HashSet<int>.ScanIteratorFunctions_Counter();
#endif
        iter.OnException(new Exception("test"),0);
    }
}
