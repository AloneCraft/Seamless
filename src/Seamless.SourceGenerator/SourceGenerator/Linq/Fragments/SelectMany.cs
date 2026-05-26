namespace Seamless.SourceGenerator.Linq.Fragments;
/*
以前の loop 展開版の断片は記録として残すが、実際の実装は Enumerator ベースに統一した。
*/

public abstract class SelectManyBase(int 番号, Enumerable source) : Enumerable(番号) {
    protected override Enumerable PrimarySource => source;
    protected abstract string CollectionElementType { get; }
    protected abstract string CollectionSelectorPrimaryConstructorParameter { get; }
    protected abstract string CreateInnerEnumeratorExpression { get; }
    protected virtual string MoveNextOuterPrologue => string.Empty;
    protected abstract string CurrentValueAssignmentFromInner { get; }

    public abstract override string StructMethod { get; }
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {this.CollectionSelectorPrimaryConstructorParameter}";
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.IEnumerator<{{this.CollectionElementType}}>? innerEnumerator = null;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        private {{this.ElementType}} {{this.currentValue}} = default!;
        """;
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if(this.innerEnumerator is not null) {
            if(this.innerEnumerator.MoveNext()) {
                var {{this.item}} = this.innerEnumerator.Current;
                {{this.CurrentValueAssignmentFromInner}}
                current = {{this.currentValue}};
                return true;
            }
            this.innerEnumerator.Dispose();
            this.innerEnumerator = null;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.MoveNextOuterPrologue}}
        {{this.outerValue}} = {{this.e}}.Current;
        this.innerEnumerator = {{this.CreateInnerEnumeratorExpression}};
        goto {{Start}};
        """;
    public override string GetCurrentExpression => this.currentValue;
    protected override string GetEnumeratorDisposeBody => $$"""
        if(this.innerEnumerator is not null) {
            this.innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;
}

public class SelectMany(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    private bool IsHoistable =>
        selector.CanInlineAssign &&
        !NeedsStructClosure(selector) &&
        !selector.RenderInlineExpression("__SeamlessHoistProbe__").Contains("__SeamlessHoistProbe__");
    public override string StructMethod =>
        IsHoistable ? string.Empty : this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorExpression =>
        IsHoistable
            ? $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {selector.RenderInlineExpression("__unused__")})"
            : $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector, nameof(selector))})";
    protected override string CollectionSelectorPrimaryConstructorParameter =>
        IsHoistable
            ? $"global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}> innerEnumerable0"
            : $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> selector";
    protected override string CreateInnerEnumeratorExpression =>
        IsHoistable ? "innerEnumerable0.GetEnumerator()" : $"{nameof(selector)}({this.outerValue}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

public class SelectMany_index(int 番号, Enumerable source, FuncFragment selector) : SelectManyBase(番号, source) {
    public override string StructMethod => this.DefineStruct(selector, nameof(selector));
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(selector, nameof(selector))})";
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, int, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> selector";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.index}} = -1;
        """;
    protected override string MoveNextOuterPrologue => $"{this.index}++;";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(selector)}({this.outerValue}, {this.index}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {this.item};";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(selector));
    public override string ElementType => this.CollectionElementType;
}

