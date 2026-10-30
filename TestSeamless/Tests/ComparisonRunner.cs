namespace TestSeamless.Tests;

public sealed class ComparisonRunner{
    [Fact]public void CompareResults(){
        var root=TestTempDirectory.NewPath("seamless-route-order-");
        try{
            var test=new global::TestSeamless.ComparisonCase("ordered","fixture","SELECT Id,Name FROM dbo.Rows ORDER BY Id",null,Ordered:true,OrderKeyOrdinals:[0]);
            Assert.True(global::TestSeamless.ComparisonRunner.CompareResults(test,[[1,"a"],[1,"b"],[2,"c"]],[[1,"b"],[1,"a"],[2,"c"]],root).Equal);
            Assert.Throws<InvalidDataException>(()=>global::TestSeamless.ComparisonRunner.CompareResults(test,[[1,"a"],[2,"c"]],[[2,"c"],[1,"a"]],root));
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }
}
