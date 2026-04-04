using Seamless.Collections;
using Seamless.ScanIteratorFunctions;

namespace Test.Seamless.ScanIteratorFunctions;

public class ScanIteratorFunctions{
    private static void Common(){
        const int Count=300;
        using var s=Store.Create<int,int>();
        using var h=s.Create();
        for(var a=0;a<Count;a++){
            h.Add(a,a);
        }
        Assert.Equal(Count, h.Count);
    }
    [Fact,Trait("Category",Unit)]public void Reader()=>Common();
    [Fact,Trait("Category",Unit)]public void OnStart()=>Common();
    [Fact,Trait("Category",Unit)]public void OnStop()=>Common();
    [Fact,Trait("Category",Unit)]public void OnException(){
        var iter = new KeyIterator<int>();
        iter.OnException(new Exception("test"),0);
    }
}
