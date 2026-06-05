namespace Seamless.SourceGenerator.Linq.Fragments;

public class Take(int 番号, Enumerable source, string takeCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    private Enumerable Source => source;
    private string TakeCountExpression => takeCount;
    private Enumerable RootSource => Source is Take take ? take.RootSource : Source;
    private string EffectiveTakeCount => Source is Take take
        ? $"global::System.Math.Min({take.EffectiveTakeCount},{TakeCountExpression})"
        : TakeCountExpression;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveTakeCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => $$"""
        private readonly int count = count < 0 ? 0 : count;
        private int {{a}} = 0;
        """;
    public override string GetTryGetNextBody => $$"""
        if(this.{{a}} >= this.count) {
            current = default!;
            return false;
        }
        if({{e}}.MoveNext()) {
            this.{{a}}++;
            current = this.{{currentValue}} = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => Source.ElementType;
}
