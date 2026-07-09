namespace Seamless.FLinq.Linq.Fragments;
/*
CountBy は key ごとの出現数を数え、KeyValuePair<TKey,int> 相当を列挙する Fragment。

入力例:
  source.CountBy(x => x.Category)

生成例:
  foreach(var item in source) {
      key = keySelector(item);
      counts[key] = counts.TryGetValue(key, out c) ? c + 1 : 1;
  }
  foreach(var pair in counts) current = pair;

設計意図:
  GroupBy(...).Select(g => (g.Key, g.Count())) より、group の List を作らず count だけを Dictionary に保持する。
*/
public class CountBy(int 番号, Enumerable source, FuncFragment keySelector) : Enumerable(番号) {
    private string KeySelectorFuncType => $"global::System.Func<{source.ElementType}, {FuncReturnTypeName(keySelector)}>";
    protected override Enumerable PrimarySource => source;
    private string CapacityExpression(string sourceCountExpression) =>
        keySelector.CardinalityUpperBound is > 0 and var upperBound
            ? $"global::System.Math.Min({sourceCountExpression},{upperBound})"
            : sourceCountExpression;
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression =>
        $"global::Seamless.System.Carrier.ValueEnumerableExtensions.CountBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>? {{this.lookup}};
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.lookup}} is null) {
            if({{this.e}}.TryGetSpan(out var __span)) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>({{this.CapacityExpression("__span.Length")}});
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{this.item}}=__span[__i];
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    __cnt++;
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>({{this.CapacityExpression("__count")}});
                }else{
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, int>();
                }
                while({{this.e}}.MoveNext()) {
                    var {{this.item}}={{this.e}}.Current;
                    {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    __cnt++;
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.MoveNext()) {
            var __kv=this.{{this.innerEnumerator}}.Current;
            current=this.{{this.currentValue}}=new global::System.Collections.Generic.KeyValuePair<{{FuncReturnTypeName(keySelector)}}, global::System.Int32>(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => $"global::System.Collections.Generic.KeyValuePair<{FuncReturnTypeName(keySelector)}, global::System.Int32>";
    public override string GetConsumeExecutionStatement(string consumerName) {
        const int MaxRangeCount = 65536;
        if(!IsInt32Type(FuncReturnTypeName(keySelector))
            || !keySelector.TryGetSmallIntegralRange(MaxRangeCount,out var min,out var max)) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        var keyType=FuncReturnTypeName(keySelector);
        var rangeCount=(long)max-min+1;
        if(rangeCount is <=0 or >MaxRangeCount) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var sourceName=$"__countBySource{this.result}";
        var seenName=$"__countBySeen{this.result}";
        var keysName=$"__countByKeys{this.result}";
        var countsName=$"__countByCounts{this.result}";
        var keyCountName=$"__countByKeyCount{this.result}";
        var keyName=$"__countByKey{this.result}";
        var indexName=$"__countByIndex{this.result}";
        var builder=new StringBuilder();
        var localDeclarations=string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector),
            this.StaticDelegateLocalDeclarations(keySelector),
        }.Where(static text=>!string.IsNullOrEmpty(text)));
        if(!string.IsNullOrEmpty(localDeclarations)) {
            builder.AppendLine(localDeclarations);
        }
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"var {seenName}=global::System.Buffers.ArrayPool<bool>.Shared.Rent({rangeCount});");
        builder.AppendLine($"var {keysName}=global::System.Buffers.ArrayPool<{keyType}>.Shared.Rent({rangeCount});");
        builder.AppendLine($"var {countsName}=global::System.Buffers.ArrayPool<int>.Shared.Rent({rangeCount});");
        builder.AppendLine("try{");
        builder.AppendLine($"    global::System.MemoryExtensions.AsSpan({seenName},0,{rangeCount}).Clear();");
        builder.AppendLine($"    var {keyCountName}=0;");
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{keyType}} {{keyName}}=default!;
            {{this.FuncAssignIndented("",keySelector,nameof(keySelector),keyName,"value")}}
            var {{indexName}}={{keyName}}-({{min}});
            if(!{{seenName}}[{{indexName}}]){
                {{seenName}}[{{indexName}}]=true;
                {{keysName}}[{{keyCountName}}++]={{keyName}};
                {{countsName}}[{{indexName}}]=0;
            }
            {{countsName}}[{{indexName}}]++;
            """,
            source.CanTryProvideSpan,
            source.CanProvideSpan,
            source.GetDirectSpanExpression(sourceName));
        builder.AppendLine($"    for(var __i{this.result}=0;__i{this.result}<{keyCountName};__i{this.result}++){{");
        builder.AppendLine($"        var {keyName}={keysName}[__i{this.result}];");
        builder.AppendLine($"        {consumerName}.Consume(new global::System.Collections.Generic.KeyValuePair<{keyType},global::System.Int32>({keyName},{countsName}[{keyName}-({min})]));");
        builder.AppendLine("    }");
        builder.AppendLine("}finally{");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<bool>.Shared.Return({seenName},true);");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<{keyType}>.Shared.Return({keysName},global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{keyType}>());");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<int>.Shared.Return({countsName},false);");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
