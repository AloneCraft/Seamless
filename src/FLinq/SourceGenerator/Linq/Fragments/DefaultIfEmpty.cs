namespace Tsavorite.SourceGenerator.Linq.Fragments;

// DefaultIfEmpty(DefaultIfEmpty(s)) は DefaultIfEmpty(s) と等価（内側が要素≥1を保証するため
// 外側の空判定は決して発火せず、最内の既定値が勝つ）。Take/TakeLast と同じ RootSource 畳み込みで
// 連鎖を1段へ融合する。
public abstract class DefaultIfEmptyBase(int 番号, Enumerable source) : Enumerable(番号) {
    private Enumerable Source => source;
    // 自身が表す既定値の式。最内でない場合は EffectiveDefaultValue 経由で無視される。
    protected abstract string OwnDefaultValue { get; }
    private Enumerable RootSource => this.Source is DefaultIfEmptyBase d ? d.RootSource : this.Source;
    private string EffectiveDefaultValue => this.Source is DefaultIfEmptyBase d ? d.EffectiveDefaultValue : this.OwnDefaultValue;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}, {this.EffectiveDefaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}, {this.RootSource.ElementType} defaultValue";
    public override string GetEnumeratorFields => $$"""
        private bool {{this.hasValue}};
        private bool {{this.yielded}};
        """;
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.MoveNext()) {
            this.{{this.hasValue}}=true;
            current=this.{{this.currentValue}}={{this.e}}.Current;
            return true;
        }
        if(!this.{{this.hasValue}}&&!this.{{this.yielded}}) {
            this.{{this.yielded}}=true;
            current=this.{{this.currentValue}}=defaultValue;
            return true;
        }
        current=default!;
        return false;
        """;
    // 非空なら RootSource の件数、空なら既定値1個ぶんの 1 を返す。
    // bool を呼び出し側が確認する実行時 fast path のため安全（CanProvideSpan は false のまま）。
    protected override string GetTryGetNonEnumeratedCountBody => $$"""
        if({{this.e}}.TryGetNonEnumeratedCount(out var __c)) {
            if(__c==0) {
                count=1;
            }else{
                count=__c;
            }
            return true;
        }
        count=0;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
}

public class DefaultIfEmpty(int 番号, Enumerable source) : DefaultIfEmptyBase(番号, source) {
    protected override string OwnDefaultValue => "default!";
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : DefaultIfEmptyBase(番号, source) {
    protected override string OwnDefaultValue => defaultValue;
}
