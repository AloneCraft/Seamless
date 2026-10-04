using Microsoft.CodeAnalysis.CSharp;
namespace Seamless.FLinq.Generator;

/*
RangeModuloOrderRoot は Enumerable.Range(start,count) をキー x%C(C は正の定数)で安定に並べた列を、並べ替えずに作る。

入力例:
  Enumerable.Range(1,n).OrderBy(x => x%1000)
  Enumerable.Range(1,n).OrderBy(x => x%1000).ThenByDescending(x => x)

考え方:
  キーが r の要素は x≡r (mod C) の等差数列なので、キーの順に区間を C 刻みで回せばよい(比較もバッファも要らない)。
  C# の剰余は被除数の符号を持つので、キーは -(C-1)..C-1 になる。
  - r<0 のキーは負の x(x<0)、r>0 のキーは正の x(x>0)、r=0 のキーは C の倍数すべて(符号を問わない)から取る。
  - 同じキーの中は元の順(昇順)。ThenBy(x) も同じ順、ThenByDescending(x) は降順。
  - OrderByDescending(x%C) はキーを降順に回し、同じキーの中は元の順(安定ソート)。
*/
internal sealed class RangeModuloOrderRoot(
    int 番号,
    RangeRoot range,
    int modulus,
    bool descendingKeys,
    bool descendingWithinKey):Enumerable(番号) {
    public override string? NonEnumeratedCountExpression => range.CountExpression;
    public override bool CountPreserving => true;
    public override string StructMethod => string.Empty;
    public override string ElementType => "int";
    public override string? DirectEnumerableExpression {
        get {
            var ordered = descendingKeys
                ? $"global::System.Linq.Enumerable.OrderByDescending(global::System.Linq.Enumerable.Range({range.StartExpression}, {range.CountExpression}), static __x => __x % {modulus})"
                : $"global::System.Linq.Enumerable.OrderBy(global::System.Linq.Enumerable.Range({range.StartExpression}, {range.CountExpression}), static __x => __x % {modulus})";
            return descendingWithinKey
                ? $"global::System.Linq.Enumerable.ThenByDescending({ordered}, static __x => __x)"
                : ordered;
        }
    }
    public override string GetEnumeratorExpression => RenderEnumeratorExpression(this.Struct,range.StartExpression,range.CountExpression);
    public override string GetEnumeratorPrimaryConstructorParameters => "int start, int count";
    public override string GetEnumeratorFields => string.Empty;
    public override string GetTryGetNextBody => throw new NotSupportedException("RangeModuloOrderRoot は専用の列挙子構造体を出す。");

    // キー r の区間 [lo,hi) と、その区間で r と合同な最初の値(昇順)・最後の値(降順)。
    private string KeyBounds(string key,string start,string end,string lo,string hi,string first) => $$"""
        var {{lo}}={{key}}>0 ? global::System.Math.Max((long){{start}},1L) : (long){{start}};
        var {{hi}}={{key}}<0 ? global::System.Math.Min({{end}},0L) : {{end}};
        var __residue{{番号}}={{key}}<0 ? (long){{key}}+{{modulus}} : (long){{key}};
        var {{first}}={{(descendingWithinKey
            ? $"{hi}-1-((({hi}-1-__residue{番号})%{modulus}+{modulus})%{modulus})"
            : $"{lo}+(((__residue{番号}-{lo})%{modulus}+{modulus})%{modulus})")}};
        """;

    public override string GetEnumeratorStruct => $$"""
        private struct {{this.Struct}} : global::Seamless.Linq.IValueEnumerator<int>
        {
            private readonly int start;
            private readonly int count;
            private readonly long end;
            private readonly int lastKey;
            private int key;
            private long next;
            private long lo;
            private long hi;
            private bool started;
            public {{this.Struct}}(int start,int count) {
                // Enumerable.Range と同じ引数検査(列挙子を作る時点で例外にする)。
                if(count<0||(long)start+count-1>int.MaxValue){
                    throw new global::System.ArgumentOutOfRangeException("count");
                }
                this.start=start;
                this.count=count;
                this.end=(long)start+count;
                {{(descendingKeys
                    ? $"this.key=this.end-1>0 ? {modulus-1} : 0;\n        this.lastKey=start<0 ? {-(modulus-1)} : 0;"
                    : $"this.key=start<0 ? {-(modulus-1)} : 0;\n        this.lastKey=this.end-1>0 ? {modulus-1} : 0;")}}
                this.next=0;
                this.lo=0;
                this.hi=0;
                this.started=false;
            }
            public int Current{get;private set;}=default!;
            public bool TryGetNonEnumeratedCount(out int count){
                count=0;
                return false;
            }
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) { span = default; return false; }
            public bool TryCopyTo(scoped global::System.Span<int> destination,global::System.Index offset) => false;
            private void SetKey(){
                {{IndentBlock(this.KeyBounds("this.key","this.start","this.end","__lo","__hi","__first"),"                ")}}
                this.lo=__lo;
                this.hi=__hi;
                this.next=__first;
            }
            public bool TryGetNext(out int current){
                if(!this.started){
                    this.started=true;
                    if(this.count<=0){
                        current=default;
                        return false;
                    }
                    this.SetKey();
                }
                while(true){
                    if({{(descendingWithinKey ? "this.next>=this.lo" : "this.next<this.hi")}}){
                        current=this.Current=(int)this.next;
                        this.next{{(descendingWithinKey ? "-=" : "+=")}}{{modulus}};
                        return true;
                    }
                    if(this.key==this.lastKey){
                        current=default;
                        return false;
                    }
                    this.key{{(descendingKeys ? "--" : "++")}};
                    this.SetKey();
                }
            }
            public void Dispose(){}
        }
        """;

    public override string GetConsumeExecutionStatement(string consumerName) {
        var start = $"__start{番号}";
        var count = $"__count{番号}";
        var end = $"__end{番号}";
        var key = $"__key{番号}";
        var value = $"__value{番号}";
        var lo = $"__lo{番号}";
        var hi = $"__hi{番号}";
        var first = $"__first{番号}";
        var firstKey = descendingKeys ? $"{end}-1>0 ? {modulus-1} : 0" : $"{start}<0 ? {-(modulus-1)} : 0";
        var lastKey = descendingKeys ? $"{start}<0 ? {-(modulus-1)} : 0" : $"{end}-1>0 ? {modulus-1} : 0";
        var keyLoop = descendingKeys
            ? $"for(var {key}={firstKey};{key}>=({lastKey});{key}--)"
            : $"for(var {key}={firstKey};{key}<=({lastKey});{key}++)";
        var valueLoop = descendingWithinKey
            ? $"for(var {value}={first};{value}>={lo};{value}-={modulus})"
            : $"for(var {value}={first};{value}<{hi};{value}+={modulus})";
        // 同じキーの中が昇順なら、隣のキーの最初の値は前のキーの最初の値の ±1 を区間 [lo,lo+C) へ折り返したものになる。
        // キーごとの剰余の計算を避け、区間が変わるキー(最初・0・±1)だけ剰余で求める。
        var bounds = this.KeyBounds(key,start,end,lo,hi,first);
        if(!descendingWithinKey) {
            var previous = $"__previousFirst{番号}";
            var started = $"__started{番号}";
            var boundary = descendingKeys ? $"{key}==0||{key}==-1" : $"{key}==0||{key}==1";
            var step = descendingKeys
                ? $"{first}={previous}-1;\nif({first}<{lo}) {first}+={modulus};"
                : $"{first}={previous}+1;\nif({first}>={lo}+{modulus}) {first}-={modulus};";
            var fullFirst = $"{lo}+((((long)({key}<0 ? (long){key}+{modulus} : (long){key})-{lo})%{modulus}+{modulus})%{modulus})";
            bounds = $$"""
                var {{lo}}={{key}}>0 ? global::System.Math.Max((long){{start}},1L) : (long){{start}};
                var {{hi}}={{key}}<0 ? global::System.Math.Min({{end}},0L) : {{end}};
                long {{first}};
                if(!{{started}}||{{boundary}}){
                    {{first}}={{fullFirst}};
                    {{started}}=true;
                }else{
                    {{IndentBlock(step,"    ")}}
                }
                {{previous}}={{first}};
                """;
            return $$"""
                {
                    {{IndentBlock(range.RenderArgumentPreparation(start,count),"    ")}}
                    var {{end}}=(long){{start}}+{{count}};
                    if({{count}}>0){
                        var {{previous}}=0L;
                        var {{started}}=false;
                        {{keyLoop}}{
                            {{IndentBlock(bounds,"            ")}}
                            {{valueLoop}}{
                                {{consumerName}}.Consume((int){{value}});
                            }
                        }
                    }
                }
                """;
        }
        return $$"""
            {
                {{IndentBlock(range.RenderArgumentPreparation(start,count),"    ")}}
                var {{end}}=(long){{start}}+{{count}};
                if({{count}}>0){
                    {{keyLoop}}{
                        {{IndentBlock(bounds,"            ")}}
                        {{valueLoop}}{
                            {{consumerName}}.Consume((int){{value}});
                        }
                    }
                }
            }
            """;
    }

    private static string IndentBlock(string text,string indent) => GroupBySupport.IndentBlock(text,indent).TrimStart();

    // キー選択子が x => x%C(C は正の int の定数)か。
    public static bool TryGetModulus(FuncFragment? keySelector,out int modulus) {
        modulus=0;
        if(keySelector is null
            ||keySelector.ContainsAnchor
            ||keySelector.RequiresStatementInlining
            ||keySelector.CanInvokeAsDelegate
            ||!keySelector.CanInlineAssign
            ||keySelector.ParameterNames.Length!=1
            ||keySelector.ReturnTypeName!="int") {
            return false;
        }
        var body = SyntaxFactory.ParseExpression(keySelector.InlineBody);
        while(body is ParenthesizedExpressionSyntax parenthesized) {
            body=parenthesized.Expression;
        }
        if(body.ContainsDiagnostics
            ||body is not BinaryExpressionSyntax binary
            ||!binary.IsKind(SyntaxKind.ModuloExpression)
            ||binary.Left is not IdentifierNameSyntax left
            ||left.Identifier.ValueText!=keySelector.ParameterNames[0]
            ||binary.Right is not LiteralExpressionSyntax { Token.Value: int value }
            ||value<=0) {
            return false;
        }
        modulus=value;
        return true;
    }
}