public class SelectMany_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(collectionSelector, nameof(collectionSelector))}}
        {{this.DefineStruct(resultSelector, nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(collectionSelector, nameof(collectionSelector))}, {this.FuncDelegate(resultSelector, nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => $$"""TEnum {{this.e}}, global::System.Func<{{source.ElementType}}, global::System.Collections.Generic.IEnumerable<{{this.CollectionElementType}}>> collectionSelector, global::System.Func<{{source.ElementType}}, {{this.CollectionElementType}}, {{this.ElementType}}> resultSelector""";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> collectionSelector";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(collectionSelector)}({this.outerValue}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {nameof(resultSelector)}({this.outerValue}, {this.item});";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}

/// <summary>
/// selector の本体 LINQ チェーンを struct のまま展開する SelectMany。
/// <br/>IEnumerator&lt;T&gt;? ではなく具象 struct 型フィールドを使うことで、
/// インターフェースディスパッチを排除し、毎外要素でのデリゲート呼び出しもなくす。
/// </summary>
public class SelectMany_WithInlinedInner(int 番号, Enumerable source, Enumerable innerFragment, string outerParamName)
    : SelectManyBase(番号, source) {

    private string InnerEnumeratorTypeName => innerFragment.EnumeratorTypeName;

    public override string StructMethod => string.Empty;

    // outer source structs + inner chain structs + outer SelectMany struct
    public override string GetEnumeratorStruct => $$"""
        {{source.GetEnumeratorStruct}}
        {{innerFragment.GetEnumeratorStruct}}
        {{this.DefineEnumeratorStructCore(this.ElementType, this.GetEnumeratorDisposeBody)}}
        """;

    // No selector parameter — outer struct only needs the outer source enumerator
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";

    // Use concrete struct type + bool flag instead of IEnumerator<T>?
    public override string GetEnumeratorFields => $$"""
        private {{InnerEnumeratorTypeName}} innerEnumerator = default!;
        private bool {{hasValue}} = false;
        private {{source.ElementType}} {{this.outerValue}} = default!;
        private {{this.ElementType}} {{this.currentValue}} = default!;
        """;

    // bool flag replaces the null check
    public override string GetTryGetNextBody => $$"""
        {{Start}}:
        if({{hasValue}}) {
            if(innerEnumerator.TryGetNext(out var {{this.item}})) {
                {{this.CurrentValueAssignmentFromInner}}
                current = {{this.currentValue}};
                return true;
            }
            innerEnumerator.Dispose();
            {{hasValue}} = false;
        }
        if(!{{this.e}}.MoveNext()) {
            current = default!;
            return false;
        }
        {{this.outerValue}} = {{this.e}}.Current;
        innerEnumerator = {{this.CreateInnerEnumeratorExpression}};
        {{hasValue}} = true;
        goto {{Start}};
        """;

    protected override string GetEnumeratorDisposeBody => $$"""
        if({{hasValue}}) {
            innerEnumerator.Dispose();
        }
        {{this.e}}.Dispose();
        """;

    // Instantiate the inner struct, substituting outer param name with the outerValue field.
    protected override string CreateInnerEnumeratorExpression =>
        System.Text.RegularExpressions.Regex.Replace(
            innerFragment.GetEnumeratorExpression,
            $@"(?<![a-zA-Z0-9_]){System.Text.RegularExpressions.Regex.Escape(outerParamName)}(?![a-zA-Z0-9_])",
            outerValue);

    // Required by SelectManyBase but not used (GetEnumeratorPrimaryConstructorParameters is overridden)
    protected override string CollectionSelectorPrimaryConstructorParameter => string.Empty;
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {this.item};";
    protected override string CollectionElementType => innerFragment.ElementType;
    public override string ElementType => innerFragment.ElementType;

    // GetEnumeratorExpression: outer struct takes only the outer source enumerator
    public override string GetEnumeratorExpression =>
        $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
}

public class SelectMany_index_resultSelector(int 番号, Enumerable source, FuncFragment collectionSelector, FuncFragment resultSelector) : SelectManyBase(番号, source) {
    public override string StructMethod => $$"""
        {{this.DefineStruct(collectionSelector, nameof(collectionSelector))}}
        {{this.DefineStruct(resultSelector, nameof(resultSelector))}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression}, {this.FuncDelegate(collectionSelector, nameof(collectionSelector))}, {this.FuncDelegate(resultSelector, nameof(resultSelector))})";
    public override string GetEnumeratorPrimaryConstructorParameters => $$"""TEnum {{this.e}}, global::System.Func<{{source.ElementType}}, int, global::System.Collections.Generic.IEnumerable<{{this.CollectionElementType}}>> collectionSelector, global::System.Func<{{source.ElementType}}, {{this.CollectionElementType}}, {{this.ElementType}}> resultSelector""";
    public override string GetEnumeratorFields => $$"""
        {{base.GetEnumeratorFields}}
        private int {{this.index}} = -1;
        """;
    protected override string CollectionSelectorPrimaryConstructorParameter => $"global::System.Func<{source.ElementType}, int, global::System.Collections.Generic.IEnumerable<{this.CollectionElementType}>> collectionSelector";
    protected override string MoveNextOuterPrologue => $"{this.index}++;";
    protected override string CreateInnerEnumeratorExpression => $"{nameof(collectionSelector)}({this.outerValue}, {this.index}).GetEnumerator()";
    protected override string CurrentValueAssignmentFromInner => $"{this.currentValue} = {nameof(resultSelector)}({this.outerValue}, {this.item});";
    protected override string CollectionElementType => SequenceElementTypeName(FuncReturnTypeName(collectionSelector));
    public override string ElementType => FuncReturnTypeName(resultSelector);
}
