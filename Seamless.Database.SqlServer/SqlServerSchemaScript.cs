using System.Globalization;
using System.Text;

namespace Seamless.Database.SqlServer;

/// <summary>
/// 製品に依存しない目録(<see cref="DatabaseCatalog"/>)から、SQL Server の DB を作る文を作る。
/// 順番は「スキーマ → 表(列・IDENTITY・既定値・計算列・主キー・一意キー)」→(行を入れる)→「外部キー・CHECK → ビュー → トリガー」。
/// 外部キーと CHECK は行を入れた後に WITH CHECK で付ける(表の順番に頼らず、全部の行で検査する)。
/// トリガーは行を入れた後に作る(行を入れるときに動かさない)。
/// </summary>
public sealed record SqlServerSchemaScript(IReadOnlyList<string> Schemas,IReadOnlyList<string> Tables,IReadOnlyList<string> Constraints,IReadOnlyList<string> Triggers){
    /// <summary>目録から文を作る。</summary>
    public static SqlServerSchemaScript Create(DatabaseCatalog catalog){
        ArgumentNullException.ThrowIfNull(catalog);
        var schemas=catalog.Schemas.Select(static schema=>schema.Name).Where(static name=>!string.Equals(name,"dbo",StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static name=>$"CREATE SCHEMA {Quote(name)};").ToArray();
        var tables=new List<string>();
        var constraints=new List<string>();
        var triggers=new List<string>();
        foreach(var schema in catalog.Schemas){
            foreach(var table in schema.Tables){
                var relation=Quote(schema.Name)+"."+Quote(table.Name);
                var lines=table.Columns.OrderBy(static column=>column.Ordinal).Select(Column).ToList();
                if(table.PrimaryKey is{} primaryKey){
                    var kind=primaryKey.IndexType==2?" NONCLUSTERED":" CLUSTERED";
                    lines.Add($"    CONSTRAINT {Quote(primaryKey.Name)} PRIMARY KEY{kind} ({Keys(primaryKey.Columns)})");
                }
                foreach(var unique in table.UniqueKeys){
                    lines.Add($"    CONSTRAINT {Quote(unique.Name)} UNIQUE ({Keys(unique.Columns)})");
                }
                tables.Add($"CREATE TABLE {relation} (\n{string.Join(",\n",lines)}\n);");
                foreach(var foreignKey in table.ForeignKeys){
                    constraints.Add($"ALTER TABLE {relation} WITH CHECK ADD CONSTRAINT {Quote(foreignKey.Name)} FOREIGN KEY ({string.Join(",",foreignKey.Columns.Select(static pair=>Quote(pair.Column)))}) REFERENCES {Quote(foreignKey.ReferencedSchema)}.{Quote(foreignKey.ReferencedTable)} ({string.Join(",",foreignKey.Columns.Select(static pair=>Quote(pair.ReferencedColumn)))}) ON DELETE {Action(foreignKey.DeleteAction)} ON UPDATE {Action(foreignKey.UpdateAction)};");
                }
                foreach(var check in table.CheckConstraints){
                    constraints.Add($"ALTER TABLE {relation} WITH CHECK ADD CONSTRAINT {Quote(check.Name)} CHECK {check.Definition};");
                    if(check.IsDisabled){constraints.Add($"ALTER TABLE {relation} NOCHECK CONSTRAINT {Quote(check.Name)};");}
                }
                foreach(var trigger in table.Triggers){
                    triggers.Add(trigger.Definition);
                    if(trigger.IsDisabled){triggers.Add($"DISABLE TRIGGER {Quote(schema.Name)}.{Quote(trigger.Name)} ON {relation};");}
                }
            }
        }
        return new(schemas,tables,constraints,triggers);
    }

    private static string Column(ColumnCatalog column){
        if(column.ComputedDefinition is{} computed){
            return $"    {Quote(column.Name)} AS {computed}{(column.IsComputedPersisted?" PERSISTED":"")}";
        }
        var builder=new StringBuilder("    ").Append(Quote(column.Name)).Append(' ').Append(TypeText(column.Type));
        if(column.Type.Collation is{} collation&&IsText(column.Type)){builder.Append(" COLLATE ").Append(collation);}
        if(column.IsIdentity){builder.Append(" IDENTITY(").Append(column.IdentitySeed.ToString(CultureInfo.InvariantCulture)).Append(',').Append(column.IdentityIncrement.ToString(CultureInfo.InvariantCulture)).Append(')');}
        builder.Append(column.Type.IsNullable?" NULL":" NOT NULL");
        if(column.DefaultDefinition is{} definition){
            if(column.DefaultConstraintName is{} name&&!column.IsDefaultSystemNamed){builder.Append(" CONSTRAINT ").Append(Quote(name));}
            builder.Append(" DEFAULT ").Append(definition);
        }
        return builder.ToString();
    }

    /// <summary>列の型の文(nvarchar(50)・decimal(19,4)・datetime2(7) など)。</summary>
    public static string TypeText(DatabaseType type){
        var name=type.SqlTypeName??throw new InvalidOperationException("SQL Server の型名の無い列は書き出せない。");
        return name switch{
            "varchar" or "nvarchar" or "varbinary" or "char" or "nchar" or "binary"=>$"{name}({(type.MaximumLength is{} length?length.ToString(CultureInfo.InvariantCulture):"max")})",
            "decimal" or "numeric"=>$"{name}({type.Precision??18},{type.Scale??0})",
            "time" or "datetime2" or "datetimeoffset"=>type.Scale is{} scale?$"{name}({scale})":name,
            "float"=>"float",
            "timestamp" or "rowversion"=>"rowversion",
            _=>name,
        };
    }

    private static bool IsText(DatabaseType type)=>type.SqlTypeName is "varchar" or "nvarchar" or "char" or "nchar" or "text" or "ntext" or "sysname";
    private static string Keys(IReadOnlyList<KeyColumnCatalog> columns)=>string.Join(",",columns.Select(static column=>Quote(column.Name)+(column.IsDescending?" DESC":" ASC")));
    private static string Action(string action)=>action.Replace('_',' ');
    /// <summary>名前を [] で囲む。</summary>
    public static string Quote(string name)=>"["+name.Replace("]","]]",StringComparison.Ordinal)+"]";
}
