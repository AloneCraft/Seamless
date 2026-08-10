namespace Seamless.FLinq;

/*
Single は source.Single() を、0件/2件以上を検出する Compute に変換する。

入力例:
  source.Single()
  source.Single(predicate)

生成例:
  if(!source.TryGetNext(out var v)) throw NoElements();
  var value = v;
  if(source.TryGetNext(out var v)) throw MoreThanOneElement();
  return value;

バリエーション:
  - span source は Length で 0/1/>1 を即判定する。
  - Range/Repeat は count 式だけで inline 判定できる。
  - predicate 付きは一致数を found flag で追跡し、2件目で例外にする。
*/
internal class Single(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
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
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    protected override string ComputeExpression => $"Compute({this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingle(range,番号,destVar,destType)
        : source is RepeatRoot repeat
            ? RangePredicateOptimizer.RenderSingle(repeat,番号,destVar,destType)
            : this.RenderDirectSpanSingleInline(destVar,destType)
            ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"Single")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanSingleInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            if({{spanName}}.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
            {{destVar}}={{spanName}}[0];
            """);
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source){
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                if(__span.Length>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return __span[0];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source){
                if(!source.TryGetNext(out var __mn53)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{resultType}} value = __mn53;
                if(source.TryGetNext(out _)) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class Single_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
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
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    protected override string ComputeExpression =>
        $"Compute({this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute({this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        source is RangeRoot range
            ? RangePredicateOptimizer.RenderSingle(range,predicate,番号,destVar,destType)
                ??(TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Single",rangeLambda)
                    : this.RenderComputeInline(destVar,destType))
        : (TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Single",lambda)
            : this.RenderComputeInline(destVar,destType));
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                {{resultType}} value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value={{this.item}};
                        found=true;
                    }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return value;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                {{resultType}} value = default!;
                bool found = false;
                while(source.TryGetNext(out var __mn55)){
                    var {{this.item}} = __mn55;
                    bool {{this.temp}} = default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value = {{this.item}};
                        found = true;
                    }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return value;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
