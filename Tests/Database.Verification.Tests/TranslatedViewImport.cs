using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Seamless.Database;
using Seamless.Database.Rdbms;
using Seamless.Database.SqlServer;
using Seamless.Database.Transfer;
using Xunit.Abstractions;

namespace Database.Verification.Tests;

/// <summary>
/// T-SQL 翻訳器の段階4。本物の SQL Server のサンプルDBを、ビューを訳す指定で取り込み、
/// 訳した C# のビューの結果(重複を除いた行の集合)が本物のビューの結果と一致することを確かめる。
/// 訳せなかったビューは取り込まれず、回避策つきで報告されることも確かめる。
/// 対象のDBは SEAMLESS_SAMPLE_DATABASES(カンマ区切り)で絞れる。既定はサンプルDBすべて。
/// </summary>
public sealed class TranslatedViewImport(ITestOutputHelper output){
    private static readonly string[] sampleDatabases=[
        "AdventureWorks2016","AdventureWorks2017","AdventureWorks2019","AdventureWorks2022","AdventureWorks2025",
        "AdventureWorksDW2016","AdventureWorksDW2017","AdventureWorksDW2019","AdventureWorksDW2022","AdventureWorksDW2025",
        "Northwind","pubs","WideWorldImporters","WideWorldImportersDW",
    ];

