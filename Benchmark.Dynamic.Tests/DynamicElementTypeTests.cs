using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// int 以外の要素型のランダムなチェーンを、実Worker経路(System / FLinq 最適化なし・あり / ZLinq)で比べる。
/// 要素型の次元を足して初めて見つかった FLinq の不具合は、直さずに「既知の不具合」として記録している
/// (パターンと、最小の再現)。直ったら、対応する表と再現テストを直す。
/// </summary>
[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicElementTypeTests{
    private const int InputElementCount=16;
    private const int QueriesPerType=30;

    public static TheoryData<QueryElementType> NonIntTypes=>new(
        Enum.GetValues<QueryElementType>().Where(static type=>type!=QueryElementType.Int));

    // 症状のパターンで見分ける FLinq の不具合(どの要素型・どの並びでも、診断にその兆候が出れば既知とする)。
    private sealed record DefectPattern(string Reason,Func<WorkerSourceExecutionCompletedEvent,bool> Matches);

    private static readonly DefectPattern[] DefectPatterns=[
        new(
            "FLinq がラムダの中の new T(...) の型名を変数のように hoist し、new this.__hoistN(...) を出してコンパイルできない(CS1526)",
            static result=>(result.DiagnosticsJson??string.Empty).Contains("\"id\":\"CS1526\"",StringComparison.Ordinal)),
        new(
            "FLinq が hoist した変数 __hoistN を宣言せず、生成コードがコンパイルできない(CS0103 / CS1061)。ExceptBy の後の述語などで、int 以外の要素型だけに出る",
            static result=>(result.DiagnosticsJson??string.Empty).Contains("__hoist",StringComparison.Ordinal)),
    ];

    // 個別の食い違い。キーは "<要素型>|<式>"(固定シード 0xE1E7・入力16件・SystemArray で再現した)。
    private static readonly Dictionary<string,string> KnownMismatches=new(StringComparer.Ordinal){
        ["Order|source.UnionBy(inner,p=>p).Distinct().Any(p=>p<-11)"]="FLinq(最適化あり)だけが True を返す。System.Linq・FLinq(最適化なし)・ZLinq は False。",
        ["String|source.UnionBy(inner,p=>p).Select(p=>p+(-6)).Any(p=>p<8)"]="FLinq(最適化あり)だけが True を返す。System.Linq・FLinq(最適化なし)・ZLinq は False。",
        ["String|source.Distinct().Prepend(-63).Any(p=>p<0)"]="FLinq(最適化あり)だけが False を返す(先頭に足した \"-63\" を見落とす)。System.Linq・FLinq(最適化なし)・ZLinq は True。",
    };

    private static RandomQueryGenerationOptions Options(QueryElementType type,int index,SourceTarget target=SourceTarget.SystemArray){
        var capability=QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[target],type);
        return new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0xE1E7"),
            QueryIndex=index,
            QueryId=$"typed-{type}-{index:D3}",
            SourceTarget=target,
            OperatorCount=3,
            MaximumDepth=3,
            InputElementCount=InputElementCount,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
            AllowedOperatorIds=capability.OperatorIds,
            ElementType=type,
        };
    }

    private static WorkerRunRequest Request(QueryElementType type,SourceTarget target=SourceTarget.SystemArray)=>new(
        $"typed-{type}",
        Seed256.Parse("0xE1E7"),
        1,
        3,
        3,
        InputElementCount,
        OperatorSetMode.Maximum,
        [target],
        new WorkerMeasurementProfile("Typed-v1",0,1,0,1),
        RandomQueryGenerator.GeneratorVersion,
        QueryCapabilityProfile.CurrentProfileVersion.ToString(),
        BuiltInOperatorCatalog.OperatorSetVersion.ToString(),
        ElementType:type);

    [Theory,Trait("Category","ChainCorpus")]
    [MemberData(nameof(NonIntTypes))]
    public async Task 要素型ごとのランダムなチェーンが全経路で一致する(QueryElementType type){
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        for(var index=0;index<QueriesPerType;index++){
            var query=new RandomQueryGenerator().Generate(Options(type,index));
            var result=await runner.ExecuteAsync(Request(type),query,index,SourceTarget.SystemArray,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.Succeeded&&result.AssemblyUnloaded){
                continue;
            }
            var key=$"{type}|{Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}";
            if(KnownMismatches.ContainsKey(key)||DefectPatterns.Any(pattern=>pattern.Matches(result))){
                continue;
            }
            failures.Add($"#{index} Status={result.Status}{Environment.NewLine}  {key}{Environment.NewLine}  {ExtractDifference(result)}");
        }
        Assert.True(
            failures.Count==0,
            $"{type}: {failures.Count}/{QueriesPerType} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    // List と IEnumerable の Source でも(IEnumerable は配列と別の展開コードを通る)。少なめの件数で。
    [Theory,Trait("Category","ChainCorpus")]
    [MemberData(nameof(NonIntTypes))]
    public async Task 要素型ごとのチェーンがListとIEnumerableのSourceでも全経路で一致する(QueryElementType type){
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        var executed=0;
        foreach(var target in new[]{SourceTarget.SystemList,SourceTarget.SystemIEnumerable}){
            for(var index=0;index<12;index++){
                var query=new RandomQueryGenerator().Generate(Options(type,index,target));
                var request=Request(type,target) with{IncludeSpecialValues=index%2==1};
                var result=await runner.ExecuteAsync(request,query,index,target,0,CancellationToken.None);
                executed++;
                if(result.Status==WorkerExecutionStatus.Succeeded&&result.AssemblyUnloaded){
                    continue;
                }
                var key=$"{type}|{Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}";
                if(KnownMismatches.ContainsKey(key)||DefectPatterns.Any(pattern=>pattern.Matches(result))){
                    continue;
                }
                failures.Add($"{target} #{index} Status={result.Status}{Environment.NewLine}  {key}{Environment.NewLine}  {ExtractDifference(result)}");
            }
        }
        Assert.Equal(24,executed);
        Assert.True(
            failures.Count==0,
            $"{type}: {failures.Count}/{executed} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    // 既知の不具合が今も再現すること(直ったら、この再現と上の表を外す)。
    [Theory,Trait("Category","ChainCorpus")]
    [InlineData(QueryElementType.CollidingKey,0,WorkerExecutionStatus.CompilationFailed,"\"id\":\"CS1526\"")]
    [InlineData(QueryElementType.Int256Struct,27,WorkerExecutionStatus.CompilationFailed,"__hoist")]
    [InlineData(QueryElementType.Order,27,WorkerExecutionStatus.CompilationFailed,"__hoist")]
    [InlineData(QueryElementType.Order,0,WorkerExecutionStatus.SemanticMismatch,"\"fLinq\":{\"isMatch\":false")]
    [InlineData(QueryElementType.String,0,WorkerExecutionStatus.SemanticMismatch,"\"fLinq\":{\"isMatch\":false")]
    [InlineData(QueryElementType.String,2,WorkerExecutionStatus.SemanticMismatch,"\"fLinq\":{\"isMatch\":false")]
    public async Task 既知のFLinqの不具合は今も再現する(QueryElementType type,int index,WorkerExecutionStatus expectedStatus,string needle){
        var query=new RandomQueryGenerator().Generate(Options(type,index));

        var result=await new DynamicWorkerExecutionRunner().ExecuteAsync(
            Request(type),query,index,SourceTarget.SystemArray,0,CancellationToken.None);

        Assert.Equal(expectedStatus,result.Status);
        Assert.Contains(needle,(result.DiagnosticsJson??string.Empty)+(result.ResultJson??string.Empty),StringComparison.Ordinal);
    }

    // System.Linq は Sum の選択子を unchecked で実行するが、FLinq(最適化なし)は選択子を checked { } の中へ取り込むので、
    // 選択子の中の桁あふれ(ここでは long から int への縮小)が例外になる。最適化ありでは、この並びでは出ていない。
    // 直ったらこのテストを反転する。
    [Fact,Trait("Category","ChainCorpus")]
    public void 既知のFLinqの不具合_Sumの選択子がcheckedの中で実行され縮小変換が例外になる(){
        var query=new RandomQueryGenerator().Generate(Options(QueryElementType.Long,18));
        Assert.Equal("source.Take(8).SkipWhile(p=>(p&1)!=0).Sum(p=>(long)p)",Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query));
        var emitted=new QuerySourceEmitter().Emit(query,new QuerySourceEmissionOptions{ClassName="CheckedSum"});
        // 選択子は、現在は unchecked((int)value) で書いている。縮小変換をそのまま書いた形に戻して、FLinq の振る舞いを見る。
        const string withUnchecked="(long)unchecked((int)value)";
        Assert.Contains(withUnchecked,emitted.SourceText,StringComparison.Ordinal);
        var source=emitted.SourceText.Replace(withUnchecked,"(long)(int)value",StringComparison.Ordinal);
        var compiled=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest(
            "Benchmark.Dynamic.CheckedSumProbe",emitted.QualifiedTypeName,source,"CheckedSum.g.cs"));
        Assert.True(compiled.Success,string.Join(Environment.NewLine,compiled.Diagnostics.Where(static d=>d.Severity=="Error").Select(static d=>d.ID+": "+d.Message)));
        var loaded=compiled.LoadedQuery!;
        var seed=SeedDerivation.Derive(Seed256.Parse("0xE1E7"),"fixture",18);
        using var fixture=new SourceFixtureFactory().Create(SourceTarget.SystemArray,InputElementCount,seed,QueryElementType.Long);

        // System.Linq は桁あふれしない(unchecked の選択子)。
        var expected=loaded.Run(DynamicImplementationKind.System,fixture.Primary,fixture.Secondary);
        Assert.Equal(QueryCardinality.Scalar,expected.Cardinality);
        // FLinq(最適化なし)は OverflowException。
        Assert.Throws<OverflowException>(()=>loaded.Run(DynamicImplementationKind.FLinqUnoptimized,fixture.Primary,fixture.Secondary));
        // 例外の呼び出し履歴が動的アセンブリの型を指すので、アセンブリの解放の確認はここでは行わない。
    }

    // 特殊な値(NaN・null・境界値・-0 など)を入力に混ぜた版。型ごとの特殊な値は ElementTypeDescriptor.SpecialValues。
    [Theory,Trait("Category","ChainCorpus")]
    [MemberData(nameof(NonIntTypes))]
    public async Task 特殊な値を混ぜた入力でも要素型ごとのチェーンが全経路で一致する(QueryElementType type){
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        for(var index=0;index<QueriesPerType;index++){
            var query=new RandomQueryGenerator().Generate(Options(type,index));
            var request=Request(type) with{IncludeSpecialValues=true};
            var result=await runner.ExecuteAsync(request,query,index,SourceTarget.SystemArray,0,CancellationToken.None);
            if(result.Status==WorkerExecutionStatus.Succeeded&&result.AssemblyUnloaded){
                continue;
            }
            var key=$"{type}|{Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query)}";
            if(KnownMismatches.ContainsKey(key)||DefectPatterns.Any(pattern=>pattern.Matches(result))){
                continue;
            }
            failures.Add($"#{index} Status={result.Status}{Environment.NewLine}  {key}{Environment.NewLine}  {ExtractDifference(result)}");
        }
        Assert.True(
            failures.Count==0,
            $"{type}: {failures.Count}/{QueriesPerType} failed:{Environment.NewLine}{string.Join(Environment.NewLine,failures)}");
    }

    private static string ExtractDifference(WorkerSourceExecutionCompletedEvent result){
        var json=result.ResultJson??string.Empty;
        var at=json.IndexOf("\"comparisons\"",StringComparison.Ordinal);
        if(at>=0){
            return json[at..Math.Min(json.Length,at+360)];
        }
        var diagnostics=result.DiagnosticsJson??string.Empty;
        var error=diagnostics.IndexOf("\"severity\":\"Error\"",StringComparison.Ordinal);
        return error>=0?diagnostics[error..Math.Min(diagnostics.Length,error+360)]:diagnostics[..Math.Min(diagnostics.Length,300)];
    }
}
