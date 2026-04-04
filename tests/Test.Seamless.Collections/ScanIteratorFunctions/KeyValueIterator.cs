using Seamless.Collections;
using Seamless.ScanIteratorFunctions;

namespace Test.Seamless.ScanIteratorFunctions;

public class KeyValueIterator{
    private static void Common(){
        const int Count=300;
        using var s=Store.Create<int,int>();
        using var h=s.Create();
        for(var a=0;a<Count;a++){
            h.Add(a,a);
        }
        foreach(var a in h) {
        }
    }
    [Fact,Trait("Category",Unit)]public void Reader()=>Common();
    [Fact,Trait("Category",Unit)]public void OnStart()=>Common();
    [Fact,Trait("Category",Unit)]public void OnStop()=>Common();}
