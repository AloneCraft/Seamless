namespace Seamless.SourceGenerator.Linq.Fragments;

public class Skip(int 番号, Enumerable source, string count) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({source.GetEnumeratorExpression}, {count})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => $$"""
        private readonly int count = count < 0 ? 0 : count;
        private {{source.ElementType}} {{currentValue}} = default!;
        private bool skipped = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(!this.skipped) {
            this.skipped = true;
            var remaining = this.count;
            while(remaining > 0 && {{e}}.MoveNext()) {
                remaining--;
            }
        }
        if({{e}}.MoveNext()) {
            current = this.{{currentValue}} = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}
