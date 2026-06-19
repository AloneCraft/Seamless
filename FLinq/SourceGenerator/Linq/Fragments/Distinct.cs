namespace Tsavorite.SourceGenerator.Linq.Fragments;

internal class Distinct(int 番号,Enumerable source):Distinct_comparer(番号,source,"null");

internal class Distinct_comparer(int 番号,Enumerable source,string comparer):DistinctBase(番号,source,comparer);

internal abstract class DistinctBase(int 番号,Enumerable source,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    public override string GetEnumerableExpression=>$"global::Tsavorite.Carrier.ValueEnumerableExtensions.Distinct({source.GetEnumerableExpression},{comparer})";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{comparer})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{source.ElementType}>? comparer";
    public override string GetEnumeratorConstructor=>$"this.{this.lookup}=CreateLookup({this.e},comparer);";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.HashSet<{{source.ElementType}}> {{this.lookup}};
        private static global::System.Collections.Generic.HashSet<{{source.ElementType}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{source.ElementType}}>? comparer){
            // Distinct は列挙途中で set を更新し続けるため、ここでは root の型に応じた span 判定を入れない。
            // 生成時点で root が span 直走査できる場合は上位の consumer 専用 lowering が選ばれ、それ以外はこの enumerator 経路に落ちる。
            if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>({{this.count}},comparer);
            }else{
                return new global::System.Collections.Generic.HashSet<{{source.ElementType}}>(comparer);
            }
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        while({{this.e}}.TryGetNext(out var {{this.item}})){
            if(this.{{this.lookup}}.Add({{this.item}})){
                current=this.{{this.currentValue}}={{this.item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=> this.currentValue;
    public override string ElementType=>source.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,source.ElementType,sourceName,comparer);
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{this.lookup}}.Add(value)){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan);
        return builder.ToString().TrimEnd();
    }
}

internal abstract class DistinctByBase(int 番号,Enumerable source,FuncFragment keySelector,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    private FuncFragment KeySelector=>keySelector;
    private string Comparer=>comparer;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression=>$"global::Tsavorite.Carrier.ValueEnumerableExtensions.DistinctBy({source.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
    public override string GetEnumeratorStruct=>$$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression=>$"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression},{comparer}{this.ClosureArgsSuffix(keySelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters=>$"TEnum {this.e},global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(keySelector)}>? comparer{this.ClosurePrimaryCtorSuffix(keySelector)}";
    public override string GetEnumeratorConstructor=>$"this.{this.lookup}=CreateLookup({this.e},comparer);";
    public override string GetEnumeratorFields=>$$"""
        private global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> {{this.lookup}};
        private static global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(keySelector)}}>? comparer){
            // DistinctBy も同じで、span 走査かどうかの判定はここではしない。
            // コンパイル時に root 形状が分かる場合は上位で専用生成されるため、ここは汎用経路として最小状態だけを保持する。
            {{this.CreateLookupBody(FuncReturnTypeName(keySelector))}}
        }
        """;
    public override string GetTryGetNextBody=>$$"""
        while({{this.e}}.TryGetNext(out var {{this.item}})){
            {{FuncReturnTypeName(keySelector)}} {{this.key}}=default!;
            {{this.FuncAssign(keySelector,nameof(keySelector),this.key,this.item)}}
            if(this.{{this.lookup}}.Add({{this.key}})){
                current=this.{{this.currentValue}}={{this.item}};
                return true;
            }
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression=> this.currentValue;
    public override string ElementType=>source.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName){
        if(source is DistinctByBase previous
            && this.CanMergeWith(previous)) {
            return previous.GetConsumeExecutionStatement(consumerName);
        }
        var sourceName=$"__src{番号}";
        var keyName=$"__key{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        if(this.TryAppendSmallIntegralRangeConsume(builder,sourceName,keyName,consumerName)) {
            return builder.ToString().TrimEnd();
        }
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,FuncReturnTypeName(keySelector),sourceName,comparer,this.KeyCapacityHint);
        TraversalSupport.AppendTraversalLoop(
            builder,
            sourceName,
            source.ElementType,
            "value",
            $$"""
            {{FuncReturnTypeName(keySelector)}} {{keyName}}=default!;
            {{this.FuncAssign(keySelector,nameof(keySelector),keyName,"value")}}
            if({{this.lookup}}.Add({{keyName}})){
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan);
        return builder.ToString().TrimEnd();
    }

    private string? KeyCapacityHint =>
        keySelector.CardinalityUpperBound is { } upperBound
            ? upperBound.ToString()
            : null;

    private bool TryAppendSmallIntegralRangeConsume(StringBuilder builder,string sourceName,string keyName,string consumerName) {
        const int MaxRangeCount = 65536;
        var keyType=FuncReturnTypeName(keySelector);
        if(!IsDefaultComparer(comparer)
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
            {{this.FuncAssign(keySelector,nameof(keySelector),keyName,"value")}}
            var {{seenIndexName}}={{keyName}}-({{min}});
            if(!{{seenName}}[{{seenIndexName}}]){
                {{seenName}}[{{seenIndexName}}]=true;
                {{consumerName}}.Consume(value);
            }
            """,source.CanTryProvideSpan);
        return true;
    }

    private string CreateLookupBody(string keyType) =>
        this.KeyCapacityHint is { } capacityHint
            ? $"return new global::System.Collections.Generic.HashSet<{keyType}>({capacityHint},comparer);"
            : $$"""
              if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                  return new global::System.Collections.Generic.HashSet<{{keyType}}>({{this.count}},comparer);
              }else{
                  return new global::System.Collections.Generic.HashSet<{{keyType}}>(comparer);
              }
              """;

    private bool CanMergeWith(DistinctByBase previous) {
        if(this.KeySelector.ContainsAnchor
            || previous.KeySelector.ContainsAnchor
            || !string.Equals(this.Comparer,previous.Comparer,StringComparison.Ordinal)
            || this.KeySelector.RequiresStatementInlining
            || previous.KeySelector.RequiresStatementInlining
            || this.KeySelector.ParameterNames.Length!=previous.KeySelector.ParameterNames.Length
            || this.KeySelector.ParameterTypeNames.Length!=previous.KeySelector.ParameterTypeNames.Length
            || this.KeySelector.ReturnTypeName!=previous.KeySelector.ReturnTypeName) {
            return false;
        }
        for(var index=0; index<this.KeySelector.ParameterTypeNames.Length; index++) {
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

    private static bool IsDefaultComparer(string comparer) =>
        string.Equals(comparer,"null",StringComparison.Ordinal);

    private static bool IsInt32Type(string typeName) =>
        string.Equals(typeName,"int",StringComparison.Ordinal)
        || string.Equals(typeName,"System.Int32",StringComparison.Ordinal)
        || string.Equals(typeName,"global::System.Int32",StringComparison.Ordinal);
}
