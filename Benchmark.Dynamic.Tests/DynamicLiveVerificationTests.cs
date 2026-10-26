using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Runtime.CompilerServices;
using Benchmark.Infrastructure;
using Benchmark.Dynamic.UI;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicLiveVerificationTests:IDisposable{
    private static string WorkerPath=>Path.Combine(AppContext.BaseDirectory,"Benchmark.Dynamic.Worker.exe");
    private readonly string directory=Path.Combine(Path.GetTempPath(),"Seamless-LiveHistory-"+Guid.NewGuid().ToString("N"));
    public void Dispose(){if(Directory.Exists(this.directory)){Directory.Delete(this.directory,true);}}

    // UI が再現テストに書く入力は、Worker が実行で使う入力と同じでなければならない。
    [Theory,Trait("Category","Unit")]
    [InlineData(SourceTarget.SystemArray,16)]
    [InlineData(SourceTarget.SystemList,7)]
    public void 再現テスト用の入力はWorkerが作る入力と一致する(SourceTarget target,int count){
        var seed=SeedDerivation.Derive(Seed256.Parse("0xF1F1"),"fixture",3);
        using var fixture=new SourceFixtureFactory().Create(target,count,seed);
        Assert.Equal(FixtureValues.Values(seed,"primary",count),ToInts(fixture.Primary));
        Assert.Equal(FixtureValues.Values(seed,"secondary",count),ToInts(fixture.Secondary));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(動的ライブ段階.ペア全組み合わせ)]
    [InlineData(動的ライブ段階.三段全組み合わせ)]
    [InlineData(動的ライブ段階.ランダム探索)]
    public void 各段階の要求件数は実Workerの上限内になる(動的ライブ段階 stage){
        var settings=new 動的ライブ設定(SourceTarget.SystemArray,QueryElementType.Int,Seed256.Parse("0x1"));
        var request=動的ライブ検証サービス.段階の要求を作る(settings,stage,0,"live-boundary");
        Assert.InRange(request.QueryCount,1,1000);
    }

    [Fact,Trait("Category","Unit")]
    public async Task ランダム段階は欠落重複なく上限内の次バッチへ進み履歴を分ける(){
        var databasePath=Path.Combine(this.directory,"batches.db");
        var client=new BatchClient();
        var service=new 動的ライブ検証サービス(WorkerPath,client,databasePath);
        var seed=Seed256.Parse("0x10182026");
        var settings=new 動的ライブ設定(SourceTarget.SystemArray,QueryElementType.Int,seed,
            開始段階:動的ライブ段階.ランダム探索,開始番号:998_500);
        var stages=new List<動的ライブ段階>();
        await service.実行Async(settings,(_,_)=>Task.CompletedTask,stages.Add,CancellationToken.None);
        Assert.Equal([998_500,999_500],client.Requests.Select(static request=>request.QueryIndexOffset));
        Assert.Equal([1_000,500],client.Requests.Select(static request=>request.QueryCount));
        Assert.All(client.Requests,request=>Assert.Equal(seed,request.BaseSeed));
        Assert.Equal(2,client.Requests.Select(static request=>request.RunId).Distinct().Count());
        Assert.Equal([動的ライブ段階.ランダム探索],stages);
        var history=new BenchmarkRepository(new BenchmarkDatabase(databasePath));
        Assert.Equal(2,history.GetRuns().Count);
        Assert.All(history.GetRuns(),static run=>Assert.Equal("Passed",run.Status));
    }

    [Fact,Trait("Category","Integration")]
    public async Task ランダム段階は実Workerで開始し取消後も結果を保存する(){
        var databasePath=Path.Combine(this.directory,"random.db");
        var service=new 動的ライブ検証サービス(WorkerPath,データベースパス:databasePath);
        var settings=new 動的ライブ設定(SourceTarget.SystemArray,QueryElementType.Int,Seed256.Parse("0x10182026"),
            開始段階:動的ライブ段階.ランダム探索);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(3));
        動的ライブ結果? first=null;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.実行Async(settings,(result,_)=>{
            first=result;
            cancellation.Cancel();
            return Task.CompletedTask;
        },null,cancellation.Token));
        Assert.NotNull(first);
        Assert.Equal(0,first.通番);
        Assert.False(string.IsNullOrWhiteSpace(first.式));
        Assert.False(string.IsNullOrWhiteSpace(first.生成コード));
        var history=new BenchmarkRepository(new BenchmarkDatabase(databasePath));
        var run=Assert.Single(history.GetRuns());
        Assert.Equal("Canceled",run.Status);
        var database=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(databasePath);
        await using var writer=new DynamicSingleWriterQueue(database);
        var snapshot=new global::Benchmark.Dynamic.Infrastructure.DynamicRepository(database,writer).GetRunSnapshot(run.ID);
        Assert.NotNull(snapshot);
        Assert.Equal(first.QueryId,Assert.Single(snapshot.Queries).QueryID);
        Assert.Equal(first.状態.ToString(),Assert.Single(snapshot.SourceExecutions).Status);
    }

    private sealed class BatchClient:IWorkerProcessClient{
        public List<WorkerRunRequest> Requests{get;}=[];
        public IWorkerProcessSession Start(string workerPath,IReadOnlyList<string>? arguments=null,string? workingDirectory=null)=>new BatchSession(this.Requests);
    }

    private sealed class BatchSession(List<WorkerRunRequest> requests):IWorkerProcessSession{
        private WorkerRunRequest? request;
        public int ProcessID=>1;
        public bool HasExited{get;private set;}
        public Task SendAsync<T>(string requestID,string messageType,T payload,CancellationToken cancellationToken=default){
            cancellationToken.ThrowIfCancellationRequested();
            if(messageType==WorkerMessageTypes.StartRun){
                this.request=Assert.IsType<WorkerRunRequest>(payload);
                requests.Add(this.request);
            }
            return Task.CompletedTask;
        }
        public async IAsyncEnumerable<WorkerMessageEnvelope> ReadAllAsync([EnumeratorCancellation]CancellationToken cancellationToken=default){
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            var run=this.request??throw new InvalidOperationException();
            yield return DynamicWorkerProtocol.Create(run.RunId,WorkerMessageTypes.RunCompleted,
                new WorkerRunCompletedEvent(run.RunId,WorkerExecutionStatus.Succeeded,run.QueryCount,0,DateTimeOffset.UtcNow));
        }
        public Task<bool> CancelAsync(string requestID){this.HasExited=true;return Task.FromResult(true);}
        public Task<bool> ShutdownAsync(string requestID){this.HasExited=true;return Task.FromResult(true);}
        public Task<string> GetStandardErrorAsync()=>Task.FromResult(string.Empty);
        public ValueTask DisposeAsync(){this.HasExited=true;return ValueTask.CompletedTask;}
    }

    private static int[] ToInts(object source)=>((IEnumerable<int>)source).ToArray();

    [Fact,Trait("Category","Unit")]
    public void 段階の要求は全組み合わせとランダム探索で作り分ける(){
        var settings=new 動的ライブ設定(SourceTarget.SystemArray,QueryElementType.Int,Seed256.Parse("0x1"));
        var pair=動的ライブ検証サービス.段階の要求を作る(settings,動的ライブ段階.ペア全組み合わせ,0,"run-pair");
        Assert.Equal(QueryGenerationMode.Exhaustive,pair.GenerationMode);
        Assert.Equal(1,pair.ExhaustiveIntermediateCount);
        Assert.Equal(2,pair.OperatorCount);
        Assert.Equal(pair.OperatorCount,pair.MaximumDepth);
        Assert.True(pair.ExpectExceptions);
        Assert.True(pair.QueryCount>100);

        var shifted=動的ライブ検証サービス.段階の要求を作る(settings,動的ライブ段階.ペア全組み合わせ,10,"run-pair");
        Assert.Equal(10,shifted.QueryIndexOffset);
        Assert.Equal(pair.QueryCount-10,shifted.QueryCount);

        var random=動的ライブ検証サービス.段階の要求を作る(settings,動的ライブ段階.ランダム探索,25,"run-random");
        Assert.Equal(QueryGenerationMode.Random,random.GenerationMode);
        Assert.Equal(25,random.QueryIndexOffset);
        Assert.Equal(4,random.OperatorCount);
    }

    [Fact,Trait("Category","Unit")]
    public async Task int以外の要素型はRangeでは使えない(){
        var service=new 動的ライブ検証サービス(WorkerPath,データベースパス:Path.Combine(this.directory,"history.db"));
        var settings=new 動的ライブ設定(SourceTarget.SystemRange,QueryElementType.Long,Seed256.Parse("0x1"));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.実行Async(settings,(_,_)=>Task.CompletedTask,null,CancellationToken.None));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryElementType.Int)]
    [InlineData(QueryElementType.String)]
    public async Task ペア全組み合わせを実Workerで検証して結果を順に受け取れる(QueryElementType type){
        Assert.True(File.Exists(WorkerPath),$"Worker was not found: {WorkerPath}");
        var databasePath=Path.Combine(this.directory,"history.db");
        var service=new 動的ライブ検証サービス(WorkerPath,データベースパス:databasePath);
        var settings=new 動的ライブ設定(SourceTarget.SystemArray,type,Seed256.Parse("0x5EED"),入力要素数:16,特殊な値を混ぜる:type!=QueryElementType.Int);
        using var cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var results=new List<動的ライブ結果>();
        var stages=new List<動的ライブ段階>();
        try{
            await service.実行Async(
                settings,
                (result,token)=>{
                    results.Add(result);
                    if(results.Count>=6){
                        cancellation.Cancel();
                    }
                    return Task.CompletedTask;
                },
                stages.Add,
                cancellation.Token);
        }catch(OperationCanceledException){
        }

        Assert.Equal(6,results.Count);
        Assert.Equal(動的ライブ段階.ペア全組み合わせ,stages[0]);
        Assert.All(results,result=>{
            Assert.Equal(type,result.要素型);
            Assert.False(string.IsNullOrWhiteSpace(result.式));
            Assert.False(string.IsNullOrWhiteSpace(result.生成コード));
            Assert.Equal(動的ライブ段階.ペア全組み合わせ,result.段階);
        });
        // 展開後のコードは、コンパイルまで進んだ結果に付く。
        Assert.Contains(results,static result=>result.展開コード.Length>0);
        // int のときは、再現テストが単独で動く形で付く。
        if(type==QueryElementType.Int){
            Assert.All(results,static result=>Assert.Contains("[Fact",result.再現テスト,StringComparison.Ordinal));
        }
        var history=new BenchmarkRepository(new BenchmarkDatabase(databasePath));
        var run=Assert.Single(history.GetRuns());
        Assert.Equal("Canceled",run.Status);
        var measurements=history.GetMeasurements(run.ID);
        Assert.NotEmpty(measurements);
        Assert.Contains(measurements,static value=>value.Conditions.Implementation=="FLinqUnoptimized");
        Assert.All(measurements,static value=>{
            Assert.True(value.Conditions.Known);
            Assert.Equal(7,value.Samples.Length);
            Assert.Equal(16,value.Conditions.Count);
            Assert.NotEmpty(value.Conditions.InputHash);
        });
    }
}
