using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.ScanIteratorFunctions;

public class ScanIteratorFunctions{
    private static void Common(){
        const int Count=300;
        using var s=Factory.Create().Create<int,int>();
        using var h=s.Create();
        for(var a=0;a<Count;a++){
            h.Add(a,a);
        }
        Assert.Equal(Count, h.Count);
    }
    [Fact,Trait("Category","Unit")]public void Reader()=>Common();
    [Fact,Trait("Category","Unit")]public void OnStart()=>Common();
    [Fact,Trait("Category","Unit")]public void OnStop()=>Common();
    [Fact,Trait("Category","Unit")]public void OnException(){
        var iter = new global::Seamless.ScanIteratorFunctions.KeyIterator<int, ManagedSerializer<int>>();
        iter.OnException(new Exception("test"),0);
    }
}
