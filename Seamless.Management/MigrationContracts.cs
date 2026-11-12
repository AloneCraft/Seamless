namespace Seamless.Management;

public sealed record ColumnMigration(string Target,string? Source=null,string? Expression=null);
public sealed record TableMigration(string Target,string Source,ColumnMigration[] Columns,string[] DroppedColumns);
public sealed record RemovedTableDefinition(int Version,string DefinitionId,string Path,string File,string Declaration,string Prefix,string Suffix,string RowFingerprint,TableMigration? Mapping,bool WasNew,string? AddedDrop);
public sealed record MigrationDefinition(int Version,TableMigration[] Tables,string[] DroppedTables,string[]? NewTables=null){
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasDataLoss=>this.DroppedTables.Length>0||this.Tables.Any(table=>table.DroppedColumns.Length>0);
    public MigrationDefinition Freeze(){
        if(this.Version!=1||this.Tables is null||this.DroppedTables is null||this.Tables.Length>256||this.DroppedTables.Length>256||this.Tables.Any(table=>table is null||table.Columns is null||table.DroppedColumns is null||table.Columns.Length>4096||table.Columns.Any(column=>column is null))){throw new ManagementException("InvalidRequest","移行定義の版・件数・内容が不正です。");}
        return this with{Tables=this.Tables.Select(table=>table with{Columns=table.Columns.ToArray(),DroppedColumns=table.DroppedColumns.ToArray()}).ToArray(),DroppedTables=this.DroppedTables.ToArray(),NewTables=this.NewTables?.ToArray()};
    }
}
