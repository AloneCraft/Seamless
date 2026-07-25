namespace Seamless.FLinq.Linq.Fragments;
/*
SkipWhile_predicate は predicate が true の間だけ読み捨て、その後は全要素を返す enumerator を生成する。

入力例:
  source.SkipWhile(x => x.IsHeader)

生成例:
  while(source.TryGetNext(out var v)) {
      var item = v;
      if(skipping && predicate(item)) continue;
      skipping = false;
      current = item;
      return true;
  }
  return false;

バリエーション:
  - index overload は読み捨て中も返却後も source から読むたびに index を増やす。
  - predicate が一度 false になると以後は predicate を見ずに要素を返す。
*/
internal class SkipWhile_predicate(int 番号,Enumerable source,FuncFragment predicate):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length>0&&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.SkipWhile({source.GetEnumerableExpression}, {predicate.DelegateConstructorArg??predicate.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => "private bool skipping = true;";
    public override string GetTryGetNextBody {
        get {
            var (preCode,condExpr)=this.FuncInvokeCondition(predicate,nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {WrapConditionExpression(condExpr)})";

            return $$"""
                while({{this.e}}.TryGetNext(out var __mn69)){
                    var {{this.item}} = __mn69;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{this.currentValue}} = {{this.item}};
                    return true;
                }
                current=default!;
                return false;
                """;
        }
    }
    public override string ElementType => source.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(predicate.ParameterTypeNames.Length!=1) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var sourceName = $"__skipWhileSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var spanName = $"__skipWhileSpan{番号}";
        var offsetName = $"__skipWhileOffset{番号}";
        var itemName = $"__skipWhileItem{番号}";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,condExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,itemName);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(condExpr)})"
            : $"{preCode}\n            if({WrapConditionExpression(condExpr)})";

        return $$"""
            {{localDeclarationBlock}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            var {{offsetName}}=0;
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{predicateCheck}} {
                    continue;
                }
                break;
            }
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }
}

internal class SkipWhile_indexPredicate(int 番号,Enumerable source,FuncFragment predicate):SkipWhile_predicate(番号,source,predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length>0&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool skipping = true;
        private int {{this.a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode,condExpr)=this.FuncInvokeCondition(predicate,nameof(predicate),this.temp,this.item,this.a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(this.skipping && {WrapConditionExpression(condExpr)})"
                : $"{preCode}\n            if(this.skipping && {WrapConditionExpression(condExpr)})";

            return $$"""
                while({{this.e}}.TryGetNext(out var __mn70)){
                    var {{this.item}} = __mn70;
                    this.{{this.a}}++;
                    {{predicateCheck}} {
                        continue;
                    }
                    this.skipping = false;
                    current = this.{{this.currentValue}} = {{this.item}};
                    return true;
                }
                current=default!;
                return false;
                """;
        }
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__skipWhileSource{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is null) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var spanName = $"__skipWhileSpan{番号}";
        var offsetName = $"__skipWhileOffset{番号}";
        var indexName = $"__skipWhileIndex{番号}";
        var itemName = $"__skipWhileItem{番号}";
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (preCode,condExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,itemName,indexName);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(condExpr)})"
            : $"{preCode}\n            if({WrapConditionExpression(condExpr)})";

        return $$"""
            {{localDeclarationBlock}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
            var {{spanName}}={{directSpanExpression}};
            var {{offsetName}}=0;
            var {{indexName}}=-1;
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{indexName}}++;
                {{predicateCheck}} {
                    continue;
                }
                break;
            }
            for(;{{offsetName}}<{{spanName}}.Length;{{offsetName}}++){
                var {{itemName}}={{spanName}}[{{offsetName}}];
                {{consumerName}}.Consume({{itemName}});
            }
            """;
    }
}
