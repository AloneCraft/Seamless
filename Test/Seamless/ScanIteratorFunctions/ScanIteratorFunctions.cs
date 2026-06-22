using Seamless.Tsavorite.Collections;
using Seamless.Tsavorite.Collections.Stores;
namespace Test.Seamless.ScanIteratorFunctions;

public class ScanIteratorFunctions{
    [Fact,Trait("Category","Unit")]
    public void CountsAllAddedElements(){
        const int Count=300;
        using var s=Store.Create().Create<int,int>();
        using var h=s.Create();
        for(var a=0; a<Count; a++){
            h.Add(a,a);
        }
        Assert.Equal(Count,h.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void OnExceptionDoesNotThrow(){
        var iter=new Dictionary<int,int,MemoryPackSerializer<int>,MemoryPackSerializer<int>>.KeyIterator();
        iter.OnException(new Exception("test"),0);
    }
}
