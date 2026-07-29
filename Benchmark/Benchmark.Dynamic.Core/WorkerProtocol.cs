using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Core;

public static class WorkerMessageTypes{
    public const string Ping="ping";
    public const string Pong="pong";
    public const string StartRun="startRun";
    public const string Cancel="cancel";
    public const string Shutdown="shutdown";
    public const string WorkerStarted="workerStarted";
    public const string RunStarted="runStarted";
    public const string QueryStarted="queryStarted";
    public const string SourceExecutionStarted="sourceExecutionStarted";
    public const string SourceExecutionCompleted="sourceExecutionCompleted";
    public const string Progress="progress";
    public const string Diagnostic="diagnostic";
    public const string RunCompleted="runCompleted";
    public const string RunCanceled="runCanceled";
    public const string Fatal="fatal";
}

public static class WorkerFatalErrorTypes{
    public const string AssemblyUnloadFailure="AssemblyUnloadResult";
}

public sealed record WorkerMessageEnvelope(
    [property:JsonPropertyName("protocolVersion")]int ProtocolVersion,
    [property:JsonPropertyName("requestId")]string RequestId,
    [property:JsonPropertyName("messageType")]string MessageType,
    [property:JsonPropertyName("payload")]JsonElement Payload);

public class DynamicWorkerProtocolException:IOException{
    public DynamicWorkerProtocolException(string message)
        :base(message){
    }

    public DynamicWorkerProtocolException(string message,Exception innerException)
        :base(message,innerException){
    }
}

public sealed class DynamicWorkerProtocolVersionException:DynamicWorkerProtocolException{
    public DynamicWorkerProtocolVersionException(int expectedVersion,int actualVersion)
        :base($"Worker protocol version mismatch. Expected={expectedVersion} Actual={actualVersion}"){
        this.ExpectedVersion=expectedVersion;
        this.ActualVersion=actualVersion;
    }

    public int ExpectedVersion{get;}
    public int ActualVersion{get;}
}

public static class DynamicWorkerProtocol{
    public const int CurrentVersion=4;
    private static readonly JsonSerializerOptions SerializerOptions=CreateSerializerOptions();

    public static JsonSerializerOptions JsonOptions=>SerializerOptions;

    public static WorkerMessageEnvelope Create<T>(string requestId,string messageType,T payload){
        ValidateIdentity(requestId,messageType);
        return new(
            CurrentVersion,
            requestId,
            messageType,
            JsonSerializer.SerializeToElement(payload,SerializerOptions));
    }

    public static string Serialize(WorkerMessageEnvelope envelope){
        ArgumentNullException.ThrowIfNull(envelope);
        Validate(envelope);
        return JsonSerializer.Serialize(envelope,SerializerOptions);
    }