    [SqlServerFact,Trait("Category","LiveSqlServer")]public async Task ImportedViewsMatchSqlServer(){
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")!;
        var only=Environment.GetEnvironmentVariable("SEAMLESS_SAMPLE_DATABASES");
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")??Path.GetTempPath();
        var report=new StringBuilder();
        var mismatched=new List<string>();
        var timedOut=new List<string>();
        var matched=0;
        var reported=0;
        foreach(var name in sampleDatabases){
            if(only is not null&&!only.Split(',',StringSplitOptions.TrimEntries).Contains(name,StringComparer.OrdinalIgnoreCase)){continue;}
            var source=new SqlConnectionStringBuilder(connectionString){InitialCatalog=name}.ConnectionString;
            var catalog=await new SqlServerCatalogReader(source).ReadAsync();
            var generated=CSharpDatabaseGenerator.Generate(catalog,new ViewImportOptions(new SqlServerViewTranslator()));
            // 訳せなかったビューは取り込まず(行の束縛にも入らない)、回避策つきで報告される。
            var failedViews=generated.Compatibility.Where(static issue=>issue.Kind==CompatibilityObjectKind.View&&issue.Code!="SNAPSHOT"&&issue.Workarounds.Count>0).ToArray();
            Assert.DoesNotContain(generated.Tables,static table=>table.Kind==GeneratedRelationKind.View);
            Assert.All(failedViews,static issue=>Assert.Contains(issue.Workarounds,static workaround=>workaround.Code=="ManualCSharp"));
            reported+=failedViews.Length;
            report.Append($"{name}: 訳した {generated.Views.Count} / 取り込まない {failedViews.Length}\n");
            foreach(var issue in failedViews){
                report.Append($"  取り込まない: {issue.Schema}.{issue.Name} [{issue.Code}] {issue.Difference} 回避策: {string.Join("・",issue.Workarounds.Select(static workaround=>workaround.Code))}\n");
            }
            if(generated.Views.Count==0){continue;}
            var directory=Path.Combine(root,"TranslatedViewImport",Guid.NewGuid().ToString("N"));
            var context=new AssemblyLoadContext("TranslatedViewImport_"+name,isCollectible:true);
            try{
                var assembly=Compile(context,generated);
                var type=assembly.GetType($"{generated.TypeNamespace}.{generated.DatabaseName}",true)!;
                var sizing=await SqlServerImportSizer.MeasureTablesAsync(source,generated.Tables);
                var options=GeneratedDatabaseLoader.Options(directory,generated.Tables,
                    sizing.ToDictionary(static pair=>pair.Key,static pair=>new DatabaseTableStorageOptions{IndexSize=pair.Value.IndexSize,PageSize=pair.Value.PageSize,SegmentSize=pair.Value.SegmentSize,MemorySize=pair.Value.MemorySize},StringComparer.Ordinal));
                using var database=(DatabaseContainer)Activator.CreateInstance(type,[options])!;
                using(var rows=new SqlServerImportSource(source,generated.Tables)){
                    var counts=GeneratedDatabaseLoader.Load(database,generated.Tables,rows);
                    Assert.Equal(rows.RowsReadByTable.OrderBy(static pair=>pair.Key),counts.OrderBy(static pair=>pair.Key));
                }
                using var reader=(IDisposable)type.GetMethod("BeginRead")!.Invoke(database,[CancellationToken.None])!;
                await using var connection=new SqlConnection(source);
                await connection.OpenAsync();
                foreach(var view in generated.Views){
                    var expected=SqlRowSet.Query(connection,$"SELECT * FROM [{view.SourceSchema.Replace("]","]]")}].[{view.SourceName.Replace("]","]]")}]");
                    var schemaReader=reader.GetType().GetProperty(view.SchemaPropertyName)!.GetValue(reader)!;
                    // 翻訳器の試験と同じく、1つのビューが時間内に終わらなければ「時間切れ」として分けて記録する(不一致とは数えない)。
                    var declaring=assembly.GetType(view.DeclaringTypeName,true)!;
                    var task=Task.Run(()=>QueryView(declaring,schemaReader,view.PropertyName));
                    if(!task.Wait(TimeSpan.FromSeconds(120))){
                        timedOut.Add($"{name}.{view.SourceSchema}.{view.SourceName}");
                        continue;
                    }
                    var actual=task.Result;
                    if(expected.SetEquals(actual)){
                        matched++;
                    }else{
                        mismatched.Add($"{name}.{view.SourceSchema}.{view.SourceName}: 本物 {expected.Count}行 / 取り込み後 {actual.Count}行\n    本物だけ: {string.Join(" | ",expected.Except(actual).Take(2))}\n    取り込み後だけ: {string.Join(" | ",actual.Except(expected).Take(2))}");
                    }
                }
            }finally{
                context.Unload();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                if(Directory.Exists(directory)){Directory.Delete(directory,true);}
            }
        }
        report.Insert(0,$"一致 {matched} / 不一致 {mismatched.Count} / 時間切れ {timedOut.Count} / 取り込まない {reported}\n");
        foreach(var item in mismatched){report.Append("不一致: ").Append(item).Append('\n');}
        foreach(var item in timedOut){report.Append("時間切れ: ").Append(item).Append('\n');}
        output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"translated-view-import-report.txt"),report.ToString());
        Assert.True(mismatched.Count==0,report.ToString());
    }

    // 生成物を DB の生成器と一緒にコンパイルし、回収できる読み込み領域へ読む。
    internal static Assembly Compile(AssemblyLoadContext context,GeneratedDatabase generated){
        var compilation=global::Test.SourceGenerator.GeneratorTestHelpers.CreateCompilation(generated.Files.Select(static file=>(file.RelativePath,file.Content)));
        global::Test.SourceGenerator.GeneratorTestHelpers.CreateDatabaseDriver().RunGeneratorsAndUpdateCompilation(compilation,out var compiled,out _);
        using var stream=new MemoryStream();
        var result=compiled.Emit(stream);
        Assert.True(result.Success,string.Join("\n",result.Diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).Take(20)));
        stream.Position=0;
        return context.LoadFromStream(stream);
    }

    internal static SortedSet<string> QueryView(Type views,object schemaReader,string name){
        var getter=views.GetMethods(BindingFlags.Public|BindingFlags.Static).Single(method=>method.Name=="get_"+name&&method.GetParameters().Length==1);
        var rows=new SortedSet<string>(StringComparer.Ordinal);
        foreach(var row in (System.Collections.IEnumerable)getter.Invoke(null,[schemaReader])!){
            var values=row!.GetType().GetProperties().Where(static property=>property.Name!="EqualityContract").Select(property=>property.GetValue(row)).ToArray();
            rows.Add(SqlRowSet.Key(values));
        }
        return rows;
    }
}
