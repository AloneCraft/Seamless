using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Benchmark.Dynamic;

public sealed record 動的実行設定(
    string 実行ID,
    int Query数,
    int N,
    int 入力要素数,
    int 最大深度,
    Seed256 Seed,
    OperatorSetMode 演算子集合モード,
    IReadOnlyList<SourceTarget> 対象一覧,
    string? 親実行ID=null,
    IReadOnlyList<WorkerReplayQuery>? 再実行Query一覧=null,
    WorkerMeasurementProfile? 計測ProfileOverride=null);

public sealed record 動的実行進捗(
    int QueryIndex,
    int Query総数,
    SourceTarget 対象,
    string 段階,
    int 完了数,
    int コンパイル失敗数,
    int 意味不一致数,
    int 非収束数,
    int 実行例外数);

public sealed record 動的実行条件Snapshot(
    int SchemaVersion,
    int QueryCount,
    int OperatorCount,
    int InputElementCount,
    int MaximumDepth,
    string BaseSeed,
    OperatorSetMode Mode,
    IReadOnlyList<SourceTarget> SourceTargets,
    WorkerMeasurementProfile MeasurementProfile,
    string GeneratorVersion,
    int CapabilityProfileVersion,
    int OperatorSetVersion,
    string CapabilityId,
    string? ReplayedFromRunId=null);

public interface I動的実行調停サービス{
    Task<動的履歴項目> 実行Async(
        動的実行設定 設定,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時);
    Task<動的履歴項目> Runを再実行Async(
        string 親実行ID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時);
    Task<動的履歴項目> Queryを再実行Async(
        string 親QueryID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時);
    Task<動的履歴項目> 複数Queryを再実行Async(
        IReadOnlyList<string> 親QueryID一覧,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時);
    Task<動的履歴項目> Queryを縮小Async(
        string 親QueryID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時);
    Task キャンセルAsync();
    IReadOnlyList<動的履歴項目> 履歴を取得(bool ごみ箱か);
    動的実行条件Snapshot 実行条件を取得(string 実行ID);
    Task ごみ箱へ移動Async(string 実行ID);
    Task 復元Async(string 実行ID);
    Task 完全削除Async(string 実行ID);
}

public sealed class 動的実行調停サービス:I動的実行調停サービス,IAsyncDisposable{
    private readonly DynamicDatabase DB;
    private readonly DynamicSingleWriterQueue Writer;
    private readonly DynamicRepository Repository;
    private readonly IWorkerProcessClient WorkerClient;
    private readonly string WorkerPath;
    private readonly WorkerMeasurementProfile MeasurementProfile;
    private readonly SemaphoreSlim 実行Gate=new(1,1);
    private CancellationTokenSource? 実行キャンセル;
    private IWorkerProcessSession? WorkerSession;

    public 動的実行調停サービス():this(
        DynamicDatabasePathResolver.Resolve(ソリューションルートを検索()),
        Workerパスを解決(),
        new WorkerMeasurementProfile()){
    }

    public 動的実行調停サービス(
        string DBパス,
        string Workerパス,
        WorkerMeasurementProfile? 計測プロファイル=null,
        IWorkerProcessClient? WorkerClient=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(DBパス);
        ArgumentException.ThrowIfNullOrWhiteSpace(Workerパス);
        this.WorkerPath=Path.GetFullPath(Workerパス);
        this.MeasurementProfile=計測プロファイル??new WorkerMeasurementProfile();
        this.WorkerClient=WorkerClient??new VersionedJsonLineWorkerProcessClient();
        this.DB=new DynamicDatabase(Path.GetFullPath(DBパス));
        this.DB.InitializeIfMissing();
        this.Writer=new DynamicSingleWriterQueue(this.DB);
        this.Repository=new DynamicRepository(this.DB,this.Writer);
    }

    public async Task<動的履歴項目> 実行Async(
        動的実行設定 設定,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        ArgumentNullException.ThrowIfNull(設定);
        ArgumentNullException.ThrowIfNull(結果受信時);
        ArgumentNullException.ThrowIfNull(進捗受信時);
        _=実行設定を検証(設定);
        await this.実行Gate.WaitAsync().ConfigureAwait(false);
        try{
            return await this.実行CoreAsync(設定,結果受信時,進捗受信時).ConfigureAwait(false);
        }finally{
            if(this.WorkerSession is not null){
                await this.WorkerSession.DisposeAsync().ConfigureAwait(false);
                this.WorkerSession=null;
            }
            this.実行キャンセル?.Dispose();
            this.実行キャンセル=null;
            this.実行Gate.Release();
        }
    }

