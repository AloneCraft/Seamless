using Seamless.Stores;
namespace Test.Seamless.Unit.ScanIteratorFunctions;

public class KeyIterator{
    private static void Common(){
        const int Count=300;
        using var s=Factory.Create().Create<int,int>();
        using var h=s.Create();
        for(var a=0; a<Count; a++){
            h.Add(a,a);
        }
        foreach(var a in h.Keys){
        }
    }
    [Fact,Trait("Category","Unit")] public void Reader()=>Common();
    [Fact,Trait("Category","Unit")] public void OnStart()=>Common();
    [Fact,Trait("Category","Unit")] public void OnStop()=>Common();
}
