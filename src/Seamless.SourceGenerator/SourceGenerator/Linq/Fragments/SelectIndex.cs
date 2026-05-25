namespace Seamless.SourceGenerator.Linq.Fragments;

public class Select_index(int 番号, Enumerable source, FuncFragment selector) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(selector)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.ClosureArgsSuffix(selector)})"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(selector)
            ? $"TEnum {this.e}, {this.ClosurePrimaryCtorSuffix(selector)}"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"TEnum {this.e}"
                : $"TEnum {this.e}, global::System.Func<{source.ElementType}, int, {this.ElementType}> selector";
    public override string GetEnumeratorFields => $$"""
        private {{this.ElementType}} {{this.currentValue}} = default!;
        private int {{this.index}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.index}}++;
            {{this.FuncAssign(selector,nameof(selector),this.currentValue,this.item,this.index)}}
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => FuncReturnTypeName(selector);
}