    public static WorkerMessageEnvelope Deserialize(string jsonLine){
        ArgumentNullException.ThrowIfNull(jsonLine);
        jsonLine=jsonLine.TrimStart('\uFEFF');
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonLine);
        if(jsonLine.Contains('\r')||jsonLine.Contains('\n')){
            throw new DynamicWorkerProtocolException("A JSON Lines message must contain exactly one line.");
        }
        try{
            var envelope=JsonSerializer.Deserialize<WorkerMessageEnvelope>(jsonLine,SerializerOptions)
                ??throw new DynamicWorkerProtocolException("Worker message was JSON null.");
            Validate(envelope);
            return envelope;
        }catch(DynamicWorkerProtocolException){
            throw;
        }catch(JsonException exception){
            throw new DynamicWorkerProtocolException("Worker message is not valid JSON.",exception);
        }
    }

    public static T DeserializePayload<T>(WorkerMessageEnvelope envelope){
        ArgumentNullException.ThrowIfNull(envelope);
        Validate(envelope);
        try{
            return envelope.Payload.Deserialize<T>(SerializerOptions)
                ??throw new DynamicWorkerProtocolException($"Worker payload could not be read as {typeof(T).FullName}.");
        }catch(DynamicWorkerProtocolException){
            throw;
        }catch(JsonException exception){
            throw new DynamicWorkerProtocolException(
                $"Worker payload could not be read as {typeof(T).FullName}.",
                exception);
        }
    }

    private static void Validate(WorkerMessageEnvelope envelope){
        if(envelope.ProtocolVersion!=CurrentVersion){
            throw new DynamicWorkerProtocolVersionException(CurrentVersion,envelope.ProtocolVersion);
        }
        ValidateIdentity(envelope.RequestId,envelope.MessageType);
        if(envelope.Payload.ValueKind==JsonValueKind.Undefined){
            throw new DynamicWorkerProtocolException("Worker message payload is missing.");
        }
    }

    private static void ValidateIdentity(string requestId,string messageType){
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        if(requestId.Contains('\r')||requestId.Contains('\n')){
            throw new ArgumentException("Worker request ID cannot contain a newline.",nameof(requestId));
        }
        if(messageType.Contains('\r')||messageType.Contains('\n')){
            throw new ArgumentException("Worker message type cannot contain a newline.",nameof(messageType));
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions(){
        var options=new JsonSerializerOptions{
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy=JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull,
            WriteIndented=false,
            UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

public sealed record WorkerPingRequest(DateTimeOffset RequestedAtUtc);
public sealed record WorkerPongEvent(DateTimeOffset RequestedAtUtc,DateTimeOffset RespondedAtUtc,string WorkerVersion);
public sealed record WorkerControlRequest(string RunId,DateTimeOffset RequestedAtUtc);

public sealed record WorkerMeasurementProfile(
    string ProfileId="Balanced-v1",
    int WarmupCount=2,
    int SampleCount=7,
    int MinimumCalibrationMilliseconds=25,
    int MaximumOperationCount=1_048_576);

public sealed record WorkerReplayQuery(
    int QueryIndex,
    string IrJson,
    string? ParentQueryId=null,
    Seed256? FixtureSeed=null,
    int? MeasurementQueryIndex=null,
    string? DerivationKind=null);

public sealed record WorkerCompletedSourceExecution(
    int QueryIndex,
    SourceTarget SourceTarget);

public sealed record WorkerRunResumeState(
    IReadOnlyList<WorkerCompletedSourceExecution> CompletedSourceExecutions,
    int FailedSourceExecutions=0,
    int CompilationFailures=0,
    int SemanticMismatches=0,
    int NonConvergences=0,
    int ExecutionFailures=0);

public sealed record WorkerRunRequest(
    string RunId,
    Seed256 BaseSeed,
    int QueryCount,
    int OperatorCount,
    int MaximumDepth,
    int InputElementCount,
    OperatorSetMode OperatorSetMode,
    IReadOnlyList<SourceTarget> SourceTargets,
    WorkerMeasurementProfile MeasurementProfile,
    string GeneratorVersion,
    string CapabilityProfileVersion,
    string OperatorSetVersion,
    IReadOnlyList<WorkerReplayQuery>? ReplayQueries=null,
    Seed256? FixtureSeedOverride=null,
    int? MeasurementQueryIndexOverride=null,
    WorkerRunResumeState? ResumeState=null);

public enum WorkerExecutionStatus{
    NotStarted,
    Running,
    Succeeded,
    CompilationFailed,
    InvocationTargetMismatch,
    SemanticMismatch,
    OptimizationNonConverged,
    ExecutionFailed,
    AssemblyUnloadFailed,
    Canceled,
    Fatal,
}

public sealed record WorkerStartedEvent(
    int ProcessId,
    string WorkerVersion,
    string RuntimeVersion,
    DateTimeOffset StartedAtUtc);

public sealed record WorkerRunStartedEvent(string RunId,DateTimeOffset StartedAtUtc,int TotalSourceExecutions);
public sealed record WorkerQueryStartedEvent(
    string RunId,
    string QueryId,
    int QueryIndex,
    Seed256 QuerySeed,
    string IrJson,
    long IrGenerationNanoseconds,
    string? ReplayedFromQueryId=null,
    string DerivationKind="SharedIR");
public sealed record WorkerSourceExecutionStartedEvent(
    string RunId,
    string QueryId,
    int QueryIndex,
    SourceTarget SourceTarget,
    int SourceOrdinal,
    DateTimeOffset StartedAtUtc);

public sealed record WorkerSourceExecutionCompletedEvent(
    string RunId,
    string QueryId,
    int QueryIndex,
    SourceTarget SourceTarget,
    WorkerExecutionStatus Status,
    string SourceText,
    string SourceHash,
    string? ResultJson,
    string? DiagnosticsJson,
    string? OptimizationTraceJson,
    string? MeasurementsJson,
    bool AssemblyUnloaded,
    DateTimeOffset CompletedAtUtc);

public sealed record WorkerProgressEvent(
    string RunId,
    int CompletedSourceExecutions,
    int TotalSourceExecutions,
    int CompilationFailures,
    int SemanticMismatches,
    int NonConvergences,
    int ExecutionFailures,
    bool CancellationRequested);

public sealed record WorkerDiagnosticEvent(
    string RunId,
    string Stage,
    string Severity,
    string Message,
    string? QueryId=null,
    SourceTarget? SourceTarget=null,
    string? DiagnosticId=null);

public sealed record WorkerRunCompletedEvent(
    string RunId,
    WorkerExecutionStatus Status,
    int CompletedSourceExecutions,
    int FailedSourceExecutions,
    DateTimeOffset CompletedAtUtc);

public sealed record WorkerFatalEvent(
    string RunId,
    string ErrorType,
    string Message,
    string? StackTrace,
    DateTimeOffset OccurredAtUtc);
