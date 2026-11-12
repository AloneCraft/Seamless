using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Seamless.Database.Transactions.Tests;

public sealed partial class DatabaseTransactionExpansionTests{
    [Fact]
    public async Task DirectViewKeyFilterMovesBeforeProjectionAndKeepsObservableSetBoundaries(){
        const string source="""
            using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;using Seamless.Database;using Seamless.FLinq;
            namespace ViewPushdownCase{
             internal static partial class DatabaseGenerator;
             public readonly record struct Input(int Id,int Value);
             public sealed partial class Database(DatabaseContainerOptions options):DatabaseContainer(options){public Table<int,Input> Inputs=>this.Table<int,Input>(static row=>row.Id);}
             public static class Probe{
              public static readonly List<string> Events=new();
              [LinqIntercept(false)]public static bool Observe(Input row){Events.Add("input:"+row.Id);return true;}
              [LinqIntercept(false)]public static void Construct(int id){Events.Add("ctor:"+id);if(id==3)throw new InvalidOperationException("constructor3");}
              [LinqIntercept(false)]public static int Hash(int id){Events.Add("hash:"+id);if(id==3)throw new InvalidOperationException("hash3");return id;}
             }
             public sealed class Entry{
              [LinqIntercept(true)]private int[] Direct(Database db)=>db.DirectRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(false)]private int[] DirectReference(Database db)=>db.DirectRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(true)]private int[] Unsafe(Database db)=>db.ThrowRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(false)]private int[] UnsafeReference(Database db)=>db.ThrowRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(true)]private int[] CustomHash(Database db)=>db.HashRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              [LinqIntercept(false)]private int[] CustomHashReference(Database db)=>db.HashRows.Where(static result=>result.Id==1).Select(static result=>result.Value).ToArray();
              public static string Run(){
               using var db=new Database(DatabaseContainerOptions.InMemory with{IndexSize=128,PageSize=4096,SegmentSize=65536,MemorySize=65536,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!});
               using(var tx=db.BeginTransaction()){for(var i=1;i<=4;i++)tx.Inputs.Add(new(i,i*10));tx.Commit();}
               var entry=new Entry();var observations=new List<object>();
               foreach(var mode in new[]{"direct","constructor","hash"}){
                object Observe(bool expanded){
                 Probe.Events.Clear();string? error=null;int[] values=[];
                 try{
                  if(mode=="direct"){values=expanded?entry.Direct(db):entry.DirectReference(db);}
                  else if(mode=="constructor"){values=expanded?entry.Unsafe(db):entry.UnsafeReference(db);}
                  else{values=expanded?entry.CustomHash(db):entry.CustomHashReference(db);}
                 }catch(InvalidOperationException exception){error=exception.Message;}
                 return new{values,error,events=Probe.Events.ToArray()};
                }
                var expected=Observe(false);var actual=Observe(true);
                observations.Add(new{mode,expected,actual,equal=JsonSerializer.Serialize(expected)==JsonSerializer.Serialize(actual)});
               }
               return JsonSerializer.Serialize(observations);
              }
             }
            }
            namespace ViewPushdownCase.Views{
             public readonly partial record struct DirectRows(int Id,int Value){public static global::System.Collections.Generic.IEnumerable<DirectRows> Query(global::ViewPushdownCase.Database container)=>container.Inputs.Where(global::ViewPushdownCase.Probe.Observe).Select(static row=>new DirectRows(row.Id,row.Value));}
             public readonly partial record struct ThrowRows{
              public int Id{get;}public int Value{get;}
              public ThrowRows(int id,int value){global::ViewPushdownCase.Probe.Construct(id);this.Id=id;this.Value=value;}
              public static global::System.Collections.Generic.IEnumerable<ThrowRows> Query(global::ViewPushdownCase.Database container)=>container.Inputs.Where(global::ViewPushdownCase.Probe.Observe).Select(static row=>new ThrowRows(row.Id,row.Value));
             }
             public readonly partial record struct HashRows(int Id,int Value){public static global::System.Collections.Generic.IEnumerable<HashRows> Query(global::ViewPushdownCase.Database container)=>container.Inputs.Where(global::ViewPushdownCase.Probe.Observe).Select(static row=>new HashRows(row.Id,row.Value));}
             public readonly partial record struct HashRows{public override int GetHashCode()=>global::ViewPushdownCase.Probe.Hash(this.Id);}
            }
            """;
        var(result,generated)=await RunBoundarySourceAsync(source,"ViewPushdownCase");
        using var observation=JsonDocument.Parse(result);
        Assert.Equal(3,observation.RootElement.GetArrayLength());
        foreach(var item in observation.RootElement.EnumerateArray()){
            Assert.True(item.GetProperty("equal").GetBoolean(),item.GetRawText());
            var actual=item.GetProperty("actual");
            var mode=item.GetProperty("mode").GetString();
            if(mode is "direct" or "hash"){
                Assert.Equal(new[]{10},actual.GetProperty("values").EnumerateArray().Select(static value=>value.GetInt32()));
                Assert.Equal(JsonValueKind.Null,actual.GetProperty("error").ValueKind);
                Assert.Equal(4,actual.GetProperty("events").GetArrayLength());
            }else{
                Assert.Equal("constructor3",actual.GetProperty("error").GetString());
            }
        }
        var syntax=generated.Select(static code=>CSharpSyntaxTree.ParseText(code).GetRoot()).ToArray();
        static JsonElement ReadPlan(AttributeSyntax attribute){
            using var plan=JsonDocument.Parse(((LiteralExpressionSyntax)attribute.ArgumentList!.Arguments[1].Expression).Token.ValueText);
            return plan.RootElement.Clone();
        }
        var plans=syntax.SelectMany(static root=>root.DescendantNodes().OfType<AttributeSyntax>())
            .Where(static attribute=>attribute.Name.ToString().EndsWith("ExecutionPlanAttribute",StringComparison.Ordinal))
            .Select(ReadPlan).ToArray();
        // 融合したビューは元のViewDefinitionRootが計画に残らず、融合した根のノードにビューの判断が記録される。
        static JsonElement ViewProperties(JsonElement plan)=>Assert.Single(plan.GetProperty("Nodes").EnumerateArray(),static node=>node.GetProperty("Properties").TryGetProperty("ExpansionStatus",out _)).GetProperty("Properties");
        var direct=ViewProperties(Assert.Single(plans,static plan=>plan.GetProperty("MethodKey").GetString()!.Contains(".Direct(")));
        Assert.Equal("Applied",direct.GetProperty("PredicatePushdownStatus").GetString());
        Assert.Equal("InlinedIntoCaller",direct.GetProperty("ExpansionStatus").GetString());
        Assert.Equal("DirectKeyFilterBeforeProjection;PointLookupNotSelected",direct.GetProperty("PredicatePushdownReason").GetString());
        Assert.Equal("ViewPushdownCase.Input.Id",direct.GetProperty("PushedKeyMember").GetString());
        foreach(var method in new[]{"Unsafe","CustomHash"}){
            var properties=ViewProperties(Assert.Single(plans,plan=>plan.GetProperty("MethodKey").GetString()!.Contains("."+method+"(")));
            Assert.Equal("NotApplied",properties.GetProperty("PredicatePushdownStatus").GetString());
            Assert.Equal("ProjectionConstructorOrEqualityNotProvenTotal",properties.GetProperty("PredicatePushdownReason").GetString());
        }
        Assert.Contains(syntax.SelectMany(static root=>root.DescendantNodes().OfType<MethodDeclarationSyntax>()),static method=>
            method.Identifier.ValueText=="TryGetNext"&&method.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(creation=>
                creation.Type.ToString().Replace("@",string.Empty).EndsWith("Views.DirectRows",StringComparison.Ordinal)
                &&method.DescendantNodes().OfType<BinaryExpressionSyntax>().Any(condition=>condition.IsKind(SyntaxKind.EqualsExpression)
                    &&condition.SpanStart<creation.SpanStart&&condition.Right.ToString()=="1")));
    }
}
