namespace Seamless.SourceGenerator.Linq.Fragments;

// GroupBy(keySelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {

    // resultSelector は Func<TKey, IEnumerable<TSource>, TResult>（3引数）で識別する。
    // elementSelector Func<TSource, TElement>（2引数）は除外。
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 2
        && method.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.CanInvokeAsDelegate;
    public override string ElementType => UseResultTypeParam ? "TResult" : FuncReturnTypeName(resultSelector);

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

    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,resultSelector)}}
        private global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>? {{lookup}};
        private global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}({{GetEnumeratorPrimaryConstructorParameters}}) {
            {{ClosureFieldAssignments(keySelector,resultSelector)}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(keySelector)}}, global::System.Collections.Generic.List<{{source.ElementType}}>>();
            this.{{group}}=new global::System.Collections.Generic.List<{{FuncReturnTypeName(keySelector)}}>();
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{FuncReturnTypeName(keySelector)}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new global::System.Collections.Generic.List<{{source.ElementType}}>();
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add({{key}});
                }
                __group.Add({{item}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__key",$"(global::System.Collections.Generic.IEnumerable<{source.ElementType}>)this.{lookup}[__key]")}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, resultSelector)})"
            : base.GetEnumerableExpression;
}
