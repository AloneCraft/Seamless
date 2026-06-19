namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Skip(int 番号, Enumerable source, string skipCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => this.Source is Skip skip ? skip.RootSource : this.Source;
    private string EffectiveSkipCount => this.Source is Skip skip
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skip.EffectiveSkipCount})+({this.SkipCountExpression}))"
        : this.SkipCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveSkipCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private bool skipped = false;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(!this.skipped) {
            this.skipped = true;
            var remaining = this.count;
            while(remaining > 0 && {{this.e}}.MoveNext()) {
                remaining--;
            }
        }
        if({{this.e}}.MoveNext()) {
            current = this.{{this.currentValue}} = {{this.e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.Source.ElementType;
}
