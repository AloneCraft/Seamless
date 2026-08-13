namespace Seamless.FLinq;
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
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
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
    public override string GetConsumePreparationStatement() {
        if(source is not RangeRoot range||predicate.ParameterTypeNames.Length!=1) {
            return base.GetConsumePreparationStatement();
        }
        var functionDeclarations = string.Join("\n",new[] {
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(functionDeclarations)
            ? range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)
            : $$"""
                {{range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)}}
                {{functionDeclarations}}
                """;
    }
    public override string ElementType => source.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(predicate.ParameterTypeNames.Length!=1) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        if(source is RangeRoot range) {
            return this.RenderRangeConsume(consumerName,range);
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

    // RangeRoot は start+i で値を計算できるので状態機械を経由せず直列 for ループにできる。
    private protected string RenderRangeConsume(string consumerName,RangeRoot range) {
        var rangeOffsetName = $"__skipWhileRangeOffset{番号}";
        var rangeItemName = $"__skipWhileRangeItem{番号}";
        var (preCode,condExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,rangeItemName);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(condExpr)})"
            : $"{preCode}\n            if({WrapConditionExpression(condExpr)})";

        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            var {{rangeOffsetName}}=0;
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{predicateCheck}} {
                    continue;
                }
                break;
            }
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{consumerName}}.Consume({{rangeItemName}});
            }
            """;
    }
}

internal class SkipWhile_indexPredicate(int 番号,Enumerable source,FuncFragment predicate):SkipWhile_predicate(番号,source,predicate) {
    public static new bool IsApplicableVariant(IMethodSymbol method) =>
        method.Parameters.Length>0&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { predicate },source.GetEnumeratorExpression);
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
    public override string GetConsumePreparationStatement() {
        if(source is not RangeRoot range) {
            return base.GetConsumePreparationStatement();
        }
        var functionDeclarations = string.Join("\n",new[] {
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(functionDeclarations)
            ? range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)
            : $$"""
                {{range.RenderArgumentPreparation(range.PreparedStartName,range.PreparedCountName)}}
                {{functionDeclarations}}
                """;
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(source is RangeRoot range) {
            return this.RenderIndexRangeConsume(consumerName,range);
        }
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

    // RangeRoot は start+i で値を計算できるので状態機械を経由せず直列 for ループにできる。
    private string RenderIndexRangeConsume(string consumerName,RangeRoot range) {
        var rangeOffsetName = $"__skipWhileRangeOffset{番号}";
        var rangeIndexName = $"__skipWhileRangeIndex{番号}";
        var rangeItemName = $"__skipWhileRangeItem{番号}";
        var (preCode,condExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,rangeItemName,rangeIndexName);
        var predicateCheck = string.IsNullOrEmpty(preCode)
            ? $"if({WrapConditionExpression(condExpr)})"
            : $"{preCode}\n            if({WrapConditionExpression(condExpr)})";

        return $$"""
            {{this.CounterCall("IncrementEnumerationLoopUsed")}}
            var {{rangeOffsetName}}=0;
            var {{rangeIndexName}}=-1;
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{rangeIndexName}}++;
                {{predicateCheck}} {
                    continue;
                }
                break;
            }
            for(;{{rangeOffsetName}}<{{range.PreparedCountName}};{{rangeOffsetName}}++){
                var {{rangeItemName}}={{range.PreparedStartName}}+{{rangeOffsetName}};
                {{consumerName}}.Consume({{rangeItemName}});
            }
            """;
    }
}
