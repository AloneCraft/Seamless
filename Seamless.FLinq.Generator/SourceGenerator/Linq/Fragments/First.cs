namespace Seamless.FLinq.Generator;

/*
First は source.First() を、最初の1要素だけ読む Compute に変換する。

入力例:
  source.First()
  source.First(predicate)

生成例:
  if(source.TryGetNext(out var v)) return v;
  throw NoElements();

バリエーション:
  - RangeRoot は start 式だけで inline できる。
  - predicate 付きは predicate(item) が true の最初の item を返す。
  - predicate が直接 lambda 化できる場合は DirectEnumerableExpression 経由で
    BufferedScalar の Compute インライン展開に乗る(System.Linq への委譲ではない。
    唯一の RenderDirectEnumerableCallInline override である BufferedScalar.cs 参照)。
*/
internal class First(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) => method.Parameters.Length==0;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderFirst(range,番号,destVar,destType)
            : this.RenderDirectSpanFirstInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"First")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanFirstInline(string destVar,string destType) {
        var sourceName = $"__firstSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return null;
        }
        var spanName = $"__firstSpan{番号}";
        return $$"""
            {{destType}} {{destVar}};
            using(var {{sourceName}}={{source.GetEnumeratorExpression}}){
                var {{spanName}}={{directSpanExpression}};
                if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}={{spanName}}[0];
            }
            """;
    }
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source){
            if(source.TryGetNext(out var __mn30)) return __mn30;
            throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class First_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }
    protected override IEnumerable<FuncFragment> GetChildFunctions() {
        foreach(var function in base.GetChildFunctions()) {
                yield return function;
        }
        if(predicate is { } __visitorChild_predicate) {
            yield return __visitorChild_predicate;
        }
    }

    public static bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length>0&&method.Parameters[0].Type.TypeKind==TypeKind.Delegate;
    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, bool>";
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"First",lambda)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
            while(source.TryGetNext(out var __mn31)){
                var {{this.item}} = __mn31;
                bool {{this.temp}} = default!;
                {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                if({{this.temp}}) return {{this.item}};
            }
            throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
