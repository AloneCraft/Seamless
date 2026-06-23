namespace Seamless.FLinq.Linq.Fragments;

// DefaultIfEmpty(DefaultIfEmpty(s)) は DefaultIfEmpty(s) と等価（内側が要素≥1を保証するため
// 外側の空判定は決して発火せず、最内の既定値が勝つ）。Take/TakeLast と同じ RootSource 畳み込みで
// 連鎖を1段へ融合する。
/*
DefaultIfEmptyBase は source が空なら defaultValue を1件だけ返す enumerator を生成する。

入力例:
  source.DefaultIfEmpty()
  source.DefaultIfEmpty(value)

生成例:
  if(source.MoveNext()) {
      hasValue = true;
      current = source.Current;
      return true;
  }
  if(!hasValue && !yieldedDefault) {
      yieldedDefault = true;
      current = defaultValue;
      return true;
  }
  return false;

バリエーション:
  - defaultValue なしは default! を constructor 引数として渡す。
  - defaultValue ありは指定式を渡す。
  - 連続 DefaultIfEmpty は最内の defaultValue だけが意味を持つため RootSource へ畳み込む。
*/
public abstract class DefaultIfEmptyBase(int 番号, Enumerable source) : Enumerable(番号) {
    private Enumerable Source => source;
    // 自身が表す既定値の式。最内でない場合は EffectiveDefaultValue 経由で無視される。
    protected abstract string OwnDefaultValue { get; }
    private Enumerable RootSource => Source is DefaultIfEmptyBase d ? d.RootSource : Source;
    private string EffectiveDefaultValue => Source is DefaultIfEmptyBase d ? d.EffectiveDefaultValue : OwnDefaultValue;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveDefaultValue})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, {RootSource.ElementType} defaultValue";
    public override string GetEnumeratorFields => $$"""
        private bool {{hasValue}};
        private bool {{yielded}};
        """;
    public override string GetTryGetNextBody => $$"""
        if({{e}}.MoveNext()) {
            this.{{hasValue}}=true;
            current=this.{{currentValue}}={{e}}.Current;
            return true;
        }
        if(!this.{{hasValue}}&&!this.{{yielded}}) {
            this.{{yielded}}=true;
            current=this.{{currentValue}}=defaultValue;
            return true;
        }
        current=default!;
        return false;
        """;
    // 非空なら RootSource の件数、空なら既定値1個ぶんの 1 を返す。
    // bool を呼び出し側が確認する実行時 fast path のため安全（CanProvideSpan は false のまま）。
    protected override string GetTryGetNonEnumeratedCountBody => $$"""
        if({{e}}.TryGetNonEnumeratedCount(out var __c)) {
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
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => RootSource.ElementType;
}

public class DefaultIfEmpty(int 番号, Enumerable source) : DefaultIfEmptyBase(番号, source) {
    protected override string OwnDefaultValue => "default!";
}

public class DefaultIfEmpty_defaultValue(int 番号, Enumerable source, string defaultValue) : DefaultIfEmptyBase(番号, source) {
    protected override string OwnDefaultValue => defaultValue;
}
