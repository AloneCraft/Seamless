namespace Seamless.FLinq.Linq.Fragments;

/*
TakeWhile_predicate は predicate が false になった時点で永続的に終了する enumerator を生成する。

入力例:
  source.TakeWhile(x => x < limit)

生成例:
  if(done || !source.TryGetNext(out var v)) return false;
  var item = v;
  if(!predicate(item)) {
      done = true;
      return false;
  }
  current = item;
  return true;

バリエーション:
  - predicate は inline/delegate/statement inline の各 FuncFragment 経路を使う。
  - index overload は index フィールドを増やし predicate(item, index) を呼ぶ。
*/
public class TakeWhile_predicate(int 番号,Enumerable source,FuncFragment predicate):Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length>0&&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => this.FuncSupport(predicate,nameof(predicate));
    public override string GetEnumerableExpression =>
        $"global::Seamless.Linq.Enumerable.TakeWhile({source.GetEnumerableExpression}, {predicate.DelegateConstructorArg??predicate.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => "private bool done = false;";
    public override string GetTryGetNextBody {
        get {
            var (preCode,condExpr)=this.FuncInvokeCondition(predicate,nameof(predicate),this.temp,this.item);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return $$"""
                if(this.done || !{{this.e}}.TryGetNext(out var __mn78)) {
                    current = default!;
                    return false;
                }
                var {{this.item}} = __mn78;
                {{predicateCheck}} {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
                """;
        }
    }
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class TakeWhile_indexPredicate(int id,Enumerable source,FuncFragment predicate):TakeWhile_predicate(id,source,predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length>0&&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private bool done = false;
        private int {{this.a}} = -1;
        """;
    public override string GetTryGetNextBody {
        get {
            var (preCode,condExpr)=this.FuncInvokeCondition(predicate,nameof(predicate),this.temp,this.item,this.a);
            var predicateCheck = string.IsNullOrEmpty(preCode)
                ? $"if(!{WrapConditionExpression(condExpr)})"
                : $"{preCode}\n        if(!{WrapConditionExpression(condExpr)})";

            return $$"""
                if(this.done || !{{this.e}}.TryGetNext(out var __mn79)) {
                    current = default!;
                    return false;
                }
                var {{this.item}} = __mn79;
                this.{{this.a}}++;
                {{predicateCheck}} {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.{{this.currentValue}} = {{this.item}};
                return true;
                """;
        }
    }
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__takeWhileSource{id}";
        var spanName = $"__takeWhileSpan{id}";
        var itemName = $"__takeWhileItem{id}";
        var indexName = $"__takeWhileIndex{id}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        var localDeclarations = string.Join("\n",new[]{
            this.HoistedLocalDeclarations(predicate),
            this.StaticDelegateLocalDeclarations(predicate),
        }.Where(static text => !string.IsNullOrEmpty(text)));
        var localDeclarationBlock = string.IsNullOrEmpty(localDeclarations)
            ? string.Empty
            : localDeclarations+"\n";
        var (spanPreCode,spanCondExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,itemName,indexName);
        var spanPredicate = string.IsNullOrEmpty(spanPreCode)
            ? $"if(!{WrapConditionExpression(spanCondExpr)})"
            : $"{spanPreCode}\n            if(!{WrapConditionExpression(spanCondExpr)})";
        var (loopPreCode,loopCondExpr)=this.FuncInvokeConditionStatic(predicate,nameof(predicate),this.temp,itemName,indexName);
        var loopPredicate = string.IsNullOrEmpty(loopPreCode)
            ? $"if(!{WrapConditionExpression(loopCondExpr)})"
            : $"{loopPreCode}\n            if(!{WrapConditionExpression(loopCondExpr)})";

        if(directSpanExpression is not null) {
            return $$"""
                {{localDeclarationBlock}}
                using var {{sourceName}}={{source.GetEnumeratorExpression}};
                var {{indexName}}=-1;
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{spanName}}={{directSpanExpression}};
                for(var __i{{id}}=0;__i{{id}}<{{spanName}}.Length;__i{{id}}++) {
                    var {{itemName}}={{spanName}}[__i{{id}}];
                    {{indexName}}++;
                    {{spanPredicate}} {
                        break;
                    }
                    {{consumerName}}.Consume({{itemName}});
                }
                """;
        }

        return $$"""
            {{localDeclarationBlock}}
            using var {{sourceName}}={{source.GetEnumeratorExpression}};
            var {{indexName}}=-1;
            global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
            if({{sourceName}}.TryGetSpan(out var {{spanName}})) {
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
                for(var __i{{id}}=0;__i{{id}}<{{spanName}}.Length;__i{{id}}++) {
                    var {{itemName}}={{spanName}}[__i{{id}}];
                    {{indexName}}++;
                    {{spanPredicate}} {
                        break;
                    }
                    {{consumerName}}.Consume({{itemName}});
                }
            }else{
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanFailed();
                global::Seamless.Linq.LinqCounterScope.IncrementEnumerationLoopUsed();
                while({{sourceName}}.TryGetNext(out {{source.ElementType}} {{itemName}})) {
                    {{indexName}}++;
                    {{loopPredicate}} {
                        break;
                    }
                    {{consumerName}}.Consume({{itemName}});
                }
            }
            """;
    }
}
