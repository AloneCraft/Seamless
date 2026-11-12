using Benchmark.Dynamic.Core;
using Core=Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class EmissionStyleTests{
    private static QueryIrDocument Generate(params string[] allowed){
        return new Core.RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Core.Seed256.Parse("0x57A1E"),
            QueryIndex=0,
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=16,
            EnableBinaryOperators=false,
            EnableNestedOperators=false,
            AllowedOperatorIds=allowed,
        });
    }

    [Fact,Trait("Category","Unit")]
    public void 既定の書き方はIRJSONにStyleを書かずConsumeも出さない(){
        var document=Generate(QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.ToArray);
        Assert.Equal(QueryEmissionStyle.Method,document.Style);
        Assert.DoesNotContain("Style",QueryIrJson.Serialize(document),StringComparison.OrdinalIgnoreCase);
        var emitted=new Core.QuerySourceEmitter().Emit(document).SourceText;
        Assert.DoesNotContain(".Consume(",emitted,StringComparison.Ordinal);
        Assert.DoesNotContain(" from value in ",emitted,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void Consume形はToArrayの終端を文で受けてIR往復もできる(){
        var document=Generate(QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.ToArray)with{Style=QueryEmissionStyle.Consume};
        var restored=QueryIrJson.Deserialize(QueryIrJson.Serialize(document));
        Assert.Equal(QueryEmissionStyle.Consume,restored.Style);
        var emitted=new Core.QuerySourceEmitter().Emit(restored).SourceText;
        Assert.Contains("using static global::Benchmark.Dynamic.Core.QueryConsumeExtensions;",emitted,StringComparison.Ordinal);
        Assert.Contains(".Consume(consumer);",emitted,StringComparison.Ordinal);
        Assert.Contains("global::System.Linq.Enumerable.ToArray(consumer.Items)",emitted,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void Consume形でもToArray以外の終端は元の書き方のまま(){
        var document=Generate(QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.Count)with{Style=QueryEmissionStyle.Consume};
        var emitted=new Core.QuerySourceEmitter().Emit(document).SourceText;
        Assert.DoesNotContain(".Consume(consumer);",emitted,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void クエリ構文はFLinqの2経路だけに出る(){
        var document=Generate(QueryOperatorIds.Where,QueryOperatorIds.Select,QueryOperatorIds.ToArray)with{Style=QueryEmissionStyle.QuerySyntax};
        var emitted=new Core.QuerySourceEmitter().Emit(document).SourceText;
        var system=Section(emitted,"RunSystem(object? primary");
        var unoptimized=Section(emitted,"RunFLinqUnoptimizedCore(object? primary");
        var optimized=Section(emitted,"RunFLinqCore(object? primary");
        Assert.DoesNotContain("from value in",system,StringComparison.Ordinal);
        Assert.Contains("from value in",unoptimized,StringComparison.Ordinal);
        Assert.Contains("from value in",optimized,StringComparison.Ordinal);
    }

    // 指定したメソッドの宣言から、次の public / private の宣言の手前までを返す。
    private static string Section(string source,string methodName){
        var start=source.IndexOf(methodName,StringComparison.Ordinal);
        Assert.True(start>=0,methodName);
        var end=source.IndexOf("    public static",start+1,StringComparison.Ordinal);
        var privateEnd=source.IndexOf("    [global::Seamless",start+1,StringComparison.Ordinal);
        var stop=new[]{end,privateEnd}.Where(index=>index>=0).DefaultIfEmpty(source.Length).Min();
        return source[start..stop];
    }
}
