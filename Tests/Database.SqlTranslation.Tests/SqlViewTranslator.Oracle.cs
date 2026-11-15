using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;
using Seamless.Database.SqlServer.Functions;
using Seamless.Database.SqlTranslation;
namespace Database.SqlTranslation.Tests;
/// <summary>本物の SQL Server が無いときは飛ばす。接続文字列は環境変数 SEAMLESS_SQLSERVER_TEST_CONNECTION から読む。</summary>
public sealed class SqlServerFactAttribute:FactAttribute{
    public SqlServerFactAttribute(){
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION"))){
            this.Skip="SEAMLESS_SQLSERVER_TEST_CONNECTION が未設定のため、本物の SQL Server との照合は未検証です。";
        }
    }
}
public sealed partial class SqlViewTranslator{
    private sealed record TestColumn(string Name,string SqlType,bool Nullable,SqlTypeDescriptor Type);
    private sealed record TestTable(string Name,IReadOnlyList<TestColumn> Columns,IReadOnlyList<object?[]> Rows);
    private const string Latin="SQL_Latin1_General_CP1_CI_AS";
    private static readonly TestTable customer=new("Customer",[
        new("Id","int",false,SqlTypeDescriptor.Of("int")),
        new("Name","nvarchar(50)",true,SqlTypeDescriptor.Of("nvarchar",50,null,null,Latin)),
        new("City","nvarchar(30)",true,SqlTypeDescriptor.Of("nvarchar",30,null,null,Latin)),
        new("Credit","decimal(10,2)",true,SqlTypeDescriptor.Of("decimal",null,10,2)),
        new("Joined","date",true,SqlTypeDescriptor.Of("date")),
        new("Rating","tinyint",true,SqlTypeDescriptor.Of("tinyint")),
    ],[
        [1,"Alice","Tokyo",1000.50m,new DateTime(2020,1,15),(byte)5],
        [2,"bob","Osaka",null,new DateTime(2021,6,30),(byte)3],
        [3,"Carol ",null,250.00m,null,null],
        [4,null,"tokyo",0.00m,new DateTime(2019,12,31),(byte)4],
        [5,"Dave","Kyoto",99999999.99m,new DateTime(2022,2,28),(byte)1],
    ]);
    private static readonly TestTable orders=new("Orders",[
        new("OrderId","int",false,SqlTypeDescriptor.Of("int")),
        new("CustomerId","int",true,SqlTypeDescriptor.Of("int")),
        new("Amount","money",false,SqlTypeDescriptor.Of("money",null,19,4)),
        new("Qty","smallint",false,SqlTypeDescriptor.Of("smallint")),
        new("OrderDate","datetime",false,SqlTypeDescriptor.Of("datetime")),
        new("Note","varchar(20)",true,SqlTypeDescriptor.Of("varchar",20,null,null,Latin)),
    ],[
        [1,1,100.00m,(short)2,new DateTime(2023,1,1,10,0,0),"first"],
        [2,1,250.25m,(short)1,new DateTime(2023,2,1),null],
        [3,2,99.99m,(short)5,new DateTime(2023,2,15,23,59,59,997),"rush"],
        [4,null,10.00m,(short)1,new DateTime(2023,3,1),null],
        [5,3,0.00m,(short)0,new DateTime(2023,3,2),"free"],
        [6,1,100.00m,(short)2,new DateTime(2023,1,1,10,0,0),"first"],
    ]);
    // {表} は SQL Server では一時表(#表)、翻訳では dbo の表になる。
    private static readonly string[] queries=[
        "SELECT Id, Name FROM {Customer} WHERE City = 'tokyo'",
        "SELECT Id FROM {Customer} WHERE Name = 'Carol'",
        "SELECT Id FROM {Customer} WHERE NOT (Credit > 100)",
        "SELECT Id FROM {Customer} WHERE Credit > 100 OR City IS NULL",
        "SELECT Id, Name FROM {Customer} WHERE Name LIKE '%a%'",
        "SELECT Id FROM {Customer} WHERE Rating IN (1, 3, NULL)",
        "SELECT Id FROM {Customer} WHERE Rating NOT IN (1, NULL)",
        "SELECT Id FROM {Customer} WHERE Joined BETWEEN '2020-01-01' AND '2021-12-31'",
        "SELECT Id, CASE WHEN Credit IS NULL THEN 'none' WHEN Credit > 500 THEN 'high' ELSE 'low' END AS Level FROM {Customer}",
        "SELECT Id, COALESCE(City, Name, 'unknown') AS Place, ISNULL(Rating, 0) AS R, NULLIF(City, 'Osaka') AS C FROM {Customer}",
        "SELECT OrderId, Amount * Qty AS Total, Amount / 3 AS Third, Qty / 2 AS Half, Qty % 2 AS Odd FROM {Orders}",
        "SELECT Id, Credit / 3 AS D, Credit * 1.5 AS M, Credit + Rating AS P, -Credit AS N FROM {Customer}",
        "SELECT Id, Name + ' (' + City + ')' AS Label FROM {Customer}",
        "SELECT Id, CAST(Id AS varchar(10)) + 'x' AS S, CONVERT(varchar(20), Joined, 112) AS J, CAST(Credit AS int) AS CI, CAST(Rating AS decimal(5,2)) AS RD FROM {Customer}",
        "SELECT OrderId, CONVERT(varchar(30), OrderDate, 121) AS D, CONVERT(varchar(30), Amount) AS A, DATEADD(day, 1, OrderDate) AS N, DATEDIFF(month, OrderDate, '2023-12-31') AS M, YEAR(OrderDate) AS Y FROM {Orders}",
        "SELECT Id, LEFT(Name, 2) AS L, UPPER(Name) AS U, LEN(Name) AS N, REPLACE(Name, 'A', 'x') AS R FROM {Customer}",
        "SELECT c.Id, o.OrderId FROM {Customer} AS c INNER JOIN {Orders} AS o ON o.CustomerId = c.Id",
        "SELECT c.Id, o.OrderId, o.Amount FROM {Customer} c LEFT JOIN {Orders} o ON o.CustomerId = c.Id AND o.Amount > 50",
        "SELECT c.Id, o.OrderId FROM {Orders} o RIGHT JOIN {Customer} c ON o.CustomerId = c.Id",
        "SELECT c.Id, o.OrderId FROM {Customer} c, {Orders} o WHERE o.CustomerId = c.Id AND o.Qty > 1",
        "SELECT CustomerId, COUNT(*) AS N, SUM(Amount) AS S, AVG(Qty) AS A, MIN(OrderDate) AS F, MAX(Note) AS MN FROM {Orders} GROUP BY CustomerId",
        "SELECT CustomerId, COUNT(DISTINCT Amount) AS D FROM {Orders} GROUP BY CustomerId HAVING COUNT(*) > 1",
        "SELECT UPPER(City) AS C, COUNT(*) AS N, SUM(Credit) AS S, AVG(Credit) AS A FROM {Customer} GROUP BY UPPER(City)",
        "SELECT Id FROM {Customer} c WHERE EXISTS (SELECT 1 FROM {Orders} o WHERE o.CustomerId = c.Id)",
        "SELECT Id FROM {Customer} c WHERE NOT EXISTS (SELECT * FROM {Orders} o WHERE o.CustomerId = c.Id)",
        "SELECT Id FROM {Customer} WHERE Id IN (SELECT CustomerId FROM {Orders})",
        "SELECT Id FROM {Customer} WHERE Id NOT IN (SELECT CustomerId FROM {Orders})",
        "SELECT Id, (SELECT MAX(Amount) FROM {Orders} o WHERE o.CustomerId = c.Id) AS MaxAmount, (SELECT COUNT(*) FROM {Orders} o WHERE o.CustomerId = c.Id) AS Cnt FROM {Customer} c",
        "SELECT Id, (SELECT o.Note FROM {Orders} o WHERE o.OrderId = c.Id) AS Note FROM {Customer} c",
        "SELECT Id, (SELECT o.OrderId FROM {Orders} o WHERE o.CustomerId = c.Id) AS Only FROM {Customer} c",
        "SELECT Id FROM {Customer} WHERE City = 'Tokyo' UNION SELECT CustomerId FROM {Orders} WHERE Qty > 1",
        "SELECT Id FROM {Customer} EXCEPT SELECT CustomerId FROM {Orders}",
        "SELECT Id FROM {Customer} INTERSECT SELECT CustomerId FROM {Orders}",
        "SELECT DISTINCT CustomerId, Amount FROM {Orders}",
        "SELECT OrderId, Amount / (Qty - Qty) AS X FROM {Orders}",
        "SELECT Id, CAST(Credit AS decimal(5,1)) AS X FROM {Customer}",
        "SELECT Id, CAST(Name AS char(8)) AS Fixed, CONVERT(char(2), Id * 100) AS Star FROM {Customer}",
        "SELECT o.OrderId, c.Name, c2.Name AS Other FROM {Orders} o LEFT JOIN {Customer} c ON c.Id = o.CustomerId LEFT JOIN {Customer} c2 ON c2.Id = o.Qty",
        "SELECT Id, DATENAME(month, Joined) AS M, DATEPART(week, Joined) AS W, EOMONTH(Joined) AS E FROM {Customer}",
        "SELECT * FROM {Customer} WHERE Credit IS NOT NULL",
        "SELECT o.CustomerId, SUM(o.Amount * o.Qty) AS Total FROM {Orders} o INNER JOIN {Customer} c ON c.Id = o.CustomerId WHERE c.City IS NOT NULL GROUP BY o.CustomerId",
        // GROUP BY の無い集約(表が空でも1行)。
        "SELECT COUNT(*) AS N, MAX(Credit) AS M, AVG(Credit) AS A, SUM(Rating) AS R FROM {Customer}",
        "SELECT COUNT(*) AS N, SUM(Amount) AS S, MIN(Note) AS F FROM {Orders} WHERE 1 = 0",
        "SELECT COUNT(DISTINCT CustomerId) AS C FROM {Orders} HAVING COUNT(*) > 3",
        // 左外部結合: 文字列のキー(照合順序で等しいもの)、複数のキー、= で結べない条件(総当たり)。
        "SELECT c.Id, o.OrderId FROM {Customer} c LEFT JOIN {Orders} o ON o.Note = c.City",
        "SELECT c.Id, c2.Id AS Other FROM {Customer} c LEFT JOIN {Customer} c2 ON c2.City = c.City AND c2.Rating = c.Rating",
        "SELECT c.Id, c2.Id AS Other FROM {Customer} c LEFT JOIN {Customer} c2 ON c2.City = c.City AND c2.Id <> c.Id",
        "SELECT c.Id, o.OrderId FROM {Customer} c LEFT JOIN {Orders} o ON o.Amount > c.Credit",
        // 派生表と PIVOT。
        "SELECT x.CustomerId, x.Total, c.Name FROM (SELECT CustomerId, SUM(Amount) AS Total FROM {Orders} GROUP BY CustomerId) AS x INNER JOIN {Customer} c ON c.Id = x.CustomerId",
        "SELECT t.N FROM (SELECT COUNT(*) AS N FROM {Orders}) t",
        "SELECT d.Id, d.Doubled FROM (SELECT Id, Rating * 2 AS Doubled FROM {Customer}) AS d (Id, Doubled) WHERE d.Doubled > 4",
        "SELECT pvt.CustomerId, pvt.[1], pvt.[2], pvt.[5] FROM (SELECT CustomerId, Qty, Amount FROM {Orders}) AS src PIVOT (SUM(Amount) FOR Qty IN ([1], [2], [5])) AS pvt",
        "SELECT pvt.* FROM (SELECT Note, Qty FROM {Orders}) AS src PIVOT (COUNT(Qty) FOR Note IN ([first], [rush], [none])) AS pvt",
    ];
    /// <summary>
    /// 同じデータを本物の SQL Server(一時表)と、訳してコンパイルした C# の両方で問い合わせ、結果の行の集合とエラー番号が一致することを確かめる。
    /// ビューは ISet を返すので、本物の結果も重複を除いて比べる。
    /// </summary>
    [SqlServerFact,Trait("Category","SqlServer")]public void MatchesSqlServer(){
        var tables=new[]{customer,orders};
        var relations=tables.Select(table=>new CatalogRelation("dbo",table.Name,$"Database.dbo.{table.Name}",$"global::Oracle.Tables.dbo.{table.Name}",table.Columns.Select(column=>new CatalogColumn(column.Name,column.Name,column.Type,column.Nullable)).ToList())).ToList();
        var catalog=new TranslationCatalog("Oracle","global::Oracle.Container","Japanese_CI_AS",relations);
        var translator=new Seamless.Database.SqlTranslation.SqlViewTranslator(catalog);
        var translations=queries.Select((query,index)=>translator.Translate("dbo",$"Q{index}",query.Replace("{","",StringComparison.Ordinal).Replace("}","",StringComparison.Ordinal))).ToList();
        var failures=new List<string>();
        foreach(var (translation,index) in translations.Select((translation,index)=>(translation,index)).Where(static pair=>!pair.translation.Succeeded)){
            failures.Add($"Q{index} 訳せない: {queries[index]}\n    {string.Join("; ",translation.Diagnostics)}");
        }
        var compiled=TranslationCompiler.Compile("Oracle",TranslationCompiler.StubSource(catalog,relations,translations),translator.SourceFile(translations));
        Assert.True(compiled.Assembly is not null,"コンパイルできない:\n"+string.Join("\n",compiled.Errors.Take(20)));
        var assembly=compiled.Assembly!;
        StubData.Set(tables.ToDictionary(table=>$"dbo.{table.Name}",table=>Rows(assembly,table)));
        var reader=Activator.CreateInstance(assembly.GetType("Oracle.StubReader")!)!;
        var views=assembly.GetType("Oracle.Queries.dboViews")!;
        using var connection=new SqlConnection(Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION"));
        connection.Open();
        Execute(connection,"SET LANGUAGE us_english; SET DATEFIRST 7; SET DATEFORMAT mdy;");
        foreach(var table in tables){
            CreateTemporaryTable(connection,table);
        }
        for(var index=0;index<queries.Length;index++){
            if(!translations[index].Succeeded){
                continue;
            }
            var expected=QueryServer(connection,queries[index].Replace("{","#",StringComparison.Ordinal).Replace("}","",StringComparison.Ordinal));
            var actual=QueryTranslation(views,reader,$"Q{index}");
            if(!expected.Equals(actual)){
                failures.Add($"Q{index}: {queries[index]}\n    本物: {expected}\n    訳 : {actual}\n    C#: {translations[index].MemberSource}");
            }
        }
        Assert.True(failures.Count==0,$"{failures.Count}/{queries.Length} 件が本物と違う:\n"+string.Join("\n",failures));
    }
    private sealed record Outcome(SortedSet<string>? Rows,int? Error){
        public override string ToString(){
            if(this.Error is{} number){
                return "エラー "+number.ToString(CultureInfo.InvariantCulture);
            }
            return "{"+string.Join(" | ",this.Rows!)+"}";
        }
        public bool Equals(Outcome? other)=>other is not null&&this.Error==other.Error&&(this.Rows is null&&other.Rows is null||this.Rows is not null&&other.Rows is not null&&this.Rows.SetEquals(other.Rows));
        public override int GetHashCode()=>0;
    }
    private static Outcome QueryServer(SqlConnection connection,string sql){
        using var command=connection.CreateCommand();
        command.CommandText=sql;
        try{
            using var reader=command.ExecuteReader();
            var rows=new SortedSet<string>(StringComparer.Ordinal);
            while(reader.Read()){
                var values=new object?[reader.FieldCount];
                for(var i=0;i<values.Length;i++){
                    values[i]=reader.IsDBNull(i)?null:reader.GetValue(i);
                }
                rows.Add(Key(values));
            }
            return new(rows,null);
        }catch(SqlException error){
            return new(null,error.Number);
        }
    }
    private static Outcome QueryTranslation(Type views,object reader,string name){
        var getter=views.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(method=>method.Name=="get_"+name&&method.GetParameters().Length==1);
        try{
            var set=(System.Collections.IEnumerable)getter.Invoke(null,[reader])!;
            var rows=new SortedSet<string>(StringComparer.Ordinal);
            foreach(var row in set){
                var values=row!.GetType().GetProperties().Where(static property=>property.Name!="EqualityContract").Select(property=>property.GetValue(row)).ToArray();
                rows.Add(Key(values));
            }
            return new(rows,null);
        }catch(Exception error)when(Unwrap(error) is SqlFunctionException function){
            return new(null,function.Number);
        }
    }
    private static Exception Unwrap(Exception error){
        while(error is TargetInvocationException{InnerException:{} inner}){
            error=inner;
        }
        return error;
    }
    // 行を、型の違い(DateOnly と DateTime、float と double、decimal の桁の0)を吸収した文字列にする。
    private static string Key(object?[] values)=>string.Join(",",values.Select(static value=>value switch{
        null=>"∅",
        DateOnly date=>date.ToDateTime(TimeOnly.MinValue).ToString("o",CultureInfo.InvariantCulture),
        DateTime date=>date.ToString("o",CultureInfo.InvariantCulture),
        decimal number=>(number/1.000000000000000000000000000000000m).ToString(CultureInfo.InvariantCulture),
        double number=>number.ToString("R",CultureInfo.InvariantCulture),
        float number=>((double)number).ToString("R",CultureInfo.InvariantCulture),
        byte or short or int or long=>System.Convert.ToInt64(value,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        bool flag=>flag?"1":"0",
        string text=>"'"+text+"'",
        TimeOnly time=>time.ToTimeSpan().ToString("c",CultureInfo.InvariantCulture),
        TimeSpan time=>time.ToString("c",CultureInfo.InvariantCulture),
        DateTimeOffset offset=>offset.ToString("o",CultureInfo.InvariantCulture),
        byte[] bytes=>"0x"+System.Convert.ToHexString(bytes),
        _=>value.ToString()!
    }));
    private static void Execute(SqlConnection connection,string sql){
        using var command=connection.CreateCommand();
        command.CommandText=sql;
        command.ExecuteNonQuery();
    }
    private static void CreateTemporaryTable(SqlConnection connection,TestTable table){
        var columns=table.Columns.Select(static column=>{
            var collation="";
            if(column.Type.Collation is{} name){
                collation=" COLLATE "+name;
            }
            var nullable=" NOT NULL";
            if(column.Nullable){
                nullable=" NULL";
            }
            return $"{column.Name} {column.SqlType}{collation}{nullable}";
        });
        Execute(connection,$"CREATE TABLE #{table.Name}({string.Join(",",columns)})");
        foreach(var row in table.Rows){
            using var command=connection.CreateCommand();
            command.CommandText=$"INSERT INTO #{table.Name} VALUES({string.Join(",",row.Select(static (_,i)=>"@p"+i.ToString(CultureInfo.InvariantCulture)))})";
            for(var i=0;i<row.Length;i++){
                command.Parameters.AddWithValue("@p"+i.ToString(CultureInfo.InvariantCulture),row[i]??DBNull.Value);
            }
            command.ExecuteNonQuery();
        }
    }
    // 試験のデータを、コンパイルした仮のコンテナの行の型の ISet にする。
    private static object Rows(Assembly assembly,TestTable table){
        var rowType=assembly.GetType($"Oracle.Tables.dbo.{table.Name}")!;
        var parameters=rowType.GetConstructors().Single(static constructor=>constructor.GetParameters().Length>1).GetParameters();
        var array=Array.CreateInstance(rowType,table.Rows.Count);
        for(var r=0;r<table.Rows.Count;r++){
            var values=table.Rows[r].Select((value,i)=>{
                if(value is DateTime date&&(Nullable.GetUnderlyingType(parameters[i].ParameterType)??parameters[i].ParameterType)==typeof(DateOnly)){
                    return (object)DateOnly.FromDateTime(date);
                }
                if(value is byte or short&&(Nullable.GetUnderlyingType(parameters[i].ParameterType)??parameters[i].ParameterType)==typeof(int)){
                    return System.Convert.ToInt32(value,CultureInfo.InvariantCulture);
                }
                return value;
            }).ToArray();
            array.SetValue(Activator.CreateInstance(rowType,values),r);
        }
        return Activator.CreateInstance(typeof(ListSet<>).MakeGenericType(rowType),array)!;
    }
}
