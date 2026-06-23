namespace Seamless.FLinq.Linq.Fragments;
/*
BufferedScalar は「Compute で先に結果を作り、TryGetNext はその結果を1回だけ返す」終端基底。

入力例:
  source.Count(predicate)
  source.First()

生成されるコード例:
  private struct GeneratedScalar<TSourceEnumerator> {
      public TResult Current { get; private set; }
      private bool yielded;
      public GeneratedScalar(TSourceEnumerator source) {
          Current = Compute(source);
      }
      public bool TryGetNext(out TResult current) {
          if(yielded) return false;
          yielded = true;
          current = Current;
          return true;
      }
  }

バリエーション:
  - ComputeExpression は Count/First/ToArray など各終端が差し替える。
  - ComputeMembers は span fast path、predicate 呼び出し、補助 Lookup 型などを追加する。
  - RenderComputeInline は Compute 呼び出しだけを呼び出し元へ直接埋め込む。
*/
public abstract class BufferedScalar(int 番号,string resultType) : Scalar(番号) {
    protected abstract string ComputeExpression { get; }
    protected virtual string ComputeMembers => string.Empty;
    protected virtual string StructSources => string.Empty;
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    public override string GetCurrentExpression => "Current";
    public override string GetEnumeratorStruct => $$"""
        {{StructSources}}
        {{DefineScalarEnumeratorStruct(resultType)}}
        """;
    public override string GetEnumeratorFields => $$"""
        public {{resultType}} Current { get; private set; }
        private bool {{yielded}} = false;
        {{ComputeMembers}}
        """;
    public override string GetEnumeratorConstructor => $"this.Current = {ComputeExpression};";
    public override string GetTryGetNextBody => $$"""
        if(this.{{yielded}}) {
            current = default!;
            return false;
        }
        this.{{yielded}} = true;
        current = this.Current;
        return true;
        """;

    // インライン化時は Struct{番号}（単一要素 enumerator）を生成せず、static Compute だけを保持する
    // 軽量 struct を出す。外側の型（補助クラス）から Compute を呼ぶため private→internal へ昇格する。
    public override string InlineSupportingMembers => $$"""
        {{StructSources}}
        private struct {{Struct}}<TEnum>
            {{StructTypeConstraints}} {
        {{IndentLines(ComputeMembers.Replace("private static","internal static"),"    ")}}
        }
        """;

    // ComputeExpression（= Compute(this.e{closure})）を「ソース列挙子をその場で作り Compute へ渡す」
    // インライン文へ変換する共通ヘルパー。Compute(...) 形を持つ単一ソース集約のみが opt-in する。
    protected string? RenderComputeInline(string destVar,string destType){
        if(PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr = src.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var call = ComputeExpression
            .Replace("Compute(",$"{Struct}<{src.EnumeratorTypeName}>.Compute(")
            .Replace(e,srcVar);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                {{destVar}}={{call}};
            }
            """;
    }

    protected string? RenderComputeInline(string destVar,string destType,params string[] extraArguments){
        if(PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr = src.GetEnumeratorExpression; }
        catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var extra = extraArguments.Length==0 ? string.Empty : ", " + string.Join(", ", extraArguments);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                {{destVar}}={{Struct}}<{{src.EnumeratorTypeName}}>.Compute({{srcVar}}{{extra}});
            }
            """;
    }

}
