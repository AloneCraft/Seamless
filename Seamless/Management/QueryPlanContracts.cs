namespace Seamless.Management;

public sealed record QueryPlanRequest(string DefinitionId,string ObjectId,long? ExpectedVersion=null,long? DraftRevision=null,string? BundleHash=null){
    public long? HistoricalVersion{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IncludeGeneratedCode{get;init;}
}
public sealed record QueryPlanSelection(string ObjectId,string[] Methods,ExecutionPlanDocument[] Plans,string? UnavailableReason=null);
public sealed record QueryPlanResult(string DefinitionId,long Version,string ArtifactHash,string SourceHash,bool FromDraft,QueryPlanSelection Selection,long? DraftRevision=null){
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SourceSnapshot? HistoricalSource{get;init;}
}
