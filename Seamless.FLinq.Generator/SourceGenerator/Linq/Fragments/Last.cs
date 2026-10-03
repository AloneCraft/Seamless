namespace Seamless.FLinq.Generator;
/*
Last は source.Last() を、最後に見た値を保持する Compute に変換する。

入力例:
  source.Last()
  source.Last(predicate)

生成例:
  found = false;
  while(source.TryGetNext(out var v)) {
      found = true;
      result = v;
  }
  if(!found) throw NoElements();
  return result;

バリエーション:
  - span source は span[^1] を返す。
  - RangeRoot<int> は start + count - 1 を inline する。
  - predicate 付き RangeRoot<int> は末尾側から逆向き loop を inline できる。
*/
internal class Last(int 番号,Enumerable source,string resultType):BufferedScalar(番号,resultType) {
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
    protected override string ComputeExpression => $"Compute(ref {this.e})";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeLastInline(destVar,destType)
        ??this.RenderDirectSpanLastInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"Last")
        ??this.RenderComputeInline(destVar,destType);
    private string? RenderDirectSpanLastInline(string destVar,string destType) {
        var span = $"__span{this.result}";
        return this.RenderSpanInline(source,destVar,destType,span,spanName => $$"""
            if({{spanName}}.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{destVar}}={{spanName}}[{{spanName}}.Length-1];
            """);
    }
    private string? RenderRangeLastInline(string destVar,string destType) {
        if(source is not RangeRoot range||source.ElementType!="int") {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        return $$"""
            {{destType}} {{destVar}};
            {
                {{range.RenderArgumentPreparation(start,count)}}
                if({{count}}==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                {{destVar}}={{start}} + {{count}} - 1;
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return __span[__span.Length-1];
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source){
                var found=false;
                {{resultType}} result=default!;
                while(source.TryGetNext(out var __mn39)){
                    found=true;
                    result=__mn39;
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class Last_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):BufferedScalar(番号,resultType) {
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
        $"Compute(ref {this.e}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorConstructor =>
        $"this.Current = Compute(ref this.{this.e}{this.ClosureArgsSuffixForCtorBody(predicate)});";
    public override string? RenderInlineComputation(string destVar,string destType) =>
        this.RenderRangeLastPredicateInline(destVar,destType)
        ??(TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Last",lambda)
            : this.RenderComputeInline(destVar,destType));
    private string? RenderRangeLastPredicateInline(string destVar,string destType) {
        if(source is not RangeRoot range
            ||source.ElementType!="int"
            ||!predicate.CanInlineAssign
            ||predicate.RequiresStatementInlining) {
            return null;
        }
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var index = $"__i{番号}";
        var found = $"__found{番号}";
        return $$"""
            {{destType}} {{destVar}}=default!;
            {
                {{range.RenderArgumentPreparation(start,count)}}
                var {{found}}=false;
                for(var {{index}}={{count}}-1;{{index}}>=0;{{index}}--){
                    var {{this.item}}={{start}} + {{index}};
                    bool {{this.temp}}=default!;
            {{IndentLines(this.FuncAssignStatic(predicate,nameof(predicate),this.temp,this.item),"        ")}}
                    if({{this.temp}}){
                        {{destVar}}={{this.item}};
                        {{found}}=true;
                        break;
                    }
                }
                if(!{{found}}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            }
            """;
    }
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                var found=false;
                {{resultType}} result=default!;
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){ found=true; result={{this.item}}; }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                var found=false;
                {{resultType}} result=default!;
                while(source.TryGetNext(out var __mn40)){
                    var {{this.item}}=__mn40;
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){ found=true; result={{this.item}}; }
                }
                if(!found) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                return result;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
