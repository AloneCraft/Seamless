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
  if(source.TryGetNext(out var v)) {
      hasValue = true;
      current = v;
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
public abstract class DefaultIfEmptyBase(int 番号,Enumerable source):Enumerable(番号) {
    private Enumerable Source => source;
    // 自身が表す既定値の式。最内でない場合は EffectiveDefaultValue 経由で無視される。
    protected abstract string OwnDefaultValue { get; }
    private Enumerable RootSource => this.Source is DefaultIfEmptyBase d ? d.RootSource : this.Source;
    private string EffectiveDefaultValue => this.Source is DefaultIfEmptyBase d ? d.EffectiveDefaultValue : this.OwnDefaultValue;
    protected override Enumerable PrimarySource => this.RootSource;
    public override bool CanTryProvideSpan => this.RootSource.CanTryProvideSpan;
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
        if({{this.e}}.TryGetNext(out var __mn23)) {
            this.{{this.hasValue}}=true;
            current=this.{{this.currentValue}}=__mn23;
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
    protected override string GetTryGetSpanBody => $$"""
        if({{this.e}}.TryGetSpan(out span)&&span.Length!=0) {
            return true;
        }
        span=default;
        return false;
        """;
    protected override string GetTryCopyToBody => $$"""
        if({{this.e}}.TryGetSpan(out var __span)&&__span.Length!=0) {
            var __spanStart=__offset.GetOffset(__span.Length);
            if(__spanStart<0||__spanStart>__span.Length||destination.Length>__span.Length-__spanStart) {
                return false;
            }
            __span.Slice(__spanStart,destination.Length).CopyTo(destination);
            return true;
        }
        if(!this.{{this.hasValue}}&&!this.{{this.yielded}}&&{{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
            if(__count==0) {
                var __defaultStart=__offset.GetOffset(1);
                if(__defaultStart<0||__defaultStart>1||destination.Length>1-__defaultStart) {
                    return false;
                }
                if(destination.Length!=0) {
                    destination[0]=defaultValue;
                }
                return true;
            }
            var __copyStart=__offset.GetOffset(__count);
            if(__copyStart<0||__copyStart>__count||destination.Length>__count-__copyStart) {
                return false;
            }
            return destination.Length==0||{{this.e}}.TryCopyTo(destination,__offset);
        }
        if({{this.e}}.TryCopyTo(destination,__offset)) {
            return true;
        }
        return false;
        """;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.RootSource is not RangeRoot range) {
            return base.GetConsumeExecutionStatement(consumerName);
        }
        return $$"""
            {
                var __start{{番号}}={{range.StartExpression}};
                var __count{{番号}}={{range.CountExpression}};
                if(__count{{番号}}==0){
                    {{consumerName}}.Consume({{this.EffectiveDefaultValue}});
                }else{
                    for(var __i{{番号}}=0;__i{{番号}}<__count{{番号}};__i{{番号}}++){
                        {{consumerName}}.Consume(__start{{番号}}+__i{{番号}});
                    }
                }
            }
            """;
    }
    public override string ElementType => this.RootSource.ElementType;
}

public class DefaultIfEmpty(int 番号,Enumerable source):DefaultIfEmptyBase(番号,source) {
    protected override string OwnDefaultValue => "default!";
}

public class DefaultIfEmpty_defaultValue(int 番号,Enumerable source,string defaultValue):DefaultIfEmptyBase(番号,source) {
    protected override string OwnDefaultValue => defaultValue;
}
