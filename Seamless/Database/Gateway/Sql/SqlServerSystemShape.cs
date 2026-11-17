using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.SqlServer;

namespace Seamless.Database.Gateway.Sql;

// 本物の SQL Server が持つシステムのビュー・表の「形」で結果を返す。
// 形は写し取った SqlServerSystemCatalog から取り、Seamless のカタログ(SystemCatalogEmulator.TryCatalog)が持つ値だけを入れる。
// 持たない列は、NULL を許す列なら NULL、許さない列なら型の既定値(0・偽・空文字など)にする。
// 持たないオブジェクトは0行で返す。これで SSMS が参照する列やビューが無くて落ちることはなくなる。
internal static class SqlServerSystemShape{
    private static readonly DateTime DefaultDate=new(1900,1,1);
    private static readonly string[] SystemDatabases=["master","msdb","model","tempdb"];

    internal static bool IsSystemDatabase(string database)=>SystemDatabases.Contains(database,StringComparer.OrdinalIgnoreCase);

    // 形を返せるのはビュー・システム表・ユーザー表(msdb など)だけ。ストアドや関数は FindRoutine 側で扱う。
    internal static SqlServerSystemObject? Find(SqlObjectName name){
        var found=SqlServerSystemCatalog.Default.Resolve(name.Database,name.Schema,name.Name);
        if(found is null||found.Type is not ("V" or "S" or "U")){return null;}
        return found;
    }

    internal static IReadOnlyList<ColumnMetadata> Columns(SqlServerSystemObject definition){
        var columns=new ColumnMetadata[definition.Columns.Count];
        for(var i=0;i<columns.Length;i++){columns[i]=Column(definition.Columns[i],i);}
        return columns;
    }

    // 0行の結果。持たない機能のビューや、値を持たないシステムデータベースのビュー用。
    internal static SqlTable Empty(SqlObjectName name,SqlServerSystemObject definition)=>ToTable(name,Columns(definition),[]);

    // Seamless のカタログが作った結果を、本物の形に広げる。列名が同じものだけ値を写す。
    internal static SqlTable Widen(SqlObjectName name,SqlServerSystemObject definition,QueryResult source){
        var columns=Columns(definition);
        var sourceOrdinals=new int[columns.Count];
        for(var i=0;i<columns.Count;i++){sourceOrdinals[i]=IndexOf(source.Columns,columns[i].Name);}
        var createDate=IndexOf(source.Columns,"create_date");
        var typical=new object?[columns.Count];
        for(var i=0;i<columns.Count;i++){typical[i]=Typical(definition.Columns[i],columns[i].ClrType);}
        var rows=new List<object?[]>();
        foreach(var sourceRow in source.Rows){
            var row=new object?[columns.Count];
            for(var i=0;i<columns.Count;i++){
                object? value=null;
                if(sourceOrdinals[i]>=0){value=Convert(sourceRow[sourceOrdinals[i]],columns[i].ClrType);}
                if(sourceOrdinals[i]<0&&typical[i] is not null){
                    value=typical[i];
                }
                if(value is null&&!columns[i].IsNullable){
                    // 本物の modify_date は create_date と同じ値から始まる。
                    if(columns[i].Name=="modify_date"&&createDate>=0&&sourceRow[createDate] is DateTime date){
                        value=date;
                    }else if(typical[i] is not null){
                        value=typical[i];
                    }else{
                        value=Default(columns[i].ClrType);
                    }
                }
                row[i]=value;
            }
            rows.Add(row);
        }
        return ToTable(name,columns,rows);
    }

    private static SqlTable ToTable(SqlObjectName name,IReadOnlyList<ColumnMetadata> columns,IReadOnlyList<object?[]> rows){
        var sqlColumns=columns.Select(c=>new SqlColumn(c.Name,new SqlType(c.SqlTypeName,c.ClrType,c.MaximumLength,c.Precision,c.Scale,c.Collation),c.IsNullable,name,c.Ordinal)).ToArray();
        return new(name,sqlColumns,_=>rows);
    }

    private static int IndexOf(IReadOnlyList<ColumnMetadata> columns,string name){
        for(var i=0;i<columns.Count;i++){
            if(string.Equals(columns[i].Name,name,StringComparison.OrdinalIgnoreCase)){return i;}
        }
        return -1;
    }

