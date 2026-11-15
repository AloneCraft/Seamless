using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace Database.SqlTranslation.Tests;
/// <summary>
/// 翻訳器が GROUP BY に出す形(Set.GroupBy、HAVING は組の Where、GROUP BY の列は組の最初の行)が、
/// FLinq で展開でき、元の集合を1回だけ読み、展開しない場合と同じ結果になること。
/// 形は FLinq の既存の GroupBy の試験と同じく、[LinqIntercept(true)] の付いたメソッドの中で ToArray まで書く
/// (ビューの本体は FLinq が呼び出し側へ取り込んでから展開するため)。
/// 以前の形(キーの集合を作り、キーごとに元の行を絞り直す)は展開できず(FLINQ002)、展開しなくても元の集合をグループ数×2回読んでいた。
/// HAVING と SELECT の両方が文のラムダで var の型が利用者の型(Row)になる形は、FLinq がブロックの中の var を式として描き直して
/// 展開に失敗していた(FLINQ002、ReturnToResultRewriter。2026-11-15 に var を描き直さないよう直した)。
/// </summary>
public sealed class GroupByExpansion{
    private const string Source="""
        using System.Linq;
        using Seamless.FLinq;
        using Seamless.Linq;
        using F=global::Seamless.Database.SqlServer.Functions;
        namespace Probe{
            public sealed class Counting<T>(T[] items):global::Seamless.Collections.ISet<T>{
                public static int Enumerations;
                private readonly global::System.Collections.Generic.HashSet<T> set=[..items];
                public int Count=>this.set.Count;
                public bool Contains(T item)=>this.set.Contains(item);
                public global::System.Collections.Generic.IEnumerator<T> GetEnumerator(){
                    Enumerations++;
                    return this.set.GetEnumerator();
                }
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
            }
            public readonly record struct Row(int Id,int? K,decimal? Amount);
            public sealed class Shapes{
                [LinqIntercept(true)]
                public static (int?,decimal?)[] Grouped(global::Seamless.Collections.ISet<Row> src)=>
                    src.GroupBy(x=>x.K).Where(g=>{var f=global::System.Linq.Enumerable.First(g);return global::System.Linq.Enumerable.Count(g)>9;}).Select(g=>{var f=global::System.Linq.Enumerable.First(g);return (f.K,F.AggregateFunctions.Sum(global::System.Linq.Enumerable.Select(g,y=>y.Amount)));}).ToArray();
            }
            public class Entry{
                public static object[] Run(){
                    var rows=new Row[1000];
                    for(var i=0;i<rows.Length;i++){
                        int? key=i%100;
                        if(i%97==0){
                            key=null;
                        }
                        decimal? amount=i;
                        // キー 7 の組は金額がすべて NULL(SUM は NULL)。
                        if(i%13==0||key==7){
                            amount=null;
                        }
                        rows[i]=new Row(i,key,amount);
                    }
                    var src=new Counting<Row>(rows);
                    Counting<Row>.Enumerations=0;
                    var result=Shapes.Grouped(src);
                    var ordered=result.OrderBy(static r=>r.Item1??-1).Select(static r=>$"{r.Item1}:{r.Item2}");
                    return [Counting<Row>.Enumerations,string.Join(";",ordered)];
                }
            }
        }
        """;
    [Fact]public void SetGroupByIsExpandedReadsTheSourceOnceAndKeepsTheResult(){
        var parse=new CSharpParseOptions(LanguageVersion.Preview).WithFeatures([new("InterceptorsPreview","true"),new("InterceptorsNamespaces","Intercept"),new("InterceptorsPreviewNamespaces","Intercept")]);
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(static path=>(MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
        foreach(var type in new[]{typeof(Seamless.Collections.ISet<>),typeof(Seamless.Linq.Set),typeof(Seamless.Database.SqlServer.Functions.AggregateFunctions)}){
            references.Add(MetadataReference.CreateFromFile(type.Assembly.Location));
        }
        var tree=CSharpSyntaxTree.ParseText(Source,parse,path:"Probe.cs");
        var compilation=CSharpCompilation.Create("GroupByExpansion",[tree],references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        var driver=CSharpGeneratorDriver.Create([new Seamless.FLinq.Generator.FLinqIncrementalGenerator().AsSourceGenerator()],parseOptions:parse);
        driver.RunGeneratorsAndUpdateCompilation(compilation,out var generatedCompilation,out var diagnostics);
        var warnings=diagnostics.Where(static diagnostic=>diagnostic.Id.StartsWith("FLINQ",StringComparison.Ordinal)&&diagnostic.Severity>=DiagnosticSeverity.Warning).Select(static diagnostic=>diagnostic.ToString());
        Assert.Empty(warnings);
        var generatedTrees=generatedCompilation.SyntaxTrees.Skip(1).ToList();
        var generated=string.Concat(generatedTrees.Select(static t=>t.ToString()));
        Assert.Contains("InterceptsLocationAttribute(1,",generated,StringComparison.Ordinal);
        // 展開したコードは Set.GroupBy を呼ばない(System.Linq へ下げて展開している)。
        Assert.DoesNotContain("global::Seamless.Linq.Set.",generated,StringComparison.Ordinal);
        // 差し替えの入ったファイルを除いた版を「FLinq なし」とする(生成された属性の宣言は残す)。
        var plain=compilation.AddSyntaxTrees(generatedTrees.Where(static t=>!t.ToString().Contains("InterceptsLocationAttribute(1,",StringComparison.Ordinal)));
        var withoutFLinq=Run(plain);
        var withFLinq=Run(generatedCompilation);
        Assert.Equal(1,withoutFLinq.Enumerations);
        Assert.Equal(1,withFLinq.Enumerations);
        Assert.Equal(withoutFLinq.Text,withFLinq.Text);
        // NULL のキーの組(11行)と、金額がすべて NULL の組(SUM は NULL)を含む。
        Assert.StartsWith(":",withFLinq.Text,StringComparison.Ordinal);
        Assert.Contains(";7:;",withFLinq.Text,StringComparison.Ordinal);
    }
    private static (int Enumerations,string Text) Run(Compilation unit){
        using var stream=new MemoryStream();
        var emitted=unit.Emit(stream);
        Assert.True(emitted.Success,string.Join("\n",emitted.Diagnostics.Where(static d=>d.Severity==DiagnosticSeverity.Error)));
        stream.Position=0;
        var context=new AssemblyLoadContext(unit.AssemblyName,true);
        try{
            var result=(object[])context.LoadFromStream(stream).GetType("Probe.Entry")!.GetMethod("Run")!.Invoke(null,null)!;
            return ((int)result[0],(string)result[1]);
        }finally{
            context.Unload();
        }
    }
}
