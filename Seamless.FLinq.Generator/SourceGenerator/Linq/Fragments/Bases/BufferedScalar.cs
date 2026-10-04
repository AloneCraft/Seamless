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
          Current = Compute(ref this.source); // 列挙子は参照で渡し、Dispose で破棄する実体と一致させる
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
    // Compute は列挙子を参照で受け取る。引数のコピーではなく、Dispose で破棄されるフィールドそのものを渡す。
    public override string GetEnumeratorConstructor => $"this.Current = {this.ComputeExpression.Replace($"ref {this.e}",$"ref this.{this.e}")};";
    public override string GetTryGetNextBody => $$"""
        if(this.{{this.yielded}}){
            current=default!;
            return false;
        }
        this.{{this.yielded}} = true;
        current = this.Current;
        return true;
        """;

    // インライン化時は Struct{番号}（単一要素 enumerator）を生成しない。Compute は呼び出し地点のローカル関数にする
    // (LocalComputeFunction)ので、ソースの列挙子だけを出す。
    public override string InlineSupportingMembers => this.StructSources;

    // Compute を差し替えメソッド内のローカル関数として出す。ジェネリック構造体の static Compute を経由すると、
    // 参照型の T(共有ジェネリックコード)では Compute の中の列挙子の呼び出しが実行時の型引き当てつきの間接呼び出しの
    // まま残り、インライン化されない。ローカル関数なら呼び出し元と同じ型文脈でインライン化される
    // (2026-10-02 の実験で参照型の Count(predicate) が約2.6倍速い。値型では差が無い)。
    private string LocalComputeFunction(Enumerable source) =>
        DrainRemainingEnumerator(source,global::System.Text.RegularExpressions.Regex.Replace(
            this.ComputeMembers.Replace("private static ","static ").Replace(" Compute(",$" {this.LocalComputeName}("),
            @"\bTEnum\b",
            source.EnumeratorTypeName));

    // ローカル関数では列挙子が具体の構造体の型なので、IEnumerable の根なら、最後まで回すループ(while(source.TryGetNext(out … x)){)を
    // 列挙子を局所変数に取って MoveNext/Current を直接呼ぶ形にする(Fragment.DrainParts と同じ理由)。続きから回るので、
    // ループの前に TryGetNext を呼んでいても同じ要素の並びになる。
    private static string DrainRemainingEnumerator(Enumerable source,string compute) {
        if(!source.ProvidesRemainingEnumerator) {
            return compute;
        }
        var number = 0;
        // 波かっこの無い1文のループ(while(source.TryGetNext(out var x)) result=x; など)も同じ形にする。
        compute=global::System.Text.RegularExpressions.Regex.Replace(
            compute,
            @"while\(source\.TryGetNext\(out (?:var|[^()]+?) (?<item>\w+)\)\) (?<body>[^{\n][^\n]*;)",
            match => {
                var rest = $"__rest_source{number++}";
                return $"var {rest}=source.GetRemainingEnumerator();\nwhile({rest}.MoveNext()){{\nvar {match.Groups["item"].Value}={rest}.Current;\n{match.Groups["body"].Value}\n}}";
            });
        // 要素を使わないループ(while(source.TryGetNext(out _)) count++; など)は MoveNext だけを回す。
        compute=global::System.Text.RegularExpressions.Regex.Replace(
            compute,
            @"while\(source\.TryGetNext\(out _\)\) (?<body>[^{\n][^\n]*;)",
            match => {
                var rest = $"__rest_source{number++}";
                return $"var {rest}=source.GetRemainingEnumerator();\nwhile({rest}.MoveNext()){{\n{match.Groups["body"].Value}\n}}";
            });
        return global::System.Text.RegularExpressions.Regex.Replace(
            compute,
            @"while\(source\.TryGetNext\(out (?:var|[^()]+?) (?<item>\w+)\)\)\{",
            match => {
                var rest = $"__rest_source{number++}";
                return $"var {rest}=source.GetRemainingEnumerator();\nwhile({rest}.MoveNext()){{\nvar {match.Groups["item"].Value}={rest}.Current;";
            });
    }

    private string LocalComputeName => $"__compute{番号}";

    // ComputeExpression（= Compute(ref this.e{closure})）を「ソース列挙子をその場で作り Compute へ渡す」
    // インライン文へ変換する共通ヘルパー。Compute(...) 形を持つ単一ソース集約のみが opt-in する。
    // 列挙子は Compute へ参照で渡し、Compute が進めた実体そのものを破棄する。using の変数は読み取り専用で
    // 参照渡しできないため try/finally で書く。値渡しにすると内側の列挙子を作ったコピーが破棄されない。
    protected string? RenderComputeInline(string destVar,string destType) {
        if(this.PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr=src.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var call = this.ComputeExpression
            .Replace("Compute(",$"{this.LocalComputeName}(")
            .Replace(this.e,srcVar);
        return RenderOwnedEnumeratorComputation(destVar,destType,srcVar,srcExpr,call,this.ComputeOwnsEnumerator,this.LocalComputeFunction(src));
    }

    // true の終端は、Compute が渡された列挙子の複製を自分で破棄する(値渡しで受けたライブラリ関数が using で破棄する)。
    // その場合に呼び出し元や終端 enumerator の Dispose でも本体を破棄すると、コンストラクタでプール配列を借りる列挙子(OrderBy 等)は
    // 複製と本体が同じ配列を指すので二重 Return になり、後続の無関係なクエリのデータが壊れる。所有者は 1 人にする。
    protected virtual bool ComputeOwnsEnumerator => false;

    protected string? RenderComputeInline(string destVar,string destType,params string[] extraArguments) {
        if(this.PrimarySource is not { } src) return null;
        string srcExpr;
        try { srcExpr=src.GetEnumeratorExpression; } catch(NotSupportedException) { return null; }
        var srcVar = $"__s{番号}";
        var extra = extraArguments.Length==0 ? string.Empty : ", "+string.Join(", ",extraArguments);
        var call = $"{this.LocalComputeName}(ref {srcVar}{extra})";
        return RenderOwnedEnumeratorComputation(destVar,destType,srcVar,srcExpr,call,this.ComputeOwnsEnumerator,this.LocalComputeFunction(src));
    }

    private static string RenderOwnedEnumeratorComputation(string destVar,string destType,string srcVar,string srcExpr,string call,bool computeOwnsEnumerator,string localFunction) => computeOwnsEnumerator ? $$"""
        {{destType}} {{destVar}};
        {
            var {{srcVar}}={{srcExpr}};
            {{destVar}}={{call}};
        {{IndentLines(localFunction,"    ")}}
        }
        """ : $$"""
        {{destType}} {{destVar}};
        {
            var {{srcVar}}={{srcExpr}};
            try{
                {{destVar}}={{call}};
            }finally{
                {{srcVar}}.Dispose();
            }
        {{IndentLines(localFunction,"    ")}}
        }
        """;

}
