using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.Data.SqlClient;
using Seamless.Database.SqlTranslation;
namespace Database.SqlTranslation.Tests;
public sealed partial class SqlViewTranslator{
    /// <summary>
    /// 訳したサンプルDBのビューを FLinq で展開して実行し、本物の SQL Server のビューの結果と比べる。
    /// ビューごとに [LinqIntercept(true)] のメソッド(ビューを読んで ToArray する)を作り、FLinq がビューの本体を取り込んで展開する。
    /// 比べるのは重複を除いた行の集合と、結果に重複が無いこと(ビューは集合を返す)。
    /// 展開できなかったビュー(コンパイルエラー・FLinq の警告・実行時の展開できない例外)は、理由とともに別に数える。
    /// </summary>
    [SqlServerFact,Trait("Category","SampleData")]public void SampleViewsMatchSqlServerThroughFLinq(){
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")!;
        var only=Environment.GetEnvironmentVariable("SEAMLESS_SAMPLE_DATABASES");
        var matched=new List<string>();
        var mismatched=new List<string>();
        var notExpanded=new List<string>();
        var timedOut=new List<string>();
        foreach(var database in SampleCatalogs.Load()){
            if(only is not null&&!only.Split(',').Contains(database.Name,StringComparer.OrdinalIgnoreCase)){
                continue;
            }
            var translator=new Seamless.Database.SqlTranslation.SqlViewTranslator(database.Catalog);
            var translations=database.Views.Select(view=>translator.Translate(view.Schema,view.Name,view.Definition)).ToList();
            var succeeded=translations.Where(static translation=>translation.Succeeded).ToList();
            if(succeeded.Count==0){
                continue;
            }
            var stub=TranslationCompiler.StubSource(database.Catalog,database.Relations,translations);
            var views=translator.SourceFile(translations);
            // ビューごとのメソッド。コンパイルできないものは外して作り直す。
            var included=Enumerable.Range(0,succeeded.Count).ToHashSet();
            TranslationCompiler.FLinqResult? compiled=null;
            for(var attempt=0;attempt<10;attempt++){
                compiled=TranslationCompiler.CompileWithFLinq(database.Name+"_FLinq",Harness(database.Catalog,succeeded,included),stub,views);
                if(compiled.Compiled.Assembly is not null){
                    break;
                }
                var dropped=false;
                foreach(var (method,error) in compiled.ErrorMethods){
                    var index=int.Parse(method[1..],CultureInfo.InvariantCulture);
                    if(included.Remove(index)){
                        notExpanded.Add($"{database.Name}.{succeeded[index].Schema}.{succeeded[index].Name}: コンパイルエラー {Shorten(error)}");
                        dropped=true;
                    }
                }
                Assert.True(dropped,$"{database.Name}: どのビューのエラーか分からない\n"+string.Join("\n",compiled.Compiled.Errors.Take(10)));
            }
            var assembly=compiled!.Compiled.Assembly!;
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,$"flinq-{database.Name}.g.cs"),compiled.Generated);
            // 差し替えがビューの数だけ無ければ、展開されずに元の Set の実装で動いている(試験の空振り)。
            var interceptions=compiled.Generated.Split("InterceptsLocationAttribute(1,").Length-1;
            Assert.True(interceptions>=included.Count,$"{database.Name}: 差し替え {interceptions} 件、ビュー {included.Count} 個");
            var warned=compiled.GeneratorDiagnostics.Where(static diagnostic=>diagnostic.Id.StartsWith("FLINQ",StringComparison.Ordinal)&&diagnostic.Severity>=DiagnosticSeverity.Warning)
                .Select(static diagnostic=>(Method:Regex.Match(diagnostic.GetMessage(CultureInfo.InvariantCulture),@"'(V\d+)'").Groups[1].Value,Text:diagnostic.ToString()))
                .Where(static item=>item.Method.Length>0).GroupBy(static item=>item.Method).ToDictionary(static group=>group.Key,static group=>group.First().Text);
            var builder=new SqlConnectionStringBuilder(connectionString){InitialCatalog=database.Name};
            using var connection=new SqlConnection(builder.ConnectionString);
            connection.Open();
            var relations=database.Relations.ToDictionary(static relation=>$"{relation.Schema}.{relation.Name}",StringComparer.OrdinalIgnoreCase);
            var tableConnection=builder.ConnectionString;
            StubData.SetLoader(key=>{
                using var own=new SqlConnection(tableConnection);
                own.Open();
                return LoadTable(own,assembly,relations[key]);
            });
            var reader=Activator.CreateInstance(assembly.GetType($"{database.Catalog.RootNamespace}.StubReader")!)!;
            var harness=assembly.GetType($"{database.Catalog.RootNamespace}.Queries.FLinqEntry")!;
            var timeouts=0;
            foreach(var index in included.Order()){
                var translation=succeeded[index];
                var name=$"{database.Name}.{translation.Schema}.{translation.Name}";
                if(warned.TryGetValue($"V{index}",out var warning)){
                    notExpanded.Add($"{name}: {Shorten(warning)}");
                    continue;
                }
                if(timeouts>=2){
                    timedOut.Add(name+"(前の時間切れのため飛ばした)");
                    continue;
                }
                var expected=QueryServer(connection,$"SELECT * FROM [{translation.Schema}].[{translation.Name}]");
                var method=harness.GetMethod($"R{index}")!;
                var task=Task.Run(()=>RunExpanded(method,reader));
                if(!task.Wait(TimeSpan.FromSeconds(60))){
                    timedOut.Add(name);
                    timeouts++;
                    continue;
                }
                var (outcome,duplicates,failure)=task.Result;
                if(failure is not null){
                    notExpanded.Add($"{name}: 実行時 {Shorten(failure)}");
                }else if(duplicates>0){
                    mismatched.Add($"{name}: 結果に重複が {duplicates} 行ある");
                }else if(expected.Equals(outcome)){
                    matched.Add($"{name}({expected.Rows?.Count??0}行)");
                }else{
                    mismatched.Add($"{name}\n    本物: {Summary(expected)}\n    訳 : {Summary(outcome!)}");
                }
            }
            StubData.Set([]);
            compiled.Compiled.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        var report=new StringBuilder();
        report.Append($"FLinq で展開して一致 {matched.Count} / 不一致 {mismatched.Count} / 展開できない {notExpanded.Count} / 時間切れ {timedOut.Count}\n");
        report.Append("一致: ").Append(string.Join(", ",matched)).Append('\n');
        foreach(var item in mismatched){
            report.Append("不一致: ").Append(item).Append('\n');
        }
        foreach(var item in notExpanded){
            report.Append("展開できない: ").Append(item).Append('\n');
        }
        foreach(var item in timedOut){
            report.Append("時間切れ: ").Append(item).Append('\n');
        }
        this.output.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"sample-views-flinq-report.txt"),report.ToString());
        Assert.True(mismatched.Count==0&&notExpanded.Count==0&&timedOut.Count==0,report.ToString());
    }
    // ビューごとに、ビューを読んで配列にするメソッド(V)と、それを呼ぶメソッド(R)。FLinq は R の中の V の呼び出しを差し替え、V の中へビューの本体を取り込んで展開する。
    // V は static class に置くと傍受されず、呼び出す箇所が無いと書き換わらない(どちらも試験が空振りになる)。
    private static string Harness(TranslationCatalog catalog,IReadOnlyList<ViewTranslation> views,IReadOnlySet<int> included){
        var builder=new StringBuilder();
        builder.Append($"#nullable enable\nusing System.Linq;\nnamespace {catalog.RootNamespace}.Queries{{\npublic sealed class FLinqHarness{{\n");
        foreach(var index in included.Order()){
            var view=views[index];
            var member=view.RowType![(view.RowType!.LastIndexOf('.')+1)..];
            builder.Append($"[global::Seamless.FLinq.LinqIntercept(true)] public static {view.RowType}[] V{index}({catalog.ReaderType(view.Schema)} q)=>q.{member}.ToArray();\n");
        }
        builder.Append("}\npublic sealed class FLinqEntry{\n");
        foreach(var index in included.Order()){
            builder.Append($"public static object R{index}(object q)=>FLinqHarness.V{index}(({catalog.ReaderType(views[index].Schema)})q);\n");
        }
        builder.Append("}\n}\n");
        return builder.ToString();
    }
    private static (Outcome? Outcome,int Duplicates,string? Failure) RunExpanded(MethodInfo method,object reader){
        try{
            var rows=(Array)method.Invoke(null,[reader])!;
            var keys=new SortedSet<string>(StringComparer.Ordinal);
            foreach(var row in rows){
                var values=row!.GetType().GetProperties().Where(static property=>property.Name!="EqualityContract").Select(property=>property.GetValue(row)).ToArray();
                keys.Add(Key(values));
            }
            // 値の文字列が同じでも行の値が違う(型の違い)ことはないので、行の数との差を重複の数とする。
            var distinct=rows.Cast<object>().Distinct().Count();
            return (new(keys,null),rows.Length-distinct,null);
        }catch(Exception error){
            var inner=Unwrap(error);
            if(inner is Seamless.Database.SqlServer.Functions.SqlFunctionException function){
                return (new(null,function.Number),0,null);
            }
            return (null,0,inner.GetType().Name+": "+inner.Message);
        }
    }
    private static string Shorten(string text){
        text=text.Replace('\n',' ');
        if(text.Length>300){
            return text[..300];
        }
        return text;
    }
}