    // MaxLength は本物ではバイト数。ColumnMetadata は文字数で持つ(nchar・nvarchar は半分)。
    private static ColumnMetadata Column(SqlServerSystemColumn c,int ordinal){
        int? length=null;
        byte? precision=null;
        byte? scale=null;
        Type clr;
        string sql;
        switch(c.TypeName.ToLowerInvariant()){
            case "bit":clr=typeof(bool);sql="bit";break;
            case "tinyint":clr=typeof(byte);sql="tinyint";break;
            case "smallint":clr=typeof(short);sql="smallint";break;
            case "int":clr=typeof(int);sql="int";break;
            case "bigint":clr=typeof(long);sql="bigint";break;
            case "real":clr=typeof(float);sql="real";break;
            case "float":clr=typeof(double);sql="float";break;
            case "decimal":clr=typeof(decimal);sql="decimal";precision=c.Precision;scale=c.Scale;break;
            case "numeric":clr=typeof(decimal);sql="numeric";precision=c.Precision;scale=c.Scale;break;
            case "money":clr=typeof(decimal);sql="money";precision=19;scale=4;break;
            case "smallmoney":clr=typeof(decimal);sql="smallmoney";precision=10;scale=4;break;
            case "datetime":clr=typeof(DateTime);sql="datetime";scale=3;break;
            case "smalldatetime":clr=typeof(DateTime);sql="smalldatetime";scale=0;break;
            case "datetime2":clr=typeof(DateTime);sql="datetime2";scale=c.Scale;break;
            case "date":clr=typeof(DateOnly);sql="date";break;
            case "time":clr=typeof(TimeOnly);sql="time";scale=c.Scale;break;
            case "datetimeoffset":clr=typeof(DateTimeOffset);sql="datetimeoffset";scale=c.Scale;break;
            case "uniqueidentifier":clr=typeof(Guid);sql="uniqueidentifier";break;
            case "binary":clr=typeof(byte[]);sql="binary";length=c.MaxLength;break;
            case "varbinary":clr=typeof(byte[]);sql="varbinary";length=c.MaxLength;break;
            case "image":clr=typeof(byte[]);sql="image";break;
            case "timestamp":clr=typeof(byte[]);sql="timestamp";length=8;break;
            case "char":clr=typeof(string);sql="char";length=c.MaxLength;break;
            case "varchar":clr=typeof(string);sql="varchar";length=c.MaxLength;break;
            case "text":clr=typeof(string);sql="text";break;
            case "nchar":clr=typeof(string);sql="nchar";length=Chars(c.MaxLength);break;
            case "nvarchar":clr=typeof(string);sql="nvarchar";length=Chars(c.MaxLength);break;
            case "ntext":clr=typeof(string);sql="ntext";break;
            case "sysname":clr=typeof(string);sql="nvarchar";length=128;break;
            case "xml":clr=typeof(string);sql="xml";break;
            case "sql_variant":clr=typeof(object);sql="sql_variant";break;
            default:
                // json など、この層が送れない型は文字列として渡す。
                clr=typeof(string);sql="nvarchar";length=-1;break;
        }
        return new(c.Name,clr,sql,c.IsNullable,false,ordinal,length,precision,scale);
    }

    private static object? Typical(SqlServerSystemColumn column,Type clr){
        if(column.Typical is null){
            return null;
        }
        if(clr==typeof(bool)){
            return column.Typical=="1";
        }
        return global::System.Convert.ChangeType(column.Typical,clr,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int Chars(short bytes){
        if(bytes<0){
            return -1;
        }
        return bytes/2;
    }

    private static object Default(Type type){
        if(type==typeof(bool)){return false;}
        if(type==typeof(byte)){return (byte)0;}
        if(type==typeof(short)){return (short)0;}
        if(type==typeof(int)){return 0;}
        if(type==typeof(long)){return 0L;}
        if(type==typeof(float)){return 0f;}
        if(type==typeof(double)){return 0d;}
        if(type==typeof(decimal)){return 0m;}
        if(type==typeof(string)){return "";}
        if(type==typeof(byte[])){return Array.Empty<byte>();}
        if(type==typeof(Guid)){return Guid.Empty;}
        if(type==typeof(DateTime)){return DefaultDate;}
        if(type==typeof(DateOnly)){return DateOnly.FromDateTime(DefaultDate);}
        if(type==typeof(TimeOnly)){return TimeOnly.MinValue;}
        if(type==typeof(DateTimeOffset)){return new DateTimeOffset(DefaultDate,TimeSpan.Zero);}
        throw new NotSupportedException($"No default value for '{type}'.");
    }

    // Seamless 側の値の型が本物の列の型と違うとき(bool と byte など)の変換。変換できない値は持たないものとして扱う。
    private static object? Convert(object? value,Type target){
        if(value is null||value is DBNull){return null;}
        if(target.IsInstanceOfType(value)){return value;}
        if(target==typeof(string)){return global::System.Convert.ToString(value,global::System.Globalization.CultureInfo.InvariantCulture);}
        if(value is DateTime date&&target==typeof(DateOnly)){return DateOnly.FromDateTime(date);}
        if(value is IConvertible&&(target.IsPrimitive||target==typeof(decimal))){
            try{
                return global::System.Convert.ChangeType(value,target,global::System.Globalization.CultureInfo.InvariantCulture);
            }catch(Exception e)when(e is FormatException or InvalidCastException or OverflowException){
                return null;
            }
        }
        return null;
    }
}
