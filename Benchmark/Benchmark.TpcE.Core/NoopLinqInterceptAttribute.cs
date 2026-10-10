namespace Benchmark.TpcE.Core;
/// <summary>
/// 標準 LINQ の組み合わせで使う、何もしない <c>LinqIntercept</c> の代わり。
/// 共有の取引コードは <c>[LinqIntercept(false)]</c> を書けるが、FLinq を参照しない組み合わせでは効果が無い。
/// </summary>
[AttributeUsage(AttributeTargets.Assembly|AttributeTargets.Class|AttributeTargets.Method,AllowMultiple=false)]
public sealed class NoopLinqInterceptAttribute(bool optimize):Attribute{
    public bool Optimize{get;}=optimize;
}
