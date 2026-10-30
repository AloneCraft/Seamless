using System.Collections;
using System.Globalization;
using System.Reflection;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;

namespace Seamless.Database.Query;

public abstract record SqlDdlCommand(string Schema,string Name);
public sealed record SqlCreateTableCommand(string Schema,string Name,IReadOnlyList<RuntimeSqlColumn> Columns):SqlDdlCommand(Schema,Name);
public sealed record SqlAddColumnCommand(string Schema,string Name,RuntimeSqlColumn Column):SqlDdlCommand(Schema,Name);
public sealed record SqlDropTableCommand(string Schema,string Name):SqlDdlCommand(Schema,Name);

/// <summary>SqlParserCS 0.6.5 の AST を限定された所有 DDL コマンドへ変換する。</summary>
public static class SqlDdlParser{
    public static SqlDdlCommand Parse(string sql){
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        try{
            var parsed=new SqlQueryParser().Parse(sql.AsSpan(),new MsSqlDialect());if(parsed.Count!=1){throw Unsupported("Exactly one DDL statement is required.");}
            return parsed[0] switch{Statement.CreateTable x=>Create(x.Element),Statement.AlterTable x=>Alter(x),Statement.Drop x=>Drop(x),_=>throw Unsupported("Unsupported DDL statement.")};
        }catch(SqlQueryException){throw;}catch(ParserException error){throw new SqlQueryException("Unsupported DDL syntax: "+error.Message,40515);}
    }
    private static SqlDdlCommand Create(CreateTable x){
        Guard(x,"Name","Columns","Constraints","HiveDistribution");if(x.HiveDistribution is not HiveDistributionStyle.None){throw Unsupported("Unsupported table distribution.");}
        var (schema,name)=Name(x.Name);if(x.Columns is null||x.Columns.Count==0){throw Unsupported("Columns are required.");}
        var columns=x.Columns.Select(Column).ToArray();var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var col in columns.Where(c=>c.IsPrimaryKey)){keys.Add(col.Name);}
        if(x.Constraints is not null){foreach(var constraint in x.Constraints){if(constraint is not TableConstraint.Unique unique||!unique.IsPrimaryKey){throw Unsupported("Only a numeric primary key constraint is supported.");}Guard(unique,"Columns","Name","IsPrimaryKey");if(keys.Count!=0){throw Unsupported("Multiple primary keys are unsupported.");}foreach(var key in unique.Columns){Guard(key,"Value","QuoteStyle");if(!keys.Add(key.Value)){throw Unsupported("Duplicate primary key column.");}}}}
        foreach(var key in keys){var i=Array.FindIndex(columns,c=>c.Name.Equals(key,StringComparison.OrdinalIgnoreCase));if(i<0){throw Unsupported("Primary key column was not found.");}if(x.Columns[i].Options?.Any(o=>o.Option is ColumnOption.Null)==true){throw Unsupported("Primary key columns cannot be declared NULL.");}columns[i]=columns[i] with{IsPrimaryKey=true,IsNullable=false};}
        return new SqlCreateTableCommand(schema,name,columns);
    }
    private static SqlDdlCommand Alter(Statement.AlterTable x){
        Guard(x,"Name","Operations");if(x.Operations is null||x.Operations.Count!=1||x.Operations[0] is not AlterTableOperation.AddColumn add){throw Unsupported("Only one ADD column is supported.");}
        Guard(add,"ColumnDef","ColumnKeyword");var column=Column(add.ColumnDef);if(!column.IsNullable||column.IsPrimaryKey){throw Unsupported("ADD requires a nullable non-key column.");}var(schema,name)=Name(x.Name);return new SqlAddColumnCommand(schema,name,column);
    }
    private static SqlDdlCommand Drop(Statement.Drop x){
        Guard(x,"Names","ObjectType");if(x.ObjectType.ToString()!="Table"||x.Names.Count!=1){throw Unsupported("Only one table may be dropped.");}var(schema,name)=Name(x.Names[0]);return new SqlDropTableCommand(schema,name);
    }
    private static (string Schema,string Name) Name(ObjectName value){
        Guard(value,"Values");if(value.Values.Count is <1 or >2){throw Unsupported("Only local one- or two-part names are supported.");}foreach(var id in value.Values){Guard(id,"Value","QuoteStyle");if(id.Value.StartsWith('#')){throw Unsupported("Temporary tables are unsupported.");}}return(value.Values.Count==2?value.Values[0].Value:"dbo",value.Values[value.Values.Count-1].Value);
    }
    private static RuntimeSqlColumn Column(ColumnDef value){
        Guard(value,"Name","DataType","Options");Guard(value.Name,"Value","QuoteStyle");var nullable=true;var sawNull=false;var primary=false;
        if(value.Options is not null){foreach(var option in value.Options){Guard(option,"Option");switch(option.Option){case ColumnOption.Null:if(sawNull){throw Unsupported("Duplicate nullability.");}nullable=true;sawNull=true;break;case ColumnOption.NotNull:if(sawNull){throw Unsupported("Duplicate nullability.");}nullable=false;sawNull=true;break;case ColumnOption.Unique u when u.IsPrimary:Guard(u,"IsPrimary");if(primary){throw Unsupported("Duplicate primary key.");}primary=true;break;default:throw Unsupported("Unsupported column option: "+option.Option.GetType().Name);}}}
        if(primary){if(nullable&&sawNull){throw Unsupported("A primary key cannot be NULL.");}nullable=false;}
        var type=value.DataType;var name=type.GetType().Name.ToLowerInvariant();int?length=null;byte?precision=null,scale=null;
        switch(name){
            case "int":case "integer":case "bigint":Guard(type);if(name=="integer"){name="int";}break;
            case "custom":{Guard(type,"Name");var n=(ObjectName)Read(type,"Name")!;Guard(n,"Values");if(n.Values.Count!=1||n.Values[0].QuoteStyle is not null||!n.Values[0].Value.Equals("bit",StringComparison.OrdinalIgnoreCase)){throw Unsupported("Unsupported SQL type.");}name="bit";break;}
            case "nvarchar":{Guard(type,"Length","CharacterLength");var l=Read(type,"CharacterLength");length=Length(l,1);break;}
            case "varbinary":{Guard(type,"Length");length=Length(Read(type,"Length"),1);break;}
            case "decimal":{Guard(type,"ExactNumberInfo");var info=Read(type,"ExactNumberInfo");precision=18;scale=0;if(info is not null&&info.GetType().Name!="None"){Guard(info,"Length","Scale");precision=checked((byte)Convert.ToInt32(Read(info,"Length"),CultureInfo.InvariantCulture));if(info.GetType().Name=="PrecisionAndScale"){scale=checked((byte)Convert.ToInt32(Read(info,"Scale"),CultureInfo.InvariantCulture));}else if(info.GetType().Name!="Precision"){throw Unsupported("Unsupported decimal precision.");}}break;}
            default:throw Unsupported("Unsupported SQL type: "+name);
        }
        return new(value.Name.Value,name,nullable,length,precision,scale,primary);
    }
    private static int Length(object? value,int fallback){if(value is null){return fallback;}if(value.GetType().Name=="Max"){Guard(value);return -1;}if(value.GetType().Name!="IntegerLength"){throw Unsupported("Unsupported length.");}Guard(value,"Length","Unit");var unit=value.GetType().GetProperty("Unit")?.GetValue(value);if(unit is not null&&unit.ToString()!="None"){throw Unsupported("Unsupported length unit.");}return checked(Convert.ToInt32(Read(value,"Length"),CultureInfo.InvariantCulture));}
    private static object? Read(object value,string property){var p=value.GetType().GetProperty(property,BindingFlags.Public|BindingFlags.Instance)??throw Unsupported("Unexpected parser shape: "+value.GetType().Name+"."+property);return p.GetValue(value);}
    private static void Guard(object value,params string[] consumed){foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){if(property.GetIndexParameters().Length!=0||!property.CanRead||consumed.Contains(property.Name,StringComparer.Ordinal)){continue;}var v=property.GetValue(value);if(v is not null&&(Nullable.GetUnderlyingType(property.PropertyType)is not null||Active(v))){throw Unsupported("Unsupported SQL clause: "+value.GetType().Name+"."+property.Name);}}}
    private static bool Active(object value){if(value is bool b){return b;}if(value is Enum e){return Convert.ToInt64(e,CultureInfo.InvariantCulture)!=0;}if(value is string s){return s.Length!=0;}if(value is IEnumerable items){var it=items.GetEnumerator();try{return it.MoveNext();}finally{(it as IDisposable)?.Dispose();}}if(value.GetType().IsValueType){return !value.Equals(Activator.CreateInstance(value.GetType()));}return true;}
    private static SqlQueryException Unsupported(string reason)=>new(reason,40515);
}
