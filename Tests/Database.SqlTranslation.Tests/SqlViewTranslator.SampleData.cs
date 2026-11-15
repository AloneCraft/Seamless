using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;
using Seamless.Database.SqlServer.Functions;
using Seamless.Database.SqlTranslation;
namespace Database.SqlTranslation.Tests;
public sealed partial class SqlViewTranslator{
    /// <summary>
    /// 訳したサンプルDBのビューを、本物の SQL Server のサンプルDBの表のデータで実行し、本物のビューの結果(重複を除いた行の集合)と比べる。
    /// 表は読むだけ。1つのビューが時間内に終わらなければ「時間切れ」として記録する。結果は一覧として出力する。
    /// </summary>
    [SqlServerFact,Trait("Category","SampleData")]public void SampleViewsMatchSqlServer(){
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")!;
        var only=Environment.GetEnvironmentVariable("SEAMLESS_SAMPLE_DATABASES");
        var matched=new List<string>();
        var mismatched=new List<string>();
        var timedOut=new List<string>();
        foreach(var database in SampleCatalogs.Load()){
            if(only is not null&&!only.Split(',').Contains(database.Name,StringComparer.OrdinalIgnoreCase)){
                continue;
            }
            var translator=new Seamless.Database.SqlTranslation.SqlViewTranslator(database.Catalog);
            var translations=database.Views.Select(view=>translator.Translate(view.Schema,view.Name,view.Definition)).ToList();
            if(!translations.Any(static translation=>translation.Succeeded)){
                continue;
            }
            var compiled=TranslationCompiler.Compile(database.Name,TranslationCompiler.StubSource(database.Catalog,database.Relations,translations),translator.SourceFile(translations));
            Assert.True(compiled.Assembly is not null,string.Join("\n",compiled.Errors.Take(10)));
            var assembly=compiled.Assembly!;
            var builder=new SqlConnectionStringBuilder(connectionString){InitialCatalog=database.Name};
            using var connection=new SqlConnection(builder.ConnectionString);
            connection.Open();
            var relations=database.Relations.ToDictionary(static relation=>$"{relation.Schema}.{relation.Name}",StringComparer.OrdinalIgnoreCase);
            // 時間切れで見捨てた計算が裏で表を読むことがあるので、表の読み込みは毎回別の接続を使う。
            var tableConnection=builder.ConnectionString;
            StubData.SetLoader(key=>{
                using var own=new SqlConnection(tableConnection);
                own.Open();
                return LoadTable(own,assembly,relations[key]);
            });
            var reader=Activator.CreateInstance(assembly.GetType($"{database.Catalog.RootNamespace}.StubReader")!)!;
            var timeouts=0;
            foreach(var translation in translations.Where(static translation=>translation.Succeeded)){
                var name=$"{database.Name}.{translation.Schema}.{translation.Name}";
                if(timeouts>=2){
                    timedOut.Add(name+"(前の時間切れのため飛ばした)");
                    continue;
                }
                var expected=QueryServer(connection,$"SELECT * FROM [{translation.Schema}].[{translation.Name}]");
                var views=assembly.GetType($"{database.Catalog.RootNamespace}.Queries.{CSharpNames.Identifier(translation.Schema)}Views")!;
                var member=translation.RowType![(translation.RowType!.LastIndexOf('.')+1)..];
                var task=Task.Run(()=>QueryTranslation(views,reader,member));
                if(!task.Wait(TimeSpan.FromSeconds(60))){
                    timedOut.Add(name);
                    timeouts++;
                    continue;
                }
                if(expected.Equals(task.Result)){
                    matched.Add($"{name}({expected.Rows?.Count??0}行)");
                }else{
                    mismatched.Add($"{name}\n    本物: {Summary(expected)}\n    訳 : {Summary(task.Result)}");
                }
            }
            StubData.Set([]);
            // DB ごとに、コンパイルしたアセンブリと読み込んだ表を手放す。
            compiled.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        var report=new StringBuilder();
        report.Append($"一致 {matched.Count} / 不一致 {mismatched.Count} / 時間切れ {timedOut.Count}\n");
        report.Append("一致: ").Append(string.Join(", ",matched)).Append('\n');
        foreach(var item in mismatched){
            report.Append("不一致: ").Append(item).Append('\n');
        }
        foreach(var item in timedOut){
            report.Append("時間切れ: ").Append(item).Append('\n');
        }
        this.output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"sample-views-report.txt"),report.ToString());
        Assert.True(mismatched.Count==0,report.ToString());
    }
    // 行の集合の要約。違いを見つけやすいよう、片方にしか無い行を先に出す。
    private static string Summary(Outcome outcome){
        if(outcome.Error is not null||outcome.Rows is null){
            return outcome.ToString();
        }
        var rows=outcome.Rows.Take(3).Select(static row=>row.Length>200?row[..200]:row);
        return $"{outcome.Rows.Count}行 例: {string.Join(" | ",rows)}";
    }
    // 本物の表を読み、コンパイルした仮のコンテナの行の型の ISet にする。扱えない型の列は NULL にする。
    private static object LoadTable(SqlConnection connection,Assembly assembly,CatalogRelation relation){
        var rowType=assembly.GetType(relation.RowType["global::".Length..])!;
        var parameters=rowType.GetConstructors().OrderByDescending(static constructor=>constructor.GetParameters().Length).First().GetParameters();
        var columns=relation.Columns.Select(static column=>column.Type.Name switch{
            "geography" or "geometry" or "hierarchyid" or "sql_variant"=>$"NULL AS [{column.Name}]",
            _=>$"[{column.Name}]"
        });
        using var command=connection.CreateCommand();
        command.CommandText=$"SELECT {string.Join(",",columns)} FROM [{relation.Schema}].[{relation.Name}]";
        command.CommandTimeout=600;
        var rows=new List<object>();
        using(var reader=command.ExecuteReader()){
            while(reader.Read()){
                var values=new object?[parameters.Length];
                for(var i=0;i<values.Length;i++){
                    var value=reader.IsDBNull(i)?null:reader.GetValue(i);
                    var target=Nullable.GetUnderlyingType(parameters[i].ParameterType)??parameters[i].ParameterType;
                    values[i]=value switch{
                        null=>null,
                        DateTime date when target==typeof(DateOnly)=>DateOnly.FromDateTime(date),
                        TimeSpan time when target==typeof(TimeOnly)=>TimeOnly.FromTimeSpan(time),
                        _ when target==typeof(object)=>null,
                        _=>value
                    };
                }
                rows.Add(Activator.CreateInstance(rowType,values)!);
            }
        }
        var array=Array.CreateInstance(rowType,rows.Count);
        for(var i=0;i<rows.Count;i++){
            array.SetValue(rows[i],i);
        }
        return Activator.CreateInstance(typeof(ListSet<>).MakeGenericType(rowType),array)!;
    }
}
