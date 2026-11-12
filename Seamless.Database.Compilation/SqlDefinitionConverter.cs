using System.Collections;
using System.Globalization;
using System.Reflection;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Seamless.Database.Compilation;

public sealed record SqlDefinitionConversionResult(string SourceSql,DatabaseCatalog Catalog,GeneratedDatabase?CSharp,ImmutableArray<NativeDiagnostic>Diagnostics);

/// <summary>SQL定義からSeamlessのC#定義を生成する純粋な変換。接続・投入・比較は行わない。</summary>
public sealed class SqlDefinitionConverter{
    public SqlDefinitionConversionResult Convert(string sql,SqlVendor vendor,string databaseName){
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        var diagnostics=ImmutableArray.CreateBuilder<NativeDiagnostic>();
        var tables=new List<(string Schema,TableCatalog Table)>();
        try{
            SqlParser.Dialects.Dialect dialect;
            if(vendor==SqlVendor.SqlServer){dialect=new MsSqlDialect();}else{dialect=new MySqlDialect();}
            foreach(var statement in new SqlQueryParser().Parse(sql.AsSpan(),dialect)){
                if(statement is not Statement.CreateTable create){diagnostics.Add(new("SQL_DEFINITION_UNSUPPORTED","The definition is retained but its C# conversion is not implemented: "+statement.GetType().Name,NativeDiagnosticSeverity.Unsupported,null,null));continue;}
                try{tables.Add(Table(create.Element));}
                catch(NotSupportedException error){diagnostics.Add(new("SQL_DEFINITION_UNSUPPORTED",error.Message,NativeDiagnosticSeverity.Unsupported,create.Element.Name.ToString(),null));}
            }
        }catch(ParserException error){diagnostics.Add(new("SQL_PARSE_ERROR",error.Message,NativeDiagnosticSeverity.CompilationError,null,null));}
        var schemas=tables.GroupBy(t=>t.Schema,StringComparer.Ordinal).Select(g=>new SchemaCatalog(g.Key,g.Select(t=>t.Table).ToArray())).ToArray();
        var catalog=new DatabaseCatalog(databaseName,schemas);
        GeneratedDatabase?generated=null;
        if(tables.Count>0){
            try{generated=CSharpDatabaseGenerator.GenerateNative(catalog);}
            catch(ArgumentException error){diagnostics.Add(new("SQL_CATALOG_INVALID",error.Message,NativeDiagnosticSeverity.CompilationError,databaseName,null));}
        }
        return new(sql,catalog,generated,diagnostics.ToImmutable());
    }

    private static (string Schema,TableCatalog Table)Table(CreateTable definition){
        Guard(definition,"Name","Columns","Constraints","HiveDistribution");
        if(definition.HiveDistribution is not HiveDistributionStyle.None){throw new NotSupportedException("Table distribution is unsupported.");}
        if(definition.Name.Values.Count is <1 or >2){throw new NotSupportedException("Only local schema-qualified table names are supported.");}
        var name=definition.Name.Values[^1].Value;var schema="dbo";
        if(definition.Name.Values.Count==2){schema=definition.Name.Values[0].Value;}
        var keys=new List<string>();var columns=new List<ColumnCatalog>();
        foreach(var source in definition.Columns??[]){
            Guard(source,"Name","DataType","Options");
            var nullable=true;var primary=false;
            foreach(var option in source.Options??[]){
                Guard(option,"Option");
                switch(option.Option){
                    case ColumnOption.Null:nullable=true;break;
                    case ColumnOption.NotNull:nullable=false;break;
                    case ColumnOption.Unique unique when unique.IsPrimary:Guard(unique,"IsPrimary");primary=true;nullable=false;break;
                    default:throw new NotSupportedException("Column option is retained but not converted: "+option.Option.GetType().Name);
                }
            }
            var type=Type(source.DataType,nullable);
            columns.Add(new(columns.Count,source.Name.Value,type,false));
            if(primary){keys.Add(source.Name.Value);}
        }
        foreach(var constraint in definition.Constraints??[]){
            if(constraint is not TableConstraint.Unique unique||!unique.IsPrimaryKey){throw new NotSupportedException("This table constraint is retained but not converted.");}
            Guard(unique,"Columns","Name","IsPrimaryKey");
            if(keys.Count>0){throw new NotSupportedException("Multiple primary keys are unsupported.");}
            keys.AddRange(unique.Columns.Select(c=>c.Value));
        }
        foreach(var key in keys){
            var index=columns.FindIndex(c=>c.Name==key);if(index<0){throw new NotSupportedException("Primary key column is missing: "+key);}
            columns[index]=columns[index] with{Type=columns[index].Type with{IsNullable=false}};
        }
        PrimaryKeyCatalog?primaryKey=null;
        if(keys.Count>0){primaryKey=new("PK_"+name,keys.Select(k=>new KeyColumnCatalog(k,false)).ToArray());}
        return(schema,new(name,columns,primaryKey));
    }

    private static DatabaseType Type(DataType type,bool nullable){
        var name=type.GetType().Name.ToLowerInvariant();
        DatabaseTypeKind kind;
        int?length=null;
        switch(name){
            case "int":case "integer":Guard(type);kind=DatabaseTypeKind.Int32;name="int";break;
            case "smallint":Guard(type);kind=DatabaseTypeKind.Int16;break;
            case "bigint":Guard(type);kind=DatabaseTypeKind.Int64;break;
            case "nvarchar":case "varchar":case "char":case "nchar":
                Guard(type,"Length","CharacterLength");kind=DatabaseTypeKind.String;
                var raw=ReadOptional(type,"CharacterLength")??ReadOptional(type,"Length");
                if(raw is not null&&raw.GetType().Name!="Max"){
                    Guard(raw,"Length","Unit");
                    var value=ReadOptional(raw,"Length")??raw;
                    length=checked(global::System.Convert.ToInt32(value,CultureInfo.InvariantCulture));
                    if(length<=0){throw new NotSupportedException("String length must be positive.");}
                }
                break;
            case "text":case "tinytext":case "mediumtext":case "longtext":Guard(type);kind=DatabaseTypeKind.String;break;
            case "double":case "doubleprecision":Guard(type);kind=DatabaseTypeKind.Double;break;
            case "real":Guard(type);kind=DatabaseTypeKind.Single;break;
            case "decimal":case "numeric":
                throw new NotSupportedException("Exact native decimal import needs a precision/range adapter; SQL precision is not imposed on the native type.");
            default:throw new NotSupportedException("SQL type is retained but its native conversion is not implemented: "+name);
        }
        return new(kind,nullable,MaximumLength:length,SqlTypeName:name);
    }

    private static object?ReadOptional(object instance,string name)=>instance.GetType().GetProperty(name)?.GetValue(instance);
    private static void Guard(object instance,params string[]consumed){
        foreach(var property in instance.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
            if(!property.CanRead||property.GetIndexParameters().Length>0||consumed.Contains(property.Name,StringComparer.Ordinal)){continue;}
            var value=property.GetValue(instance);if(Active(value)){throw new NotSupportedException("Unsupported SQL clause: "+instance.GetType().Name+"."+property.Name);}
        }
    }
    private static bool Active(object?value){
        if(value is null){return false;}if(value is bool flag){return flag;}if(value is string text){return text.Length>0;}
        if(value is IEnumerable items){return items.GetEnumerator().MoveNext();}
        if(value.GetType().IsEnum){return global::System.Convert.ToInt64(value,CultureInfo.InvariantCulture)!=0;}
        if(value is int number){return number!=0;}return true;
    }
}
