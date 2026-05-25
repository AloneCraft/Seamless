namespace Seamless.SourceGenerator.Linq.Fragments;

public class Take(int 番号, Enumerable source, string count) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {count})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, int count";
    public override string GetEnumeratorFields => $$"""
        private readonly int count = count < 0 ? 0 : count;
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private int {{this.index}} = 0;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.index}} >= count) {
            current = default!;
            return false;
        }
        if({{this.e}}.MoveNext()) {
            {{this.index}}++;
            {{this.currentValue}} = {{this.e}}.Current;
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}
