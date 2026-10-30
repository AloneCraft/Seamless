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
        LastDemandRenderer.TryRender(this,source,new LastTerminalContract(false,null),destVar,destType)
        ??this.RenderRangeLastInline(destVar,destType)
        ??this.RenderDirectSpanLastInline(destVar,destType)
        ??this.RenderConsumeBasedLastInline(destVar,destType)
        ??this.RenderDirectEnumerableCallInline(source,destVar,destType,"Last")
        ??this.RenderComputeInline(destVar,destType);

    private string? RenderConsumeBasedLastInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_LAST_{番号}__";
        string consumeStatement;
        try {
            consumeStatement=source.GetConsumeExecutionStatement(placeholder);
        } catch(NotSupportedException) {
            return null;
        }
        if(string.IsNullOrWhiteSpace(consumeStatement)
            ||consumeStatement.Contains("TryGetNext")
            ||consumeStatement.Contains("GetRemainingEnumerator")
            ||!consumeStatement.Contains($"{placeholder}.Consume(")) {
            return null;
        }
        var foundVar = $"__found{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            return $"{indent}{destVar}={arg};\n{indent}{foundVar}=true;";
        });
        return $$"""
            {{destType}} {{destVar}}=default!;
            var {{foundVar}}=false;
            {{replaced}}
            if(!{{foundVar}}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            """;
    }

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
        OrderedLastDemandRenderer.TryRender(this,source,new LastTerminalContract(false,predicate),destVar,destType)
        ??LastDemandRenderer.TryRender(this,source,new LastTerminalContract(false,predicate),destVar,destType)
        ??this.RenderRangeLastPredicateInline(destVar,destType)
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
                if(!{{found}}) throw new global::System.InvalidOperationException("Sequence contains no matching element");
            }
            """;
    }
    private string PredicateSpanName=>GeneratedNames.Current?.StableLocal("Last_predicate:"+this.FragmentIndex+":Span","__span")??"__span";
    private string PredicateIndexName=>GeneratedNames.Current?.StableLocal("Last_predicate:"+this.FragmentIndex+":Index","__i")??"__i";
    private string RuntimeLastSpanName=>GeneratedNames.Current?.StableLocal("Last_predicate:"+this.FragmentIndex+":RuntimeSpan","__lastSpan")??"__lastSpan";
    private string RuntimeLastIndexName=>GeneratedNames.Current?.StableLocal("Last_predicate:"+this.FragmentIndex+":RuntimeIndex","__lastIndex")??"__lastIndex";
    private string RenderRuntimeRootLastPredicateSpan(){
        if(source is not Root{CanTryProvideSpan:true}){
            return string.Empty;
        }
        return $$"""
            if(source.TryGetSpan(out var {{this.RuntimeLastSpanName}})){
                for(var {{this.RuntimeLastIndexName}}={{this.RuntimeLastSpanName}}.Length-1;{{this.RuntimeLastIndexName}}>=0;{{this.RuntimeLastIndexName}}--){
                    var {{this.item}}={{this.RuntimeLastSpanName}}[{{this.RuntimeLastIndexName}}];
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}){return {{this.item}};}
                }
                throw new global::System.InvalidOperationException("Sequence contains no matching element");
            }
            """;
    }
    protected override string ComputeMembers{
        get{
            if(source is ArrayRoot or ListRoot){
                return $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                        source.TryGetSpan(out var {{this.PredicateSpanName}});
                        for(var {{this.PredicateIndexName}}={{this.PredicateSpanName}}.Length-1;{{this.PredicateIndexName}}>=0;{{this.PredicateIndexName}}--){
                            var {{this.item}}={{this.PredicateSpanName}}[{{this.PredicateIndexName}}];
                            bool {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                            if({{this.temp}}){return {{this.item}};}
                        }
                        throw new global::System.InvalidOperationException("Sequence contains no matching element");
                    }
                    """;
            }
            if(source.CanProvideSpan){
                // spanを返せることだけではSystem.LinqのIList後方走査契約を証明しない。
                return $$"""
                    {{AggressiveInliningAttribute}}
                    private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                        var found=false;
                        {{resultType}} result=default!;
                        source.TryGetSpan(out var {{this.PredicateSpanName}});
                        foreach(var {{this.item}} in {{this.PredicateSpanName}}){
                            bool {{this.temp}}=default!;
                            {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                            if({{this.temp}}){found=true;result={{this.item}};}
                        }
                        if(!found) throw new global::System.InvalidOperationException("Sequence contains no matching element");
                        return result;
                    }
                    """;
            }
            return $$"""
                {{AggressiveInliningAttribute}}
                private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                    {{this.RenderRuntimeRootLastPredicateSpan()}}
                    var found=false;
                    {{resultType}} result=default!;
                    while(source.TryGetNext(out var __mn40)){
                        var {{this.item}}=__mn40;
                        bool {{this.temp}}=default!;
                        {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                        if({{this.temp}}){found=true;result={{this.item}};}
                    }
                    if(!found) throw new global::System.InvalidOperationException("Sequence contains no matching element");
                    return result;
                }
                """;
        }
    }
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
