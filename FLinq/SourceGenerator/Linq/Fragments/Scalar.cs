namespace Seamless.FLinq.Linq.Fragments;
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

    protected string? RenderDirectEnumerableCallInline(Enumerable source,string destVar,string destType,string methodName,params string[] arguments) {
        if(source.DirectEnumerableExpression is not { } sourceExpression) return null;
        var sourceVar=$"__src{this.result}";
        var argumentList=arguments.Length==0 ? string.Empty : ", "+string.Join(", ",arguments);
        return $$"""
            {{destType}} {{destVar}};
            {
                var {{sourceVar}}={{sourceExpression}};
                {{destVar}}=global::System.Linq.Enumerable.{{methodName}}({{sourceVar}}{{argumentList}});
            }
            """;
    }

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
                    {{destVar}}=global::System.Linq.Enumerable.ToArray({{sourceVar}});
                }
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
                    {{destVar}}=global::System.Linq.Enumerable.ToList({{sourceVar}});
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
                    {{destVar}}=global::System.Linq.Enumerable.ToHashSet({{sourceVar}},{{comparer}});
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
