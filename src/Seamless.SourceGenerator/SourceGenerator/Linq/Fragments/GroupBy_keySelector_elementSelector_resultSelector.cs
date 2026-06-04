namespace Seamless.SourceGenerator.Linq.Fragments;

// GroupBy(keySelector, elementSelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    : BufferedSequence(番号, resultSelector.CanInvokeAsDelegate ? "TResult" : FuncReturnTypeName(resultSelector)) {

    // 3引数: keySelector, elementSelector(Func<TSource,TElement> 型引数2個), resultSelector(Func<TKey,IEnumerable<TElement>,TResult> 型引数3個)
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[2].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.CanInvokeAsDelegate;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);

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
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, elementSelector, resultSelector)}}));
        """;

    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))), FuncSupport(resultSelector, nameof(resultSelector))), FactorySource);

    protected override string StructSources => source.GetEnumeratorStruct;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum source{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}";

    protected override string BuildExpression =>
        $"Build(source{ClosureArgsSuffixForFactory(keySelector, elementSelector, resultSelector)})";

    protected override string BuildMembers => $$"""
        {{AggressiveInliningAttribute}}
        private static global::System.Collections.Generic.List<{{ElementType}}> Build(TEnum source{{ClosureMethodParameterSuffix(keySelector, elementSelector, resultSelector)}}) {
            var {{result}} = new global::System.Collections.Generic.List<{{ElementType}}>();
            var map = new global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>();
            var order = new global::System.Collections.Generic.List<{{KeyType}}>();
            while(source.MoveNext()) {
                var {{item}} = source.Current;
                {{KeyType}} {{key}} = default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!map.TryGetValue({{key}}, out var {{group}})) {
                    {{group}} = new global::System.Collections.Generic.List<{{ElemType}}>();
                    map.Add({{key}}, {{group}});
                    order.Add({{key}});
                }
                {{ElemType}} {{a}} = default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                {{group}}.Add({{a}});
            }
            foreach(var {{key}} in order) {
                {{ElementType}} {{temp}} = default!;
                {{FuncAssign(resultSelector,nameof(resultSelector),temp,key,$"(global::System.Collections.Generic.IEnumerable<{ElemType}>)map[{key}]")}}
                {{result}}.Add({{temp}});
            }
            return {{result}};
        }
        """;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})"
            : base.GetEnumerableExpression;
}
