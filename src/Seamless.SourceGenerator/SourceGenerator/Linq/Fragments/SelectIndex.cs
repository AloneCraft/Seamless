namespace Seamless.SourceGenerator.Linq.Fragments;

public class Select_index(int 番号, Enumerable source, FuncFragment selector) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(selector,nameof(selector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(selector)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(selector)}";
    public override string GetEnumeratorFields => $$"""
        private {{ElementType}} {{currentValue}} = default!;
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            {{a}}++;
            {{FuncAssign(selector,nameof(selector),currentValue,item,a)}}
            current = {{currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => FuncReturnTypeName(selector);
}
