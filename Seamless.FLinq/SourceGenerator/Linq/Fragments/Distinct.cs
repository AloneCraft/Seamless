namespace Seamless.FLinq.Linq.Fragments;

internal class Distinct(int 番号,Enumerable source):Distinct_comparer(番号,source,"null");

internal class Distinct_comparer(int 番号,Enumerable source,string comparer):DistinctBase(番号,source,comparer);

/*
DistinctBase は source.Distinct(comparer) を HashSet で既出判定する enumerator/direct consume へ変換する。

入力例:
  source.Distinct()
  source.Distinct(comparer)

生成例:
  while(source.TryGetNext(out var item)) {
      if(seen.Add(item)) {
          current = item;
          return true;
      }
  }
  return false;

設計意図:
  TryGetNonEnumeratedCount で HashSet capacity を見積もる。Consume 直結では TraversalSupport による
  span/while ループを使い、current フィールド更新を省いて consumer.Consume(value) へ直接渡す。
*/
internal abstract class DistinctBase(int 番号,Enumerable source,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    private string Comparer=>ComparerExpressionSupport.RenderEqualityComparer(comparer,source.ElementType);
    public override string StructMethod=>string.Empty;
    public override string GetEnumerableExpression=>$"global::Seamless.System.Carrier.ValueEnumerableExtensions.Distinct({source.GetEnumerableExpression},{Comparer})";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{Comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorConstructor=>$"this.{lookup}=CreateLookup({e},comparer);";
    public override string GetEnumeratorFields=>$$"""
        private global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}> {{lookup}};
        private static global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            // Distinct は列挙途中で set を更新し続けるため、ここでは root の型に応じた span 判定を入れない。
            // 生成時点で root が span 直走査できる場合は上位の consumer 専用 lowering が選ばれ、それ以外はこの enumerator 経路に落ちる。
            if(source.TryGetNonEnumeratedCount(out var {{count}})){
                return new global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}>({{count}},comparer);
            }else{
                return new global::Seamless.System.Carrier.HashSetSlim<{{source.ElementType}}>(comparer);
            }
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        while({{e}}.TryGetNext(out var {{item}})){
            if(this.{{lookup}}.Add({{item}})){
                current=this.{{currentValue}}={{item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=> currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>$"{lookup}.Dispose();\n{e}.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        if(ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,source.ElementType)&&IsInt32Type(source.ElementType)&&source.CanTryProvideSpan) {
            AppendSortedIntDistinctFastPath(builder,sourceName,consumerName);
        }
        TraversalSupport.AppendHashSetInitialization(builder,lookup,source.ElementType,sourceName,Comparer);
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{lookup}}.Add(value)){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
        return builder.ToString().TrimEnd();
    }

    private void AppendSortedIntDistinctFastPath(StringBuilder builder,string sourceName,string consumerName) {
        var spanName=$"__distinctSpan{番号}";
        var sortedName=$"__distinctSorted{番号}";
        var directSpanExpression=source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            AppendSortedIntDistinctFastPathBody(builder,spanName,sortedName,consumerName,string.Empty);
            return;
        }
        if(source.CanProvideSpan) {
            builder.AppendLine($"{sourceName}.TryGetSpan(out var {spanName});");
            AppendSortedIntDistinctFastPathBody(builder,spanName,sortedName,consumerName,string.Empty);
            return;
        }
        builder.AppendLine($"if({sourceName}.TryGetSpan(out var {spanName})){{");
        builder.AppendLine($"    var {sortedName}=true;");
        builder.AppendLine($"    for(var __i=1;__i<{spanName}.Length;__i++){{");
        builder.AppendLine($"        if({spanName}[__i]<={spanName}[__i-1]){{");
        builder.AppendLine($"            {sortedName}=false;");
        builder.AppendLine("            break;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine($"    if({sortedName}){{");
        builder.AppendLine($"        for(var __i=0;__i<{spanName}.Length;__i++){{");
        builder.AppendLine($"            {consumerName}.Consume({spanName}[__i]);");
        builder.AppendLine("        }");
        builder.AppendLine("        return;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendSortedIntDistinctFastPathBody(StringBuilder builder,string spanName,string sortedName,string consumerName,string indent) {
        builder.AppendLine($"{indent}var {sortedName}=true;");
        builder.AppendLine($"{indent}for(var __i=1;__i<{spanName}.Length;__i++){{");
        builder.AppendLine($"{indent}    if({spanName}[__i]<={spanName}[__i-1]){{");
        builder.AppendLine($"{indent}        {sortedName}=false;");
        builder.AppendLine($"{indent}        break;");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}if({sortedName}){{");
        builder.AppendLine($"{indent}    for(var __i=0;__i<{spanName}.Length;__i++){{");
        builder.AppendLine($"{indent}        {consumerName}.Consume({spanName}[__i]);");
        builder.AppendLine($"{indent}    }}");
        builder.AppendLine($"{indent}    return;");
        builder.AppendLine($"{indent}}}");
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}

/*
DistinctByBase は keySelector(item) の結果を HashSet に入れ、初めて見た key の item だけを返す。

入力例:
  source.DistinctBy(x => x.Id, comparer)

生成例:
  while(source.TryGetNext(out var item)) {
      var key = keySelector(item);
      if(seenKeys.Add(key)) {
          current = item;
          return true;
      }
  }

バリエーション:
  - keySelector が identity かつ型/比較器が合う連続 DistinctBy は direct consume を前段へ統合できる。
  - key の範囲が小さい int と分かる場合、HashSet ではなく bool[] seen を使う direct consume を生成する。
*/
internal abstract class DistinctByBase(int 番号,Enumerable source,FuncFragment keySelector,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    private FuncFragment KeySelector=>keySelector;
    private string Comparer=>ComparerExpressionSupport.RenderEqualityComparer(comparer,FuncReturnTypeName(keySelector));
    public override string StructMethod=> FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression=>$"global::Seamless.System.Carrier.ValueEnumerableExtensions.DistinctBy({source.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{Comparer})";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {EnumeratorTypeName}({source.GetEnumeratorExpression},{Comparer}{ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorConstructor=>$"this.{lookup}=CreateLookup({e},comparer);";
    public override string GetEnumeratorFields=>$$"""
        private global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> {{lookup}};
        private static global::Seamless.System.Carrier.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            // DistinctBy も同じで、span 走査かどうかの判定はここではしない。
            // コンパイル時に root 形状が分かる場合は上位で専用生成されるため、ここは汎用経路として最小状態だけを保持する。
            {{CreateLookupBody(FuncReturnTypeName(keySelector))}}
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        while({{e}}.TryGetNext(out var {{item}})){
            {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
            {{FuncAssignIndented("    ",keySelector,nameof(keySelector),key,item)}}
            if(this.{{lookup}}.Add({{key}})){
                current=this.{{currentValue}}={{item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=> currentValue;
    public override string ElementType=>source.ElementType;
    protected override string GetEnumeratorDisposeBody=>$"{lookup}.Dispose();\n{e}.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName){
        if(source is DistinctByBase previous
            && CanMergeWith(previous)) {
            return previous.GetConsumeExecutionStatement(consumerName);
        }
        var sourceName=$"__src{番号}";
        var keyName=$"__key{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        var hoistedDeclarations=HoistedLocalDeclarations(keySelector);
        if(!string.IsNullOrEmpty(hoistedDeclarations)) {
            builder.AppendLine(hoistedDeclarations);
        }
        if(TryAppendSmallIntegralRangeConsume(builder,sourceName,keyName,consumerName)) {
            return builder.ToString().TrimEnd();
        }
        TraversalSupport.AppendHashSetInitialization(builder,lookup,FuncReturnTypeName(keySelector),sourceName,Comparer,KeyCapacityHint);
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{FuncReturnTypeName(keySelector)}} {{keyName}}=default!;
            {{FuncAssignIndented("",keySelector,nameof(keySelector),keyName,"value")}}
            if({{lookup}}.Add({{keyName}})){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
        return builder.ToString().TrimEnd();
    }

    private string? KeyCapacityHint =>
        keySelector.CardinalityUpperBound is { } upperBound
            ? upperBound.ToString()
            : null;

    private bool TryAppendSmallIntegralRangeConsume(StringBuilder builder,string sourceName,string keyName,string consumerName) {
        const int MaxRangeCount = 65536;
        var keyType=FuncReturnTypeName(keySelector);
        if(!ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,keyType)
            || !IsInt32Type(keyType)
            || !keySelector.TryGetSmallIntegralRange(MaxRangeCount,out var min,out var max)) {
            return false;
        }

        var rangeCount=(long)max-min+1;
        if(rangeCount is <=0 or >MaxRangeCount) {
            return false;
        }

        var seenName=$"__seen{番号}";
        var seenIndexName=$"__seenIndex{番号}";
        builder.AppendLine($"var {seenName}=new bool[{rangeCount}];");
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{keyType}} {{keyName}}=default!;
            {{FuncAssignIndented("",keySelector,nameof(keySelector),keyName,"value")}}
            var {{seenIndexName}}={{keyName}}-({{min}});
            if(!{{seenName}}[{{seenIndexName}}]){
                {{seenName}}[{{seenIndexName}}]=true;
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName));
        return true;
    }

    private string CreateLookupBody(string keyType) =>
        KeyCapacityHint is { } capacityHint
            ? $"return new global::Seamless.System.Carrier.HashSetSlim<{keyType}>({capacityHint},comparer);"
            : $$"""
              if(source.TryGetNonEnumeratedCount(out var {{count}})){
                  return new global::Seamless.System.Carrier.HashSetSlim<{{keyType}}>({{count}},comparer);
              }else{
                  return new global::Seamless.System.Carrier.HashSetSlim<{{keyType}}>(comparer);
              }
              """;

    private bool CanMergeWith(DistinctByBase previous) {
        if(KeySelector.ContainsAnchor
            || previous.KeySelector.ContainsAnchor
            || !string.Equals(Comparer,previous.Comparer,StringComparison.Ordinal)
            || KeySelector.RequiresStatementInlining
            || previous.KeySelector.RequiresStatementInlining
            || KeySelector.ParameterNames.Length!=previous.KeySelector.ParameterNames.Length
            || KeySelector.ParameterTypeNames.Length!=previous.KeySelector.ParameterTypeNames.Length
            || KeySelector.ReturnTypeName!=previous.KeySelector.ReturnTypeName) {
            return false;
        }
        for(var index=0; index<KeySelector.ParameterTypeNames.Length; index++) {
            if(KeySelector.ParameterTypeNames[index]!=previous.KeySelector.ParameterTypeNames[index]) {
                return false;
            }
        }
        try {
            return string.Equals(KeySelector.RenderAsLambda(),previous.KeySelector.RenderAsLambda(),StringComparison.Ordinal);
        } catch(InvalidOperationException) {
            return false;
        }
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
