namespace Seamless.SourceGenerator.Linq.Fragments;
public class Select(int 番号,Enumerable source,FuncFragment selector):Enumerable(番号){
    // static ラムダなら selector helper を struct として定義する。
    public override string StructMethod => selector.CanInlineAssign ? string.Empty : this.DefineStruct(selector,nameof(selector));
    // source 側 enumerator と Select 自身の enumerator をまとめて出す。
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // Carrier/Enumerator モードでは source enumerable と selector delegate を渡す。
    // NeedsStructClosure: 非 static ラムダ + ContainingTypeName あり → @this を primary ctor で渡してインライン展開。
    // CanInlineAssign && CanGenerateMethod: static ラムダ → source のみ。
    // それ以外 (ParameterFunc または非 static ラムダで closure なし): デリゲートを ctor に渡す。
    public override string GetEnumeratorExpression =>
        NeedsStructClosure(selector)
            ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.ClosureArgsSuffix(selector)})"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression})"
                : $"new {this.EnumeratorTypeName}({source.GetEnumerableExpression}, {this.FuncDelegate(selector,nameof(selector))})";
    public override string GetEnumeratorPrimaryConstructorParameters =>
        NeedsStructClosure(selector)
            ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, {this.ClosurePrimaryCtorSuffix(selector)}"
            : selector.CanInlineAssign && selector.CanGenerateMethod
                ? $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source"
                : $"global::System.Collections.Generic.IEnumerable<{source.ElementType}> source, global::System.Func<{source.ElementType}, {this.ElementType}> selector";
    // selector と source enumerator、現在値を保持する。
    public override string GetEnumeratorFields => $$"""
        private readonly global::System.Collections.Generic.IEnumerator<{{source.ElementType}}> {{this.e}} = source.GetEnumerator();
        private {{this.ElementType}} {{this.currentValue}} = default!;
        """;
    // 1 要素読むごとに selector を適用して TResult を流す。
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            var {{this.item}} = {{this.e}}.Current;
            {{this.AssignSelectedValue}}
            current = {{this.currentValue}};
            return true;
        }
        current = default!;
        return false;
        """;
    // Current は selector 適用後の値。
    public override string GetCurrentExpression => this.currentValue;
    private string AssignSelectedValue => this.FuncAssign(selector,nameof(selector),this.currentValue,this.item);
    // Select の要素型は selector 戻り値型。
    public override string ElementType => FuncReturnTypeName(selector);
}
