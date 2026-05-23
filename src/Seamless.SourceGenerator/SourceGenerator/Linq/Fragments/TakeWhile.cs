namespace Seamless.SourceGenerator.Linq.Fragments;

public class TakeWhile(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
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
        private bool done = false;
        """;
    public override string GetTryGetNextBody => $$"""
        if(done || !{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        if(!{{this.FuncInvoke(predicate,nameof(predicate),this.item)}}) {
            done = true;
            current = default!;
            return false;
        }
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}

public class TakeWhile_index(int 番号, Enumerable source, FuncFragment predicate) : TakeWhile(番号, source, predicate) {
    public override string GetEnumeratorExpression => predicate.CanInlineAssign
        ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
        : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorPrimaryConstructorParameters => predicate.CanInlineAssign
        ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
        : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, int, bool> predicate";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{source.ElementType}} {{this.currentValue}} = default!;
        private bool done = false;
        private int {{this.index}} = -1;
        """;
    public override string GetTryGetNextBody => $$"""
        if(done || !{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        {{this.index}}++;
        if(!{{this.FuncInvoke(predicate,nameof(predicate),this.item,this.index)}}) {
            done = true;
            current = default!;
            return false;
        }
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        """;
}
