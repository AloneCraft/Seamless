namespace Benchmark.Infrastructure.Tests;

public sealed class ContentDigest{
    [Fact]public void AssertMatches(){
        using var first=new Infrastructure.ContentDigest();using var same=new Infrastructure.ContentDigest();using var wrong=new Infrastructure.ContentDigest();
        foreach(var value in new[]{1,4,4,7}){first.Add(value);}
        foreach(var value in new[]{7,4,1,4}){same.Add(value);}
        foreach(var value in new[]{2,3,5,6}){wrong.Add(value);}
        first.AssertMatches(same);
        Assert.Throws<InvalidDataException>(()=>first.AssertMatches(same,true));
        Assert.Throws<InvalidDataException>(()=>first.AssertMatches(wrong));
        using var empty=new Infrastructure.ContentDigest();using var other=new Infrastructure.ContentDigest();empty.AssertMatches(other,true);
        for(var index=0;index<10000;index++){empty.Add(index);other.Add(index);}
        empty.AssertMatches(other,true);other.Add(0);
        Assert.Throws<InvalidDataException>(()=>empty.AssertMatches(other));
    }
}
