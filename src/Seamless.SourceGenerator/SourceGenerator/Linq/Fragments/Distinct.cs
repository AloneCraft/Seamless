namespace Seamless.SourceGenerator.Linq.Fragments;

internal class Distinct(int 番号,Enumerable source):Distinct_comparer(番号,source,"null");

internal class Distinct_comparer(int 番号,Enumerable source,string comparer):DistinctBase(番号,source,comparer);

internal abstract class DistinctBase(int 番号,Enumerable source,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=>string.Empty;
    public override string GetEnumerableExpression=>$"global::Seamless.Carrier.ValueEnumerableExtensions.Distinct({source.GetEnumerableExpression},{comparer})";
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
    public string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,source.ElementType,sourceName,comparer);
        TraversalSupport.AppendTraversalLoop(builder,sourceName,source.ElementType,"value",$$"""
            if({{this.lookup}}.Add(value)){
                {{consumerName}}.Consume(value);
            }
            """);
        return builder.ToString().TrimEnd();
    }
}

internal abstract class DistinctByBase(int 番号,Enumerable source,FuncFragment keySelector,string comparer):Enumerable(番号),ConsumeDirectExecutionFragment{
    protected override Enumerable PrimarySource=>source;
    public override string StructMethod=> this.FuncSupport(keySelector,nameof(keySelector));
    public override string GetEnumerableExpression=>$"global::Seamless.Carrier.ValueEnumerableExtensions.DistinctBy({source.GetEnumerableExpression},{keySelector.DelegateConstructorArg??keySelector.RenderAsLambda()},{comparer})";
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
            if(source.TryGetNonEnumeratedCount(out var {{this.count}})){
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>({{this.count}},comparer);
            }else{
                return new global::System.Collections.Generic.HashSet<{{FuncReturnTypeName(keySelector)}}>(comparer);
            }
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
    public string GetConsumeExecutionStatement(string consumerName){
        var sourceName=$"__src{番号}";
        var keyName=$"__key{番号}";
        var builder=new StringBuilder();
        builder.AppendLine($"using var {sourceName}={source.GetEnumeratorExpression};");
        TraversalSupport.AppendHashSetInitialization(builder,this.lookup,FuncReturnTypeName(keySelector),sourceName,comparer);
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
            """);
        return builder.ToString().TrimEnd();
    }
}
