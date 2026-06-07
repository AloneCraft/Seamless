namespace Seamless.SourceGenerator.Linq.Fragments;

public class Skip(int 番号, Enumerable source, string skipCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => Source is Skip skip ? skip.RootSource : Source;
    private string EffectiveSkipCount => Source is Skip skip
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skip.EffectiveSkipCount})+({SkipCountExpression}))"
        : SkipCountExpression;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveSkipCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => $$"""
        private bool skipped = false;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
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
    public override string ElementType => Source.ElementType;
}
