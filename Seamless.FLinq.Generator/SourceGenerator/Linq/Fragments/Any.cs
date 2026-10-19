namespace Seamless.FLinq.Generator;
/*
Any は source.Any() を「件数 fast path か MoveNext 1回」で判定する scalar に変換する。

入力例:
  source.Any()

生成例:
  if(source.TryGetNonEnumeratedCount(out var count)) result = count != 0;
  else result = source.TryGetNext(out var v);

バリエーション:
  - Range/Repeat は count 式だけで inline 結果を出せる。
  - Any(predicate) は predicate が true になった時点で true を返す Compute を生成する。
  - Any(indexPredicate) は index を増やしながら predicate(item, index) を評価する。
*/
internal class Any(int 番号,Enumerable source):Scalar(番号) {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    internal static bool TryBuildWithPredicate(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol containingType,
        ref int uniqueNumber,
        out Fragment fragment,
        out string staticMethods,
        List<string>? unsafeAccessorCollector,
        BuildEnumerableFragmentDelegate buildEnumerable,
        BuildFuncFragmentDelegate buildFunc,
        CreateFragmentDelegate createFragment) {
        fragment=null!;
        staticMethods=string.Empty;
        if(semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return false;
        ExpressionSyntax sourceExpression;
        var argumentStartIndex = 0;
        if(method.ReducedFrom is not null
            &&invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpression }) {
            sourceExpression=receiverExpression;
        } else {
            var definition = method.ReducedFrom??method;
            if(definition.ContainingType.ToDisplayString()!="System.Linq.Enumerable"
                ||definition.Parameters.Length==0
                ||invocation.ArgumentList.Arguments.Count==0) {
                return false;
            }
            sourceExpression=invocation.ArgumentList.Arguments[0].Expression;
            argumentStartIndex=1;
        }
        if(!buildEnumerable(semanticModel,sourceExpression,containingType,ref uniqueNumber,out var sourceFragment,out var sourceStaticMethods,unsafeAccessorCollector))
            return false;
        if(invocation.ArgumentList.Arguments.Count-argumentStartIndex==1) {
            if(!buildFunc(invocation.ArgumentList.Arguments[argumentStartIndex].Expression,method.Parameters[argumentStartIndex].Type,ref uniqueNumber,out var predicate,semanticModel,containingType))
                return false;
            var anyVariantName = predicate.ParameterTypeNames.Length>1 ? "indexPredicate" : "predicate";
            var anyPredicateFragment = createFragment("Any",anyVariantName,uniqueNumber++,new object?[] { sourceFragment,predicate });
            if(anyPredicateFragment is null)
                return false;
            fragment=anyPredicateFragment;
            staticMethods=JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
            return true;
        } else if(invocation.ArgumentList.Arguments.Count!=argumentStartIndex) {
            return false;
        }
        var anyFragment = createFragment("Any",string.Empty,uniqueNumber++,new object?[] { sourceFragment });
        if(anyFragment is null)
            return false;
        fragment=anyFragment;
        staticMethods=JoinStaticMethods(sourceStaticMethods,fragment.StructMethod);
        return true;
    }
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.EnumeratorTypeName,source.GetEnumeratorExpression);
    public override string InlineSupportingMembers => source.GetEnumeratorStruct;
    public override string? RenderInlineComputation(string destVar,string destType) {
        if(source is RangeRoot range) {
            return RangePredicateOptimizer.RenderAny(range,番号,destVar,destType);
        }
        if(source is RepeatRoot repeat) {
            return RangePredicateOptimizer.RenderAny(repeat,番号,destVar,destType);
        }
        if(source.NonEnumeratedCountExpression is { } countExpression) {
            return $"{destType} {destVar}=({countExpression})!=0;";
        }
        if(this.RenderConsumeBasedAnyInline(destVar,destType) is { } directConsume) {
            return directConsume;
        }
        if(this.RenderDirectEnumerableCallInline(source,destVar,destType,"Any") is { } directCall) {
            return directCall;
        }
        return this.RenderEnumeratorInline(destVar,destType);
    }

    private string? RenderConsumeBasedAnyInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }
        var placeholder = $"__FLINQ_ANY_{番号}__";
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
        var endLabel = $"__anyEnd{番号}";
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            return $"{indent}{destVar}=true;\n{indent}goto {endLabel};";
        });
        return $$"""
            {{destType}} {{destVar}}=false;
            {{replaced}}
            {{endLabel}}:;
            """;
    }

    private string RenderEnumeratorInline(string destVar,string destType) {
        var enumeratorName = $"__s{番号}";
        return $$"""
            {{destType}} {{destVar}};
            using(var {{enumeratorName}}={{source.GetEnumeratorExpression}}){
                if({{enumeratorName}}.TryGetNonEnumeratedCount(out var __count{{番号}})){
                    {{destVar}}=__count{{番号}}!=0;
                }else{
                    {{destVar}}={{enumeratorName}}.TryGetNext(out _);
                }
            }
            """;
    }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{this.DefineScalarEnumeratorStruct("bool")}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.yielded}} = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}){
            current = default;
            return false;
        }
        this.{{this.yielded}} = true;
        if({{this.e}}.TryGetNonEnumeratedCount(out var __count)){
            this.{{this.currentValue}} = __count != 0;
        }else{
            this.{{this.currentValue}} = {{this.e}}.TryGetNext(out _);
        }
        current = this.{{this.currentValue}};
        return true;
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

