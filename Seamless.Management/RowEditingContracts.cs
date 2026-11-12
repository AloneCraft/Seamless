using System.Text.Json;

namespace Seamless.Management;

public sealed record TableRowsRequest(string Database,string Table,int Limit=100,long? ExpectedVersion=null,JsonElement? Key=null){
    public TableRowCursor? Cursor{get;init;}
    public TableRowsRequest Freeze()=>this with{Key=this.Key?.Clone()};
}
public sealed record TableRowCursor(long Offset,string PrefixHash);
public sealed record TableRowSnapshot(JsonElement Key,JsonElement Values,string Stamp);
public sealed record TableRows(long Version,TableRowSnapshot[] Rows,bool HasMore,CatalogField[]? Columns=null){
    public TableRowCursor? NextCursor{get;init;}
    public CatalogField[] KeyMembers{get;init;}=[];
}
public enum TableRowChangeKind{Insert,Replace,Delete}
public sealed record TableRowChange(string Table,TableRowChangeKind Kind,JsonElement? Key=null,string? ExpectedStamp=null,JsonElement? Values=null);
public sealed record TableRowBatch(string Database,long ExpectedVersion,Guid OperationId,TableRowChange[] Changes){
    public TableRowBatch Freeze(){
        if(this.Changes is null||this.Changes.Any(change=>change is null)){throw new ManagementException("InvalidRequest","行の変更内容を指定してください。");}
        return this with{Changes=this.Changes.Select(change=>change with{Key=change.Key?.Clone(),Values=change.Values?.Clone()}).ToArray()};
    }
}
public sealed record TableRowCommit(long Version,Guid OperationId,int ChangedRows);
