namespace Seamless.SourceGenerator.Linq.Fragments;
public class GroupJoin(int 番号, Enumerable outer, Enumerable inner, FuncFragment outerKeySelector, FuncFragment innerKeySelector, FuncFragment resultSelector, string comparer) : Enumerable(番号) {
    protected override string StructTypeParameters => "<TOuterEnum, TInnerEnum>";
    protected override string StructTypeConstraints =>
        $"where TOuterEnum : struct, global::System.Collections.Generic.IEnumerator<{outer.ElementType}>\n    where TInnerEnum : struct, global::System.Collections.Generic.IEnumerator<{inner.ElementType}>";
    public override string EnumeratorTypeName => $"{Struct}<{outer.EnumeratorTypeName}, {inner.EnumeratorTypeName}>";
    public override string StructMethod => $$"""
                                             {{this.FuncSupport(outerKeySelector,nameof(outerKeySelector))}}
                                             {{this.FuncSupport(innerKeySelector,nameof(innerKeySelector))}}
                                             {{this.FuncSupport(resultSelector,nameof(resultSelector))}}
                                             """;
    public override string GetEnumeratorStruct => $$"""
        {{outer.GetEnumeratorStruct}}
        {{inner.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    // --- Enumerator struct パス (ストリーミング用) ---
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({outer.GetEnumeratorExpression}, {inner.GetEnumeratorExpression}" +
        (!outerKeySelector.CanGenerateMethod && !outerKeySelector.RequiresStatementInlining ? $", {this.FuncDelegate(outerKeySelector, nameof(outerKeySelector))}" : "") +
        (!innerKeySelector.CanGenerateMethod && !innerKeySelector.RequiresStatementInlining ? $", {this.FuncDelegate(innerKeySelector, nameof(innerKeySelector))}" : "") +
        (!resultSelector.CanGenerateMethod && !resultSelector.RequiresStatementInlining ? $", {this.FuncDelegate(resultSelector, nameof(resultSelector))}" : "") +
        $", {comparer})";
    public override string GetEnumeratorFields => $$"""
        {{(!outerKeySelector.CanGenerateMethod && !outerKeySelector.RequiresStatementInlining ? $"private readonly global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)};" : "")}}
        {{(!innerKeySelector.CanGenerateMethod && !innerKeySelector.RequiresStatementInlining ? $"private readonly global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)};" : "")}}
        {{(!resultSelector.CanGenerateMethod && !resultSelector.RequiresStatementInlining ? $"private readonly global::System.Func<{outer.ElementType}, global::System.Collections.Generic.IEnumerable<{inner.ElementType}>, {this.ElementType}> {nameof(resultSelector)};" : "")}}
        private readonly global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>> {{this.lookup}};
        private TOuterEnum {{this.e}};
        private int {{this.state}};
        private {{this.ElementType}} {{this.currentValue}};
        """;
    private string OKSConstructorParam => outerKeySelector.CanGenerateMethod || outerKeySelector.RequiresStatementInlining ? ""
        : $",\n            global::System.Func<{outer.ElementType}, {FuncReturnTypeName(outerKeySelector)}> {nameof(outerKeySelector)}";
    private string IKSConstructorParam => innerKeySelector.CanGenerateMethod || innerKeySelector.RequiresStatementInlining ? ""
        : $",\n            global::System.Func<{inner.ElementType}, {FuncReturnTypeName(innerKeySelector)}> {nameof(innerKeySelector)}";
    private string RSConstructorParam => resultSelector.CanGenerateMethod || resultSelector.RequiresStatementInlining ? ""
        : $",\n            global::System.Func<{outer.ElementType}, global::System.Collections.Generic.IEnumerable<{inner.ElementType}>, {this.ElementType}> {nameof(resultSelector)}";
    public override string GetEnumeratorConstructor => $$"""
        public {{Struct}}(
            TOuterEnum outer,
            TInnerEnum inner{{OKSConstructorParam}}{{IKSConstructorParam}}{{RSConstructorParam}},
            global::System.Collections.Generic.IEqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>? comparer) {
            {{(!outerKeySelector.CanGenerateMethod && !outerKeySelector.RequiresStatementInlining ? $"this.{nameof(outerKeySelector)} = {nameof(outerKeySelector)};" : "")}}
            {{(!innerKeySelector.CanGenerateMethod && !innerKeySelector.RequiresStatementInlining ? $"this.{nameof(innerKeySelector)} = {nameof(innerKeySelector)};" : "")}}
            {{(!resultSelector.CanGenerateMethod && !resultSelector.RequiresStatementInlining ? $"this.{nameof(resultSelector)} = {nameof(resultSelector)};" : "")}}
            {{this.lookup}} = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>(comparer ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
            using var innerEnumerator = inner;
            while(innerEnumerator.MoveNext()) {
                var {{this.item}} = innerEnumerator.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{this.key}} = default!;
                {{this.FuncAssign(innerKeySelector,nameof(innerKeySelector),this.key,this.item)}}
                if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                    {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{this.lookup}}.Add({{this.key}}, {{this.group}});
                }
                {{this.group}}.Add({{this.item}});
            }
            {{this.e}} = outer;
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
            {{FuncReturnTypeName(outerKeySelector)}} {{this.key}} = default!;
            {{this.FuncAssign(outerKeySelector,nameof(outerKeySelector),this.key,this.outerValue)}}
            if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
            }
            {{this.FuncAssign(resultSelector,nameof(resultSelector),this.currentValue,this.outerValue,this.group)}}
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
        var {{this.lookup}} = new global::System.Collections.Generic.Dictionary<{{FuncReturnTypeName(innerKeySelector)}}, global::System.Collections.Generic.List<{{inner.ElementType}}>>({{comparer}} ?? global::System.Collections.Generic.EqualityComparer<{{FuncReturnTypeName(innerKeySelector)}}>.Default);
        {
            using var {{this.e}} = {{inner.GetEnumeratorExpression}};
            while({{this.e}}.MoveNext()) {
                var {{this.item}} = {{this.e}}.Current;
                {{FuncReturnTypeName(innerKeySelector)}} {{this.key}} = default!;
                {{this.FuncAssignFlat(innerKeySelector,nameof(innerKeySelector),this.key,this.item)}}
                if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                    {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                    {{this.lookup}}.Add({{this.key}}, {{this.group}});
                }
                {{this.group}}.Add({{this.item}});
            }
        }
        {
            using var {{this.temp}} = {{outer.GetEnumeratorExpression}};
            while({{this.temp}}.MoveNext()) {
                var {{this.outerValue}} = {{this.temp}}.Current;
                {{FuncReturnTypeName(outerKeySelector)}} {{this.key}} = default!;
                {{this.FuncAssignFlat(outerKeySelector,nameof(outerKeySelector),this.key,this.outerValue)}}
                if(!{{this.lookup}}.TryGetValue({{this.key}}, out var {{this.group}})) {
                    {{this.group}} = new global::System.Collections.Generic.List<{{inner.ElementType}}>();
                }
                {{this.ElementType}} {{elementVariable}} = default!;
                {{this.FuncAssignFlat(resultSelector,nameof(resultSelector),elementVariable,this.outerValue,this.group)}}
                {{bodyCode}}
            }
        }
        """;
}
