namespace Seamless.SourceGenerator.Linq.Fragments;
public class GroupJoin(int 番号, Enumerable outer, Enumerable inner, FuncFragment outerKeySelector, FuncFragment innerKeySelector, FuncFragment resultSelector, string comparer) : Enumerable(番号) {
    private bool UseResultTypeParam => resultSelector.CanInvokeAsDelegate;
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
    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({outer.GetEnumeratorExpression}, {inner.GetEnumeratorExpression}, {comparer}{ClosureArgsSuffix(outerKeySelector,innerKeySelector,resultSelector)})"
            : base.GetEnumerableExpression;
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(outerKeySelector,innerKeySelector,resultSelector)}}
        private readonly global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> {{lookup}};
        private TOuterEnum {{e}};
        private int {{state}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            TOuterEnum outer,
            TInnerEnum inner,
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer{{ClosureMethodParameterSuffix(outerKeySelector,innerKeySelector,resultSelector)}}) {
            {{ClosureFieldAssignments(outerKeySelector,innerKeySelector,resultSelector)}}
            {{lookup}} = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            if(inner.TryGetSpan(out var __span)) {
                foreach(var {{item}} in __span){
                    {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                    {{FuncAssign(innerKeySelector,nameof(innerKeySelector),key,item)}}
                    if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                        {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                        {{lookup}}.Add({{key}}, {{group}});
                    }
                    {{group}}.Add({{item}});
                }
            }else{
                using var innerEnumerator = inner;
                while(innerEnumerator.MoveNext()) {
                    var {{item}} = innerEnumerator.Current;
                    {{FuncReturnTypeName(innerKeySelector)}} {{key}} = default!;
                    {{FuncAssign(innerKeySelector,nameof(innerKeySelector),key,item)}}
                    if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                        {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                        {{lookup}}.Add({{key}}, {{group}});
                    }
                    {{group}}.Add({{item}});
                }
            }
            {{e}} = outer;
            {{state}} = 0;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{state}}==1) {
            current = default!;
            return false;
        }
        if({{e}}.MoveNext()) {
            var {{outerValue}} = {{e}}.Current;
            {{FuncReturnTypeName(outerKeySelector)}} {{key}} = default!;
            {{FuncAssign(outerKeySelector,nameof(outerKeySelector),key,outerValue)}}
            if(!{{lookup}}.TryGetValue({{key}}, out var {{group}})) {
                {{group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>(0);
            }
            {{FuncAssign(resultSelector,nameof(resultSelector),$"this.{currentValue}",outerValue,group)}}
            current = this.{{currentValue}};
            return true;
        }
        {{state}} = 1;
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    protected override string GetEnumeratorDisposeBody => $"{e}.Dispose();";
}
