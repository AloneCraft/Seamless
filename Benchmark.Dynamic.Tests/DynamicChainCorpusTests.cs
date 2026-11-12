using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// チェーン検証(Sample.FLinqChainVerifier)の「ペア網羅」を、Benchmark.Dynamic の実Worker経路で回す。
/// 中間演算子1個+終端1個の全組み合わせを、System / FLinq(最適化なし・あり) / ZLinq の各経路で
/// コンパイルして結果を比べる。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicChainCorpusTests{
    private const int InputElementCount=16;

    // チェーン検証のペア網羅が使っていた終端(ToArray/Count/Sum/FirstOrDefault/Any)に、後から足した Single / SingleOrDefault を加えたもの。
    private static readonly string[] PairTerminals=[
        QueryOperatorIds.ToArray,
        QueryOperatorIds.Count,
        QueryOperatorIds.Sum,
        QueryOperatorIds.FirstOrDefault,
        QueryOperatorIds.Any,
        QueryOperatorIds.Single,
        QueryOperatorIds.SingleOrDefault,
    ];

    // 2026-10-22 時点では全て直して表は空だった。以降に見つかったものだけを下に記録する。
    // 新しく見つかった不具合は、直すか、ここに並びと理由を記録する(直ったら外す)。
    private static readonly Dictionary<string,string> KnownFlinqDefects=new(StringComparer.Ordinal){
        // 2026-11-12 に見つけた。GroupBy の直後の Select(g=>g.Key*10+g.Count()) を終端へ渡すと、FLinq(最適化なし)の生成コードが
        // 作っていない補助フィールド(__groupCount__ / __groupKeys__ / __groupAccs__)を参照して CS1061 になる。生成される式は演算子追加前と同じ(追加前に通っていたかは未検証)。
        ["groupBy.count > toArray"]="FLinq生成コードが未定義の__groupCount__等を参照する(CS1061)",
        ["groupBy.count > count"]="FLinq生成コードが未定義の__groupCount__等を参照する(CS1061)",
        // Consume形(列.Consume(consumer) の文)で受けるときだけ、Reverse を2回重ねた列を FLinq が展開できない(FLINQ002)。メソッド形では通る。
        ["reverse > reverse > toList"]="Consume形で FLINQ002(展開できない)",
        // 2026-11-12 の2演算子全組み合わせ(9961件)で見つかった。Where の後ろの Reverse が、FLinq(最適化なし)で効かず、
        // 先頭の要素を返す(System.Linq は 0、FLinq 最適化なしは -10)。演算子追加とは無関係(新しい演算子を含まない並び)。
        ["where > reverse > firstOrDefault"]="FLinq(最適化なし)で Where 後の Reverse が効かない(意味の不一致)",
    };

    [Fact,Trait("Category","ChainCorpus")]
    public async Task 中間演算子1個と終端の全組み合わせがSystemArrayで全経路一致する(){
        await RunPairsAsync(SourceTarget.SystemArray);
    }

    // 中間演算子2個+終端の全組み合わせは数千通りになるので、通常は25件に1件を決定的に抜き取る。
    [Fact,Trait("Category","ChainCorpus")]
    public async Task 中間演算子2個と終端の組み合わせを抜き取ってSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:25,requireKnownDefectsToFail:true);
    }

    // 夜間用。全組み合わせを回す(数千件、1時間前後)。
    [Fact,Trait("Category","ChainCorpusFull")]
    public async Task 中間演算子2個と終端の全組み合わせがSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:1,requireKnownDefectsToFail:false);
    }

    [Fact,Trait("Category","ChainCorpus")]
    public void ペア網羅は成り立つ並びを十分な数で含む(){
        var target=SourceTarget.SystemArray;
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var generated=ExhaustiveQueryEnumerator.Enumerate(1,intermediates,PairTerminals)
            .Count(spec=>ExhaustiveQueryEnumerator.TryGenerate(spec,Seed256.Parse("0xC0DE"),target,InputElementCount,out _));
        Assert.True(generated>=140,$"generated={generated}");
    }

    // 手書きのチェイン2(Chain2)が受け持っていた、Consume(consumer) の文で受ける形。ToArray / ToList の終端だけが対象。
    private static readonly string[] ConsumeTerminals=[QueryOperatorIds.ToArray,QueryOperatorIds.ToList];

    [Fact,Trait("Category","ChainCorpus")]
    public async Task Consume形で受ける中間演算子1個の全組み合わせがSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,1,stride:1,requireKnownDefectsToFail:false,QueryEmissionStyle.Consume,ConsumeTerminals);
    }

    [Fact,Trait("Category","ChainCorpus")]
    public async Task Consume形で受ける中間演算子2個の組み合わせを抜き取ってSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:7,requireKnownDefectsToFail:false,QueryEmissionStyle.Consume,ConsumeTerminals);
    }

    // 手書きのチェイン2の Query() 相当。Where / Select / OrderBy をクエリ構文で書く(それ以外の演算子はメソッド形のまま)。
    [Fact,Trait("Category","ChainCorpus")]
    public async Task クエリ構文で書く中間演算子2個の組み合わせを抜き取ってSystemArrayで全経路一致する(){
        await RunAsync(SourceTarget.SystemArray,2,stride:25,requireKnownDefectsToFail:false,QueryEmissionStyle.QuerySyntax);
    }

    // 削除した手書きのチェイン2のうち、自動側で表せる組み合わせ(Where_Where、Where_Select、Select_Select、Select_Where、Select_Take、
    // Skip_Take、SkipLast_SkipLast、TakeLast_TakeLast、Concat_Select、Index_Select、Where_ToArray、Where_Count、Select_Sum)を、
    // 抜き取りに頼らず必ず3つの書き方(メソッド形・Consume形・クエリ構文)で回す。
    private static IEnumerable<ExhaustiveQuerySpec> HandWrittenChain2Specs(string sequenceTerminal){
        string[][] pairs=[
            [QueryOperatorIds.Where,QueryOperatorIds.Where],
            [QueryOperatorIds.Where,QueryOperatorIds.Select],
            [QueryOperatorIds.Select,QueryOperatorIds.Select],
            [QueryOperatorIds.Select,QueryOperatorIds.Where],
            [QueryOperatorIds.Select,QueryOperatorIds.Take],
            [QueryOperatorIds.Skip,QueryOperatorIds.Take],
            [QueryOperatorIds.SkipLast,QueryOperatorIds.SkipLast],
            [QueryOperatorIds.TakeLast,QueryOperatorIds.TakeLast],
            [QueryOperatorIds.Concat,QueryOperatorIds.Select],
            [QueryOperatorIds.IndexXor,QueryOperatorIds.Select],
        ];
        var ordinal=900_000L;
        foreach(var pair in pairs){
            yield return new(ordinal++,pair,sequenceTerminal);
        }
        yield return new(ordinal++,[QueryOperatorIds.Where],sequenceTerminal);
        yield return new(ordinal++,[QueryOperatorIds.Where],QueryOperatorIds.Count);
        yield return new(ordinal++,[QueryOperatorIds.Select],QueryOperatorIds.Sum);
    }

    [Theory,Trait("Category","ChainCorpus")]
    [InlineData(QueryEmissionStyle.Method)]
    [InlineData(QueryEmissionStyle.Consume)]
    [InlineData(QueryEmissionStyle.QuerySyntax)]
    public async Task 手書きチェイン2の代表の組み合わせが3つの書き方で全経路一致する(QueryEmissionStyle style){
        await RunAsync(SourceTarget.SystemArray,2,stride:1,requireKnownDefectsToFail:false,style,specs:HandWrittenChain2Specs(QueryOperatorIds.ToArray));
    }

    // 2026-11-12 の2演算子全組み合わせで見つかった FLinq の限界。Cast<object>().Cast<T>() / OfType<T>() の直後(または直前)に
    // OrderBy / OrderBy(キー) / Reverse が並び、Count / Sum / Any で終わると、FLinq(最適化あり)が FLINQ002(展開できない)になる。
    // 型を消した列の並べ替えを、スカラーの終端へ畳めない。結果は System.Linq と食い違わない(実行時に例外になる)。
    private static bool IsKnownCastReorderLimit(ExhaustiveQuerySpec spec,WorkerExecutionStatus status){
        if(status!=WorkerExecutionStatus.ExpansionIncomplete||spec.Operators.Count!=2){
            return false;
        }
        static bool IsCast(string id)=>id is QueryOperatorIds.Cast or QueryOperatorIds.OfType;
        static bool IsReorder(string id)=>id is QueryOperatorIds.OrderBy or QueryOperatorIds.OrderByKey or QueryOperatorIds.Reverse;
        var adjacent=(IsCast(spec.Operators[0])&&IsReorder(spec.Operators[1]))||(IsReorder(spec.Operators[0])&&IsCast(spec.Operators[1]));
        return adjacent&&spec.Terminal is QueryOperatorIds.Count or QueryOperatorIds.Sum or QueryOperatorIds.Any;
    }

    private static Task RunPairsAsync(SourceTarget target)=>RunAsync(target,1,stride:1,requireKnownDefectsToFail:false);

    private static async Task RunAsync(
        SourceTarget target,
        int intermediateCount,
        int stride,
        bool requireKnownDefectsToFail,
        QueryEmissionStyle style=QueryEmissionStyle.Method,
        IReadOnlyList<string>? terminals=null,
        IEnumerable<ExhaustiveQuerySpec>? specs=null){
        var seed=Seed256.Parse("0xC0DE");
        var runner=new DynamicWorkerExecutionRunner();
        var intermediates=ExhaustiveQueryEnumerator.GetIntermediateOperatorIds(target);
        var failures=new List<string>();
        var knownFailed=new HashSet<string>(StringComparer.Ordinal);
        var executed=0;
        foreach(var spec in specs??ExhaustiveQueryEnumerator.Enumerate(intermediateCount,intermediates,terminals??PairTerminals)){
            if(spec.Ordinal%stride!=0
                ||!ExhaustiveQueryEnumerator.TryGenerate(spec,seed,target,InputElementCount,out var query)){
                continue;
            }
            query=query!with{Style=style};
            var request=new WorkerRunRequest(
                $"corpus-{target}-{spec.Ordinal}",
                seed,
                1,
                query!.RequestedOperatorCount,
                query.MaximumDepth,
                InputElementCount,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("Corpus-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());
            var result=await runner.ExecuteAsync(request,query,(int)spec.Ordinal,target,0,CancellationToken.None);
            ChainCorpusProgress.Record(intermediateCount,stride,spec.Ordinal,spec.Operators,spec.Terminal,result.Status,result.AssemblyUnloaded);
            executed++;
            var key=$"{string.Join(" > ",spec.Operators)} > {spec.Terminal}";
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                // groupBy.count の後ろは、何が続いても同じ不具合(未定義の __groupCount__ を参照)になる。
                if(KnownFlinqDefects.ContainsKey(key)||IsKnownCastReorderLimit(spec,result.Status)||(result.DiagnosticsJson?.Contains("__groupCount__",StringComparison.Ordinal)??false)){
                    knownFailed.Add(key);
                    continue;
                }
                failures.Add(
                    $"{string.Join(" > ",spec.Operators)} > {spec.Terminal}: Status={result.Status}, ALC={result.AssemblyUnloaded}{Environment.NewLine}"+
                    $"{Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}{Environment.NewLine}{result.DiagnosticsJson}{Environment.NewLine}{result.ResultJson}");
            }
        }
        Assert.True(executed>0);
        if(requireKnownDefectsToFail){
            // 表のキーは「中間演算子 > ... > 終端」。全件(stride 1)で回したときの、同じ段数の並びだけを直ったかの判定対象にする。
            var fixedAlready=KnownFlinqDefects.Keys.Where(key=>key.Split(" > ").Length-1==intermediateCount&&stride==1).Except(knownFailed).ToArray();
            Assert.True(
                fixedAlready.Length==0,
                $"既知の不具合として表にあるが、今回は通った(直ったなら表から外す): {string.Join(", ",fixedAlready)}");
        }
        Assert.True(
            failures.Count==0,
            $"{failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine+Environment.NewLine,failures)}");
    }
}
