namespace Seamless.Management;

public static partial class DatabaseDesigner{
    public static Definition PrepareTableRestore(Definition published,Definition draft,string path){
        var migration=draft.Migration?.Freeze();
        var reverse=published with{Migration=migration is null?null:new MigrationDefinition(migration.Version,
            migration.Tables.Select(item=>new TableMigration(item.Source,item.Target,[],[])).ToArray(),migration.NewTables??[],migration.DroppedTables)};
        var selection=SelectTables(draft,reverse,path);
        if(selection.Length!=1){
            throw new ManagementException("ForeignKeyDefinition","この表だけを戻すと別の表の外部キーが成立しません。関連する下書きを確認してください。");
        }
        var target=selection[0];
        var draftPaths=target.PublishedPath is{} before?new HashSet<string>(StringComparer.Ordinal){before}:new(StringComparer.Ordinal);
        var livePaths=target.DraftPath is{} after?new HashSet<string>(StringComparer.Ordinal){after}:new(StringComparer.Ordinal);
        var source=ProjectTables(draft,reverse,selection,true) with{Revision=draft.Source.Revision};
        SourceEditor.Validate(source);
        return draft with{Source=source,Migration=FilterMigration(migration,livePaths,draftPaths,false)};
    }
}
