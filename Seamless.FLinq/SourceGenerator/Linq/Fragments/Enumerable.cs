namespace Seamless.FLinq.Linq.Fragments;
/*
Enumerable は遅延評価されるシーケンス Fragment の基底。

入力例:
  source.Where(predicate)

生成されるコード例:
  using var e = sourceFragment.GetEnumerator();
  while(e.TryGetNext(out var item)) {
      consumer.Consume(item);
  }

主なパラメーター/override:
  - ElementType: この Fragment が流す要素型。
  - GetEnumeratorExpression: 呼び出し側が使う new GeneratedEnumerator(...) 式。
  - CanProvideSpan / CanTryProvideSpan: span fast path を生成してよいか。
  - GetConsumeExecutionStatement: Consume(consumer) など終端が列挙だけで済む場合の direct execution。

バリエーション:
  - ArrayRoot/ListRoot のように Span を確実に返せる source は for(span) へ展開される。
  - 通常 source は while(TryGetNext) に落ちる。
  - Distinct/Set 系のように事前 HashSet 構築が必要な Fragment は direct consume を override する。
*/
public abstract class Enumerable(int 番号):Fragment(番号),ConsumeDirectExecutionFragment{
    // この enumerable 断片が流す要素型。
    public abstract string ElementType{ get; }
    // 生成コード内で IEnumerable<T> として再評価できる元ソース式。
    // Array/List など既に専用高速 root を持つものは null のままにし、
    // 型情報を失った IEnumerable<T> root だけ terminal 側の軽量 fast path に使う。
    public virtual string? DirectEnumerableExpression => null;
    // この enumerable の enumerator が TryGetSpan で連続メモリの Span を返せるか。
    // ソース生成時に確定する（ArrayRoot/ListRoot のみ true）。span fast path を分岐なしで生成するために使う。
    public virtual bool CanProvideSpan => false;
    // TryGetSpan を実行時に試してよいか。false の可能性があるため呼び出し側は必ず fallback を持つ。
    public virtual bool CanTryProvideSpan => this.CanProvideSpan;
    // Direct span expression for already-created enumerator locals.
    // Example: int[] first; first.Except(second).Consume(c) can generate
    // `var __setSrcSpan0=__src0.GetSpan();` instead of
    // `if(__src0.TryGetSpan(...)) { ... } else { while(__src0.TryGetNext(...)) ... }`.
    // IEnumerable<T>, Tsavorite, and Persisted roots return null, so they keep the runtime/fallback path.
    public virtual string? GetDirectSpanExpression(string enumeratorName) => null;
    public virtual string? NonEnumeratedCountExpression => null;
    public virtual bool CountPreserving => false;
    // interceptor が IEnumerable<T> を返すときに使う ValueEnumerable 構築式。
    public virtual string GetEnumerableExpression => $"new global::Seamless.System.Carrier.ValueEnumerable<{this.EnumeratorTypeName}, {this.ElementType}>({this.GetEnumeratorExpression})";
    // 既定では下流の enumerator を Dispose する。
    protected virtual string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    // enumerable 系は要素型を Current とする enumerator struct を共通生成する。
    public override string GetEnumeratorStruct => this.DefineEnumeratorStructCore(this.ElementType,this.GetEnumeratorDisposeBody);

    // expr.Consume(consumer) 文に対する直接実行コード片。
    // この enumerable を列挙し各要素を consumer へ渡す。各 LINQ 演算子の意味は自分の enumerator が表現するため、
    // interceptor 側に演算子別の特殊処理を置かずここで一様に展開できる（コード片は Fragment が持つ原則）。
    // TraversalSupport.AppendTraversalLoop が span 可能ソース（ArrayRoot/ListRoot）では TryGetSpan→for(span)、
    // それ以外は while(TryGetNext) を生成するため span fast path は維持される。
    // span 直走査やキー集合の事前構築など演算子固有の最適化が必要な断片は本メソッドを override する（例: Distinct/Set 系）。
    public virtual string GetConsumeExecutionStatement(string consumerName){
        var builder=new StringBuilder();
        var enumeratorName=$"__ce{番号}";
        var valueName=$"__cv{番号}";
        builder.AppendLine($"using var {enumeratorName}={this.GetEnumeratorExpression};");
        TraversalSupport.AppendTraversalLoop(
            builder,
            enumeratorName,
            this.ElementType,
            valueName,
            $"{consumerName}.Consume({valueName});",
            this.CanTryProvideSpan,
            this.CanProvideSpan,
            this.GetDirectSpanExpression(enumeratorName));
        return builder.ToString().TrimEnd();
    }
}
