namespace Seamless.FLinq.Generator;
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
internal abstract class BufferedScalar(int 番号,string resultType):Scalar(番号) {
    protected abstract string ComputeExpression { get; }
    protected virtual string ComputeMembers => string.Empty;
    protected virtual string StructSources => string.Empty;

    protected override string? RenderDirectEnumerableCallInline(Enumerable source,string destVar,string destType,string methodName,params string[] arguments) {
        return this.RenderComputeInline(destVar,destType);
    }
    protected override string GetCurrentPropertyDeclaration(string currentType) => string.Empty;
    public override string GetEnumeratorStruct => $$"""
        {{this.StructSources}}
        {{this.DefineScalarEnumeratorStruct(resultType)}}
        """;
    public override string GetEnumeratorFields => $$"""
        public {{resultType}} Current { get; private set; }
        private bool {{this.yielded}} = false;
        {{this.ComputeMembers}}
        """;
    public override string GetEnumeratorConstructor => $"this.Current = {this.ComputeExpression};";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}){
            current=default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;

    // インライン化時は Struct{番号}（単一要素 enumerator）を生成せず、static Compute だけを保持する
    // 軽量 struct を出す。外側の型（補助クラス）から Compute を呼ぶため private→internal へ昇格する。
    public override string InlineSupportingMembers => $$"""
        {{this.StructSources}}
        private struct {{this.Struct}}{{this.StructTypeParametersWithOuter("TEnum")}}
            {{this.StructTypeConstraints}} {
        {{IndentLines(this.ComputeMembers.Replace("private static","internal static"),"    ")}}
        }
        """;

    // ComputeExpression（= Compute(this.e{closure})）を「ソース列挙子をその場で作り Compute へ渡す」
    // インライン文へ変換する共通ヘルパー。Compute(...) 形を持つ単一ソース集約のみが opt-in する。
    protected string? RenderComputeInline(string destVar,string destType) {
        if(this.PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr=src.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var call = this.ComputeExpression
            .Replace("Compute(",$"{this.EnumeratorTypeNameWithOuter(src.EnumeratorTypeName)}.Compute(")
            .Replace(this.e,srcVar);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                {{destVar}}={{call}};
            }
            """;
    }

    protected string? RenderComputeInline(string destVar,string destType,params string[] extraArguments) {
        if(this.PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr=src.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var extra = extraArguments.Length==0 ? string.Empty : ", "+string.Join(", ",extraArguments);
        return $$"""
            {{destType}} {{destVar}};
            using(var {{srcVar}}={{srcExpr}}){
                {{destVar}}={{this.EnumeratorTypeNameWithOuter(src.EnumeratorTypeName)}}.Compute({{srcVar}}{{extra}});
            }
            """;
    }

}
