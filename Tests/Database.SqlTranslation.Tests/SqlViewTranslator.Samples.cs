using System.Text;
using System.Text.RegularExpressions;
using Seamless.Database.SqlTranslation;
using Xunit.Abstractions;
namespace Database.SqlTranslation.Tests;
public sealed partial class SqlViewTranslator(ITestOutputHelper output){
    private readonly ITestOutputHelper output=output;
    /// <summary>
    /// サンプルDB(AdventureWorks 2016〜2025・DW、Northwind、pubs、WideWorldImporters・DW)の全ビューを訳し、
    /// 訳せたものはすべて C# としてコンパイルできることを確かめる。訳せなかった理由の内訳を出力する。
    /// </summary>
    [Fact,Trait("Category","Unit")]public void SampleViews(){
        var total=0;
        var translatedCount=0;
        var reasons=new Dictionary<string,int>();
        var compileFailures=new List<string>();
        var report=new StringBuilder();
        foreach(var database in SampleCatalogs.Load()){
            var translator=new Seamless.Database.SqlTranslation.SqlViewTranslator(database.Catalog);
            var translations=database.Views.Select(view=>translator.Translate(view.Schema,view.Name,view.Definition)).ToList();
            total+=translations.Count;
            translatedCount+=translations.Count(static translation=>translation.Succeeded);
            foreach(var translation in translations.Where(static translation=>!translation.Succeeded)){
                var diagnostic=translation.Diagnostics.First(static diagnostic=>diagnostic.Severity==TranslationSeverity.Unsupported);
                var key=$"{diagnostic.Code} {Regex.Replace(diagnostic.Message,@"[A-Za-z_]+\.[A-Za-z_.]+|'[^']*'","…")}";
                reasons[key]=reasons.GetValueOrDefault(key)+1;
                report.Append($"  {database.Name}.{translation.Schema}.{translation.Name}: {diagnostic}\n");
            }
            if(translations.All(static translation=>!translation.Succeeded)){
                continue;
            }
            var views=translator.SourceFile(translations);
            var stubs=TranslationCompiler.StubSource(database.Catalog,database.Relations,translations);
            var result=TranslationCompiler.Compile(database.Name,stubs,views);
            if(result.Assembly is null){
                var lines=views.Split('\n');
                foreach(var error in result.Errors.Where(static error=>error.StartsWith("(",StringComparison.Ordinal)).Take(20)){
                    var match=Regex.Match(error,@"^\((\d+),");
                    var line="";
                    if(match.Success&&int.Parse(match.Groups[1].Value)-1<lines.Length){
                        line=lines[int.Parse(match.Groups[1].Value)-1].Trim();
                        if(line.Length>160){
                            line=line[..160];
                        }
                    }
                    compileFailures.Add($"{database.Name}: {error}\n    {line}");
                }
                if(compileFailures.Count==0){
                    compileFailures.AddRange(result.Errors.Take(10).Select(error=>database.Name+": "+error));
                }
            }
        }
        this.output.WriteLine($"訳せたビュー: {translatedCount}/{total}");
        foreach(var (reason,count) in reasons.OrderByDescending(static pair=>pair.Value)){
            this.output.WriteLine($"{count,4} {reason}");
        }
        this.output.WriteLine(report.ToString());
        Assert.Equal(171,total);
        Assert.True(compileFailures.Count==0,"訳したビューがコンパイルできない:\n"+string.Join("\n",compileFailures));
    }
}
