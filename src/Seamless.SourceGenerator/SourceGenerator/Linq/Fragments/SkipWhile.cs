namespace Seamless.SourceGenerator.Linq.Fragments;
public class SkipWhile(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public override string StructMethod => predicate.CanInlineAssign ? string.Empty : this.DefineStruct(predicate, nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool skipping = true;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            if(skipping && {{this.FuncInvoke(predicate,nameof(predicate),this.item)}}) {
                continue;
            }
            skipping = false;
            {{this.currentValue}} = {{this.item}};
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class SkipWhile_index(int 番号, Enumerable source, FuncFragment predicate) : SkipWhile(番号, source, predicate) {
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, int, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool skipping = true;
        private int {{this.index}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        while({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.index}}++;
            if(skipping && {{this.FuncInvoke(predicate,nameof(predicate),this.item,this.index)}}) {
                continue;
            }
            skipping = false;
            {{this.currentValue}} = {{this.item}};
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
}
