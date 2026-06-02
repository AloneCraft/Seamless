namespace Seamless.SourceGenerator.Linq.Fragments;

// GroupBy(keySelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    : BufferedSequence(番号, resultSelector.CanInvokeAsDelegate ? "TResult" : FuncReturnTypeName(resultSelector)) {

    // resultSelector は Func<TKey, IEnumerable<TSource>, TResult>（3引数）で識別する。
    // elementSelector Func<TSource, TElement>（2引数）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.CanInvokeAsDelegate;

    protected override string StructTypeParameters =>
        UseResultTypeParam ? "<TEnum, TResult>" : base.StructTypeParameters;

    protected override string StructTypeConstraints =>
        UseResultTypeParam
            ? $"where TEnum : struct, global::System.Collections.Generic.IEnumerator<{source.ElementType}>"
            : base.StructTypeConstraints;

    public override string EnumeratorTypeName =>
        UseResultTypeParam ? $"{Struct}<{source.EnumeratorTypeName}, TResult>" : base.EnumeratorTypeName;

    private string FactorySource => !UseResultTypeParam ? string.Empty : $$"""
        {{AggressiveInliningAttribute}}
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, resultSelector)}}));
        """;

    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(resultSelector, nameof(resultSelector))), FactorySource);

    protected override string StructSources => source.GetEnumeratorStruct;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}";

    protected override string BuildExpression =>
        $"Build(source{ClosureArgsSuffixForFactory(keySelector, resultSelector)})";

    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{ElementType}}> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector, resultSelector)}}) {
            var {{result}} = new global::System.Collections.Generic.List<{{ElementType}}>();
            var map = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>();
            var order = new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{source.ElementType}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{group}}.Add({{item}});
            }
            foreach(var {{key}} in order) {
                {{ElementType}} {{temp}} = default!;
                {{FuncAssign(resultSelector,nameof(resultSelector),temp,key,$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)map[{key}]")}}
                {{result}}.Add({{temp}});
            }
            return {{result}};
        }
        """;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})"
            : base.GetEnumerableExpression;
}
