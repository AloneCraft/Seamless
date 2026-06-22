using Seamless.Tsavorite.Collections.Stores;
namespace Test.Seamless.ScanIteratorFunctions;

public class KeyIterator{
    [Fact,Trait("Category","Unit")]
    public void EnumeratesAllKeys(){
        const int Count=300;
        using var s=Store.Create().Create<int,int>();
        using var h=s.Create();
        for(var a=0; a<Count; a++){
            h.Add(a,a);
        }
        var seen=0;
        foreach(var _ in h.Keys){
            seen++;
        }
        Assert.Equal(Count,seen);
    }
}
