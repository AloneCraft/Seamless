namespace Seamless.SourceGenerator.Linq.Fragments;
public class GroupJoin(int 番号, Enumerable outer, Enumerable inner, FuncFragment outerKeySelector, FuncFragment innerKeySelector, FuncFragment resultSelector, string comparer) : Enumerable(番号) {
    public override string StructMethod => $$"""
                                             {{(outerKeySelector.CanInlineAssign ? string.Empty : this.DefineStruct(outerKeySelector, nameof(outerKeySelector)))}}
                                             {{(innerKeySelector.CanInlineAssign ? string.Empty : this.DefineStruct(innerKeySelector, nameof(innerKeySelector)))}}
                                             {{(resultSelector.CanInlineAssign ? string.Empty : this.DefineStruct(resultSelector, nameof(resultSelector)))}}
                                             """;
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // --- Enumerator struct パス (ストリーミング用) ---
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({outer.GetEnumerableExpression}, {inner.GetEnumerableExpression}" +
        (!outerKeySelector.CanGenerateMethod ? $", {this.FuncDelegate(outerKeySelector, nameof(outerKeySelector))}" : "") +
        (!innerKeySelector.CanGenerateMethod ? $", {this.FuncDelegate(innerKeySelector, nameof(innerKeySelector))}" : "") +
        (!resultSelector.CanGenerateMethod ? $", {this.FuncDelegate(resultSelector, nameof(resultSelector))}" : "") +
        $", {comparer})";
    public override string GetEnumeratorFields => $$"""
        {{(!outerKeySelector.CanGenerateMethod ? $"private readonly global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)};" : "")}}
        {{(!innerKeySelector.CanGenerateMethod ? $"private readonly global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)};" : "")}}
        {{(!resultSelector.CanGenerateMethod ? $"private readonly global::System.Func<{outer.ElementType}, global::System.Collections.Generic.IEnumerable<{inner.ElementType}>, {this.ElementType}> {nameof(resultSelector)};" : "")}}
        private readonly global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> {{this.lookup}};
        private readonly global::System.Collections.Generic.IEnumerator<{{outer.ElementType}}> {{this.e}};
        private int {{this.state}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    private string OKSConstructorParam => outerKeySelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)}";
    private string IKSConstructorParam => innerKeySelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)}";
    private string RSConstructorParam => resultSelector.CanGenerateMethod ? ""
        : $",\n            global::System.Func<{outer.ElementType}, global::System.Collections.Generic.IEnumerable<{inner.ElementType}>, {this.ElementType}> {nameof(resultSelector)}";
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            global::System.Collections.Generic.IEnumerable<{{outer.ElementType}}> outer,
            global::System.Collections.Generic.IEnumerable<{{inner.ElementType}}> inner{{OKSConstructorParam}}{{IKSConstructorParam}}{{RSConstructorParam}},
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer) {
            {{(!outerKeySelector.CanGenerateMethod ? $"this.{nameof(outerKeySelector)} = {nameof(outerKeySelector)};" : "")}}
            {{(!innerKeySelector.CanGenerateMethod ? $"this.{nameof(innerKeySelector)} = {nameof(innerKeySelector)};" : "")}}
            {{(!resultSelector.CanGenerateMethod ? $"this.{nameof(resultSelector)} = {nameof(resultSelector)};" : "")}}
            {{this.lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            foreach(var {{this.item}} in inner) {
                var {{this.key}} = {{this.FuncInvokeField(innerKeySelector, nameof(innerKeySelector), this.item)}};
                if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                    {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{this.lookup}}.Add({{this.key}}, {{this.group}});
                }
                {{this.group}}.Add({{this.item}});
            }
            {{this.e}} = outer.GetEnumerator();
            {{this.state}} = 0;
            {{this.currentValue}} = default!;
        }
        """;
    // FuncInvokeField: static ラムダ→インライン、非 static ラムダ・ParameterFunc→フィールド呼び出し
    public override string GetTryGetNextBody => $$"""
        if({{this.state}}==1) {
            current = default!;
            return false;
        }
        if({{this.e}}.MoveNext()) {
            var {{this.outerValue}} = {{this.e}}.Current;
            if(!{{this.lookup}}.TryGetValue({{this.FuncInvokeField(outerKeySelector, nameof(outerKeySelector), this.outerValue)}}, out var {{this.group}})) {
                {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
            }
            {{this.currentValue}} = {{this.FuncInvokeField(resultSelector, nameof(resultSelector), this.outerValue, this.group)}};
            current = {{this.currentValue}};
            return true;
        }
        {{this.state}} = 1;
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string ElementType => FuncReturnTypeName(resultSelector);

    // --- フラットループパス (末尾メソッド用) ---
    public override bool SupportsFlatLoop => true;
    public override string GetFlatLoopCode(string elementVariable,string bodyCode) => $$"""
        var {{this.lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>({{comparer}} ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
        foreach(var {{this.item}} in {{inner.GetEnumerableExpression}}) {
            var {{this.key}} = {{this.FuncInvokeFlat(innerKeySelector, nameof(innerKeySelector), this.item)}};
            if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                {{this.lookup}}.Add({{this.key}}, {{this.group}});
            }
            {{this.group}}.Add({{this.item}});
        }
        foreach(var {{this.outerValue}} in {{outer.GetEnumerableExpression}}) {
            if(!{{this.lookup}}.TryGetValue({{this.FuncInvokeFlat(outerKeySelector, nameof(outerKeySelector), this.outerValue)}}, out var {{this.group}})) {
                {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
            }
            var {{elementVariable}} = {{this.FuncInvokeFlat(resultSelector, nameof(resultSelector), this.outerValue, this.group)}};
            {{bodyCode}}
        }
        """;
}
