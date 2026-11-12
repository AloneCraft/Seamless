using Benchmark.Dynamic.Worker;
using Benchmark.Dynamic.Core;
using Seamless.Linq;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicFallbackCaptureTests{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 直接実行は正常終了と例外のどちらでもフォールバックを不合格にする(bool throws){
        Assert.Throws<Xunit.Sdk.EqualException>(()=>DirectQueryExecution.Capture(()=>{
            using var nested=LinqCounterScope.Start();
            nested.Counter.IncrementEnumerableFallback("direct-query");
            nested.Counter.Reset();
            if(throws){
                throw new InvalidOperationException("expected");
            }
            return 42;
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 直接実行はフォールバックのない結果と例外を比較用に返す(bool throws){
        var observed=DirectQueryExecution.Capture(()=>{
            if(throws){
                throw new InvalidOperationException("expected");
            }
            return 42;
        });
        Assert.Equal(0,observed.FallbackCount);
        if(throws){
            Assert.IsType<InvalidOperationException>(observed.Exception);
        }else{
            Assert.Null(observed.Exception);
            Assert.Equal(42,observed.Value);
        }
    }

    [Fact]
    public void 入れ子の計数範囲と例外でもフォールバックを失わない(){
        var result=DynamicFallbackCapture.Run<int>(()=>{
            using var nested=LinqCounterScope.Start();
            nested.Counter.IncrementEnumerableFallback("nested");
            nested.Counter.Reset();
            throw new InvalidOperationException("nested failure");
        });
        Assert.Equal(1,result.FallbackCount);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal(0,DynamicFallbackCapture.Run(()=>0).FallbackCount);
    }
    [Fact]
    public void 成功時と例外時の両方でフォールバックを記録する(){
        var success=DynamicFallbackCapture.Run(()=>{
            LinqCounterScope.Current!.IncrementEnumerableFallback("success");
            return 42;
        });
        Assert.Equal(42,success.Value);
        Assert.Equal(1,success.FallbackCount);
        Assert.Null(success.Exception);

        var failure=DynamicFallbackCapture.Run<int>(()=>{
            LinqCounterScope.Current!.IncrementEnumerableFallback("failure");
            throw new InvalidOperationException("expected");
        });
        Assert.Equal(1,failure.FallbackCount);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public void 通常実行はゼロを返し取消は伝播する(){
        var result=DynamicFallbackCapture.Run(()=>7);
        Assert.Equal(7,result.Value);
        Assert.Equal(0,result.FallbackCount);
        Assert.Null(result.Exception);
        Assert.Throws<OperationCanceledException>(()=>DynamicFallbackCapture.Run<int>(()=>throw new OperationCanceledException()));
    }

    [Fact]
    public void 移行したチェーンは元の入力で全実装が一致しフォールバックしない(){
        var input=new[]{1,2,3,4,5};
        var cases=new (string Id,object Expected,QueryCardinality Cardinality)[]{
            ("fallback-where-select-sum",12,QueryCardinality.Scalar),
            ("fallback-skip-take-list",new[]{2,3},QueryCardinality.Sequence),
        };
        foreach(var (id,value,cardinality) in cases){
            var item=Benchmark.Dynamic.Core.FixedCaseCatalog.All.Single(item=>item.ID==id);
            var compiled=new DynamicQueryCompiler().Compile(new("fallback-"+Guid.NewGuid().ToString("N"),"Benchmark.Dynamic.Generated.FixedQuery",Benchmark.Dynamic.Core.FixedCaseCatalog.Emit(item),id+".cs"));
            Assert.True(compiled.Success,string.Join("\n",compiled.Diagnostics.Where(item=>item.Severity=="Error").Select(item=>item.Message)));
            var loaded=compiled.LoadedQuery!;
            try{
                var expected=DynamicQueryValue.FromObject(value,QueryValueType.Int,cardinality,QueryOrderSemantics.Ordered);
                foreach(var implementation in new[]{DynamicImplementationKind.System,DynamicImplementationKind.FLinqUnoptimized,DynamicImplementationKind.FLinq,DynamicImplementationKind.ZLinq,DynamicImplementationKind.Ideal}){
                    var observed=DynamicFallbackCapture.Run(()=>loaded.Run(implementation,input,Array.Empty<int>()));
                    Assert.Null(observed.Exception);
                    Assert.Equal(0,observed.FallbackCount);
                    Assert.True(DynamicQueryComparer.Compare(expected,observed.Value!).IsMatch,id+" "+implementation);
                }
            }finally{Assert.True(loaded.UnloadAndVerify().Unloaded);}
        }
    }
}
