#if BENCH_AS_TEST
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

namespace Seamless.Benchmarks;

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
#else
namespace Seamless.Benchmarks;

// BDN(Benchmark.csproj)側では xunit への依存を持たないための no-op 属性。
// 各ベンチメソッドの [BenchFact] はここでは何もしないマーカーとしてのみ存在する。
[AttributeUsage(AttributeTargets.Method,AllowMultiple=false)]
public sealed class BenchFactAttribute:Attribute;
#endif
