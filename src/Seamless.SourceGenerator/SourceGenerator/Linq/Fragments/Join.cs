namespace Seamless.SourceGenerator.Linq.Fragments;
public class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号) {
    private bool UseResultTypeParam => resultSelector.RequiresResultTypeParameter;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);
    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TOuterEnum, TInnerEnum, TResult>" : "<TOuterEnum, TInnerEnum>";
    protected override string StructTypeConstraints =>
        $"where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{outer.ElementType}>\n    where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{inner.ElementType}>";
    public override string EnumeratorTypeName =>
        UseResultTypeParam
            ? $"{Struct}<{outer.EnumeratorTypeName}, {inner.EnumeratorTypeName}, TResult>"
            : $"{Struct}<{outer.EnumeratorTypeName}, {inner.EnumeratorTypeName}>";
    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TOuterEnum, TInnerEnum, TResult>, TResult> {{FactoryMethodName}}<TOuterEnum, TInnerEnum, TResult>(TOuterEnum outer, TInnerEnum inner, global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer{{ClosurePrimaryCtorSuffix(outerKeySelector,innerKeySelector,resultSelector)}})
            where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{{outer.ElementType}}>
            where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{{inner.ElementType}}>
            => new(new {{Struct}}<TOuterEnum, TInnerEnum, TResult>(outer, inner, comparer{{ClosureArgsSuffixForFactory(outerKeySelector,innerKeySelector,resultSelector)}}));
        """;
    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(
        FuncSupport(outerKeySelector,nameof(outerKeySelector)),
        FuncSupport(innerKeySelector,nameof(innerKeySelector))),
        FuncSupport(resultSelector,nameof(resultSelector))),
        FactorySource);
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({outer.GetEnumeratorExpression}, {inner.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(outerKeySelector,innerKeySelector,resultSelector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TOuterEnum outer,TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{FuncReturnTypeName(innerKeySelector)}>? comparer{ClosurePrimaryCtorSuffix(outerKeySelector,innerKeySelector,resultSelector)}";
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({outer.GetEnumeratorExpression}, {inner.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(outerKeySelector,innerKeySelector,resultSelector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(outerKeySelector,innerKeySelector,resultSelector)}}
        private readonly global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> {{lookup}}=BuildLookup(inner,comparer{{ClosureArgsSuffixForFactory(outerKeySelector,innerKeySelector,resultSelector)}});
        private TOuterEnum {{e}}=outer;
        private global::System.Collections.Generic.List<{{inner.ElementType}}>? {{group}};
        private int {{groupIndex}};
        private int {{state}};
        private {{outer.ElementType}} {{outerValue}};
        private static bool IsNullKey<T>(T key) {
            return key is null;
        }
        private static global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer{{ClosureMethodParameterSuffix(outerKeySelector,innerKeySelector,resultSelector)}}) {
            var {{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            using var {{innerEnumerator}}=inner;
            while({{innerEnumerator}}.MoveNext()) {
                var {{item}}={{innerEnumerator}}.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{key}}=default!;
                {{FuncAssign(innerKeySelector,nameof(innerKeySelector),key,item)}}
                if(IsNullKey({{key}})) {
                    continue;
                }
                if(!{{lookup}}.TryGetValue({{key}},out var {{group}})) {
                    {{group}}=new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{lookup}}.Add({{key}},{{group}});
                }
                {{group}}.Add({{item}});
            }
            return {{lookup}};
        }
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if({{state}}==1 && {{group}} is not null) {
            if({{groupIndex}}<{{group}}.Count) {
                var {{item}} = {{group}}[{{groupIndex}}++];
                {{FuncAssignCurrent(resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,item)}}
                return true;
            }
            {{group}} = null;
            {{state}} = 0;
        }
        if(!{{e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{outerValue}} = {{e}}.Current;
        {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
        {{FuncAssign(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
        if(IsNullKey({{key}})) {
            goto {{Start}};
        }
        if(!{{lookup}}.TryGetValue({{key}},out {{group}})) {
            goto {{Start}};
        }
        {{groupIndex}} = 0;
        {{state}} = 1;
        goto {{Start}};
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
