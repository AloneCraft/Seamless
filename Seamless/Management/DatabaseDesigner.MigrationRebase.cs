namespace Seamless.Management;

public static partial class DatabaseDesigner{
    internal static void CheckMigrationRebase(DefinitionVersion previous,DefinitionVersion latest,Definition local){
        if(local.Migration is not{} migration){return;}
        migration=migration.Freeze();
        if(latest.Version-previous.Version!=1){
            throw new ManagementException("MigrationRequired","複数の稼働版にまたがるため、現稼働版を基準に移行設定を作り直してください。");
        }
        var before=previous.Definition;
        var active=latest.Definition;
        if(migration.Tables.Any(table=>table.Columns.Any(column=>column.Expression is not null))&&
            (before with{Source=before.Source with{Revision=""},Migration=null}).Hash()!=
            (active with{Source=active.Source with{Revision=""},Migration=null}).Hash()){
            throw new ManagementException("MigrationRequired","変換式の参照先が変わり得ます。現稼働版を基準に移行設定を作り直してください。");
        }
        var publishedMigration=active.Migration;
        var changed=Touched(publishedMigration).ToHashSet(StringComparer.Ordinal);
        var oldTables=Tables(before).ToHashSet(StringComparer.Ordinal);
        var activeTables=Tables(active).ToHashSet(StringComparer.Ordinal);
        foreach(var path in migration.Tables.Select(item=>item.Source).Concat(migration.DroppedTables).Distinct(StringComparer.Ordinal)){
            if(changed.Contains(path)||!oldTables.Contains(path)||!activeTables.Contains(path)||!SameMigrationTable(before,active,path)){
                throw new ManagementException("MigrationRequired","移行元の表が変わりました。現稼働版を基準に移行設定を作り直してください。 "+path);
            }
        }
        foreach(var path in (migration.NewTables??[]).Concat(migration.Tables.Where(item=>item.Source!=item.Target).Select(item=>item.Target))){
            if(changed.Contains(path)||oldTables.Contains(path)||activeTables.Contains(path)){
                throw new ManagementException("MigrationRequired","移行先の表名が競合しています。 "+path);
            }
        }
    }

    internal static void CheckRebasedMigrationTarget(Definition local,Definition candidate){
        if(candidate.Migration is not{} migration){return;}
        var tables=Tables(candidate).ToHashSet(StringComparer.Ordinal);
        foreach(var path in migration.Tables.Select(item=>item.Target).Concat(migration.NewTables??[]).Distinct(StringComparer.Ordinal)){
            if(!tables.Contains(path)||!SameMigrationTable(local,candidate,path)){
                throw new ManagementException("MigrationRequired","比較中に移行先の表が変わりました。下書きと移行設定を確認してください。 "+path);
            }
        }
        if(migration.DroppedTables.Any(tables.Contains)){
            throw new ManagementException("MigrationRequired","削除する表が統合後の定義に残っています。移行設定を確認してください。");
        }
    }

    private static IEnumerable<string> Touched(MigrationDefinition? migration){
        if(migration is null){return [];}
        return migration.Tables.SelectMany(table=>new[]{table.Source,table.Target}).Concat(migration.DroppedTables).Concat(migration.NewTables??[]);
    }

    private static bool SameMigrationTable(Definition first,Definition second,string path){
        return TableSource(first,path).Text==TableSource(second,path).Text&&Read(first,path).Columns.Select(Shape).SequenceEqual(Read(second,path).Columns.Select(Shape));

        static (string Name,string Type,bool PrimaryKey) Shape(ColumnDesign column)=>(column.Name,column.Type,column.PrimaryKey);
    }
}
