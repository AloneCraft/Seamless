namespace Seamless.SourceGenerator.Linq.Fragments;
public class Where(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public override string StructMethod => predicate.CanInlineAssign ? string.Empty : this.DefineStruct(predicate, nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(predicate)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.ClosureArgsSuffix(predicate)})"
            : predicate.CanInlineAssign && predicate.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(predicate)
            ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {this.ClosurePrimaryCtorSuffix(predicate)}"
            : predicate.CanInlineAssign && predicate.CanGenerateMethod
                ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
                : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{source.ElementType}} {{this.currentValue}} = default!;
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        if(!{{this.FuncInvoke(predicate,nameof(predicate),this.item)}})
            goto {{this.Skip}};
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        {{this.Skip}}:
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class Where_index(int 番号, Enumerable source, FuncFragment predicate) : Where(番号, source, predicate) {
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(predicate)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, @this)"
            : predicate.CanInlineAssign && predicate.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(predicate)
            ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {ContainingTypeName} @this"
            : predicate.CanInlineAssign && predicate.CanGenerateMethod
                ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
                : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, int, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private int {{this.index}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        {{this.index}}++;
        if(!{{this.FuncInvoke(predicate,nameof(predicate),this.item,this.index)}})
            goto {{this.Skip}};
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        {{this.Skip}}:
        goto {{Start}};
        """;
}
