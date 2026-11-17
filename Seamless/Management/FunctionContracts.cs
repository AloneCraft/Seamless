using System.Text.Json;

namespace Seamless.Management;

public enum FunctionOrigin{DatabaseMember,RegisteredEntry}
public sealed record FunctionTarget(FunctionOrigin Origin,string DatabaseId,string DefinitionId,string ObjectId){
    public void Validate(){
        if(!Enum.IsDefined(this.Origin)||string.IsNullOrWhiteSpace(this.DatabaseId)||this.DatabaseId.Length is <4 or >131||!this.DatabaseId.StartsWith("db:",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(this.DefinitionId)||this.DefinitionId.Length>128||string.IsNullOrWhiteSpace(this.ObjectId)||this.ObjectId.Length>4096||this.DatabaseId.Any(char.IsControl)||this.DefinitionId.Any(char.IsControl)||this.ObjectId.Any(char.IsControl)){throw new ManagementException("InvalidRequest","関数の識別情報が不正です。");}
        if(this.Origin==FunctionOrigin.RegisteredEntry&&this.ObjectId!=this.DefinitionId){throw new ManagementException("InvalidRequest","登録関数の識別情報が一致しません。");}
    }
}
public sealed record FunctionSummary(FunctionTarget Target,string Name,string Schema,long Version,string ArtifactHash,bool WritesData){
    public string? DeclaringType{get;init;}
    public string? EntryMember{get;init;}
}
public sealed record FunctionDetails(FunctionSummary Function,CatalogField[] Parameters,string ReturnType,bool IsSequence,CatalogField[] Columns,CatalogSource Source){
    public string? ElementType{get;init;}
    public string? UnavailableReason{get;init;}
}
public sealed record FunctionExecutionRequest(FunctionTarget Target,JsonElement[] Arguments,int Limit=100,long? ExpectedVersion=null,string? ExpectedArtifactHash=null){
    public ObjectExecutionMeasurementMode MeasurementMode{get;init;}
    public QueryTraceOptions? TraceOptions{get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ResourceHeatOptions? ResourceHeatOptions{get;init;}
    public FunctionExecutionRequest Freeze(){
        if(this.Target is null){throw new ManagementException("InvalidRequest","関数の対象がありません。");}this.Target.Validate();
        if(this.Arguments is null||this.Limit is <1 or >1000||!Enum.IsDefined(this.MeasurementMode)){throw new ManagementException("InvalidRequest","関数の引数または1〜1000行の表示上限を確認してください。");}
        this.TraceOptions?.Validate();
        this.ResourceHeatOptions?.Validate();
        return this with{Arguments=this.Arguments.Select(value=>value.Clone()).ToArray()};
    }
}
