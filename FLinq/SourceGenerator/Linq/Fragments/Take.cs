namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Take(int 番号, Enumerable source, string takeCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    private Enumerable Source => source;
    private string TakeCountExpression => takeCount;
    private Enumerable RootSource => this.Source is Take take ? take.RootSource : this.Source;
    private string EffectiveTakeCount => this.Source is Take take
        ? $"global::System.Math.Min({take.EffectiveTakeCount},{this.TakeCountExpression})"
        : this.TakeCountExpression;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveTakeCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}} = 0;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.a}} >= this.count) {
            current = default!;
            return false;
        }
        if({{this.e}}.MoveNext()) {
            this.{{this.a}}++;
            current = this.{{this.currentValue}} = {{this.e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.Source.ElementType;
}
