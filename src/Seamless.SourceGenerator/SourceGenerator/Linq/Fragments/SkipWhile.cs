namespace Seamless.SourceGenerator.Linq.Fragments;
public class SkipWhile(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        !(method.Parameters.Length > 0 &&
          method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 });
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => FuncSupport(predicate,nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{currentValue}} = default!;
        private bool skipping = true;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            bool {{temp}} = default!;
            {{FuncAssign(predicate,nameof(predicate),temp,item)}}
            if(skipping && {{temp}}) {
                continue;
            }
            skipping = false;
            {{currentValue}} = {{item}};
            current = {{currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}

public class SkipWhile_index(int 番号, Enumerable source, FuncFragment predicate) : SkipWhile(番号, source, predicate) {
    public static new bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length > 0 &&
        method.Parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 3 };
    public override string GetEnumeratorExpression =>
        $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}{ClosureArgsSuffix(predicate)})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        $"TEnum {e}{ClosurePrimaryCtorSuffix(predicate)}";
    public override string GetEnumeratorFields => $$"""
        private {{source.ElementType}} {{currentValue}} = default!;
        private bool skipping = true;
        private int {{a}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{e}}.MoveNext()) {
            var {{item}} = {{e}}.Current;
            {{a}}++;
            bool {{temp}} = default!;
            {{FuncAssign(predicate,nameof(predicate),temp,item,a)}}
            if(skipping && {{temp}}) {
                continue;
            }
            skipping = false;
            {{currentValue}} = {{item}};
            current = {{currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
}
