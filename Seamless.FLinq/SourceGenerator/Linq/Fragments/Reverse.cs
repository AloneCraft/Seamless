namespace Seamless.FLinq.Linq.Fragments;

/*
Reverse は source.Reverse() を、初回に逆順で読める状態を作ってから末尾から返す enumerator へ変換する。

入力例:
  source.Reverse()
  source.Reverse().Reverse()

生成例:
  if(notInitialized) {
      if(source.TryGetSpan(out var span)) index = span.Length - 1;
      else buffer = source.ToList(); index = buffer.Count - 1;
  }
  if(index >= 0) return spanOrBuffer[index--];
  return false;

バリエーション:
  - source が span を返せる場合は buffer を作らず span の末尾から読む。
  - span が無い場合は List に全件 buffer する。
  - 連続 Reverse は偶数回なら RootSource そのものへ畳み込む。
*/
public class Reverse(int 番号,Enumerable source):Enumerable(番号) {
    private Enumerable Source => source;
    private Enumerable RootSource => this.Source is Reverse reverse ? reverse.RootSource : this.Source;
    private bool IsIdentity => this.Source is Reverse reverse&&!reverse.IsIdentity;
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression =>
        this.IsIdentity
            ? this.RootSource.GetEnumerableExpression
            : $"global::Seamless.Linq.Enumerable.Reverse({this.RootSource.GetEnumerableExpression})";
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{(this.IsIdentity ? string.Empty : base.GetEnumeratorStruct)}}
        """;
    public override string EnumeratorTypeName => this.IsIdentity
        ? this.RootSource.EnumeratorTypeName
        : base.EnumeratorTypeName;
    public override string GetEnumeratorExpression => this.IsIdentity
        ? this.RootSource.GetEnumeratorExpression
        : $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}";
    public override string GetEnumeratorFields => $$"""
        private int {{this.a}}=int.MinValue;
        private global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>? {{this.group}};
        """;
    public override string GetTryGetNextBody => this.RootSource.CanTryProvideSpan
        ? $$"""
            // {{this.a}} は int.MinValue=未初期化 / -1=列挙完了 を区別する。
            // -1 を未初期化扱いにすると、TryGetNext を完了後も呼ぶ消費側（TraversalSupport の while ループ等）で
            // 末尾到達ごとに Length-1 へ再初期化して無限ループになる。
            if(this.{{this.a}}==int.MinValue) {
                if({{this.e}}.TryGetSpan(out var __span0)) {
                    this.{{this.a}}=__span0.Length-1;
                }else{
                    if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                        this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>(__count);
                    }else{
                        this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>();
                    }
                    while({{this.e}}.TryGetNext(out var __mn46)) this.{{this.group}}.Add(__mn46);
                    this.{{this.a}}=this.{{this.group}}.Count-1;
                }
            }
            if(this.{{this.a}}>=0) {
                if({{this.e}}.TryGetSpan(out var __span)) {
                    current=this.{{this.currentValue}}=__span[this.{{this.a}}--];
                }else{
                    current=this.{{this.currentValue}}=this.{{this.group}}![this.{{this.a}}--];
                }
                return true;
            }
            current=default!;
            return false;
            """
        : $$"""
            if(this.{{this.group}} is null) {
                if({{this.e}}.TryGetNonEnumeratedCount(out var __count)) {
                    this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>(__count);
                }else{
                    this.{{this.group}}=new global::System.Collections.Generic.List<{{this.RootSource.ElementType}}>();
                }
                while({{this.e}}.TryGetNext(out var __mn47)) this.{{this.group}}.Add(__mn47);
                this.{{this.a}}=this.{{this.group}}.Count-1;
            }
            if(this.{{this.a}}>=0) {
                current=this.{{this.currentValue}}=this.{{this.group}}[this.{{this.a}}--];
                return true;
            }
            current=default!;
            return false;
            """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => this.RootSource.ElementType;
    public override string GetConsumeExecutionStatement(string consumerName) {
        if(this.IsIdentity) {
            return this.RootSource.GetConsumeExecutionStatement(consumerName);
        }
        var sourceName = $"__reverseSource{番号}";
        var directSpanExpression = this.RootSource.GetDirectSpanExpression(sourceName);
        if(directSpanExpression is not null) {
            var spanName = $"__reverseSpan{番号}";
            return $$"""
                using var {{sourceName}}={{this.RootSource.GetEnumeratorExpression}};
                global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                var {{spanName}}={{directSpanExpression}};
                for(var __i{{番号}}={{spanName}}.Length-1;__i{{番号}}>=0;__i{{番号}}--) {
                    {{consumerName}}.Consume({{spanName}}[__i{{番号}}]);
                }
                """;
        }
        return base.GetConsumeExecutionStatement(consumerName);
    }
}
