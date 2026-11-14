using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    // ビューの形11例。各例で、傍受しない実行(Seamless.Linq.Set の集合)とFLinqの展開の値・副作用・例外を比べる。
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
             // ビューは生成される入口(Database.IReader)への拡張プロパティ。
             public static class Queries{
              extension(Database.IReader q){
               public Seamless.Collections.ISet<Views.Doubled> Doubled=>q.Items.Select(static i=>new Views.Doubled(i.Id,i.Value*2));
               public Seamless.Collections.ISet<Views.Large> Large=>q.Items.Where(static i=>i.Value>10).Select(static i=>new Views.Large(i.Id,i.Value));
               public Seamless.Collections.ISet<Views.Named> Named=>q.Others.Join(q.Items,static o=>o.ItemId,static i=>i.Id,static (o,i)=>new Views.Named(i.Id,o.Code));
               public Seamless.Collections.ISet<Views.Nested> Nested=>q.Doubled.Where(static v=>v.Twice>40).Select(static v=>new Views.Nested(v.Id));
               public Seamless.Collections.ISet<Views.Groups> Groups=>q.Items.Select(static i=>new Views.Groups(i.Group));
               public Seamless.Collections.ISet<Views.Anchored> Anchored=>q.Items.Where(static i=>global::ViewCatalogCase.Probe.Observe(i.Id).Anchor).Select(static i=>new Views.Anchored(i.Id));
              }
             }
             public static class Probe{
              public static readonly List<string> Events=new();
              [LinqIntercept(false)]public static bool Observe(int id){Events.Add("observe:"+id);return true;}
             }
             public sealed class Entry{
              // 傍受しない側(R)はビューの本体を Seamless.Linq.Set の集合として評価する。傍受する側(C)は本体を取り込んで展開する。
              // 1. 単純参照
              [LinqIntercept(true)]private int[] C01(Database.Reader r)=>r.Doubled.Select(static v=>v.Twice).ToArray();
              [LinqIntercept(false)]private int[] R01(Database.Reader r)=>r.Doubled.Select(static v=>v.Twice).ToArray();
              // 2. キー列の等値検索(変数)
              [LinqIntercept(true)]private int[] C02(Database.Reader r,int id)=>r.Doubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              [LinqIntercept(false)]private int[] R02(Database.Reader r,int id)=>r.Doubled.Where(v=>v.Id==id).Select(static v=>v.Twice).ToArray();
              // 3. 外側でさらに射影
              [LinqIntercept(true)]private int[] C03(Database.Reader r)=>r.Doubled.Select(static v=>v.Twice+1).ToArray();
              [LinqIntercept(false)]private int[] R03(Database.Reader r)=>r.Doubled.Select(static v=>v.Twice+1).ToArray();
              // 4. 内側と外側の条件
              [LinqIntercept(true)]private int[] C04(Database.Reader r)=>r.Large.Where(static v=>v.Value<40).Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R04(Database.Reader r)=>r.Large.Where(static v=>v.Value<40).Select(static v=>v.Id).ToArray();
              // 5. 内側と外側のJoin
              [LinqIntercept(true)]private int[] C05(Database.Reader r)=>r.Named.Join(r.Items,static v=>v.ItemId,static i=>i.Id,static (v,i)=>v.Code*100+i.Group).ToArray();
              [LinqIntercept(false)]private int[] R05(Database.Reader r)=>r.Named.Join(r.Items,static v=>v.ItemId,static i=>i.Id,static (v,i)=>v.Code*100+i.Group).ToArray();
              // 6. 複数のビューの結合
              [LinqIntercept(true)]private int[] C06(Database.Reader r)=>r.Doubled.Join(r.Large,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Value).ToArray();
              [LinqIntercept(false)]private int[] R06(Database.Reader r)=>r.Doubled.Join(r.Large,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Value).ToArray();
              // 7. ビューから別のビュー
              [LinqIntercept(true)]private int[] C07(Database.Reader r)=>r.Nested.Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R07(Database.Reader r)=>r.Nested.Select(static v=>v.Id).ToArray();
              // 8. 重複除去が必要な射影
              [LinqIntercept(true)]private int[] C08(Database.Reader r)=>r.Groups.Select(static v=>v.Group).ToArray();
              [LinqIntercept(false)]private int[] R08(Database.Reader r)=>r.Groups.Select(static v=>v.Group).ToArray();
              // 9. 一意性を証明できる射影(キーを含む)
              [LinqIntercept(true)]private int C09(Database.Reader r)=>r.Doubled.Count();
              [LinqIntercept(false)]private int R09(Database.Reader r)=>r.Doubled.Count();
              // 10. 同じ定義を2回参照
              [LinqIntercept(true)]private int[] C10(Database.Reader r)=>r.Doubled.Join(r.Doubled,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Twice).ToArray();
              [LinqIntercept(false)]private int[] R10(Database.Reader r)=>r.Doubled.Join(r.Doubled,static a=>a.Id,static b=>b.Id,static (a,b)=>a.Twice+b.Twice).ToArray();
              // 11. Anchorで評価順を保護
              [LinqIntercept(true)]private int[] C11(Database.Reader r)=>r.Anchored.Where(static v=>v.Id<=2).Select(static v=>v.Id).ToArray();
              [LinqIntercept(false)]private int[] R11(Database.Reader r)=>r.Anchored.Where(static v=>v.Id<=2).Select(static v=>v.Id).ToArray();
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){for(var i=1;i<=6;i++){tx.Items.Add(new(i,i*10,i%2));}
               tx.Others.Add(new(1,2,7));tx.Others.Add(new(2,4,8));tx.Others.Add(new(3,9,9));tx.Commit();}
               using var r=db.BeginRead();
               var entry=new Entry();var cases=new List<object>();
               void Case(string name,Func<object> reference,Func<object> expanded){
                object Observe(Func<object> run){
                 Probe.Events.Clear();object? value=null;string? error=null;
                 // ビューは集合なので並びを持たない。列の値と観測は並べ替えて比べる。
                 try{value=run();if(value is int[] values){value=values.Order().ToArray();}}catch(Exception exception){error=exception.GetType().Name+":"+exception.Message;}
                 return new{value,error,events=Probe.Events.Order().ToArray()};
                }
                var expected=Observe(reference);var actual=Observe(expanded);
                cases.Add(new{name,expected,actual,equal=JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(actual)});
               }
               Case("01",()=>entry.R01(r),()=>entry.C01(r));
               Case("02",()=>entry.R02(r,3),()=>entry.C02(r,3));
               Case("03",()=>entry.R03(r),()=>entry.C03(r));
               Case("04",()=>entry.R04(r),()=>entry.C04(r));
               Case("05",()=>entry.R05(r),()=>entry.C05(r));
               Case("06",()=>entry.R06(r),()=>entry.C06(r));
               Case("07",()=>entry.R07(r),()=>entry.C07(r));
               Case("08",()=>entry.R08(r),()=>entry.C08(r));
               Case("09",()=>entry.R09(r),()=>entry.C09(r));
               Case("10",()=>entry.R10(r),()=>entry.C10(r));
               Case("11",()=>entry.R11(r),()=>entry.C11(r));
               return JsonSerializer.Serialize(cases);
              }
             }
            }
            namespace ViewCatalogCase.Views{
             public readonly record struct Doubled(int Id,int Twice);
             public readonly record struct Large(int Id,int Value);
             public readonly record struct Named(int ItemId,int Code);
             public readonly record struct Nested(int Id);
             public readonly record struct Groups(int Group);
             public readonly record struct Anchored(int Id);
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"ViewCatalogCase");
        using var observation=JsonDocument.Parse(result);
        // 傍受する側では、ビューの本体を取り込んで展開する(ビューを呼ぶコードも Seamless.Linq.Set の演算も生成コードに残らない)。
        var code=string.Join("\n",generated);
        Assert.DoesNotContain("global::Seamless.Linq.Set.",code);
        foreach(var item in observation.RootElement.EnumerateArray()){
            Assert.True(item.GetProperty("equal").GetBoolean(),item.GetRawText());
        }
    }
}
