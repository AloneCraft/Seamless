namespace Seamless.FLinq.Generator;

/*
MinMaxByBase は source.MinBy(keySelector) / MaxBy(keySelector) を、最良 key を持つ元要素へ変換する。

入力例:
  source.MaxBy(x => x.Score)

生成例:
  if(!source.TryGetNext(out var v)) throw NoElements();
  bestElement = v;
  bestKey = keySelector(bestElement);
  while(source.TryGetNext(out var v)) {
      key = keySelector(item);
      if(comparer.Compare(key, bestKey) is better) {
          bestElement = item;
          bestKey = key;
      }
  }
  return bestElement;

バリエーション:
  - span source は最初に span path を使い、だめなら ComputeEnumerator に戻る。
  - direction により > 0 / < 0 を切り替える。
*/
internal abstract class MinMaxByBase(int 番号,Enumerable source,FuncFragment keySelector,string resultType,string direction):BufferedScalar(番号,resultType) {
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
        if(keySelector is { } __visitorChild_keySelector) {
            yield return __visitorChild_keySelector;
        }
    }

    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    protected override string ComputeExpression => $"Compute(ref {this.e}{this.ClosureArgsSuffix(keySelector)})";
    public override string? RenderInlineComputation(string destVar,string destType) {
        // Range の x%C のキーは周期 C で繰り返すので、最初に現れる極値は先頭 C 個の中で決まる(RangeModuloDistinctRoot.RenderExtremeBy)。
        if(source is RangeRoot range
            &&resultType=="int"
            &&RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)) {
            return RangeModuloDistinctRoot.RenderExtremeBy(range,modulus,番号,destVar,destType,direction=="greater");
        }
        var methodName = direction=="greater" ? "MaxBy" : "MinBy";
        return this.RenderConsumeBasedMinMaxByInline(destVar,destType)
            ??this.RenderComputeInline(destVar,destType)
            ??(TryRenderDirectLambda(keySelector) is { } lambda
                ? this.RenderDirectEnumerableCallInline(source,destVar,destType,methodName,lambda)
                : null);
    }

    private string? RenderConsumeBasedMinMaxByInline(string destVar,string destType) {
        if(source is not (Join or Zip or Zip_resultSelector or GroupJoin or SelectManyBase)) {
            return null;
        }else{
        }
        var placeholder = $"__FLINQ_MINMAXBY_{番号}__";
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
        }else{
        }
        var keyType = FuncReturnTypeName(keySelector);
        var foundVar = $"__found{番号}";
        var bestElementVar = $"__bestElem{番号}";
        var bestKeyVar = $"__bestKey{番号}";
        var keyVar = $"__key{番号}";
        var comparerVar = $"__comparer{番号}";
        var isMax = direction=="greater";
        string compOp;
        if(isMax) {
            compOp=">0";
        }else{
            compOp="<0";
        }
        var escaped = global::System.Text.RegularExpressions.Regex.Escape(placeholder);
        var pattern = $@"(?<indent>[ \t]*){escaped}\.Consume\((?<arg>.+?)\);";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector),
            this.StaticDelegateLocalDeclarations(keySelector),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var replaced = global::System.Text.RegularExpressions.Regex.Replace(consumeStatement,pattern,match => {
            var indent = match.Groups["indent"].Value;
            var arg = match.Groups["arg"].Value;
            var assign = this.FuncAssignStatic(keySelector,nameof(keySelector),keyVar,arg);
            string compExpr;
            if(this.IsIntegralKey) {
                if(isMax) {
                    compExpr=$"{keyVar}>{bestKeyVar}";
                }else{
                    compExpr=$"{keyVar}<{bestKeyVar}";
                }
            }else{
                compExpr=$"{comparerVar}.Compare({keyVar},{bestKeyVar}){compOp}";
            }
            return $"{indent}{keyType} {keyVar}=default!;\n{indent}{assign}\n{indent}if(!{foundVar}) {{\n{indent}    {bestElementVar}={arg};\n{indent}    {bestKeyVar}={keyVar};\n{indent}    {foundVar}=true;\n{indent}}} else if({compExpr}) {{\n{indent}    {bestElementVar}={arg};\n{indent}    {bestKeyVar}={keyVar};\n{indent}}}";
        });
        string comparerInit;
        if(this.IsIntegralKey) {
            comparerInit="";
        }else{
            comparerInit=$"var {comparerVar}=global::System.Collections.Generic.Comparer<{keyType}>.Default;\n";
        }
        return $$"""
            {{localDeclarations}}
            {{destType}} {{bestElementVar}}=default!;
            {{keyType}} {{bestKeyVar}}=default!;
            var {{foundVar}}=false;
            {{comparerInit}}{{replaced}}
            if(!{{foundVar}}) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{destType}} {{destVar}}={{bestElementVar}};
            """;
    }
    protected override string ComputeMembers => source.CanTryProvideSpan
        ? this.BuildTrySpanCompute()
        : this.BuildEnumeratorCompute();
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    private string CompareOp => direction=="greater" ? ">" : "<";
    // キーが整数型なら Comparer<T>.Default.Compare と演算子の結果が同じなので、演算子で比べる(Compare は三方向の比較で分岐が2つになる)。
    // 浮動小数点数は NaN の扱いが違い、参照型は null の扱いがあるので Compare のまま。
    private bool IsIntegralKey => FuncReturnTypeName(keySelector) is "int" or "long" or "short" or "byte" or "sbyte" or "ushort" or "uint" or "ulong" or "char" or "nint" or "nuint";
    private string Better(string key,string best) => this.IsIntegralKey
        ? $"{key}{this.CompareOp}{best}"
        : $"comparer.Compare({key},{best}){this.CompareOp}0";
    private string BuildSpanCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            source.TryGetSpan(out var __span);
            if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            // 最初の要素で初期化し、残りだけを比べる(ループの中で「最初か」を判定しない)。
            {{resultType}} bestElement=__span[0];
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
            for(var __index=1;__index<__span.Length;__index++){
                var {{this.item}}=__span[__index];
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if({{this.Better(this.key,"bestKey")}}){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildTrySpanCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            if(source.TryGetSpan(out var __span)){
                var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
                if(__span.Length==0) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
                // 最初の要素で初期化し、残りだけを比べる(ループの中で「最初か」を判定しない)。
                {{resultType}} bestElement=__span[0];
                {{FuncReturnTypeName(keySelector)}} bestKey=default!;
                {{this.FuncAssignIndented("                ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
                for(var __index=1;__index<__span.Length;__index++){
                    var {{this.item}}=__span[__index];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    if({{this.Better(this.key,"bestKey")}}){
                        bestElement={{this.item}};
                        bestKey={{this.key}};
                    }
                }
                return bestElement;
            }
            return ComputeEnumerator(source{{this.ClosureArgsSuffix(keySelector)}});
        }
        {{AggressiveInliningAttribute}}
        private static {{resultType}} ComputeEnumerator(TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.TryGetNext(out var __mn104)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=__mn104;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.TryGetNext(out var __mn105)){
                var {{this.item}}=__mn105;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if({{this.Better(this.key,"bestKey")}}){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
    private string BuildEnumeratorCompute() => $$"""
        {{AggressiveInliningAttribute}}
        private static {{resultType}} Compute(ref TEnum source{{this.ClosureMethodParameterSuffix(keySelector)}}){
            var comparer=global::System.Collections.Generic.Comparer<{{FuncReturnTypeName(keySelector)}}>.Default;
            if(!source.TryGetNext(out var __mn106)) throw global::Seamless.Linq.GeneratedCodeHelpers.NoElements();
            {{resultType}} bestElement=__mn106;
            {{FuncReturnTypeName(keySelector)}} bestKey=default!;
            {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),"bestKey","bestElement")}}
            while(source.TryGetNext(out var __mn107)){
                var {{this.item}}=__mn107;
                {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                {{this.FuncAssignIndented("        ",keySelector,nameof(keySelector),this.key,this.item)}}
                if({{this.Better(this.key,"bestKey")}}){
                    bestElement={{this.item}};
                    bestKey={{this.key}};
                }
            }
            return bestElement;
        }
        """;
}
