namespace Seamless.SourceGenerator.Linq.Fragments;
public class Where(int 番号, Enumerable source, FuncFragment predicate) : Enumerable(番号) {
    public override string StructMethod => this.DefineStruct(predicate, nameof(predicate));
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, bool> {{nameof(predicate)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{source.ElementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, bool> predicate) {
            {{this.e}} = source.GetEnumerator();
            this.{{nameof(predicate)}} = predicate;
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        if(!{{nameof(predicate)}}({{this.item}}))
            goto {{this.Skip}};
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        {{this.Skip}}:
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;

    public override string GetBeginGlobal => $$"""
        {{source.GetBeginGlobal}}
        {{var_List(this.result, this.ElementType)}}
        """;
    public override string GetBeginLoopOutside => $$"""
        {{source.GetBeginLoopOutside}}
        {{this.result}}.Clear();
        """;
    public override string GetBeginLoop => source.GetBeginLoop;
    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        if(!{{this.FuncInvoke(predicate,nameof(predicate),source.GetElement)}})
            goto {{this.Skip}};
        """;
    public override string GetEndLoopInside => $$"""
        {{Skip}}:
        {{source.GetEndLoopInside}}
        """;
    public override string GetEndLoop => source.GetEndLoop;
    public override string GetEndLoopOutside => source.GetEndLoopOutside;
    public override string GetEndGlobal => source.GetEndGlobal;
    public override string GetAdd => $$"""
        {{this.result}}.Add({{source.GetElement}});
        """;

    public override string GetElement => source.GetElement;

    public override string ElementType => source.ElementType;

    public override string MoveNext => source.MoveNext;

    public override string Result => this.result;
}

public class Where_index(int 番号, Enumerable source, FuncFragment predicate) : Where(番号, source, predicate) {
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(predicate,nameof(predicate))})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Func<{{source.ElementType}}, int, bool> {{nameof(predicate)}};
        private global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}};
        private {{source.ElementType}} {{this.currentValue}};
        private int {{this.index}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{source.ElementType}}> source, global::System.Func<{{source.ElementType}}, int, bool> predicate) {
            {{this.e}} = source.GetEnumerator();
            this.{{nameof(predicate)}} = predicate;
            {{this.currentValue}} = default!;
            {{this.index}} = -1;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        var {{this.item}} = {{this.e}}.Current;
        {{this.index}}++;
        if(!{{nameof(predicate)}}({{this.item}}, {{this.index}}))
            goto {{this.Skip}};
        {{this.currentValue}} = {{this.item}};
        current = {{this.currentValue}};
        return true;
        {{this.Skip}}:
        goto {{Start}};
        """;
    public override string GetBeginLoopOutside => $$"""
        {{base.GetBeginLoopOutside}}
        var {{this.index}}=-1;
        """;
    public override string GetBeginLoopInside => $$"""
        {{source.GetBeginLoopInside}}
        {{this.index}}++;
        if(!{{this.FuncInvoke(predicate,nameof(predicate),source.GetElement,this.index)}})
            goto {{this.Skip}};
        """;
}
