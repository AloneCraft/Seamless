namespace TestSeamless.Tests;
public sealed class ResultBag{
    [Fact]public void Compare(){var root=TestTempDirectory.NewPath("seamless-bag-");try{
        Assert.True(global::TestSeamless.ResultBag.Compare([[1,"x"],[1,"x"],[null,"n"]],[[null,"n"],[1,"x"],[1,"x"]],root,false).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1],[1]],[[1]],root,false).Equal);
        Assert.True(global::TestSeamless.ResultBag.Compare([[1.0]],[[1.0+1e-10]],root,true).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0]],[[1.0+1e-10]],root,false).Equal);
        var strict=global::TestSeamless.FloatingTolerance.Parse("{\"DoubleAbsolute\":0,\"DoubleRelative\":0}");
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0]],[[1.0+1e-10]],root,true,strict).Equal);
        Assert.Throws<InvalidDataException>(()=>global::TestSeamless.FloatingTolerance.Parse("{\"DoubleAbsolute\":-1}"));
        // 同じ結果に計算由来と直接投影の浮動列が混在しても、直接投影列は厳密に比較する。
        Assert.True(global::TestSeamless.ResultBag.Compare([[1.0,0.25f]],[[1.0+1e-10,0.25f]],root,true,computedFloatingPointOrdinals:new HashSet<int>{0}).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0,0.25f]],[[1.0+1e-10,0.250001f]],root,true,computedFloatingPointOrdinals:new HashSet<int>{0}).Equal);
        Assert.False(global::TestSeamless.ResultBag.CompareOrder([[1,0.25f]],[[1,0.250001f]],[1],true,new HashSet<int>{0}));
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0001m]],[[1.0002m]],root,true).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[true]],[[1]],root,false).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[null]],[[""]],root,false).Equal);
        Assert.True(global::TestSeamless.ResultBag.Compare([[1.0f]],[[1.0f+0.000001f]],root,true).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0f]],[[1.01f]],root,true).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1,"x"]],[["x",1]],root,false).Equal);
        // 複数列ではソート順の対応が交差しても、許容誤差内の完全対応を見つける。
        Assert.True(global::TestSeamless.ResultBag.Compare([[1.0,1.0],[1.0000000009,3.0]],[[1.0000000008,3.0],[1.0000000009,1.0]],root,true).Equal);
        Assert.False(global::TestSeamless.ResultBag.Compare([[1.0,1.0],[1.0000000009,3.0]],[[1.0000000008,3.0],[1.0000000009,4.0]],root,true).Equal);
        Assert.True(global::TestSeamless.ResultBag.CompareOrder([[1,"a"],[1,"b"],[2,"c"]],[[1,"b"],[1,"a"],[2,"c"]],[0],false));
        Assert.False(global::TestSeamless.ResultBag.CompareOrder([[1,"a"],[2,"b"]],[[2,"b"],[1,"a"]],[0],false));
        Assert.Throws<InvalidDataException>(()=>global::TestSeamless.ResultBag.CompareOrder([[1]],[[1]],[1],false));
    }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}}
}
