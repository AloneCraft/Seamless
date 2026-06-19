#if BENCH_AS_TEST
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

[assembly: Xunit.TestCaseOrderer(typeof(Benchmark.Shared.TestPriorityOrderer))]

namespace Benchmark.Shared;

// [Fact,Trait("Category","Benchmark")] を1個に集約した属性。
// xunit.v3 では ITraitAttribute.GetTraits() で Trait を付与できる。
// 各ベンチメソッドには別途 BDN 用の [Benchmark] を併記する（フレームワーク2種が各自の属性を要求するため統合は不可）。
[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class BenchFactAttribute:FactAttribute,ITraitAttribute{
    // テストエクスプローラのソース位置解決のため、CallerFilePath/LineNumber を基底へ転送する（xUnit3003）。
    public BenchFactAttribute([CallerFilePath]string?sourceFilePath=null,[CallerLineNumber]int sourceLineNumber=-1)
        :base(sourceFilePath,sourceLineNumber){}

    public IReadOnlyCollection<KeyValuePair<string,string>>GetTraits()=>[new("Category","Benchmark")];
}

public sealed class TestPriorityAttribute(int priority):Attribute{
    public int Priority{get;}=priority;
}

public sealed class TestPriorityOrderer:ITestCaseOrderer{
    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
        where TTestCase:notnull,Xunit.Sdk.ITestCase{
        var ordered=new List<TTestCase>(testCases);
        ordered.Sort(static (left,right) => {
            var priorityComparison=GetPriority(left).CompareTo(GetPriority(right));
            if(priorityComparison!=0){
                return priorityComparison;
            }
            return StringComparer.Ordinal.Compare(left.TestMethodName,right.TestMethodName);
        });
        return ordered;
    }

    private static int GetPriority(Xunit.Sdk.ITestCase testCase){
        var type=FindType(testCase.TestClassName);
        var methodName=testCase.TestMethodName;
        if(type is null||methodName is null){
            return 0;
        }
        var method=type.GetMethod(methodName,global::System.Reflection.BindingFlags.Instance|global::System.Reflection.BindingFlags.Static|global::System.Reflection.BindingFlags.Public|global::System.Reflection.BindingFlags.NonPublic);
        var attribute=method is null?null:Attribute.GetCustomAttribute(method,typeof(TestPriorityAttribute),false) as TestPriorityAttribute;
        return attribute?.Priority??0;
    }

    private static Type? FindType(string? typeName){
        if(typeName is null){
            return null;
        }
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
            var type=assembly.GetType(typeName,false);
            if(type is not null){
                return type;
            }
        }
        return null;
    }
}
#else
namespace Benchmark.Shared;

// BDN(Benchmark.csproj)側では xunit への依存を持たないための no-op 属性。
// 各ベンチメソッドの [BenchFact] はここでは何もしないマーカーとしてのみ存在する。
[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class BenchFactAttribute:Attribute;

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class FactAttribute:Attribute;

[AttributeUsage(AttributeTargets.Method,AllowMultiple=true)]
public sealed class TraitAttribute(string name,string value):Attribute{
    public string Name{get;}=name;
    public string Value{get;}=value;
}

[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class TestPriorityAttribute(int priority):Attribute{
    public int Priority{get;}=priority;
}
#endif
