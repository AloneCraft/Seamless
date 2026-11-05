namespace Seamless.Management.Server;

public sealed partial class ManagementStore{
    private AuditQueryInfo? ReadAuditMethod(string? artifact){
        if(string.IsNullOrEmpty(artifact)){return null;}var value=this.Read<QueryAuditMetadata>("queryAudit",artifact);if(value is null){return null;}QueryAuditMetadataBuilder.Validate(value,value.SourceHash,artifact);
        if(value.SourceHash is null||value.SourceHash.Length!=64||!value.SourceHash.All(static character=>Uri.IsHexDigit(character))||value.ArtifactHash.Length!=64||!value.ArtifactHash.All(static character=>Uri.IsHexDigit(character))||(value.MetadataToken&0x00ffffff)==0){throw new InvalidDataException("集計に対応する問い合わせの監査情報が不正です。");}
        TextBound(value.Namespace,1024);TextBound(value.TypeName,1024);TextBound(value.MethodName,512);TextBound(value.MethodSignature,2048);TextBound(value.QueryTemplate,8192);return new(value.Namespace,value.TypeName,value.MethodName,value.MethodSignature,value.ModuleId,value.MetadataToken,value.QueryTemplate);
    }
    private static AuditReadEntry ReadAuditEntry(AuditSummaryRow row,AuditQueryInfo? method,bool allowTemplate){if(!allowTemplate&&method is not null){method=method with{QueryTemplate=null};}return new(row.Sequence,row.Key,row.Count,row.SavedAt){Method=method};}
    private static string ReadAuditSearchText(AuditReadKey key,AuditQueryInfo? method)=>string.Join('\n',key.SubjectId,key.Database,key.QueryId,key.CodeVersion,method?.Namespace,method?.TypeName,method?.MethodName,method?.MethodSignature,method?.QualifiedName).ToUpperInvariant();
}
