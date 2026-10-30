using System.Text.Json;
using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]public void ExistingViewDefinitions(){
        var catalog=new CapturedCatalog();var engine=new Seamless.Database.Query.SqlQueryEngine(catalog);var failures=new List<string>();
        Assert.Equal(171,catalog.Views.Count);
        foreach(var view in catalog.Views){try{var plan=engine.Prepare("SELECT * FROM ["+view.Database+"].["+view.Schema+"].["+view.Name+"]",view.Database);Assert.NotEmpty(plan.Columns);}catch(Exception exception){failures.Add(view+": "+exception.Message);}}
        Assert.True(failures.Count==0,string.Join(Environment.NewLine,failures));
    }
    private sealed class CapturedCatalog:ISqlCatalog{
        private readonly Dictionary<string,SqlTable>tables=new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,SqlRoutine>routines=new(StringComparer.OrdinalIgnoreCase);
        public long Generation=>1;
        public List<SqlObjectName>Views{get;}=[];
        internal CapturedCatalog(){
            using var definitions=Read("catalog.json");
            var views=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in definitions.RootElement.EnumerateArray()){var name=ObjectName(item);var definition=item.GetProperty("definition").GetString()!;if(item.GetProperty("type").GetString()!.Trim()=="V"){views.Add(Key(name),definition);this.Views.Add(name);}else{this.routines.Add(Key(name),new(name,definition));}}
            using var columns=Read("columns.json");
            foreach(var item in columns.RootElement.EnumerateArray()){var name=ObjectName(item);var metadata=new List<SqlColumn>();foreach(var column in item.GetProperty("columns").EnumerateArray()){var typeName=column.GetProperty("typeName").GetString()!;var length=column.GetProperty("length").GetInt32();if(typeName is "nvarchar" or "nchar"&&length>0){length/=2;}var collation=column.TryGetProperty("collation",out var value)?value.GetString():null;var type=new SqlType(typeName,ClrType(typeName),length,column.GetProperty("precision").GetByte(),column.GetProperty("scale").GetByte(),collation);metadata.Add(new(column.GetProperty("name").GetString()!,type,column.GetProperty("nullable").GetBoolean(),name,column.GetProperty("ordinal").GetInt32()-1));}views.TryGetValue(Key(name),out var definition);this.tables.Add(Key(name),new(name,metadata,_=>[],definition));}
        }
        public SqlTable?FindTable(SqlObjectName name){this.tables.TryGetValue(Key(name),out var table);return table;}
        public SqlRoutine?FindRoutine(SqlObjectName name){this.routines.TryGetValue(Key(name),out var routine);return routine;}
        private static JsonDocument Read(string name){using var stream=typeof(SqlQueryEngine).Assembly.GetManifestResourceStream("Database.Query.Tests.Fixtures."+name)!;return JsonDocument.Parse(stream);}
        private static string Key(SqlObjectName name)=>name.Database+"."+name.Schema+"."+name.Name;
        private static SqlObjectName ObjectName(JsonElement value)=>new(value.GetProperty("database").GetString()!,value.GetProperty("schema").GetString()!,value.GetProperty("name").GetString()!);
        private static Type ClrType(string name)=>name switch{"bit"=>typeof(bool),"tinyint"=>typeof(byte),"smallint"=>typeof(short),"int"=>typeof(int),"bigint"=>typeof(long),"decimal" or "numeric" or "money" or "smallmoney"=>typeof(decimal),"float"=>typeof(double),"real"=>typeof(float),"date"=>typeof(DateOnly),"datetime" or "datetime2" or "smalldatetime"=>typeof(DateTime),"time"=>typeof(TimeOnly),"datetimeoffset"=>typeof(DateTimeOffset),"uniqueidentifier"=>typeof(Guid),"binary" or "varbinary" or "image" or "timestamp"=>typeof(byte[]),_=>typeof(string)};
    }
}
