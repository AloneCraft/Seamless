namespace Seamless.FLinq.Linq.Fragments;

/*
BufferedSequence は OrderBy/Shuffle など「一度 List に構築してから列挙する」シーケンス基底。

入力例:
  source.OrderBy(x => x.Key)

生成されるコード例:
  private List<T>.Enumerator buffered;
  public GeneratedEnumerator(TSourceEnumerator source) {
      buffered = Build(source).GetEnumerator();
  }
  public bool TryGetNext(out T current) {
      if(buffered.TryGetNext(out var v)) {
          current = v;
          return true;
      }
      return false;
  }

設計意図:
  遅延評価の外形は保ったまま、最初の MoveNext 前に並べ替えやシャッフルの全体構築を済ませる。
  BuildMembers に SortItem や比較 delegate などの補助メンバーを差し込める。
*/
public abstract class BufferedSequence(int 番号,string elementType):Enumerable(番号) {
    protected abstract string BuildExpression { get; }
    protected virtual string BuildMembers => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.StructSources}}
        {{base.GetEnumeratorStruct}}
        """;
    protected virtual string StructSources => string.Empty;
    public override string GetEnumeratorFields => $$"""
        private global::System.Collections.Generic.List<{{this.ElementType}}>.Enumerator {{this.e}};
        {{this.BuildMembers}}
        """;
    public override string GetEnumeratorConstructor => $"this.{this.e} = ({this.BuildExpression}).GetEnumerator();";
    public override string GetTryGetNextBody => $$"""
        if({{this.e}}.TryGetNext(out var __mn101)) {
            this.Current = __mn101;
            current = this.Current;
            return true;
        }
        current = default!;
        return false;
        """;
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => elementType;
}
