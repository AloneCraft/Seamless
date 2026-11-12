using System.Text.Json;

namespace Seamless.Management;

public enum ObjectExecutionMeasurementMode{None,Summary,Profiled}
public sealed record ObjectExecutionRequest(string Database,string ObjectId,JsonElement[] Arguments,int Limit=100,long? ExpectedVersion=null){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public ObjectExecutionMeasurementMode MeasurementMode{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public QueryTraceOptions? TraceOptions{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ResourceHeatOptions? ResourceHeatOptions{get;init;}
    public ObjectExecutionRequest Freeze(){
        if(this.Arguments is null){throw new ManagementException("InvalidRequest","引数を指定してください。");}
        this.ResourceHeatOptions?.Validate();
        return this with{Arguments=this.Arguments.Select(value=>value.Clone()).ToArray()};
    }
}
public sealed record ObjectExecutionResult(long Version,bool IsSequence,JsonElement? Value,JsonElement[] Rows,CatalogField[] Columns,bool HasMore){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectExecutionStatistics? Statistics{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? TraceExecutionId{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ResourceHeatTrace? ResourceHeat{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ObjectExecutionProfile? Profile{get;init;}
}
/// <summary>DBゲートの待機開始から結果構築・列挙・読取セッション解放までの実測。</summary>
public sealed record ObjectExecutionStatistics(Guid ExecutionId,string DefinitionId,string ObjectId,long Version,string ArtifactHash,
    DateTimeOffset StartedAtUtc,DateTimeOffset CompletedAtUtc,TimeSpan Elapsed,TimeSpan DatabaseGateWait,TimeSpan? TimeToFirstOutput,
    long ObservedOutputCount,long ReturnedOutputCount,bool IsPartial){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? GateWaitStartedOffset{get;init;}
}
