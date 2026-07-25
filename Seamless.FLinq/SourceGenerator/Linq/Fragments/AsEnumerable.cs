namespace Seamless.FLinq.Linq.Fragments;

public class AsEnumerable(int 番号,Enumerable source):Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string? DirectEnumerableExpression => source.DirectEnumerableExpression;
    public override bool CanProvideSpan => source.CanProvideSpan;
    public override bool CanTryProvideSpan => source.CanTryProvideSpan;
    public override string? NonEnumeratedCountExpression => source.NonEnumeratedCountExpression;
    public override bool CountPreserving => source.CountPreserving;
    public override string GetEnumeratorFields => string.Empty;
    protected override string GetTryGetNonEnumeratedCountBody => $"return this.{this.e}.TryGetNonEnumeratedCount(out count);";
    protected override string GetTryGetSpanBody => $"return this.{this.e}.TryGetSpan(out span);";
    protected override string GetTryCopyToBody => $"return this.{this.e}.TryCopyTo(destination,__offset);";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var __mn6)) {
            current = this.{{this.currentValue}} = __mn6;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string ElementType => source.ElementType;
}
