using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    // 設計補足(2026-11-11)の12例。各例で、傍受しない元のC#実行とFLinq実行の値・副作用の順序・例外を比べる。
    // 最適化の達成度(融合・条件移動・重複除去の省略・キー検索)はまだ合否に入れず、計画と生成コードの観測として記録する。
    // 証拠の保存先は環境変数 VIEW_BOUNDARY_PROOF_ROOT (未設定なら保存しない)。
    [Fact]
    public async Task ViewExpansionCatalogKeepsReferenceResults(){
        const string source="""
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;using Seamless.Linq;
            namespace ViewCatalogCase{
             internal static partial class DatabaseGenerator;
             public readonly record struct Item(int Id,int Value,int Group);
             public readonly record struct Other(int Id,int ItemId,int Code);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){
              public Table<int,Item> Items=>this.Table<int,Item>(static row=>row.Id);
              public Table<int,Other> Others=>this.Table<int,Other>(static row=>row.Id);
             }
             public static class Probe{
              public static readonly List<string> Events=new();
              [LinqIntercept(false)]public static bool Observe(int id){Events.Add("observe:"+id);return true;}
             }
             public sealed class Entry{
              // 1. 単純参照
              [LinqIntercept(true)]private int[] C01(Database db)=>db.Doubled.Select(static v=>v.Twice).ToArray();
              [LinqIntercept(false)]private int[] R01(Database db)=>db.Doubled.Select(static v=>v.Twice).ToArray();
              // 2. キー列の等値検索(変数)
              [LinqIntercept(true)]private int[] C02(Database db,int id)=>db.Doubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              [LinqIntercept(false)]private int[] R02(Database db,int id)=>db.Doubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              // 2b. 匿名型の本体と生成投影を通したキー等値検索
              [LinqIntercept(true)]private int[] C02b(Database db,int id)=>db.AnonymousDoubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              [LinqIntercept(false)]private int[] R02b(Database db,int id)=>db.AnonymousDoubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              // 3. 外側でさらに射影
              [LinqIntercept(true)]private int[] C03(Database db)=>db.Doubled.Select(static v=>v.Twice+1).ToArray();
              [LinqIntercept(false)]private int[] R03(Database db)=>db.Doubled.Select(static v=>v.Twice+1).ToArray();
              // 4. 内側と外側の条件
              [LinqIntercept(true)]private int[] C04(Database db)=>db.Large.Where(static v=>v.Value<40).Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R04(Database db)=>db.Large.Where(static v=>v.Value<40).Select(static v=>v.Id).ToArray();
              // 5. 内側と外側のJoin
              [LinqIntercept(true)]private int[] C05(Database db)=>db.Named.Join(db.Items,static v=>v.ItemId,static i=>i.Id,static (v,i)=>v.Code*100+i.Group).ToArray();
              [LinqIntercept(false)]private int[] R05(Database db)=>db.Named.Join(db.Items,static v=>v.ItemId,static i=>i.Id,static (v,i)=>v.Code*100+i.Group).ToArray();
              // 6. 複数のビューの結合
              [LinqIntercept(true)]private int[] C06(Database db)=>db.Doubled.Join(db.Large,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Value).ToArray();
              [LinqIntercept(false)]private int[] R06(Database db)=>db.Doubled.Join(db.Large,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Value).ToArray();
              // 7. ビューから別のビュー
              [LinqIntercept(true)]private int[] C07(Database db)=>db.Nested.Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R07(Database db)=>db.Nested.Select(static v=>v.Id).ToArray();
              // 8. 重複除去が必要な射影
              [LinqIntercept(true)]private int[] C08(Database db)=>db.Groups.Select(static v=>v.Group).ToArray();
              [LinqIntercept(false)]private int[] R08(Database db)=>db.Groups.Select(static v=>v.Group).ToArray();
              // 9. 一意性を証明できる射影(キーを含む)
              [LinqIntercept(true)]private int C09(Database db)=>db.Doubled.Count();
              [LinqIntercept(false)]private int R09(Database db)=>db.Doubled.Count();
              // 10. 同じ定義を2回参照
              [LinqIntercept(true)]private int[] C10(Database db)=>db.Doubled.Join(db.Doubled,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Twice).ToArray();
              [LinqIntercept(false)]private int[] R10(Database db)=>db.Doubled.Join(db.Doubled,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Twice).ToArray();
              // 11. Anchorで評価順を保護
              [LinqIntercept(true)]private int[] C11(Database db)=>db.Anchored.Where(static v=>v.Id<=2).Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R11(Database db)=>db.Anchored.Where(static v=>v.Id<=2).Select(static v=>v.Id).ToArray();
              // 12. 展開できない形(旧CreateView)
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){for(var i=1;i<=6;i++){tx.Items.Add(new(i,i*10,i%2));}
               tx.Others.Add(new(1,2,7));tx.Others.Add(new(2,4,8));tx.Others.Add(new(3,9,9));tx.Commit();}
               var entry=new Entry();var cases=new List<object>();
               void Case(string name,Func<object> reference,Func<object> expanded){
                object Observe(Func<object> run){
                 Probe.Events.Clear();object? value=null;string? error=null;
                 try{value=run();}catch(Exception exception){error=exception.GetType().Name+":"+exception.Message;}
                 return new{value,error,events=Probe.Events.ToArray()};
                }
                var expected=Observe(reference);var actual=Observe(expanded);
                cases.Add(new{name,expected,actual,equal=JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(actual)});
               }
               Case("01",()=>entry.R01(db),()=>entry.C01(db));
               Case("02",()=>entry.R02(db,3),()=>entry.C02(db,3));
               Case("02b",()=>entry.R02b(db,3),()=>entry.C02b(db,3));
               Case("03",()=>entry.R03(db),()=>entry.C03(db));
               Case("04",()=>entry.R04(db),()=>entry.C04(db));
               Case("05",()=>entry.R05(db),()=>entry.C05(db));
               Case("06",()=>entry.R06(db),()=>entry.C06(db));
               Case("07",()=>entry.R07(db),()=>entry.C07(db));
               Case("08",()=>entry.R08(db),()=>entry.C08(db));
               Case("09",()=>entry.R09(db),()=>entry.C09(db));
               Case("10",()=>entry.R10(db),()=>entry.C10(db));
               Case("11",()=>entry.R11(db),()=>entry.C11(db));
               return JsonSerializer.Serialize(cases);
              }
             }
            }
            namespace ViewCatalogCase.Views{
             public readonly partial record struct Doubled(int Id,int Twice){public static global::System.Collections.Generic.IEnumerable<Doubled> Query(global::ViewCatalogCase.Database container)=>container.Items.Select(static i=>new Doubled(i.Id,i.Value*2));}
             public readonly partial record struct AnonymousDoubled{public static global::System.Collections.Generic.IEnumerable<object> Query(global::ViewCatalogCase.Database container)=>container.Items.Select(static i=>new{i.Id,Twice=i.Value*2});}
             public readonly partial record struct Large(int Id,int Value){public static global::System.Collections.Generic.IEnumerable<Large> Query(global::ViewCatalogCase.Database container)=>container.Items.Where(static i=>i.Value>10).Select(static i=>new Large(i.Id,i.Value));}
             public readonly partial record struct Named(int ItemId,int Code){public static global::System.Collections.Generic.IEnumerable<Named> Query(global::ViewCatalogCase.Database container)=>container.Others.Join(container.Items,static o=>o.ItemId,static i=>i.Id,static (o,i)=>new Named(i.Id,o.Code));}
             public readonly partial record struct Nested(int Id){public static global::System.Collections.Generic.IEnumerable<Nested> Query(global::ViewCatalogCase.Database container)=>container.Doubled.Where(static v=>v.Twice>40).Select(static v=>new Nested(v.Id));}
             public readonly partial record struct Groups(int Group){public static global::System.Collections.Generic.IEnumerable<Groups> Query(global::ViewCatalogCase.Database container)=>container.Items.Select(static i=>new Groups(i.Group));}
             public readonly partial record struct Anchored(int Id){public static global::System.Collections.Generic.IEnumerable<Anchored> Query(global::ViewCatalogCase.Database container)=>container.Items.Where(static i=>global::ViewCatalogCase.Probe.Observe(i.Id).Anchor).Select(static i=>new Anchored(i.Id));}
            }
            """;
        var evidence=Environment.GetEnvironmentVariable("VIEW_BOUNDARY_PROOF_ROOT");
        string? generatedEvidence=null;
        if(evidence is not null){generatedEvidence=Path.Combine(evidence,"catalog-generated.cs.txt");}
        var(result,generated)=await RunBoundarySourceAsync(source,"ViewCatalogCase",generatedEvidence);
        using var observation=JsonDocument.Parse(result);
        // 現状の観測: 計画のビュー関連ノードと、生成コードの境界の印を例ごとに集める。
        var syntax=generated.Select(static code=>CSharpSyntaxTree.ParseText(code).GetRoot()).ToArray();
        var plans=syntax.SelectMany(static root=>root.DescendantNodes().OfType<AttributeSyntax>())
            .Where(static attribute=>attribute.Name.ToString().EndsWith("ExecutionPlanAttribute",StringComparison.Ordinal))
            .Select(static attribute=>{using var plan=JsonDocument.Parse(((LiteralExpressionSyntax)attribute.ArgumentList!.Arguments[1].Expression).Token.ValueText);return plan.RootElement.Clone();})
            .ToArray();
        var code=string.Join("\n",generated);
        var report=new List<object>();
        foreach(var item in observation.RootElement.EnumerateArray()){
            var name=item.GetProperty("name").GetString()!;
            var plan=plans.Where(plan=>plan.GetProperty("MethodKey").GetString()!.Contains(".C"+name+"(",StringComparison.Ordinal)).ToArray();
            var viewNodes=plan.SelectMany(static plan=>plan.GetProperty("Nodes").EnumerateArray())
                .Where(static node=>node.GetProperty("Properties").TryGetProperty("ExpansionStatus",out _))
                .Select(static node=>new{Label=node.GetProperty("Label").GetString(),Properties=node.GetProperty("Properties").Clone()}).ToArray();
            report.Add(new{name,equal=item.GetProperty("equal").GetBoolean(),expected=item.GetProperty("expected").Clone(),actual=item.GetProperty("actual").Clone(),planFound=plan.Length,viewNodes});
        }
        var summary=new{
            cases=report,
            generatedMarkers=new{
                expandDefinitionCalls=CountOf(code,".ExpandDefinition<"),
                distinctCalls=CountOf(code,".Distinct("),
                opaqueViewRoots=CountOf(code,"OpaqueViewRoot"),
            },
        };
        if(evidence is not null){File.WriteAllText(Path.Combine(evidence,"catalog-observation.json"),JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));}
        foreach(var item in observation.RootElement.EnumerateArray()){
            Assert.True(item.GetProperty("equal").GetBoolean(),item.GetRawText());
        }
    }
    private static int CountOf(string text,string value){
        var count=0;
        for(var index=text.IndexOf(value,StringComparison.Ordinal);index>=0;index=text.IndexOf(value,index+value.Length,StringComparison.Ordinal)){count++;}
        return count;
    }
}
