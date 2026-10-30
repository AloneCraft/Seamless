using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

public sealed class FLinqExpansionValidatorTests{
    [Fact,Trait("Category","Unit")]
    public void 警告FLINQ002は展開未完了として返す(){
        var warning=new DynamicCompilationDiagnostic("Generator","Warning","FLINQ002","could not expand Where","Query.g.cs(3,4)");

        var result=FLinqExpansionValidator.Validate([warning],string.Empty).ToArray();

        var error=Assert.Single(result);
        Assert.Equal("Error",error.Severity);
        Assert.Equal(FLinqExpansionValidator.DiagnosticId,error.ID);
        Assert.Contains("FLINQ002",error.Message,StringComparison.Ordinal);
        Assert.Equal("Query.g.cs(3,4)",error.Location);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("UnsupportedLinqExpansion(")]
    [InlineData("LinqInterceptorFailure(")]
    public void 展開失敗の目印が生成物にあれば展開未完了として返す(string marker){
        var generated="static int Run()=>throw global::Seamless.Linq.GeneratedCodeHelpers."+marker+"\"x\",\"y\");";

        var result=FLinqExpansionValidator.Validate([],generated).ToArray();

        var error=Assert.Single(result);
        Assert.Equal(FLinqExpansionValidator.DiagnosticId,error.ID);
        Assert.Contains(marker,error.Message,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void 通常の診断と生成物では何も返さない(){
        DynamicCompilationDiagnostic[] diagnostics=[
            new("Generator","Info","GeneratedSource","Q.g.cs",string.Empty),
            new("Emit","Warning","CS8019","unused using",string.Empty),
        ];

        Assert.Empty(FLinqExpansionValidator.Validate(diagnostics,"public static int Run()=>1;"));
    }

    [Fact,Trait("Category","Unit")]
    public void 警告と目印が両方あれば両方を返す(){
        var warning=new DynamicCompilationDiagnostic("Generator","Warning","FLINQ002","m",string.Empty);

        var result=FLinqExpansionValidator.Validate([warning],"UnsupportedLinqExpansion(").ToArray();

        Assert.Equal(2,result.Length);
    }
}
