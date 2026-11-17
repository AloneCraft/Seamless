namespace Seamless.Management;

public sealed record ResourceHeatPersistence(string State,string? Reason=null);
public sealed record ResourceHeatQuery(string DefinitionId,string ObjectId,Guid ExecutionId,long? ExpectedVersion=null,string? ExpectedArtifactHash=null){
    public void Validate(){
        if(string.IsNullOrWhiteSpace(this.DefinitionId)||this.DefinitionId.Length>4096||string.IsNullOrWhiteSpace(this.ObjectId)||this.ObjectId.Length>4096||this.ExecutionId==Guid.Empty||this.ExpectedVersion is <1||this.ExpectedArtifactHash is{} hash&&(hash.Length!=64||!hash.All(Uri.IsHexDigit))){throw new ManagementException("InvalidRequest","資源履歴の実行識別または版を確認してください。");}
    }
}
/// <summary>今回のcollector起動分。保存済み期間の総数・サンプル数とは別のカウンター。</summary>
public sealed record ResourceHeatHistoryStatus(Guid SessionId,long Captured,long Saved,long Dropped,long SaveFailures,long PendingBytes,int PendingCount,string? ErrorCode,bool Closed);
public sealed record ResourceHeatReadResult(ResourceHeatTrace? Trace,ResourceHeatHistoryStatus Status,int InvalidPeriods,bool ScanLimited,string? UnavailableReason);
