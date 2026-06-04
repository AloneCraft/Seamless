namespace Seamless.SourceGenerator.Linq.Fragments;

// GroupBy(keySelector, elementSelector, resultSelector) のフラグメント。
// resultSelector が匿名型を返す場合は TResult ジェネリック型パラメータを使う。
public class GroupBy_keySelector_elementSelector_resultSelector(
    int 番号,
    Enumerable source,
    FuncFragment keySelector,
    FuncFragment elementSelector,
    FuncFragment resultSelector)
    : Enumerable(番号) {

    // 3引数: keySelector, elementSelector(Func<TSource,TElement> 型引数2個), resultSelector(Func<TKey,IEnumerable<TElement>,TResult> 型引数3個)
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 3
        && method.Parameters[2].Type is INamedTypeSymbol { TypeArguments.Length: 3 };

    protected override Enumerable PrimarySource => source;
    private bool UseResultTypeParam => resultSelector.CanInvokeAsDelegate;
    private string KeyType => FuncReturnTypeName(keySelector);
    private string ElemType => FuncReturnTypeName(elementSelector);
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
        private static global::Seamless.Linq.Carrier.ValueEnumerable<{{Struct}}<TEnum, TResult>, TResult> {{FactoryMethodName}}<TEnum, TResult>(TEnum source{{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}})
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<{{source.ElementType}}>
            => new(new {{Struct}}<TEnum, TResult>(source{{ClosureArgsSuffixForFactory(keySelector, elementSelector, resultSelector)}}));
        """;

    public override string StructMethod => JoinStaticMethods(JoinStaticMethods(JoinStaticMethods(FuncSupport(keySelector, nameof(keySelector)), FuncSupport(elementSelector, nameof(elementSelector))), FuncSupport(resultSelector, nameof(resultSelector))), FactorySource);

    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;

    public override string GetEnumeratorExpression =>
        UseResultTypeParam
            ? throw new global::System.NotSupportedException($"{GetType().Name} does not support direct GetEnumeratorExpression for anonymous result type; use GetEnumerableExpression.")
            : $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})";

    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(keySelector, elementSelector, resultSelector)}";
    public override string GetEnumeratorFields => $$"""
        {{ClosureFields(keySelector,elementSelector,resultSelector)}}
        private global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>? {{lookup}};
        private global::System.Collections.Generic.List<{{KeyType}}>? {{group}};
        private int {{a}}=-1;
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}({{GetEnumeratorPrimaryConstructorParameters}}) {
            {{ClosureFieldAssignments(keySelector,elementSelector,resultSelector)}}
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{lookup}} is null) {
            this.{{lookup}}=new global::System.Collections.Generic.Dictionary<{{KeyType}}, global::System.Collections.Generic.List<{{ElemType}}>>();
            this.{{group}}=new global::System.Collections.Generic.List<{{KeyType}}>();
            while({{e}}.MoveNext()) {
                var {{item}}={{e}}.Current;
                {{KeyType}} {{key}}=default!;
                {{FuncAssign(keySelector,nameof(keySelector),key,item)}}
                if(!this.{{lookup}}.TryGetValue({{key}},out var __group)) {
                    __group=new global::System.Collections.Generic.List<{{ElemType}}>();
                    this.{{lookup}}.Add({{key}},__group);
                    this.{{group}}.Add({{key}});
                }
                {{ElemType}} {{a}}=default!;
                {{FuncAssign(elementSelector,nameof(elementSelector),a,item)}}
                __group.Add({{a}});
            }
        }
        this.{{a}}++;
        if(this.{{group}} is not null&&(uint)this.{{a}}<(uint)this.{{group}}.Count) {
            var __key=this.{{group}}[this.{{a}}];
            {{FuncAssignCurrent(resultSelector,nameof(resultSelector),currentValue,"__key",$"(global::System.Collections.Generic.IEnumerable<{ElemType}>)this.{lookup}[__key]")}}
            return true;
        }
        current=default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;

    public override string GetEnumerableExpression =>
        UseResultTypeParam
            ? $"{FactoryMethodName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(keySelector, elementSelector, resultSelector)})"
            : base.GetEnumerableExpression;
}