    private async Task<動的履歴項目> 実行CoreAsync(
        動的実行設定 設定,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        var 開始日時=DateTimeOffset.UtcNow;
        this.実行キャンセル=new CancellationTokenSource();
        var Token=this.実行キャンセル.Token;
        var Capability=QueryCapabilityProfiles.CreateSet(設定.演算子集合モード,設定.対象一覧);
        var 計測Profile=設定.計測ProfileOverride??this.MeasurementProfile;
        var 条件JSON=JsonSerializer.Serialize(
            new 動的実行条件Snapshot(
                1,
                設定.Query数,
                設定.N,
                設定.入力要素数,
                設定.最大深度,
                設定.Seed.ToString(),
                設定.演算子集合モード,
                設定.対象一覧.Order().ToArray(),
                計測Profile,
                RandomQueryGenerator.GeneratorVersion,
                Capability.ProfileVersion,
                Capability.OperatorSetVersion,
                Capability.CapabilityId,
                設定.親実行ID),
            DynamicWorkerProtocol.JsonOptions);
        var Run=new DynamicRunRecord(
            設定.実行ID,
            "Running",
            unchecked((long)設定.Seed.A),
            計測Profile.ProfileId,
            条件JSON,
            開始日時,
            開始日時,
            AppVersion:typeof(App).Assembly.GetName().Version?.ToString()??string.Empty,
            StartedAt:開始日時,
            ReplayedFromRunID:設定.親実行ID);
        var ReplayByIndex=設定.再実行Query一覧?.ToDictionary(static Query=>Query.QueryIndex);
        var 予定Query一覧=Enumerable.Range(0,設定.Query数).Select(QueryIndex=>{
            var QueryID=$"{設定.実行ID}-q{QueryIndex:D6}";
            var QuerySeed=SeedDerivation.Derive(設定.Seed,"query",QueryIndex);
            WorkerReplayQuery? Replay=null;
            _=ReplayByIndex?.TryGetValue(QueryIndex,out Replay);
            return new DynamicQueryRecord(
                QueryID,
                設定.実行ID,
                QueryIndex,
                Replay?.DerivationKind??"SharedIR",
                unchecked((long)QuerySeed.A),
                "null",
                string.Empty,
                string.Empty,
                "NotStarted",
                Replay?.ParentQueryId);
        }).ToArray();
        var 予定実行一覧=予定Query一覧.SelectMany(Query=>
            設定.対象一覧.Select((対象,Source順序)=>new DynamicSourceExecutionRecord(
                $"{Query.QueryID}-{対象}-a1",
                Query.QueryID,
                対象.ToString(),
                1,
                checked(Query.Ordinal*設定.対象一覧.Count+Source順序),
                "NotStarted"))).ToArray();
        await this.Repository.SaveCheckpointAsync(
            new DynamicRunCheckpoint(
                Run,
                Sources:設定.対象一覧.Select((対象,順序)=>new DynamicRunSourceRecord(
                設定.実行ID,
                対象.ToString(),
                true,
                順序)).ToArray(),
                Queries:予定Query一覧,
                SourceExecutions:予定実行一覧),
            CancellationToken.None).ConfigureAwait(false);
        var Query開始一覧=new Dictionary<string,WorkerQueryStartedEvent>(StringComparer.Ordinal);
        var Source開始一覧=new Dictionary<(string QueryID,SourceTarget 対象),WorkerSourceExecutionStartedEvent>();
        var 現在QueryIndex=0;
        var 現在対象=設定.対象一覧[0];
        var 最新進捗=new WorkerProgressEvent(設定.実行ID,0,checked(設定.Query数*設定.対象一覧.Count),0,0,0,0,false);
        var 最終状態="Failed";
        var 致命障害か=false;
        var 完了済み実行一覧=new HashSet<WorkerCompletedSourceExecution>();
        var 失敗Source数=0;
        var RunEventSequence=0;
        var Worker再起動回数=0;
        try{
            while(true){
                Token.ThrowIfCancellationRequested();
                var ALC解放失敗後に再起動するか=false;
                var Session終了か=false;
                var Session=this.WorkerClient.Start(this.WorkerPath);
                this.WorkerSession=Session;
                try{
                    var ResumeState=完了済み実行一覧.Count==0
                        ?null
                        :new WorkerRunResumeState(
                            完了済み実行一覧.OrderBy(static 実行=>実行.QueryIndex)
                                .ThenBy(static 実行=>実行.SourceTarget)
                                .ToArray(),
                            失敗Source数,
                            最新進捗.CompilationFailures,
                            最新進捗.SemanticMismatches,
                            最新進捗.NonConvergences,
                            最新進捗.ExecutionFailures);
                    var RequestID=設定.実行ID;
                    await Session.SendAsync(
                        RequestID,
                        WorkerMessageTypes.StartRun,
                        new WorkerRunRequest(
                            設定.実行ID,
                            設定.Seed,
                            設定.Query数,
                            設定.N,
                            設定.最大深度,
                            設定.入力要素数,
                            設定.演算子集合モード,
                            設定.対象一覧,
                            計測Profile,
                            RandomQueryGenerator.GeneratorVersion,
                            Capability.ProfileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            Capability.OperatorSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            設定.再実行Query一覧,
                            ResumeState:ResumeState),
                        CancellationToken.None).ConfigureAwait(false);
                    await foreach(var Envelope in Session.ReadAllAsync(CancellationToken.None).ConfigureAwait(false)){
                switch(Envelope.MessageType){
                    case WorkerMessageTypes.WorkerStarted:{
                        var 開始=DynamicWorkerProtocol.DeserializePayload<WorkerStartedEvent>(Envelope);
                        Run=Run with{
                            WorkerVersion=開始.WorkerVersion,
                            UpdatedAt=DateTimeOffset.UtcNow,
                        };
                        await this.Repository.SaveRunAsync(Run,CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.QueryStarted:{
                        var Query開始=DynamicWorkerProtocol.DeserializePayload<WorkerQueryStartedEvent>(Envelope);
                        Query開始一覧[Query開始.QueryId]=Query開始;
                        現在QueryIndex=Query開始.QueryIndex;
                        var IR=QueryIrJson.Deserialize(Query開始.IrJson);
                        await this.Repository.SaveQueryAsync(
                            new DynamicQueryRecord(
                                Query開始.QueryId,
                                設定.実行ID,
                                Query開始.QueryIndex,
                                Query開始.DerivationKind,
                                unchecked((long)Query開始.QuerySeed.A),
                                Query開始.IrJson,
                                string.Empty,
                                QueryIrJson.ComputeHash(IR),
                                "Running",
                                Query開始.ReplayedFromQueryId,
                                IR.IrSchemaVersion,
                                IR.ActualOperatorCount,
                                IR.ActualDepth,
                                IR.Root.OutputType.ToString(),
                                IR.Root.OrderSemantics.ToString(),
                                Query開始.IrGenerationNanoseconds),
                            CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.SourceExecutionStarted:{
                        var Source開始=DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionStartedEvent>(Envelope);
                        Source開始一覧[(Source開始.QueryId,Source開始.SourceTarget)]=Source開始;
                        現在QueryIndex=Source開始.QueryIndex;
                        現在対象=Source開始.SourceTarget;
                        await this.Repository.SaveSourceExecutionAsync(
                            new DynamicSourceExecutionRecord(
                                $"{Source開始.QueryId}-{Source開始.SourceTarget}-a1",
                                Source開始.QueryId,
                                Source開始.SourceTarget.ToString(),
                                1,
                                checked(Source開始.QueryIndex*設定.対象一覧.Count+Source開始.SourceOrdinal),
                                "Running",
                                StartedAt:Source開始.StartedAtUtc),
                            CancellationToken.None).ConfigureAwait(false);
                        進捗受信時(進捗へ変換(設定,Source開始.QueryIndex,Source開始.SourceTarget,"実行中",最新進捗));
                        break;
                    }
                    case WorkerMessageTypes.SourceExecutionCompleted:{
                        var 結果=DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>(Envelope);
                        if(!完了済み実行一覧.Add(new(結果.QueryIndex,結果.SourceTarget))){
                            throw new InvalidDataException(
                                $"SourceExecutionCompletedが重複しました: QueryIndex={結果.QueryIndex} SourceTarget={結果.SourceTarget}");
                        }
                        if(結果.Status!=WorkerExecutionStatus.Succeeded){
                            失敗Source数++;
                        }
                        if(!Query開始一覧.TryGetValue(結果.QueryId,out var Query開始)){
                            throw new InvalidDataException($"QueryStarted event is missing: {結果.QueryId}");
                        }
                        Source開始一覧.TryGetValue((結果.QueryId,結果.SourceTarget),out var Source開始);
                        await this.Source結果を保存Async(
                            設定,
                            Run,
                            Query開始,
                            Source開始,
                            結果,
                            結果受信時,
                            CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.Progress:{
                        最新進捗=DynamicWorkerProtocol.DeserializePayload<WorkerProgressEvent>(Envelope);
                        進捗受信時(進捗へ変換(
                            設定,
                            現在QueryIndex,
                            現在対象,
                            最新進捗.CancellationRequested?"キャンセル中":"実行中",
                            最新進捗));
                        break;
                    }
                    case WorkerMessageTypes.Diagnostic:{
                        var 診断=DynamicWorkerProtocol.DeserializePayload<WorkerDiagnosticEvent>(Envelope);
                        await this.Repository.SaveRunEventAsync(
                            new DynamicRunEventRecord(
                                Guid.NewGuid().ToString("N"),
                                設定.実行ID,
                                checked(++RunEventSequence),
                                診断.Severity,
                                診断.Stage,
                                診断.Message,
                                JsonSerializer.Serialize(診断,DynamicWorkerProtocol.JsonOptions),
                                DateTimeOffset.UtcNow,
                                診断.QueryId),
                            CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.RunCompleted:{
                        _=DynamicWorkerProtocol.DeserializePayload<WorkerRunCompletedEvent>(Envelope);
                        最終状態="Completed";
                        Session終了か=true;
                        await Session.ShutdownAsync($"shutdown-{設定.実行ID}").ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.RunCanceled:{
                        _=DynamicWorkerProtocol.DeserializePayload<WorkerRunCompletedEvent>(Envelope);
                        最終状態="Canceled";
                        Session終了か=true;
                        await Session.ShutdownAsync($"shutdown-{設定.実行ID}").ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.Fatal:{
                        var 致命=DynamicWorkerProtocol.DeserializePayload<WorkerFatalEvent>(Envelope);
                        ALC解放失敗後に再起動するか=致命.ErrorType==WorkerFatalErrorTypes.AssemblyUnloadFailure
                            &&完了済み実行一覧.Count<最新進捗.TotalSourceExecutions
                            &&!Token.IsCancellationRequested;
                        致命障害か=!ALC解放失敗後に再起動するか;
                        if(致命障害か){
                            最終状態="Failed";
                        }
                        await this.Repository.SaveRunEventAsync(
                            new DynamicRunEventRecord(
                                Guid.NewGuid().ToString("N"),
                                設定.実行ID,
                                checked(++RunEventSequence),
                                ALC解放失敗後に再起動するか?"Warning":"Error",
                                "Fatal",
                                致命.Message,
                                JsonSerializer.Serialize(致命,DynamicWorkerProtocol.JsonOptions),
                                致命.OccurredAtUtc),
                            CancellationToken.None).ConfigureAwait(false);
                        Session終了か=true;
                        await Session.ShutdownAsync($"fatal-{設定.実行ID}").ConfigureAwait(false);
                        break;
                    }
                }
                        if(Session終了か){
                            break;
                        }
                    }
                }finally{
                    await Session.DisposeAsync().ConfigureAwait(false);
                    if(ReferenceEquals(this.WorkerSession,Session)){
                        this.WorkerSession=null;
                    }
                }
                if(!ALC解放失敗後に再起動するか){
                    break;
                }
                Worker再起動回数++;
                await this.Repository.SaveRunEventAsync(
                    new DynamicRunEventRecord(
                        Guid.NewGuid().ToString("N"),
                        設定.実行ID,
                        checked(++RunEventSequence),
                        "Warning",
                        "WorkerRestart",
                        "ALC解放失敗が3件連続したため、新しいWorkerで残りのSourceExecutionを継続します。",
                        JsonSerializer.Serialize(new{
                            Worker再起動回数,
                            完了数=完了済み実行一覧.Count,
                            総数=最新進捗.TotalSourceExecutions,
                        },DynamicWorkerProtocol.JsonOptions),
                        DateTimeOffset.UtcNow),
                    CancellationToken.None).ConfigureAwait(false);
            }
            var 完了日時=DateTimeOffset.UtcNow;
            if(Token.IsCancellationRequested&&!致命障害か&&最終状態=="Failed"){
                最終状態="Canceled";
            }
            if(最終状態=="Canceled"){
                await this.キャンセル状態を確定Async(設定.実行ID,完了日時).ConfigureAwait(false);
            }
            Run=Run with{
                Status=最終状態,
                UpdatedAt=完了日時,
                CompletedAt=最終状態 is "Completed" or "Failed"?完了日時:null,
                CancelledAt=最終状態=="Canceled"?完了日時:null,
            };
            await this.Repository.SaveRunAsync(Run,CancellationToken.None).ConfigureAwait(false);
            return 履歴を作成(設定,開始日時,最終状態);
        }catch(OperationCanceledException){
            var 完了日時=DateTimeOffset.UtcNow;
            await this.キャンセル状態を確定Async(設定.実行ID,完了日時).ConfigureAwait(false);
            await this.Repository.SaveRunAsync(
                Run with{
                    Status="Canceled",
                    UpdatedAt=完了日時,
                    CancelledAt=完了日時,
                },
                CancellationToken.None).ConfigureAwait(false);
            return 履歴を作成(設定,開始日時,"Canceled");
        }catch(Exception 例外){
            var 完了日時=DateTimeOffset.UtcNow;
            if(Token.IsCancellationRequested){
                await this.キャンセル状態を確定Async(設定.実行ID,完了日時).ConfigureAwait(false);
                await this.Repository.SaveRunAsync(
                    Run with{
                        Status="Canceled",
                        UpdatedAt=完了日時,
                        CancelledAt=完了日時,
                    },
                    CancellationToken.None).ConfigureAwait(false);
                return 履歴を作成(設定,開始日時,"Canceled");
            }
            await this.Repository.SaveCheckpointAsync(
                new DynamicRunCheckpoint(
                    Run with{
                        Status="Failed",
                        UpdatedAt=完了日時,
                        CompletedAt=完了日時,
                    },
                    RunEvents:[
                            new DynamicRunEventRecord(
                                Guid.NewGuid().ToString("N"),
                                設定.実行ID,
                                checked(++RunEventSequence),
                            "Error",
                            "Fatal",
                            例外.Message,
                            JsonSerializer.Serialize(new{Exception=例外.GetType().FullName},DynamicWorkerProtocol.JsonOptions),
                            完了日時),
                    ]),
                CancellationToken.None).ConfigureAwait(false);
            return 履歴を作成(設定,開始日時,"Failed: "+例外.Message);
        }
    }

    public async Task キャンセルAsync(){
        this.実行キャンセル?.Cancel();
        var Session=this.WorkerSession;
        if(Session is not null&&!Session.HasExited){
            await Session.CancelAsync("ui-cancel-"+Guid.NewGuid().ToString("N")).ConfigureAwait(false);
        }
    }

    public Task<動的履歴項目> Runを再実行Async(
        string 親実行ID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        ArgumentException.ThrowIfNullOrWhiteSpace(親実行ID);
        var Snapshot=this.Repository.GetRunSnapshot(親実行ID)
            ??throw new KeyNotFoundException($"Runが見つかりません: {親実行ID}");
        var 条件=保存条件を読む(Snapshot.Run);
        var Query一覧=Snapshot.Queries.OrderBy(static Query=>Query.Ordinal).ToArray();
        if(Query一覧.Length!=条件.QueryCount){
            throw new InvalidOperationException("元Runには再実行に必要な全Query IRが保存されていません。");
        }
        var Seed=Seed256.Parse(条件.BaseSeed);
        var Replay一覧=Query一覧.Select((Query,Index)=>ReplayQueryを作成(Query,Index,Seed)).ToArray();
        return this.実行Async(
            再実行設定を作成(条件,親実行ID,Replay一覧),
            結果受信時,
            進捗受信時);
    }

    public Task<動的履歴項目> Queryを再実行Async(
        string 親QueryID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        ArgumentException.ThrowIfNullOrWhiteSpace(親QueryID);
        var Snapshot=this.Repository.GetRunSnapshotByQueryID(親QueryID)
            ??throw new KeyNotFoundException($"Queryが見つかりません: {親QueryID}");
        var Query=Snapshot.Queries.Single(Query=>Query.QueryID==親QueryID);
        var 条件=保存条件を読む(Snapshot.Run);
        var Seed=Seed256.Parse(条件.BaseSeed);
        var Replay=ReplayQueryを作成(Query,0,Seed);
        return this.実行Async(
            再実行設定を作成(条件,Snapshot.Run.RunID,[Replay]),
            結果受信時,
            進捗受信時);
    }

    public Task<動的履歴項目> 複数Queryを再実行Async(
        IReadOnlyList<string> 親QueryID一覧,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        ArgumentNullException.ThrowIfNull(親QueryID一覧);
        if(親QueryID一覧.Count==0){
            throw new ArgumentException("再実行するQueryを1件以上指定してください。",nameof(親QueryID一覧));
        }
        var Snapshot=this.Repository.GetRunSnapshotByQueryID(親QueryID一覧[0])
            ??throw new KeyNotFoundException($"Queryが見つかりません: {親QueryID一覧[0]}");
        var 条件=保存条件を読む(Snapshot.Run);
        var Seed=Seed256.Parse(条件.BaseSeed);
        var QueryByID=Snapshot.Queries.ToDictionary(static Query=>Query.QueryID);
        var Replay一覧=親QueryID一覧.Select((QueryID,Index)=>{
            if(!QueryByID.TryGetValue(QueryID,out var Query)){
                throw new KeyNotFoundException($"Queryが見つかりません: {QueryID}");
            }
            return ReplayQueryを作成(Query,Index,Seed);
        }).ToArray();
        return this.実行Async(
            再実行設定を作成(条件,Snapshot.Run.RunID,Replay一覧),
            結果受信時,
            進捗受信時);
    }

    public async Task<動的履歴項目> Queryを縮小Async(
        string 親QueryID,
        Action<動的結果行> 結果受信時,
        Action<動的実行進捗> 進捗受信時){
        ArgumentException.ThrowIfNullOrWhiteSpace(親QueryID);
        ArgumentNullException.ThrowIfNull(結果受信時);
        ArgumentNullException.ThrowIfNull(進捗受信時);
        var Snapshot=this.Repository.GetRunSnapshotByQueryID(親QueryID)
            ??throw new KeyNotFoundException($"Queryが見つかりません: {親QueryID}");
        var QueryRecord=Snapshot.Queries.Single(Query=>Query.QueryID==親QueryID);
        var 失敗Execution=Snapshot.SourceExecutions
            .Where(Execution=>Execution.QueryID==親QueryID)
            .OrderBy(static Execution=>Execution.ExecutionOrder)
            .FirstOrDefault(static Execution=>Execution.Status is not(
                nameof(WorkerExecutionStatus.Succeeded)
                or nameof(WorkerExecutionStatus.NotStarted)
                or nameof(WorkerExecutionStatus.Running)
                or nameof(WorkerExecutionStatus.Canceled)))
            ??throw new InvalidOperationException("縮小対象Queryに保存済みの再現可能な失敗がありません。");
        if(!Enum.TryParse<WorkerExecutionStatus>(失敗Execution.Status,out var 元Status)
            ||!Enum.TryParse<SourceTarget>(失敗Execution.SourceKey,out var 対象)){
            throw new InvalidDataException("縮小対象のstatusまたはSourceTargetを読み取れません。");
        }
        var 条件=保存条件を読む(Snapshot.Run);
        var BaseSeed=Seed256.Parse(条件.BaseSeed);
        var FixtureSeed=SeedDerivation.Derive(BaseSeed,"fixture",QueryRecord.Ordinal);
        var OriginalIR=QueryIrJson.Deserialize(QueryRecord.CanonicalIRJSON);
        var ResultJSON=診断詳細を取得(Snapshot,失敗Execution.ExecutionID,"ValidationResults");
        var TraceJSON=診断詳細を取得(Snapshot,失敗Execution.ExecutionID,"OptimizationTrace");
        var 期待署名=FailureSignatureFactory.FromWorkerArtifacts(
            元Status,
            失敗Execution.Error,
            ResultJSON,
            TraceJSON);

        ShrinkResult 縮小結果;
        await this.実行Gate.WaitAsync().ConfigureAwait(false);
        try{
            this.実行キャンセル=new CancellationTokenSource();
            var Token=this.実行キャンセル.Token;
            var Shrinker=new QueryShrinker();
            縮小結果=await Shrinker.ShrinkAsync(
                new ShrinkRequest(OriginalIR,条件.InputElementCount,期待署名,32,30_000),
                async(Candidate,CancellationToken)=>{
                    var Event=await this.縮小候補を評価Async(
                        Candidate,
                        対象,
                        BaseSeed,
                        FixtureSeed,
                        QueryRecord,
                        条件.Mode,
                        CancellationToken).ConfigureAwait(false);
                    return Event is null?null:FailureSignatureFactory.FromWorkerArtifacts(
                        Event.Status,
                        Event.DiagnosticsJson,
                        Event.ResultJson,
                        Event.OptimizationTraceJson);
                },
                Token).ConfigureAwait(false);
        }finally{
            if(this.WorkerSession is not null){
                await this.WorkerSession.DisposeAsync().ConfigureAwait(false);
                this.WorkerSession=null;
            }
            this.実行キャンセル?.Dispose();
            this.実行キャンセル=null;
            this.実行Gate.Release();
        }
        if(縮小結果.AppliedDerivations.Count==0){
            throw new InvalidOperationException("同じ失敗署名を維持する縮小候補が見つかりませんでした。");
        }
        var Replay=new WorkerReplayQuery(
            0,
            QueryIrJson.Serialize(縮小結果.ShrunkQuery),
            QueryRecord.QueryID,
            FixtureSeed,
            QueryRecord.Ordinal,
            "Shrink");
        return await this.実行Async(
            new 動的実行設定(
                Guid.NewGuid().ToString("N"),
                1,
                縮小結果.ShrunkQuery.ActualOperatorCount,
                縮小結果.ShrunkInputElementCount,
                縮小結果.ShrunkQuery.ActualDepth,
                BaseSeed,
                条件.Mode,
                [対象],
                Snapshot.Run.RunID,
                [Replay],
                条件.MeasurementProfile),
            結果受信時,
            進捗受信時).ConfigureAwait(false);
    }

    public IReadOnlyList<動的履歴項目> 履歴を取得(bool ごみ箱か){
        return this.Repository.GetRuns(trashOnly:ごみ箱か)
            .Select(DB履歴を作成)
            .ToArray();
    }

    public 動的実行条件Snapshot 実行条件を取得(string 実行ID){
        ArgumentException.ThrowIfNullOrWhiteSpace(実行ID);
        var Run=this.Repository.GetRunSnapshot(実行ID)?.Run
            ??throw new KeyNotFoundException($"Runが見つかりません: {実行ID}");
        return 保存条件を読む(Run);
    }

    public async Task ごみ箱へ移動Async(string 実行ID){
        _=await this.Repository.SoftDeleteRunAsync(実行ID,DateTimeOffset.UtcNow,"UI").ConfigureAwait(false);
    }

    public async Task 復元Async(string 実行ID){
        _=await this.Repository.RestoreRunAsync(実行ID,DateTimeOffset.UtcNow).ConfigureAwait(false);
    }

    public async Task 完全削除Async(string 実行ID){
        _=await this.Repository.PurgeRunAsync(実行ID).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync(){
        await this.キャンセルAsync().ConfigureAwait(false);
        await this.Writer.DisposeAsync().ConfigureAwait(false);
        this.実行Gate.Dispose();
    }

    private static WorkerReplayQuery ReplayQueryを作成(
        DynamicQueryRecord Query,
        int QueryIndex,
        Seed256 BaseSeed){
        _=QueryIrJson.Deserialize(Query.CanonicalIRJSON);
        return new(
            QueryIndex,
            Query.CanonicalIRJSON,
            Query.QueryID,
            SeedDerivation.Derive(BaseSeed,"fixture",Query.Ordinal),
            Query.Ordinal,
            "Replay");
    }

    private async Task<WorkerSourceExecutionCompletedEvent?> 縮小候補を評価Async(
        ShrinkCandidate Candidate,
        SourceTarget 対象,
        Seed256 BaseSeed,
        Seed256 FixtureSeed,
        DynamicQueryRecord OriginalQuery,
        OperatorSetMode Mode,
        CancellationToken cancellationToken){
        var RunID=$"shrink-eval-{Guid.NewGuid():N}";
        var Replay=new WorkerReplayQuery(
            0,
            QueryIrJson.Serialize(Candidate.Query),
            OriginalQuery.QueryID,
            FixtureSeed,
            OriginalQuery.Ordinal,
            Candidate.DerivationKind);
        var request=new WorkerRunRequest(
            RunID,
            BaseSeed,
            1,
            Candidate.Query.ActualOperatorCount,
            Candidate.Query.ActualDepth,
            Candidate.InputElementCount,
            Mode,
            [対象],
            new WorkerMeasurementProfile("Shrink-evaluation-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [Replay]);
        var Session=this.WorkerClient.Start(this.WorkerPath);
        this.WorkerSession=Session;
        try{
            await Session.SendAsync(
                RunID,
                WorkerMessageTypes.StartRun,
                request,
                cancellationToken).ConfigureAwait(false);
            await foreach(var Envelope in Session.ReadAllAsync(cancellationToken).ConfigureAwait(false)){
                if(Envelope.MessageType==WorkerMessageTypes.SourceExecutionCompleted){
                    return DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>(Envelope);
                }
                if(Envelope.MessageType==WorkerMessageTypes.Fatal){
                    return null;
                }
            }
            return null;
        }finally{
            await Session.DisposeAsync().ConfigureAwait(false);
            if(ReferenceEquals(this.WorkerSession,Session)){
                this.WorkerSession=null;
            }
        }
    }

    private static string? 診断詳細を取得(
        DynamicRunSnapshot Snapshot,
        string ExecutionID,
        string Name){
        return Snapshot.Diagnostics
            .Where(Diagnostic=>Diagnostic.ExecutionID==ExecutionID&&Diagnostic.Name==Name)
            .OrderByDescending(static Diagnostic=>Diagnostic.RecordedAt)
            .Select(static Diagnostic=>Diagnostic.DetailsJSON)
            .FirstOrDefault();
    }

    private static 動的実行設定 再実行設定を作成(
        動的実行条件Snapshot 条件,
        string 親実行ID,
        IReadOnlyList<WorkerReplayQuery> Replay一覧){
        return new(
            Guid.NewGuid().ToString("N"),
            Replay一覧.Count,
            条件.OperatorCount,
            条件.InputElementCount,
            条件.MaximumDepth,
            Seed256.Parse(条件.BaseSeed),
            条件.Mode,
            条件.SourceTargets,
            親実行ID,
            Replay一覧,
            条件.MeasurementProfile);
    }

    private static 動的実行条件Snapshot 保存条件を読む(DynamicRunRecord Run){
        try{
            var 条件=JsonSerializer.Deserialize<動的実行条件Snapshot>(
                Run.SettingsJSON,
                DynamicWorkerProtocol.JsonOptions)
                ??throw new InvalidDataException("保存実行条件がJSON nullです。");
            if(条件.SchemaVersion!=1){
                throw new InvalidDataException($"保存実行条件のschema versionを実行できません: {条件.SchemaVersion}");
            }
            return 条件;
        }catch(JsonException 例外){
            throw new InvalidDataException("保存実行条件を読み取れないため再実行できません。",例外);
        }
    }

    private static QueryCapabilitySet 実行設定を検証(動的実行設定 設定){
        ArgumentException.ThrowIfNullOrWhiteSpace(設定.実行ID);
        if(設定.Query数<1||設定.Query数>1_000){
            throw new ArgumentOutOfRangeException(nameof(設定.Query数));
        }
        if(設定.N<1||設定.N>64){
            throw new ArgumentOutOfRangeException(nameof(設定.N));
        }
        if(設定.入力要素数<1||設定.入力要素数>100_000){
            throw new ArgumentOutOfRangeException(nameof(設定.入力要素数));
        }
        if(設定.最大深度<1||設定.最大深度>Math.Min(設定.N,32)){
            throw new ArgumentOutOfRangeException(nameof(設定.最大深度));
        }
        if(設定.対象一覧 is null||設定.対象一覧.Count<1
            ||設定.対象一覧.Count>Enum.GetValues<SourceTarget>().Length
            ||設定.対象一覧.Distinct().Count()!=設定.対象一覧.Count
            ||設定.対象一覧.Any(static 対象=>!Enum.IsDefined(対象))){
            throw new ArgumentException("SourceTargetを1～8件、重複なしで選択してください。",nameof(設定.対象一覧));
        }
        if(!Enum.IsDefined(設定.演算子集合モード)){
            throw new ArgumentOutOfRangeException(nameof(設定.演算子集合モード));
        }
        var Capability=QueryCapabilityProfiles.CreateSet(設定.演算子集合モード,設定.対象一覧);
        var 計測Profile=設定.計測ProfileOverride??new WorkerMeasurementProfile();
        if(計測Profile.WarmupCount<0||計測Profile.SampleCount<1
            ||計測Profile.MinimumCalibrationMilliseconds<0
            ||計測Profile.MaximumOperationCount<1||計測Profile.MaximumOperationCount>1_048_576){
            throw new ArgumentException("Measurement profile values are outside the supported range.",nameof(設定.計測ProfileOverride));
        }
        if(設定.再実行Query一覧 is not null){
            if(設定.再実行Query一覧.Count!=設定.Query数
                ||!設定.再実行Query一覧.Select(static Query=>Query.QueryIndex).Order()
                    .SequenceEqual(Enumerable.Range(0,設定.Query数))){
                throw new ArgumentException(
                    "再実行Query一覧は0始まりの全QueryIndexを1件ずつ含む必要があります。",
                    nameof(設定.再実行Query一覧));
            }
            var 許可演算子=Capability.OperatorIds.ToHashSet(StringComparer.Ordinal);
            foreach(var Replay in 設定.再実行Query一覧){
                var IR=QueryIrJson.Deserialize(Replay.IrJson);
                QueryIrValidator.ValidateForExecutionAndThrow(IR,設定.入力要素数);
                if(IR.ActualOperatorCount!=設定.N||IR.ActualDepth>設定.最大深度){
                    throw new InvalidDataException("保存IRのNまたは深度が保存実行条件と一致しません。");
                }
                var 未対応=演算子一覧(IR.Root)
                    .Where(Operator=>Operator!=QueryOperatorIds.Source&&!許可演算子.Contains(Operator))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if(未対応.Length!=0){
                    throw new InvalidDataException(
                        $"保存IRは現在のCapabilityでは再実行できません: {string.Join(",",未対応)}");
                }
            }
            return Capability;
        }
        _=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=設定.Seed,
            QueryIndex=0,
            QueryId="preflight",
            SourceTarget=設定.対象一覧.Order().First(),
            OperatorCount=設定.N,
            MaximumDepth=設定.最大深度,
            InputElementCount=設定.入力要素数,
            MaximumCardinality=100_000,
            EnableBinaryOperators=true,
            EnableNestedOperators=true,
            AllowedOperatorIds=Capability.OperatorIds,
        });
        return Capability;
    }

    private static IEnumerable<string> 演算子一覧(QueryNode Node){
        yield return Node.OperatorId;
        switch(Node){
            case UnaryQueryNode Unary:
                foreach(var Operator in 演算子一覧(Unary.Input)){
                    yield return Operator;
                }
                break;
            case BinaryQueryNode Binary:
                foreach(var Operator in 演算子一覧(Binary.Left)){
                    yield return Operator;
                }
                foreach(var Operator in 演算子一覧(Binary.Right)){
                    yield return Operator;
                }
                break;
            case NestedQueryNode Nested:
                foreach(var Operator in 演算子一覧(Nested.Outer)){
                    yield return Operator;
                }
                foreach(var Operator in 演算子一覧(Nested.Inner)){
                    yield return Operator;
                }
                break;
            case TerminalQueryNode Terminal:
                foreach(var Operator in 演算子一覧(Terminal.Input)){
                    yield return Operator;
                }
                break;
        }
    }

    private async Task キャンセル状態を確定Async(string 実行ID,DateTimeOffset 完了日時){
        var Snapshot=this.Repository.GetRunSnapshot(実行ID);
        if(Snapshot is null){
            return;
        }
        var Query一覧=Snapshot.Queries
            .Where(static Query=>Query.Status=="Running")
            .Select(Query=>Query with{Status="Canceled"})
            .ToArray();
        var Execution一覧=Snapshot.SourceExecutions
            .Where(static Execution=>Execution.Status=="Running")
            .Select(Execution=>Execution with{
                Status="Canceled",
                CompletedAt=完了日時,
                Partial=true,
            })
            .ToArray();
        if(Query一覧.Length==0&&Execution一覧.Length==0){
            return;
        }
        await this.Repository.SaveCheckpointAsync(
            new DynamicRunCheckpoint(
                Snapshot.Run with{UpdatedAt=完了日時},
                Queries:Query一覧,
                SourceExecutions:Execution一覧),
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task Source結果を保存Async(
        動的実行設定 設定,
        DynamicRunRecord Run,
        WorkerQueryStartedEvent Query開始,
        WorkerSourceExecutionStartedEvent? Source開始,
        WorkerSourceExecutionCompletedEvent 結果,
        Action<動的結果行> 結果受信時,
        CancellationToken Token){
        var IR=QueryIrJson.Deserialize(Query開始.IrJson);
        var Artifact=Artifactを解析(結果.MeasurementsJson);
        var 検証=検証結果を解析(結果.Status,結果.ResultJson);
        var ResultDigest=Hashを計算(結果.ResultJson);
        var QueryHash=QueryIrJson.ComputeHash(IR);
        var ExecutionID=$"{結果.QueryId}-{結果.SourceTarget}-a1";
        var 開始日時=Source開始?.StartedAtUtc??結果.CompletedAtUtc;
        var CapabilityProfileJSON=JsonSerializer.Serialize(
            QueryCapabilityProfiles.Get(結果.SourceTarget),
            DynamicWorkerProtocol.JsonOptions);
        var FixedPointTermination=結果.Status switch{
            WorkerExecutionStatus.OptimizationNonConverged=>"NonConverged",
            _ when Artifact.FixedPointか=>"FixedPoint",
            _=>"NotAvailable",
        };
        var RewriteSummaryJSON=JsonSerializer.Serialize(
            new{Artifact.RewriteCount,ReachedFixedPoint=Artifact.FixedPointか},
            DynamicWorkerProtocol.JsonOptions);
        var Query=new DynamicQueryRecord(
            結果.QueryId,
            設定.実行ID,
            結果.QueryIndex,
            Query開始.DerivationKind,
            unchecked((long)Query開始.QuerySeed.A),
            Query開始.IrJson,
            結果.SourceText,
            QueryHash,
            結果.Status.ToString(),
            Query開始.ReplayedFromQueryId,
            IR.IrSchemaVersion,
            IR.ActualOperatorCount,
            IR.ActualDepth,
            IR.Root.OutputType.ToString(),
            IR.Root.OrderSemantics.ToString(),
            Query開始.IrGenerationNanoseconds);
        var System計測=計測を取得(Artifact,"System");
        var Execution=new DynamicSourceExecutionRecord(
            ExecutionID,
            結果.QueryId,
            結果.SourceTarget.ToString(),
            1,
            checked(結果.QueryIndex*設定.対象一覧.Count+(Source開始?.SourceOrdinal??0)),
            結果.Status.ToString(),
            LoopCount:System計測?.OperationCount??0,
            WarmupCount:System計測?.WarmupCount??0,
            StartedAt:開始日時,
            CompletedAt:結果.CompletedAtUtc,
            ResultDigest:ResultDigest,
            Error:結果.DiagnosticsJson??string.Empty,
            Partial:結果.Status==WorkerExecutionStatus.Canceled,
            CapabilityProfileID:Hashを計算(CapabilityProfileJSON),
            CapabilityProfileJSON:CapabilityProfileJSON,
            GeneratedSource:結果.SourceText,
            SourceHash:Hashを計算(結果.SourceText),
            SourceEmissionNanoseconds:Artifact.Source生成ナノ秒,
            SourceTextNanoseconds:Artifact.SourceTextナノ秒,
            SyntaxTreeNanoseconds:Artifact.SyntaxTreeナノ秒,
            CompilationNanoseconds:Artifact.Compilationナノ秒,
            GeneratorNanoseconds:Artifact.Generatorナノ秒,
            EmitNanoseconds:Artifact.Emitナノ秒,
            LoadNanoseconds:Artifact.Loadナノ秒,
            BindNanoseconds:Artifact.Bindナノ秒,
            RewriteSummaryJSON:RewriteSummaryJSON,
            FixedPointTermination:FixedPointTermination,
            AssemblyUnloaded:結果.AssemblyUnloaded);
        var Validations=検証一覧を作成(ExecutionID,結果);
        var Diagnostics=診断一覧を作成(ExecutionID,結果,ResultDigest);
        var Measurements=計測一覧を作成(ExecutionID,Artifact,開始日時,結果.CompletedAtUtc);
        var OptimizationEvents=最適化Event一覧を作成(
            ExecutionID,
            結果.QueryId,
            結果.SourceTarget,
            結果.OptimizationTraceJson,
            結果.CompletedAtUtc);
        await this.Repository.SaveCheckpointAsync(
            new DynamicRunCheckpoint(
                Run with{UpdatedAt=結果.CompletedAtUtc},
                Queries:[Query],
                SourceExecutions:[Execution],
                Validations:Validations,
                Measurements:Measurements,
                Diagnostics:Diagnostics,
                OptimizationEvents:OptimizationEvents),
            Token).ConfigureAwait(false);
        結果受信時(結果行を作成(
            設定,
            Query開始,
            IR,
            結果,
            Artifact,
            検証));
    }

    private static 動的結果行 結果行を作成(
        動的実行設定 設定,
        WorkerQueryStartedEvent Query開始,
        QueryIrDocument IR,
        WorkerSourceExecutionCompletedEvent 結果,
        実行Artifact Artifact,
        検証概要 検証){
        var System計測=計測を取得(Artifact,"System");
        var FLinq計測=計測を取得(Artifact,"FLinq");
        var ZLinq計測=計測を取得(Artifact,"ZLinq");
        return new 動的結果行{
            実行ID=設定.実行ID,
            QueryID=結果.QueryId,
            対象=結果.SourceTarget,
            QueryIndex=結果.QueryIndex,
            短縮式=IR.Root.OperatorId,
            N=IR.ActualOperatorCount,
            深度=IR.ActualDepth,
            状態=結果.Status.ToString(),
            生成時間=ナノ秒を表示(Query開始.IrGenerationNanoseconds+Artifact.Source生成ナノ秒),
            コンパイル時間=ナノ秒を表示(Artifact.コンパイル合計ナノ秒),
            System中央値=ナノ秒を表示(System計測?.中央値??double.NaN),
            System割当=バイトを表示(System計測?.割当Bytes??double.NaN),
            FLinq中央値=ナノ秒を表示(FLinq計測?.中央値??double.NaN),
            FLinq割当=バイトを表示(FLinq計測?.割当Bytes??double.NaN),
            FLinq比=比を表示(System計測?.中央値??double.NaN,FLinq計測?.中央値??double.NaN),
            ZLinq中央値=ナノ秒を表示(ZLinq計測?.中央値??double.NaN),
            ZLinq割当=バイトを表示(ZLinq計測?.割当Bytes??double.NaN),
            ZLinq比=比を表示(System計測?.中央値??double.NaN,ZLinq計測?.中央値??double.NaN),
            意味一致=検証.一致か?"一致":"不一致",
            RewriteCount=Artifact.RewriteCount,
            FixedPoint=Artifact.FixedPointか?"到達":"未到達",
            概要=$"{結果.Status}; Source={結果.SourceTarget}; ALC={(結果.AssemblyUnloaded?"unloaded":"alive")}",
            生成Source=結果.SourceText,
            IR=Query開始.IrJson,
            結果=結果.ResultJson??string.Empty,
            診断=結果.DiagnosticsJson??string.Empty,
            最適化Trace=結果.OptimizationTraceJson??string.Empty,
            計測詳細=結果.MeasurementsJson??string.Empty,
        };
    }

    private static IReadOnlyList<DynamicMeasurementBundle> 計測一覧を作成(
        string ExecutionID,
        実行Artifact Artifact,
        DateTimeOffset 開始日時,
        DateTimeOffset 完了日時){
        return Artifact.計測一覧.Select(計測=>{
            var MeasurementID=$"{ExecutionID}-{計測.実装}";
            var Record=new DynamicMeasurementRecord(
                MeasurementID,
                ExecutionID,
                計測.実装,
                計測.OperationCount,
                計測.SampleCount,
                計測.中央値,
                計測.平均,
                計測.最小,
                計測.最大,
                計測.標準偏差,
                計測.標準誤差,
                計測.割当Bytes,
                計測.Gen0,
                計測.Gen1,
                計測.Gen2,
                開始日時,
                完了日時);
            var Samples=計測.Sample一覧.Select(Sample=>new DynamicRawSampleRecord(
                MeasurementID,
                $"{計測.実装}:{Sample.ExecutionOrder}",
                Sample.SampleIndex,
                Sample.ElapsedNanoseconds,
                Sample.OperationCount,
                Sample.AllocatedBytes,
                Sample.Gen0,
                Sample.Gen1,
                Sample.Gen2,
                完了日時)).ToArray();
            return new DynamicMeasurementBundle(Record,Samples);
        }).ToArray();
    }

    private static IReadOnlyList<DynamicDiagnosticRecord> 診断一覧を作成(
        string ExecutionID,
        WorkerSourceExecutionCompletedEvent 結果,
        string ResultDigest){
        var 時刻=結果.CompletedAtUtc;
        var 一覧=new List<DynamicDiagnosticRecord>{
            new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "GeneratedSource",
                JsonSerializer.Serialize(new{結果.SourceHash,結果.SourceText},DynamicWorkerProtocol.JsonOptions),
                時刻,
                TextValue:結果.SourceHash),
            new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "ValidationResults",
                結果.ResultJson??"null",
                時刻,
                TextValue:ResultDigest),
            new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "ExecutionArtifact",
                結果.MeasurementsJson??"null",
                時刻),
            new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "AssemblyUnload",
                JsonSerializer.Serialize(new{結果.AssemblyUnloaded},DynamicWorkerProtocol.JsonOptions),
                時刻,
                NumericValue:結果.AssemblyUnloaded?1:0),
        };
        if(!string.IsNullOrWhiteSpace(結果.DiagnosticsJson)){
            一覧.Add(new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "CompilerDiagnostics",
                結果.DiagnosticsJson,
                時刻));
        }
        if(!string.IsNullOrWhiteSpace(結果.OptimizationTraceJson)){
            一覧.Add(new(
                Guid.NewGuid().ToString("N"),
                ExecutionID,
                "OptimizationTrace",
                結果.OptimizationTraceJson,
                時刻));
        }
        return 一覧;
    }

    private static IReadOnlyList<DynamicValidationRecord> 検証一覧を作成(
        string ExecutionID,
        WorkerSourceExecutionCompletedEvent 結果){
        if(結果.Status==WorkerExecutionStatus.Canceled){
            return[];
        }
        JsonDocument? Document=null;
        try{
            if(!string.IsNullOrWhiteSpace(結果.ResultJson)){
                Document=JsonDocument.Parse(結果.ResultJson);
            }
            var Root=Document?.RootElement;
            var 一覧=new List<DynamicValidationRecord>(4);
            foreach(var 実装 in new[]{"System","FLinqUnoptimized","FLinq","ZLinq"}){
                var JSONKey=実装 switch{
                    "System"=>"system",
                    "FLinqUnoptimized"=>"fLinqUnoptimized",
                    "FLinq"=>"fLinq",
                    "ZLinq"=>"zLinq",
                    _=>throw new InvalidOperationException($"Unknown implementation kind: {実装}"),
                };
                var FullResult=子要素JSON(Root,"results",JSONKey);
                var ExceptionJSON=子要素JSON(Root,"exceptions",JSONKey);
                var Status=検証Statusを取得(Root,実装,JSONKey,FullResult,ExceptionJSON);
                一覧.Add(new(
                    $"{ExecutionID}-{実装}",
                    ExecutionID,
                    実装,
                    Status,
                    結果.CompletedAtUtc,
                    FullResult=="null"?Status:Hashを計算(FullResult),
                    FullResult,
                    ExceptionJSON,
                    有効なJSONへ変換(結果.DiagnosticsJson)));
            }
            return 一覧;
        }finally{
            Document?.Dispose();
        }
    }

    private static string 検証Statusを取得(
        JsonElement? Root,
        string 実装,
        string JSONKey,
        string FullResult,
        string ExceptionJSON){
        if(ExceptionJSON!="null"){
            return"Exception";
        }
        if(実装=="System"){
            return FullResult=="null"?"NotExecuted":"Success";
        }
        if(Root is{ }要素
            &&要素.TryGetProperty("comparisons",out var Comparisons)
            &&Comparisons.ValueKind==JsonValueKind.Object
            &&Comparisons.TryGetProperty(JSONKey,out var Comparison)
            &&Comparison.TryGetProperty("isMatch",out var IsMatch)
            &&IsMatch.ValueKind is JsonValueKind.True or JsonValueKind.False){
            return IsMatch.GetBoolean()?"Match":"Mismatch";
        }
        return FullResult=="null"?"NotExecuted":"Uncompared";
    }

    private static string 子要素JSON(JsonElement? Root,string 親,string 子){
        return Root is{ }要素
            &&要素.TryGetProperty(親,out var Parent)
            &&Parent.ValueKind==JsonValueKind.Object
            &&Parent.TryGetProperty(子,out var Child)
                ?Child.GetRawText()
                :"null";
    }

    private static string 有効なJSONへ変換(string? JSON){
        if(string.IsNullOrWhiteSpace(JSON)){
            return"null";
        }
        try{
            using var _=JsonDocument.Parse(JSON);
            return JSON;
        }catch(JsonException){
            return JsonSerializer.Serialize(JSON,DynamicWorkerProtocol.JsonOptions);
        }
    }

    private static IReadOnlyList<DynamicOptimizationEventRecord> 最適化Event一覧を作成(
        string ExecutionID,
        string QueryID,
        SourceTarget 対象,
        string? TraceJSON,
        DateTimeOffset 時刻){
        if(string.IsNullOrWhiteSpace(TraceJSON)){
            return[];
        }
        var 結果=new List<DynamicOptimizationEventRecord>();
        try{
            using var 外側=JsonDocument.Parse(TraceJSON);
            foreach(var Trace要素 in 外側.RootElement.EnumerateArray()){
                var JSON=Trace要素.GetString();
                if(string.IsNullOrWhiteSpace(JSON)){
                    continue;
                }
                using var Trace=JsonDocument.Parse(JSON);
                if(!Trace.RootElement.TryGetProperty("Events",out var Events)){
                    continue;
                }
                foreach(var Event in Events.EnumerateArray()){
                    var Sequence=checked(結果.Count+1);
                    var Layer=Event.GetProperty("Layer").GetString()??"Unknown";
                    var Phase=Event.GetProperty("Phase").GetString()??string.Empty;
                    var Rule=Event.GetProperty("RuleId").GetString()??string.Empty;
                    int? Depth=null;
                    if(Event.TryGetProperty("Depth",out var DepthElement)
                        &&DepthElement.ValueKind==JsonValueKind.Number){
                        Depth=DepthElement.GetInt32();
                    }
                    int? StageIndex=null;
                    if(Event.TryGetProperty("StageIndex",out var StageIndexElement)
                        &&StageIndexElement.ValueKind==JsonValueKind.Number){
                        StageIndex=StageIndexElement.GetInt32();
                    }
                    結果.Add(new(
                        $"{ExecutionID}-trace-{Sequence:D6}",
                        QueryID,
                        対象.ToString(),
                        Sequence,
                        $"{Layer}:{Phase}:{Rule}",
                        Event.GetRawText(),
                        時刻,
                        ExecutionID:ExecutionID,
                        Layer:Layer,
                        Phase:Phase,
                        RuleID:Rule,
                        Round:Event.GetProperty("Round").GetInt32(),
                        Pass:Event.GetProperty("Pass").GetInt32(),
                        Visit:Event.GetProperty("VisitNumber").GetInt32(),
                        NodeID:Event.GetProperty("NodeId").GetString()??string.Empty,
                        NodePath:Event.GetProperty("NodePath").GetString()??string.Empty,
                        Depth:Depth,
                        StageIndex:StageIndex,
                        ParentKind:Event.GetProperty("ParentKind").GetString()??string.Empty,
                        IsLeft:Event.GetProperty("Left").GetBoolean(),
                        IsRight:Event.GetProperty("Right").GetBoolean(),
                        IsInner:Event.GetProperty("Inner").GetBoolean(),
                        Before:Event.GetProperty("Before").GetString()??string.Empty,
                        After:Event.GetProperty("After").GetString()??string.Empty,
                        Changed:Event.GetProperty("Changed").GetBoolean(),
                        Revisited:Event.GetProperty("Revisited").GetBoolean()));
                }
            }
        }catch(Exception 例外) when(例外 is JsonException or InvalidOperationException or KeyNotFoundException){
        }
        return 結果;
    }

    private static 実行Artifact Artifactを解析(string? JSON){
        if(string.IsNullOrWhiteSpace(JSON)){
            return 実行Artifact.Empty;
        }
        try{
            using var Document=JsonDocument.Parse(JSON);
            var Root=Document.RootElement;
            var Source生成=Root.GetProperty("sourceEmissionNanoseconds").GetInt64();
            var Compilation=Root.GetProperty("compilationTimings");
            var SourceText=Compilation.GetProperty("sourceTextNanoseconds").GetInt64();
            var SyntaxTree=Compilation.GetProperty("syntaxTreeNanoseconds").GetInt64();
            var Compilation時間=Compilation.GetProperty("compilationNanoseconds").GetInt64();
            var Generator=Compilation.GetProperty("generatorNanoseconds").GetInt64();
            var Emit=Compilation.GetProperty("emitNanoseconds").GetInt64();
            var Load=Compilation.GetProperty("loadNanoseconds").GetInt64();
            var Bind=Compilation.GetProperty("bindNanoseconds").GetInt64();
            var Compile合計=
                SourceText+
                SyntaxTree+
                Compilation時間+
                Generator+
                Emit+
                Load+
                Bind;
            var RewriteCount=Root.GetProperty("rewriteCount").GetInt32();
            var FixedPoint=Root.GetProperty("reachedFixedPoint").GetBoolean();
            var 計測一覧=new List<計測概要>();
            if(Root.TryGetProperty("measurement",out var Measurement)
                &&Measurement.ValueKind==JsonValueKind.Object
                &&Measurement.TryGetProperty("measurements",out var Measurements)){
                foreach(var Summary in Measurements.EnumerateArray()){
                    var Sample一覧=Summary.GetProperty("samples").EnumerateArray()
                        .Select(static Sample=>new 計測Sample(
                            Sample.GetProperty("sampleIndex").GetInt32(),
                            Sample.GetProperty("executionOrder").GetInt32(),
                            Sample.GetProperty("elapsedNanoseconds").GetInt64(),
                            Sample.GetProperty("operationCount").GetInt32(),
                            Sample.GetProperty("allocatedBytes").GetInt64(),
                            Sample.GetProperty("gen0Collections").GetInt32(),
                            Sample.GetProperty("gen1Collections").GetInt32(),
                            Sample.GetProperty("gen2Collections").GetInt32()))
                        .ToArray();
                    計測一覧.Add(new(
                        実装名を正規化(Summary.GetProperty("implementation").GetString()),
                        Summary.GetProperty("operationCount").GetInt32(),
                        Summary.GetProperty("warmupCount").GetInt32(),
                        Summary.GetProperty("sampleCount").GetInt32(),
                        Summary.GetProperty("medianNanoseconds").GetDouble(),
                        Summary.GetProperty("meanNanoseconds").GetDouble(),
                        Summary.GetProperty("minimumNanoseconds").GetDouble(),
                        Summary.GetProperty("maximumNanoseconds").GetDouble(),
                        Summary.GetProperty("standardDeviationNanoseconds").GetDouble(),
                        Summary.GetProperty("standardErrorNanoseconds").GetDouble(),
                        Summary.GetProperty("allocatedBytesPerOperation").GetDouble(),
                        Summary.GetProperty("gen0Collections").GetInt32(),
                        Summary.GetProperty("gen1Collections").GetInt32(),
                        Summary.GetProperty("gen2Collections").GetInt32(),
                        Sample一覧));
                }
            }
            return new(
                Source生成,
                Compile合計,
                SourceText,
                SyntaxTree,
                Compilation時間,
                Generator,
                Emit,
                Load,
                Bind,
                RewriteCount,
                FixedPoint,
                計測一覧);
        }catch(Exception 例外) when(例外 is JsonException or InvalidOperationException or KeyNotFoundException){
            return 実行Artifact.Empty;
        }
    }

    private static 検証概要 検証結果を解析(WorkerExecutionStatus Status,string? JSON){
        if(string.IsNullOrWhiteSpace(JSON)){
            return new(false,Status.ToString());
        }
        try{
            using var Document=JsonDocument.Parse(JSON);
            var Root=Document.RootElement;
            if(Root.TryGetProperty("exceptions",out var Exceptions)
                &&Exceptions.ValueKind==JsonValueKind.Object
                &&Exceptions.EnumerateObject().Any()){
                return new(false,"実行例外が発生しました。");
            }
            var 一致=true;
            var 差分一覧=new List<string>();
            if(Root.TryGetProperty("comparisons",out var Comparisons)
                &&Comparisons.ValueKind==JsonValueKind.Object){
                foreach(var Comparison in Comparisons.EnumerateObject()){
                    if(!Comparison.Value.GetProperty("isMatch").GetBoolean()){
                        一致=false;
                        var 差分=Comparison.Value.TryGetProperty("difference",out var Difference)
                            ?Difference.GetString()
                            :null;
                        差分一覧.Add($"{Comparison.Name}: {差分}");
                    }
                }
            }else{
                一致=false;
                差分一覧.Add("比較結果がありません。");
            }
            return new(一致,string.Join(Environment.NewLine,差分一覧));
        }catch(JsonException 例外){
            return new(false,例外.Message);
        }
    }

    private static 動的実行進捗 進捗へ変換(
        動的実行設定 設定,
        int QueryIndex,
        SourceTarget 対象,
        string 段階,
        WorkerProgressEvent 進捗){
        return new(
            QueryIndex,
            設定.Query数,
            対象,
            段階,
            進捗.CompletedSourceExecutions,
            進捗.CompilationFailures,
            進捗.SemanticMismatches,
            進捗.NonConvergences,
            進捗.ExecutionFailures);
    }

    private static 動的履歴項目 履歴を作成(
        動的実行設定 設定,
        DateTimeOffset 開始日時,
        string 状態){
        return new(){
            実行ID=設定.実行ID,
            開始日時=開始日時,
            状態=状態,
            条件=$"Query {設定.Query数} / N {設定.N} / Input {設定.入力要素数} / {設定.演算子集合モード}",
        };
    }

    private static 動的履歴項目 DB履歴を作成(DynamicRunRecord Run){
        var 条件=Run.Profile;
        try{
            var Snapshot=JsonSerializer.Deserialize<動的実行条件Snapshot>(
                Run.SettingsJSON,
                DynamicWorkerProtocol.JsonOptions);
            if(Snapshot is not null){
                条件=$"Query {Snapshot.QueryCount} / N {Snapshot.OperatorCount} / Input {Snapshot.InputElementCount} / {Snapshot.Mode}";
            }
        }catch(JsonException){
        }
        return new(){
            実行ID=Run.RunID,
            開始日時=Run.StartedAt??Run.CreatedAt,
            状態=Run.Status,
            条件=条件,
            削除済みか=Run.DeletedAt is not null,
        };
    }

    private static 計測概要? 計測を取得(実行Artifact Artifact,string 実装){
        return Artifact.計測一覧.FirstOrDefault(
            計測=>string.Equals(計測.実装,実装,StringComparison.OrdinalIgnoreCase));
    }

    private static string 実装名を正規化(string? 値){
        if(string.Equals(値,"system",StringComparison.OrdinalIgnoreCase)){
            return"System";
        }
        if(string.Equals(値,"fLinq",StringComparison.OrdinalIgnoreCase)){
            return"FLinq";
        }
        if(string.Equals(値,"zLinq",StringComparison.OrdinalIgnoreCase)){
            return"ZLinq";
        }
        return 値??"Unknown";
    }

    private static string Hashを計算(string? 値){
        return string.IsNullOrEmpty(値)
            ?string.Empty
            :Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(値)));
    }

    private static string Workerパスを解決(){
        var ルート=Path.Combine(AppContext.BaseDirectory,"DynamicWorker");
        var EXE=Path.Combine(ルート,"Benchmark.Dynamic.Worker.exe");
        if(File.Exists(EXE)){
            return EXE;
        }
        var DLL=Path.Combine(ルート,"Benchmark.Dynamic.Worker.dll");
        if(File.Exists(DLL)){
            return DLL;
        }
        throw new FileNotFoundException("Benchmark.Dynamic.Workerが見つかりません。",EXE);
    }

    private static string? ソリューションルートを検索(){
        var ディレクトリ=new DirectoryInfo(AppContext.BaseDirectory);
        while(ディレクトリ is not null){
            if(File.Exists(Path.Combine(ディレクトリ.FullName,"Seamless.slnx"))){
                return ディレクトリ.FullName;
            }
            ディレクトリ=ディレクトリ.Parent;
        }
        return null;
    }

    private static string ナノ秒を表示(double 値){
        return double.IsFinite(値)?値.ToString("N1")+" ns":"—";
    }

    private static string バイトを表示(double 値){
        return double.IsFinite(値)?値.ToString("N1")+" B":"—";
    }

    private static string 比を表示(double 基準,double 比較){
        return 基準>0&&比較>0?(基準/比較).ToString("N2")+"x":"—";
    }

    private sealed record 実行Artifact(
        long Source生成ナノ秒,
        long コンパイル合計ナノ秒,
        long SourceTextナノ秒,
        long SyntaxTreeナノ秒,
        long Compilationナノ秒,
        long Generatorナノ秒,
        long Emitナノ秒,
        long Loadナノ秒,
        long Bindナノ秒,
        int RewriteCount,
        bool FixedPointか,
        IReadOnlyList<計測概要> 計測一覧){
        public static readonly 実行Artifact Empty=new(0,0,0,0,0,0,0,0,0,0,false,[]);
    }

    private sealed record 計測概要(
        string 実装,
        int OperationCount,
        int WarmupCount,
        int SampleCount,
        double 中央値,
        double 平均,
        double 最小,
        double 最大,
        double 標準偏差,
        double 標準誤差,
        double 割当Bytes,
        int Gen0,
        int Gen1,
        int Gen2,
        IReadOnlyList<計測Sample> Sample一覧);

    private sealed record 計測Sample(
        int SampleIndex,
        int ExecutionOrder,
        long ElapsedNanoseconds,
        int OperationCount,
        long AllocatedBytes,
        int Gen0,
        int Gen1,
        int Gen2);

    private sealed record 検証概要(bool 一致か,string 差分);
}
