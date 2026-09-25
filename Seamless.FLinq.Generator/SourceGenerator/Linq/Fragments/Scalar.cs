namespace Seamless.FLinq.Generator;
/*
Scalar は Count/First/ToArray など「シーケンスから単一結果を返す」終端 Fragment の基底。

入力例:
  source.Count()
  source.Where(x => x > 0).ToList()

生成されるコード例:
  Result result;
  var sourceEnumerator = source.GetEnumerator();
  try {
      result = Compute(ref sourceEnumerator); // 値渡しにするとコピー側の内側列挙子が破棄されない
  } finally {
      sourceEnumerator.Dispose();
  }

主な分岐:
  - RenderInlineComputation が非 null なら単一要素 enumerator を生成せず、呼び出し元へ直接文を出す。
  - DirectEnumerableExpression がある source は RenderDirectEnumerableCallInline 経由で
    Compute インライン展開や配列/List 専用 fast path を選べる(唯一の override である
    BufferedScalar.cs は methodName を無視して FLinq 自身の Compute を埋め込むため、
    System.Linq への委譲ではない。Scalar.cs:44 の RenderDirectEnumerableToArrayHybridInline は
    Seamless.Linq.GeneratedCodeHelpers への委譲であり、これも System.Linq ではない)。
  - それ以外は BufferedScalar などが Compute(...) を持つ struct を生成する。

設計意図:
  終端結果だけが必要な場面で Struct{番号}+TryGetNext の死にコードを避け、materialize 系では
  TryGetNonEnumeratedCount/TryGetSpan/TryCopyTo を使って容量確保とコピーを最短化する。
*/
internal abstract class Scalar(int 番号):Fragment(番号) {
    // スカラー断片は内部に子 enumerator を持たないことも多いので既定は何もしない。
    protected virtual string GetEnumeratorDisposeBody => string.Empty;
    // bool/int/T など単一結果を返す enumerator struct を共通生成する。
    protected string DefineScalarEnumeratorStruct(string resultType) => this.DefineEnumeratorStructCore(resultType,this.GetEnumeratorDisposeBody);

    // スカラー終端は呼び出し側で 1 回だけ TryGetNext される。単一要素 enumerator struct（Struct{番号}）を
    // 生成すると __yd0 ガード・Current・到達不能な IValueEnumerator 各メンバ等が丸ごと死にコードになる。
    // これを避けるため、結果を destVar へ直接書き込むインライン文を返せる断片はこれを override する。
    // 戻り値 null（既定）なら従来の Struct{番号}+TryGetNext 経路へフォールバックする。
    public virtual string? RenderInlineComputation(string destVar,string destType) => null;
    // RenderInlineComputation がインライン展開を返すとき、補助クラスへ積む補助メンバ群。
    // Struct{番号}（単一要素 enumerator）を含まない点が GetEnumeratorStruct と異なる。
    public virtual string InlineSupportingMembers => this.GetEnumeratorStruct;

    protected virtual string? RenderDirectEnumerableCallInline(Enumerable source,string destVar,string destType,string methodName,params string[] arguments) => null;

    // 型情報を失った IEnumerable<T> ソースの materialize は
    // GeneratedCodeHelpers の共通ラダーへ委譲する（1 コール地点あたり 36 行 → 1 行）。
    protected string? RenderDirectEnumerableToArrayHybridInline(Enumerable source,string destVar,string destType) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        return $"{destType} {destVar}=global::Seamless.Linq.GeneratedCodeHelpers.ToArrayFromEnumerable<{source.ElementType}>({sourceExpression});";
    }

    // 型情報を失った IEnumerable<T> ソースの件数計測を MoveNext だけのループへ inline する。
    // TryGetNext(out _) 経由だと値を捨てる場合でも必ず Current を読んでしまい、Persisted のように
    // Current の実装が逆シリアライズを伴うソースでは要素ごとに実コスト(割当)を払う羽目になる
    // (実測: Persisted<ClassInt32> の Count() で 1 要素あたり約 56B、System.Linq.Enumerable.Count()
    // は Current に一切触れないため同じ条件でほぼ 0B)。ここは System.Linq への委譲ではなく、
    // 同じアルゴリズムを Current を読まない形で FLinq 自身がインライン展開している。
    protected string? RenderDirectEnumerableCountInline(Enumerable source,string destVar,string destType) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        var srcVar = $"__cntSrc{this.result}";
        var fastVar = $"__cntFast{this.result}";
        var countVar = $"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{srcVar}}={{sourceExpression}};
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount({{srcVar}},out var {{fastVar}})){
                    {{destVar}}=({{destType}}){{fastVar}};
                } else {
                    {{destType}} {{countVar}}=0;
                    using(var __e={{srcVar}}.GetEnumerator()){
                        checked{
                            while(__e.MoveNext()) {{countVar}}++;
                        }
                    }
                    {{destVar}}={{countVar}};
                }
            }
            """;
    }

    protected string? RenderSpanInline(Enumerable source,string destVar,string destType,string spanVar,Func<string,string> renderBody) {
        string srcExpr;
        try {
            srcExpr=source.GetEnumeratorExpression;
        } catch(NotSupportedException) {
            return null;
        }

        var srcVar = $"__src{this.result}";
        var directSpanExpression = source.GetDirectSpanExpression(srcVar);
        // Direct span roots materialize and scalar terminals without runtime probes.
        // Example: `int[] src; src.ToArray()` can emit `var __span=__src.GetSpan();`
        // and skip TryGetNonEnumeratedCount/TryCopyTo/TryGetSpan. IEnumerable<T>,
        // Tsavorite, and Persisted roots return null here and keep their existing
        // runtime or native-scan path.
        if(directSpanExpression is not null) {
            return $$"""
                {{destType}} {{destVar}};
                using(var {{srcVar}}={{srcExpr}}){
                    global::Seamless.Linq.LinqCounterScope.IncrementDirectSpanUsed();
                    var {{spanVar}}={{directSpanExpression}};
                    {{IndentLines(renderBody(spanVar),"    ")}}
                }
                """;
        }
        if(!source.CanProvideSpan) {
            return null;
        }

        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
                {{srcVar}}.TryGetSpan(out var {{spanVar}});
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
                {{IndentLines(renderBody(spanVar),"    ")}}
            }
            """;
    }

    protected static string? TryRenderDirectLambda(FuncFragment func) {
        if(func.CapturedLocals.Length!=0||func.RequiresStatementInlining) {
            return null;
        }
        try {
            return func.RenderAsLambda();
        } catch(InvalidOperationException) {
            return null;
        }
    }
}
