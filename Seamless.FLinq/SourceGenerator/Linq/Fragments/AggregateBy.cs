namespace Seamless.FLinq.Linq.Fragments;
/*
AggregateBy は key ごとに accumulator を持つ grouping 集約を生成する。

入力例:
  source.AggregateBy(x => x.GroupId, seed, (acc, item) => Step(acc, item))

生成例:
  foreach(var item in source) {
      key = keySelector(item);
      if(!map.TryGetValue(key, out acc)) acc = seed;
      map[key] = func(acc, item);
  }
  foreach(var pair in map) current = pair;

バリエーション:
  - keySelector/func はそれぞれ FuncFragment として closure/delegate/inline に対応する。
  - resultElementType は返す KeyValuePair 相当の要素型を決める。
*/
public class AggregateBy(int 番号, Enumerable source, FuncFragment keySelector, string seed, FuncFragment func, string resultElementType) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string CapacityExpression(string sourceCountExpression) =>
        keySelector.CardinalityUpperBound is > 0 and var upperBound
            ? $"global::System.Math.Min({sourceCountExpression},{upperBound})"
            : sourceCountExpression;

    public override string StructMethod => $$"""
        {{this.FuncSupport(keySelector,nameof(keySelector))}}
        {{this.FuncSupport(func,nameof(func))}}
        """;
    public override string GetEnumerableExpression =>
        $"global::Seamless.System.Carrier.ValueEnumerableExtensions.AggregateBy({source.GetEnumerableExpression}, {keySelector.DelegateConstructorArg ?? keySelector.RenderAsLambda()}, {seed}, {func.DelegateConstructorArg ?? func.RenderAsLambda()})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}{this.ClosureArgsSuffix(keySelector,func)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {this.e}{this.ClosurePrimaryCtorSuffix(keySelector,func)}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>? {{this.lookup}};
        private global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>.Enumerator {{this.innerEnumerator}};
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.lookup}} is null) {
            if({{this.e}}.TryGetSpan(out var __span)) {
                this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>({{this.CapacityExpression("__span.Length")}});
                for(var __i=0;__i<__span.Length;__i++) {
                    var {{this.item}}=__span[__i];
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }else{
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>({{this.CapacityExpression("__count")}});
                }else{
                    this.{{this.lookup}}=new global::System.Collections.Generic.Dictionary<{{this.KeyType}}, {{FuncReturnTypeName(func)}}>();
                }
                while({{this.e}}.MoveNext()) {
                    var {{this.item}}={{this.e}}.Current;
                    {{this.KeyType}} {{this.key}}=default!;
                    {{this.FuncAssignIndented("            ",keySelector,nameof(keySelector),this.key,this.item)}}
                    ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.{{this.lookup}},{{this.key}},out var __exists);
                    if(!__exists) {
                        __agg={{seed}};
                    }
                    {{this.FuncAssignIndented("            ",func,nameof(func),"__agg","__agg",this.item)}}
                }
            }
            this.{{this.innerEnumerator}}=this.{{this.lookup}}.GetEnumerator();
        }
        if(this.{{this.innerEnumerator}}.MoveNext()) {
            var __kv=this.{{this.innerEnumerator}}.Current;
            current=this.{{this.currentValue}}=new {{resultElementType}}(__kv.Key,__kv.Value);
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => resultElementType;

    public override string GetConsumeExecutionStatement(string consumerName) {
        const int MaxRangeCount = 65536;
        if(!IsInt32Type(this.KeyType)
            || !keySelector.TryGetSmallIntegralRange(MaxRangeCount,out var min,out var max)) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var rangeCount=(long)max-min+1;
        if(rangeCount is <=0 or >MaxRangeCount) {
            return base.GetConsumeExecutionStatement(consumerName);
        }

        var valueType=FuncReturnTypeName(func);
        var sourceName=$"__aggregateBySource{this.result}";
        var seenName=$"__aggregateBySeen{this.result}";
        var keysName=$"__aggregateByKeys{this.result}";
        var valuesName=$"__aggregateByValues{this.result}";
        var keyCountName=$"__aggregateByKeyCount{this.result}";
        var keyName=$"__aggregateByKey{this.result}";
        var indexName=$"__aggregateByIndex{this.result}";
        var aggregateName=$"__aggregateByValue{this.result}";
        var builder=new StringBuilder();
        var localDeclarations=string.Join("\n",new[]{
            this.HoistedLocalDeclarations(keySelector,func),
            this.StaticDelegateLocalDeclarations(keySelector,func),
        }.Where(static text=>!string.IsNullOrEmpty(text)));
        if(!string.IsNullOrEmpty(localDeclarations)) {
            builder.AppendLine(localDeclarations);
        }

        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        builder.AppendLine($"var {seenName}=global::System.Buffers.ArrayPool<bool>.Shared.Rent({rangeCount});");
        builder.AppendLine($"var {keysName}=global::System.Buffers.ArrayPool<{this.KeyType}>.Shared.Rent({rangeCount});");
        builder.AppendLine($"var {valuesName}=global::System.Buffers.ArrayPool<{valueType}>.Shared.Rent({rangeCount});");
        builder.AppendLine("try{");
        builder.AppendLine($"    global::System.MemoryExtensions.AsSpan({seenName},0,{rangeCount}).Clear();");
        builder.AppendLine($"    var {keyCountName}=0;");
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{this.KeyType}} {{keyName}}=default!;
            {{this.FuncAssignIndented("",keySelector,nameof(keySelector),keyName,"value")}}
            var {{indexName}}={{keyName}}-({{min}});
            if(!{{seenName}}[{{indexName}}]){
                {{seenName}}[{{indexName}}]=true;
                {{keysName}}[{{keyCountName}}++]={{keyName}};
                {{valuesName}}[{{indexName}}]={{seed}};
            }
            var {{aggregateName}}={{valuesName}}[{{indexName}}];
            {{this.FuncAssignIndented("",func,nameof(func),aggregateName,aggregateName,"value")}}
            {{valuesName}}[{{indexName}}]={{aggregateName}};
            """,
            source.CanTryProvideSpan,
            source.CanProvideSpan,
            source.GetDirectSpanExpression(sourceName));
        builder.AppendLine($"    for(var __i{this.result}=0;__i{this.result}<{keyCountName};__i{this.result}++){{");
        builder.AppendLine($"        var {keyName}={keysName}[__i{this.result}];");
        builder.AppendLine($"        {consumerName}.Consume(new {resultElementType}({keyName},{valuesName}[{keyName}-({min})]));");
        builder.AppendLine("    }");
        builder.AppendLine("}finally{");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<bool>.Shared.Return({seenName},true);");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<{this.KeyType}>.Shared.Return({keysName},global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{this.KeyType}>());");
        builder.AppendLine($"    global::System.Buffers.ArrayPool<{valueType}>.Shared.Return({valuesName},global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{valueType}>());");
        builder.AppendLine("}");
        return builder.ToString().TrimEnd();
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
