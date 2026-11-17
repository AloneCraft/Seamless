using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Test.SourceGenerator;

// 関係代入(Document/refactoring-designs/20261117-関係代入.md)の書き方と、解析器の SDB004・SDB005 の試験。
public sealed class RelationalAssignmentAnalyzerTests{
    private static string Source(string body)=>$$"""
        using Seamless.Database;
        namespace Sample{
            internal static class DatabaseGenerator;
            public readonly partial record struct Item(int Key,string Name):global::Seamless.Collections.IRecord<int>;
            public sealed partial class Shop(global::Seamless.Database.DatabaseContainerOptions options):global::Seamless.Database.DatabaseContainer(options){
                public partial global::Seamless.Database.Table<int,Item> Items{get;set;}
            }
            public static class Use{
                {{body}}
            }
        }
        """;

    private static (ImmutableArray<Diagnostic> Compiler,ImmutableArray<Diagnostic> Analyzer) Analyze(string body){
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",Source(body))]);
        GeneratorTestHelpers.CreateDatabaseDriver().RunGeneratorsAndUpdateCompilation(compilation,out var output,out _);
        var analyzer=output.WithAnalyzers([new global::Seamless.Database.Generator.RelationalAssignmentAnalyzer()]);
        var compiler=output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToImmutableArray();
        return(compiler,analyzer.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult());
    }

    [Fact,Trait("Category","Unit")]
    public void PartialTablePropertiesAndEveryShortFormCompile(){
        var(compiler,analyzer)=Analyze("""
            public static void Write(Shop db,global::System.Collections.Generic.IEnumerable<Item> query,global::System.Collections.Generic.List<Item> list,Item[] array){
                db.Items=db.Items.Union([new Item(1,"a")]);
                db.Items=db.Items.Where(static row=>row.Key>0).Except(array).Intersect(list);
                db.Items=new(query);
                db.Items=[..query];
                db.Items=[new Item(2,"b")];
                db.Items=list;
                db.Items=array;
                db.Items.Add(new Item(3,"c"));
                db.Items[3]=new Item(3,"d");
                db.Items.Remove(3);
                db.Items.Remove(new Item(2,"b"));
                long count=db.Items.LongCount;
            }
            public static void InTransaction(Shop db,Item[] rows){
                using var tx=db.BeginTransaction();
                tx.Items=tx.Items.Union(rows);
                tx.Items=new(rows);
                tx.Commit();
            }
            """);
        Assert.True(compiler.IsEmpty,string.Join(global::System.Environment.NewLine,compiler));
        Assert.True(analyzer.IsEmpty,string.Join(global::System.Environment.NewLine,analyzer));
    }

    [Fact,Trait("Category","Unit")]
    public void WritingToATableExpressionIsAnError(){
        var(compiler,analyzer)=Analyze("""
            public static void Write(Shop db,Item item,Item[] rows){
                db.Items.Union(rows).Add(item);
                db.Items.Where(static row=>row.Key>0)[1]=item;
                new global::Seamless.Database.Table<int,Item>(rows).Remove(1);
                ((global::Seamless.Database.Table<int,Item>)rows).Remove(item);
                var expression=db.Items.Except(rows);
                expression.Add(item);
            }
            """);
        Assert.True(compiler.IsEmpty,string.Join(global::System.Environment.NewLine,compiler));
        // 変数に入れた式は実行時に見分ける(書き込むと例外)。
        Assert.Equal(4,analyzer.Count(static diagnostic=>diagnostic.Id=="SDB004"));
    }

    [Fact,Trait("Category","Unit")]
    public void UsingADatabaseTableWhileATransactionIsOpenIsAnError(){
        var(compiler,analyzer)=Analyze("""
            public static void Write(Shop db,Item item,Item[] rows){
                db.Items.Add(item);
                using(var tx=db.BeginTransaction()){
                    db.Items.Add(item);
                    tx.Items=tx.Items.Union(db.Items);
                    global::System.Action later=()=>db.Items.Add(item);
                    tx.Commit();
                }
                db.Items=db.Items.Union(rows);
            }
            public static async global::System.Threading.Tasks.Task Read(Shop db){
                await using var read=await db.BeginReadOnlyTransactionAsync();
                _=db.Items.LongCount;
            }
            """);
        Assert.True(compiler.IsEmpty,string.Join(global::System.Environment.NewLine,compiler));
        // 取引の範囲の中の3つだけ。範囲の外とラムダの中は対象外(ラムダは後で別の流れで動きうるので、実行時の例外で守る)。
        Assert.Equal(3,analyzer.Count(static diagnostic=>diagnostic.Id=="SDB005"));
    }
}
