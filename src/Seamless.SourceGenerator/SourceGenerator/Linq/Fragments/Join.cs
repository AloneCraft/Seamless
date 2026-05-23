namespace Seamless.SourceGenerator.Linq.Fragments;
public class Join(int 番号,Enumerable outer,Enumerable inner,FuncFragment outerKeySelector,FuncFragment innerKeySelector,FuncFragment resultSelector,string comparer):Enumerable(番号) {
    // CanInlineAssign=true のラムダはインライン化するので DefineStruct は不要。
    // ParameterFunc(CanInlineAssign=false) は CanGenerateMethod=false のため DefineStruct は空を返す。
    // 結果として StructMethod は常に空だが、他のフラグメントとの一貫性のため条件式を維持する。
    public override string StructMethod => $$"""
                                             {{(outerKeySelector.CanInlineAssign ? string.Empty : this.DefineStruct(outerKeySelector,nameof(outerKeySelector)))}}
                                             {{(innerKeySelector.CanInlineAssign ? string.Empty : this.DefineStruct(innerKeySelector,nameof(innerKeySelector)))}}
                                             {{(resultSelector.CanInlineAssign ? string.Empty : this.DefineStruct(resultSelector,nameof(resultSelector)))}}
                                             """;
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // --- Enumerator struct パス (ストリーミング用) ---
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({outer.GetEnumerableExpression}, {inner.GetEnumerableExpression}" +
        (!outerKeySelector.CanGenerateMethod ? $", {this.FuncDelegate(outerKeySelector,nameof(outerKeySelector))}" : "") +
        (!innerKeySelector.CanGenerateMethod ? $", {this.FuncDelegate(innerKeySelector,nameof(innerKeySelector))}" : "") +
        (!resultSelector.CanGenerateMethod ? $", {this.FuncDelegate(resultSelector,nameof(resultSelector))}" : "") +
        $", {comparer})";
    public override string GetEnumeratorFields => $$"""
        {{(!outerKeySelector.CanGenerateMethod ? $"private readonly global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)};" : "")}}
        {{(!innerKeySelector.CanGenerateMethod ? $"private readonly global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)};" : "")}}
        {{(!resultSelector.CanGenerateMethod ? $"private readonly global::System.Func<{outer.ElementType}, {inner.ElementType}, {this.ElementType}> {nameof(resultSelector)};" : "")}}
        private readonly global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}> {{this.lookup}};
        private readonly global::System.Collections.Generic.IEnumerator<{{outer.ElementType}}> {{this.e}};
        private global::System.Collections.Generic.List<{{inner.ElementType}}>? {{this.group}};
        private int {{this.groupIndex}};
        private int {{this.state}};
        private {{outer.ElementType}} {{this.outerValue}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    private string OKSConstructorParam => outerKeySelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)}";
    private string IKSConstructorParam => innerKeySelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)}";
    private string RSConstructorParam => resultSelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{outer.ElementType}, {inner.ElementType}, {this.ElementType}> {nameof(resultSelector)}";
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            global::System.Collections.Generic.IEnumerable<{{outer.ElementType}}> outer,
            global::System.Collections.Generic.IEnumerable<{{inner.ElementType}}> inner{{OKSConstructorParam}}{{IKSConstructorParam}}{{RSConstructorParam}},
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer) {
            {{(!outerKeySelector.CanGenerateMethod ? $"this.{nameof(outerKeySelector)} = {nameof(outerKeySelector)};" : "")}}
            {{(!innerKeySelector.CanGenerateMethod ? $"this.{nameof(innerKeySelector)} = {nameof(innerKeySelector)};" : "")}}
            {{(!resultSelector.CanGenerateMethod ? $"this.{nameof(resultSelector)} = {nameof(resultSelector)};" : "")}}
            {{this.lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            foreach(var {{this.item}} in inner) {
                {{this.lookup}}.Add({{this.FuncInvokeField(innerKeySelector,nameof(innerKeySelector),this.item)}}, {{this.item}});
            }
            {{this.e}} = outer.GetEnumerator();
            {{this.group}} = null;
            {{this.groupIndex}} = 0;
            {{this.state}} = 0;
            {{this.outerValue}} = default!;
            {{this.currentValue}} = default!;
        }
        """;
    // FuncInvokeField: static ラムダ→インライン、非 static ラムダ・ParameterFunc→フィールド呼び出し
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if({{this.state}}==1 && {{this.group}} is not null) {
            if({{this.groupIndex}}<{{this.group}}.Count) {
                var {{this.item}} = {{this.group}}[{{this.groupIndex}}++];
                {{this.currentValue}} = {{this.FuncInvokeField(resultSelector,nameof(resultSelector),this.outerValue,this.item)}};
                current = {{this.currentValue}};
                return true;
            }
            {{this.group}} = null;
            {{this.state}} = 0;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.outerValue}} = {{this.e}}.Current;
        {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>({{this.lookup}}[{{this.FuncInvokeField(outerKeySelector,nameof(outerKeySelector),this.outerValue)}}]);
        {{this.groupIndex}} = 0;
        {{this.state}} = 1;
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string ElementType => FuncReturnTypeName(resultSelector);

    // --- フラットループパス (末尾メソッド用) ---
    // 末尾メソッドのスコープに展開されるため @this が使用可能 → 全ラムダをインライン化できる。
    public override bool SupportsFlatLoop => true;
    public override string GetFlatLoopCode(string elementVariable,string bodyCode) => $$"""
        var {{this.lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, {{inner.ElementType}}>({{comparer}} ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
        foreach(var {{this.item}} in {{inner.GetEnumerableExpression}}) {
            {{this.lookup}}.Add({{this.FuncInvokeFlat(innerKeySelector,nameof(innerKeySelector),this.item)}}, {{this.item}});
        }
        foreach(var {{this.outerValue}} in {{outer.GetEnumerableExpression}}) {
            var {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>({{this.lookup}}[{{this.FuncInvokeFlat(outerKeySelector,nameof(outerKeySelector),this.outerValue)}}]);
            for(var {{this.groupIndex}} = 0; {{this.groupIndex}} < {{this.group}}.Count; {{this.groupIndex}}++) {
                var {{elementVariable}} = {{this.FuncInvokeFlat(resultSelector,nameof(resultSelector),this.outerValue,$"{this.group}[{this.groupIndex}]")}};
                {{bodyCode}}
            }
        }
        """;
}
