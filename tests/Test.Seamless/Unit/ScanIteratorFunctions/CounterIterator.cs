using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Unit.ScanIteratorFunctions;

public class CounterIterator{
    private static void Common(){
        const int Count=300;
        using var s=Factory.Create().Create<int>();
        using var h=s.Create();
        for(var a=0; a<Count; a++){
            h.Add(a);
        }
        Assert.Equal(Count,h.Count);
    }
    [Fact,Trait("Category","Unit")] public void Reader()=>Common();
    [Fact,Trait("Category","Unit")] public void OnStart()=>Common();
    [Fact,Trait("Category","Unit")] public void OnStop()=>Common();
    [Fact,Trait("Category","Unit")]
    public void OnException(){
#if TSAVORITE2_0BETA3
        var iter=new ManagedHashSet<int>.CounterIterator();
#elif TSAVORITE1_1_1
        var iter=new ManagedHashSet<int>.CounterIterator();
#endif
        iter.OnException(new Exception("test"),0);
    }
}
