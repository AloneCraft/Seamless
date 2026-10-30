namespace Seamless.FLinq.Generator;

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
internal abstract class DistinctBase(int 番号,Enumerable source,string comparer):Enumerable(番号), ConsumeDirectExecutionFragment {
    // Explicit Fragment child visitor
    protected override IEnumerable<Fragment> GetChildFragments() {
        foreach(var child in base.GetChildFragments()) {
                yield return child;
        }
        if(source is { } __visitorChild_source) {
            yield return __visitorChild_source;
        }
    }

    protected override Enumerable PrimarySource => source;
    private string Comparer => ComparerExpressionSupport.RenderEqualityComparer(comparer,source.ElementType);
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression => $"global::Seamless.Linq.Enumerable.Distinct({source.GetEnumerableExpression},{this.Comparer})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionSeparated(this.EnumeratorTypeName,",",source.GetEnumeratorExpression,this.Comparer);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorConstructor => $"this.{this.lookup}=CreateLookup({this.e},comparer);";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.HashSetSlim<{{source.ElementType}}> {{this.lookup}};
        private static global::Seamless.Linq.HashSetSlim<{{source.ElementType}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            // Distinct は列挙途中で set を更新し続けるため、ここでは root の型に応じた span 判定を入れない。
            // 生成時点で root が span 直走査できる場合は上位の consumer 専用 lowering が選ばれ、それ以外はこの enumerator 経路に落ちる。
            if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                return new global::Seamless.Linq.HashSetSlim<{{source.ElementType}}>({{this.count}},comparer);
            }else{
                return new global::Seamless.Linq.HashSetSlim<{{source.ElementType}}>(comparer);
            }
        }
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.TryGetNext(out var {{this.item}})){
            if(this.{{this.lookup}}.Add({{this.item}})){
                current=this.{{this.currentValue}}={{this.item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => $"{this.lookup}.Dispose();\n{this.e}.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName) {
        var sourceName = $"__src{番号}";
        var builder = new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        // 整列済みの int は集合を使わずそのまま流す。ここは Consume の文なので、メソッドの途中に埋め込まれても良いよう、
        // return ではなく「処理済み」の旗で残りを飛ばす(return; は値を返すメソッドや、後ろにまだ続きがある文では使えない)。
        var handledName = $"__distinctHandled{番号}";
        var hasSortedFastPath = ComparerExpressionSupport.IsDefaultEqualityComparer(comparer,source.ElementType)&&IsInt32Type(source.ElementType)&&source.CanTryProvideSpan;
        if(hasSortedFastPath) {
            builder.AppendLine($"var {handledName}=false;");
            this.AppendSortedIntDistinctFastPath(builder,sourceName,consumerName,handledName);
            builder.AppendLine($"if(!{handledName}){{");
        }
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,source.ElementType,sourceName,this.Comparer);
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{this.lookup}}.Add(value)){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName),source.ProvidesRemainingEnumerator);
        if(hasSortedFastPath) {
            builder.AppendLine("}");
        }
        return builder.ToString().TrimEnd();
    }

    private void AppendSortedIntDistinctFastPath(StringBuilder builder,string sourceName,string consumerName,string handledName) {
        var spanName = $"__distinctSpan{番号}";
        var sortedName = $"__distinctSorted{番号}";
        var directSpanExpression = source.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            builder.AppendLine($"var {spanName}={directSpanExpression};");
            AppendSortedIntDistinctFastPathBody(builder,spanName,sortedName,consumerName,string.Empty,handledName);
            return;
        }
        if(source.CanProvideSpan) {
            builder.AppendLine($"{sourceName}.TryGetSpan(out var {spanName});");
            AppendSortedIntDistinctFastPathBody(builder,spanName,sortedName,consumerName,string.Empty,handledName);
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
        builder.AppendLine($"        {handledName}=true;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
    }

    private static void AppendSortedIntDistinctFastPathBody(StringBuilder builder,string spanName,string sortedName,string consumerName,string indent,string handledName) {
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
        builder.AppendLine($"{indent}    {handledName}=true;");
        builder.AppendLine($"{indent}}}");
    }

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        ||string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        ||string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
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
internal abstract class DistinctByBase(int 番号,Enumerable source,FuncFragment keySelector,string comparer):Enumerable(番号), ConsumeDirectExecutionFragment {
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
    private FuncFragment KeySelector => keySelector;
    private string Comparer => ComparerExpressionSupport.RenderEqualityComparer(comparer,FuncReturnTypeName(keySelector));
    public override string StructMethod => this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression => $"global::Seamless.Linq.Enumerable.DistinctBy({source.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{this.Comparer})";
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => RenderEnumeratorExpressionWithClosure(this.EnumeratorTypeName,new[] { keySelector },source.GetEnumeratorExpression,this.Comparer);
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorConstructor => $"this.{this.lookup}=CreateLookup({this.e},comparer);";
    public override string GetEnumeratorFields => $$"""
        private global::Seamless.Linq.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> {{this.lookup}};
        private static global::Seamless.Linq.HashSetSlim<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            // DistinctBy も同じで、span 走査かどうかの判定はここではしない。
            // コンパイル時に root 形状が分かる場合は上位で専用生成されるため、ここは汎用経路として最小状態だけを保持する。
            {{this.CreateLookupBody(FuncReturnTypeName(keySelector))}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.TryGetNext(out var {{this.item}})){
            {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
            {{this.FuncAssignIndented("    ",keySelector,nameof(keySelector),this.key,this.item)}}
            if(this.{{this.lookup}}.Add({{this.key}})){
                current=this.{{this.currentValue}}={{this.item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string ElementType => source.ElementType;
    protected override string GetEnumeratorDisposeBody => $"{this.lookup}.Dispose();\n{this.e}.Dispose();";
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(source is DistinctByBase previous
            &&this.CanMergeWith(previous)) {
            return previous.GetConsumeExecutionStatement(consumerName);
        }
        // Range の x%C のキーは各符号の区間の先頭 C 個で初めて現れる(RangeModuloDistinctRoot)。Consume の文は列挙子の構造体を使わない。
        if(source is RangeRoot range
            &&comparer=="null"
            &&!source.TreeContainsAnchor()
            &&RangeModuloOrderRoot.TryGetModulus(keySelector,out var modulus)) {
            return new RangeModuloDistinctRoot(番号,range,modulus).GetConsumeExecutionStatement(consumerName);
        }
        var sourceName = $"__src{番号}";
        var keyName = $"__key{番号}";
        var builder = new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        var hoistedDeclarations = this.HoistedLocalDeclarations(keySelector);
        if(!string.IsNullOrEmpty(hoistedDeclarations)) {
            builder.AppendLine(hoistedDeclarations);
        }
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,FuncReturnTypeName(keySelector),sourceName,this.Comparer,null);
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{FuncReturnTypeName(keySelector)}} {{keyName}}=default!;
            {{this.FuncAssignIndented("",keySelector,nameof(keySelector),keyName,"value")}}
            if({{this.lookup}}.Add({{keyName}})){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan,source.CanProvideSpan,source.GetDirectSpanExpression(sourceName),source.ProvidesRemainingEnumerator);
        return builder.ToString().TrimEnd();
    }

    private string CreateLookupBody(string keyType) =>
        $$"""
        if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
            return new global::Seamless.Linq.HashSetSlim<{{keyType}}>({{this.count}},comparer);
        }else{
            return new global::Seamless.Linq.HashSetSlim<{{keyType}}>(comparer);
        }
        """;

    private bool CanMergeWith(DistinctByBase previous) {
        if(this.KeySelector.ContainsAnchor
            ||previous.KeySelector.ContainsAnchor
            ||!string.Equals(this.Comparer,previous.Comparer,StringComparison.Ordinal)
            ||this.KeySelector.RequiresStatementInlining
            ||previous.KeySelector.RequiresStatementInlining
            ||this.KeySelector.ParameterNames.Length!=previous.KeySelector.ParameterNames.Length
            ||this.KeySelector.ParameterTypeNames.Length!=previous.KeySelector.ParameterTypeNames.Length
            ||this.KeySelector.ReturnTypeName!=previous.KeySelector.ReturnTypeName) {
            return false;
        }
        for(var index = 0;index<this.KeySelector.ParameterTypeNames.Length;index++) {
            if(this.KeySelector.ParameterTypeNames[index]!=previous.KeySelector.ParameterTypeNames[index]) {
                return false;
            }
        }
        try {
            return string.Equals(this.KeySelector.RenderAsLambda(),previous.KeySelector.RenderAsLambda(),StringComparison.Ordinal);
        } catch(InvalidOperationException) {
            return false;
        }
    }

}