/*
Any_predicate は source.Any(predicate) を、最初に true になった要素で短絡する Compute にする。

生成例:
  foreach(var item in source) {
      if(predicate(item)) return true;
  }
  return false;

source が span を確実に返せる場合は foreach(span) を生成し、それ以外は MoveNext loop にする。
*/
internal class Any_predicate(int 番号,Enumerable source,FuncFragment predicate):BufferedScalar(番号,"bool") {
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
        source is RangeRoot range
            ? (predicate.ContainsAnchor ? null : RangePredicateOptimizer.RenderAny(range,predicate,番号,destVar,destType)) // .Anchor を含む述語は評価の回数を変えてはいけないので、範囲の形から答えを出す近道を使わない。
                ??(TryRenderDirectLambda(predicate) is { } rangeLambda
                    ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Any",rangeLambda)??this.RenderComputeInline(destVar,destType)
                    : this.RenderComputeInline(destVar,destType))
        : TryRenderDirectLambda(predicate) is { } lambda
            ? this.RenderDirectEnumerableCallInline(source,destVar,destType,"Any",lambda)??this.RenderComputeInline(destVar,destType)
            : this.RenderComputeInline(destVar,destType);
    protected override string ComputeMembers => source.CanProvideSpan
        ? $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                source.TryGetSpan(out var __span);
                foreach(var {{this.item}} in __span){
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return true;
                }
                return false;
            }
            """
        : $$"""
            {{AggressiveInliningAttribute}}
            private static bool Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
                while(source.TryGetNext(out var __mn4)){
                    var {{this.item}}=__mn4;
                    bool {{this.temp}}=default!;
                    {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item)}}
                    if({{this.temp}}) return true;
                }
                return false;
            }
            """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}

internal class Any_indexPredicate(int 番号,Enumerable source,FuncFragment predicate):BufferedScalar(番号,"bool") {
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

    private string PredicateFuncType => $"global::System.Func<{source.ElementType}, int, bool>";
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
    protected override string ComputeMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static bool Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(predicate)}}){
            var {{this.a}}=0;
            while(source.TryGetNext(out var __mn5)){
                var {{this.item}}=__mn5;
                bool {{this.temp}}=default!;
                {{this.FuncAssignStaticIndented("        ",predicate,nameof(predicate),this.temp,this.item,this.a)}}
                if({{this.temp}}) return true;
                {{this.a}}++;
            }
            return false;
        }
        """;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
}
