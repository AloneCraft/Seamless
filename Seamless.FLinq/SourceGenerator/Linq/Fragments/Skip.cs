namespace Seamless.FLinq.Linq.Fragments;

/*
Skip は source.Skip(count) を、初回 TryGetNext で count 件だけ読み捨てる enumerator へ変換する。

入力例:
  source.Skip(5)
  source.Skip(a).Skip(b)

生成例:
  if(!skipped) {
      skipped = true;
      while(count-- > 0 && source.MoveNext()) {}
  }
  if(source.MoveNext()) {
      current = source.Current;
      return true;
  }

バリエーション:
  - count < 0 は constructor で 0 に丸める。
  - 連続 Skip は RootSource へ畳み込み、count は previous + current を int.MaxValue 上限で合成する。
*/
public class Skip(int 番号, Enumerable source, string skipCount) : Enumerable(番号) {
    public static bool IsApplicableOverload(IMethodSymbol method) =>
        method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    private Enumerable Source => source;
    private string SkipCountExpression => skipCount;
    private Enumerable RootSource => Source is Skip skip ? skip.RootSource : Source;
    private string EffectiveSkipCount => Source is Skip skip
        ? $"(int)global::System.Math.Min(int.MaxValue,(long)({skip.EffectiveSkipCount})+({SkipCountExpression}))"
        : SkipCountExpression;
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}, {EffectiveSkipCount})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}, int count";
    public override string GetEnumeratorFields => $$"""
        private bool skipped = false;
        """;
    public override string GetEnumeratorConstructor => "if(this.count < 0) { this.count = 0; }";
    public override string GetTryGetNextBody => $$"""
        if(!this.skipped) {
            this.skipped = true;
            var remaining = this.count;
            while(remaining > 0 && {{e}}.MoveNext()) {
                remaining--;
            }
        }
        if({{e}}.MoveNext()) {
            current = this.{{currentValue}} = {{e}}.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => Source.ElementType;
}
