namespace Seamless.SourceGenerator.Linq.Fragments;

// Rootは、ソース・オペランド自体が他のFragmentサブクラスによって展開されない場合に使用される、フォールバック用の列挙可能（enumerable）なフラグメントです。
// 典型的な例としては、Source()、配列変数、およびすでに列挙可能なオブジェクトを生成しているその他の式が挙げられます。
// Aggregate/Where/Selectなどの下流のフラグメントは、Rootをループ・チェーンのインライン・ベース（基底）として扱い、その上に生成された制御フローを構築します。
public class Root(int 番号,string sourceExpression,string elementType,bool useEnumerableInterface) : Enumerable(番号) {
    public override string StructMethod => string.Empty;
    public override bool CanExpandInLocalMethodBody => false;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({(useEnumerableInterface ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))" : sourceExpression)})";
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerable<{{elementType}}> {{this.result}};
        private global::System.Collections.Generic.IEnumerator<{{elementType}}> {{this.e}};
        private {{elementType}} {{this.currentValue}};
        """;
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(global::System.Collections.Generic.IEnumerable<{{elementType}}> source) {
            {{this.result}} = source;
            {{this.e}} = source.GetEnumerator();
            {{this.currentValue}} = default!;
        }
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            {{this.currentValue}} = {{this.e}}.Current;
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;

    public override string GetBeginGlobal => string.Empty;

    // 配列は GetEnumerator() が非ジェネリック IEnumerator を返すため、
    // IEnumerable<T> に寄せて Current の型を安定させる。
    // それ以外の既知コレクションは元の Enumerator を使う。
    public override string GetBeginLoopOutside => $$"""
        using var {{this.e}}={{(useEnumerableInterface ? $"((global::System.Collections.Generic.IEnumerable<{elementType}>)({sourceExpression}))" : sourceExpression)}}.GetEnumerator();
        """;

    public override string GetBeginLoop => $$"""
        goto {{this.Start}};
        {{this.Head}}:
        var {{this.item}} = {{this.e}}.Current;
        """;

    public override string GetBeginLoopInside => string.Empty;

    public override string GetEndLoopInside => string.Empty;

    public override string GetEndLoop => $$"""
        {{this.Start}}:
        if ({{this.e}}.MoveNext()) goto {{this.Head}};
        """;

    public override string GetEndLoopOutside => string.Empty;

    public override string GetEndGlobal => string.Empty;

    public override string GetAdd => $$"""
        {{result}}.Add({{this.GetElement}});
        """;

    public override string GetElement => this.item;

    public override string MoveNext => $"{this.e}.MoveNext()";

    public override string ElementType => elementType;

    public override string Result => this.GetElement;
}
