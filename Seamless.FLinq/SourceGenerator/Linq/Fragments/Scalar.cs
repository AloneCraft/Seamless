namespace Seamless.FLinq.Linq.Fragments;
/*
Scalar は Count/First/ToArray など「シーケンスから単一結果を返す」終端 Fragment の基底。

入力例:
  source.Count()
  source.Where(x => x > 0).ToList()

生成されるコード例:
  Result result;
  using(var sourceEnumerator = source.GetEnumerator()) {
      result = Compute(sourceEnumerator);
  }

主な分岐:
  - RenderInlineComputation が非 null なら単一要素 enumerator を生成せず、呼び出し元へ直接文を出す。
  - DirectEnumerableExpression がある source は System.Linq.Enumerable への軽い委譲や配列/List 専用 fast path を選べる。
  - それ以外は BufferedScalar などが Compute(...) を持つ struct を生成する。

設計意図:
  終端結果だけが必要な場面で Struct{番号}+TryGetNext の死にコードを避け、materialize 系では
  TryGetNonEnumeratedCount/TryGetSpan/TryCopyTo を使って容量確保とコピーを最短化する。
*/
public abstract class Scalar(int 番号):Fragment(番号){
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

    protected string? RenderDirectEnumerableToArrayHybridInline(Enumerable source,string destVar,string destType) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        var elementType = source.ElementType;
        var sourceVar=$"__src{this.result}";
        var arrayVar=$"__array{this.result}";
        var listVar=$"__list{this.result}";
        var collectionVar=$"__collection{this.result}";
        var spanVar=$"__span{this.result}";
        var countVar=$"__count{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                if({{sourceVar}} is {{elementType}}[] {{arrayVar}}) {
                    if({{arrayVar}}.Length==0) {
                        {{destVar}}=global::System.Array.Empty<{{elementType}}>();
                    } else {
                        {{destVar}}=global::System.GC.AllocateUninitializedArray<{{elementType}}>({{arrayVar}}.Length);
                        global::System.MemoryExtensions.AsSpan({{arrayVar}}).CopyTo(global::System.MemoryExtensions.AsSpan({{destVar}}));
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.List<{{elementType}}> {{listVar}}) {
                    var {{spanVar}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{listVar}});
                    if({{spanVar}}.Length==0) {
                        {{destVar}}=global::System.Array.Empty<{{elementType}}>();
                    } else {
                        {{destVar}}=global::System.GC.AllocateUninitializedArray<{{elementType}}>({{spanVar}}.Length);
                        {{spanVar}}.CopyTo(global::System.MemoryExtensions.AsSpan({{destVar}}));
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{elementType}}> {{collectionVar}}) {
                    var {{countVar}}={{collectionVar}}.Count;
                    if({{countVar}}==0) {
                        {{destVar}}=global::System.Array.Empty<{{elementType}}>();
                    } else {
                        {{destVar}}=global::System.GC.AllocateUninitializedArray<{{elementType}}>({{countVar}});
                        {{collectionVar}}.CopyTo({{destVar}},0);
                    }
                } else {
                    var __fallbackList{{this.result}}=new global::System.Collections.Generic.List<{{elementType}}>();
                    foreach(var __fallbackItem{{this.result}} in {{sourceVar}}) {
                        __fallbackList{{this.result}}.Add(__fallbackItem{{this.result}});
                    }
                    {{destVar}}=__fallbackList{{this.result}}.ToArray();
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

        var srcVar=$"__src{this.result}";
        var directSpanExpression=source.GetDirectSpanExpression(srcVar);
        // Direct span roots materialize and scalar terminals without runtime probes.
        // Example: `int[] src; src.ToArray()` can emit `var __span=__src.GetSpan();`
        // and skip TryGetNonEnumeratedCount/TryCopyTo/TryGetSpan. IEnumerable<T>,
        // Tsavorite, and Persisted roots return null here and keep their existing
        // runtime or native-scan path.
        if(directSpanExpression is not null) {
            return $$"""
                {{destType}} {{destVar}};
                using(var {{srcVar}}={{srcExpr}}) {
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
            using(var {{srcVar}}={{srcExpr}}) {
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanCalled();
                {{srcVar}}.TryGetSpan(out var {{spanVar}});
                global::Seamless.Linq.LinqCounterScope.IncrementTryGetSpanSucceeded();
                {{IndentLines(renderBody(spanVar),"    ")}}
            }
            """;
    }

    protected string? RenderDirectEnumerableToListHybridInline(Enumerable source,string destVar,string destType) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        var elementType = source.ElementType;
        var sourceVar=$"__src{this.result}";
        var arrayVar=$"__array{this.result}";
        var listVar=$"__list{this.result}";
        var collectionVar=$"__collection{this.result}";
        var sourceSpanVar=$"__sourceSpan{this.result}";
        var resultSpanVar=$"__resultSpan{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                if({{sourceVar}} is {{elementType}}[] {{arrayVar}}) {
                    {{destVar}}=new({{arrayVar}}.Length);
                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({{destVar}},{{arrayVar}}.Length);
                    global::System.MemoryExtensions.AsSpan({{arrayVar}}).CopyTo(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{destVar}}));
                } else if({{sourceVar}} is global::System.Collections.Generic.List<{{elementType}}> {{listVar}}) {
                    var {{sourceSpanVar}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{listVar}});
                    {{destVar}}=new({{sourceSpanVar}}.Length);
                    global::System.Runtime.InteropServices.CollectionsMarshal.SetCount({{destVar}},{{sourceSpanVar}}.Length);
                    var {{resultSpanVar}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{destVar}});
                    {{sourceSpanVar}}.CopyTo({{resultSpanVar}});
                } else if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{elementType}}> {{collectionVar}}) {
                    {{destVar}}=new({{collectionVar}});
                } else {
                    {{destVar}}=new global::System.Collections.Generic.List<{{elementType}}>();
                    foreach(var __fallbackItem{{this.result}} in {{sourceVar}}) {
                        {{destVar}}.Add(__fallbackItem{{this.result}});
                    }
                }
            }
            """;
    }

    protected string? RenderDirectEnumerableToHashSetHybridInline(Enumerable source,string destVar,string destType,string? comparerExpression=null) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        var elementType = source.ElementType;
        var comparer = comparerExpression ?? "null";
        var sourceVar=$"__src{this.result}";
        var arrayVar=$"__array{this.result}";
        var listVar=$"__list{this.result}";
        var collectionVar=$"__collection{this.result}";
        var spanVar=$"__span{this.result}";
        var itemVar=$"__item{this.result}";
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                if({{sourceVar}} is {{elementType}}[] {{arrayVar}}) {
                    {{destVar}}=new({{arrayVar}}.Length,{{comparer}});
                    var {{spanVar}}=global::System.MemoryExtensions.AsSpan({{arrayVar}});
                    for(var __i=0; __i<{{spanVar}}.Length; __i++) {
                        {{destVar}}.Add({{spanVar}}[__i]);
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.List<{{elementType}}> {{listVar}}) {
                    var {{spanVar}}=global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({{listVar}});
                    {{destVar}}=new({{spanVar}}.Length,{{comparer}});
                    for(var __i=0; __i<{{spanVar}}.Length; __i++) {
                        {{destVar}}.Add({{spanVar}}[__i]);
                    }
                } else if({{sourceVar}} is global::System.Collections.Generic.ICollection<{{elementType}}> {{collectionVar}}) {
                    {{destVar}}=new({{collectionVar}}.Count,{{comparer}});
                    foreach(var {{itemVar}} in {{collectionVar}}) {
                        {{destVar}}.Add({{itemVar}});
                    }
                } else {
                    {{destVar}}=new global::System.Collections.Generic.HashSet<{{elementType}}>({{comparer}});
                    foreach(var __fallbackItem{{this.result}} in {{sourceVar}}) {
                        {{destVar}}.Add(__fallbackItem{{this.result}});
                    }
                }
            }
            """;
    }

    protected static string? TryRenderDirectLambda(FuncFragment func) {
        if(func.CapturedLocals.Length!=0 || func.RequiresStatementInlining) {
            return null;
        }
        try {
            return func.RenderAsLambda();
        } catch(InvalidOperationException) {
            return null;
        }
    }
}
