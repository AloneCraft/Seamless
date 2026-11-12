using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class GeneratedViewRow{
    [Fact]
    public void TryCreate(){
        var definition=Designer.AddSchema(Designer.Empty("generated-view","Generated view"),"Sales");
        const string views="""
            using System.Collections.Generic;
            using System.Linq;
            using Alias=System.String;
            namespace UserCode.Views{
                // 列を書かず、Queryに本体を直接書く
                public readonly partial record struct Summary{
                    public static IEnumerable<object> Query(global::UserCode.ManagedDb db)=>
                        new[]{3,1,2,2}.Where(value=>value>0).Select(value=>new{Id=(long)value,Title=(Alias?)Label(value)});
                    private static string Label(int value)=>"row"+value;
                }
            }
            namespace UserCode.Views.Sales{
                public readonly partial record struct Summary{
                    public static IEnumerable<object> Query(global::UserCode.ManagedDb db){
                        return new[]{5,5,6}.Select(value=>new{Amount=value*1.5m}).Where(row=>row.Amount>0).Select(row=>new{row.Amount,Twice=row.Amount*2});
                    }
                }
            }
            namespace UserCode.Views{
                // 列を手書きする形。空のビューもこの形で書ける
                public readonly partial record struct Fixed(int Id){
                    public static IEnumerable<Fixed> Query(global::UserCode.ManagedDb db){
                        return [];
                    }
                }
                // 複数の文とreturnを持つ本体
                public readonly partial record struct Branches{
                    public static IEnumerable<object> Query(global::UserCode.ManagedDb db){
                        var values=new[]{4,7};
                        if(values.Length==0){return values.Select(value=>new{Value=value});}
                        return values.Where(value=>value>5).Select(value=>new{Value=value});
                    }
                }
            }
            namespace UserCode.Views{
                public readonly partial record struct Empty{
                    public static IEnumerable<object> Query(global::UserCode.ManagedDb db)=>[];
                }
            }
            """;
        const string probe="""
            using System.Linq;
            using Seamless.FLinq;
            namespace UserCode;
            public sealed class ViewProbe{
                [LinqIntercept(true)] private static long[] Read(ManagedDb db)=>db.Summary.Select(row=>row.Id).ToArray();
                public static long[] Run(ManagedDb db)=>Read(db);
                public static string[] Titles(ManagedDb db)=>db.Summary.OrderBy(row=>row.Id).Select(row=>row.Title??"").ToArray();
                public static decimal[] Twice(ManagedDb db)=>db.Sales.Summary.Select(row=>row.Twice).OrderBy(value=>value).ToArray();
                public static int FixedCount(ManagedDb db)=>db.Fixed.Count();
                public static int[] Branches(ManagedDb db)=>db.Branches.Select(row=>row.Value).ToArray();
            }
            """;
        {
            //推論できない空のビューは、本文の位置へ診断を出して生成しない
            var withEmpty=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Views/Generated.cs",views)]}};
            using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(withEmpty.Source);
            var diagnostic=Assert.Single(compiled.Result.Diagnostics,item=>item.Code=="SDB004");
            Assert.Contains("Empty",diagnostic.Message,StringComparison.Ordinal);
        }
        {
            //匿名型の結果から列を生成し、DB直下とスキーマ内の同名ビューを区別して実行する
            var source=views.Substring(0,views.LastIndexOf("namespace UserCode.Views{",StringComparison.Ordinal));
            definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Views/Generated.cs",source),new("ViewProbe.cs",probe)]}};
            using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(definition.Source);
            var errors=compiled.Result.Diagnostics.Where(item=>item.Severity=="Error").ToArray();
            Assert.True(errors.Length==0,string.Join("\n",errors.Select(item=>item.Code+" "+item.Message)));
            var plan=Assert.Single(compiled.Result.Plans,plan=>plan.MethodKey.Contains("ViewProbe.Read",StringComparison.Ordinal));
            Assert.Contains(plan.Nodes,node=>node.Label=="ViewDefinitionRoot"&&node.Properties.GetValueOrDefault("ExpansionStatus")=="ExpandedDefinitionWithSetBoundary");
            var context=new System.Runtime.Loader.AssemblyLoadContext("GeneratedViewRow",isCollectible:true);
            try{
                using var image=new MemoryStream(compiled.AssemblyBytes);
                var assembly=context.LoadFromStream(image);
                var row=assembly.GetType("UserCode.Views.Summary")!;
                Assert.Equal(new[]{("Id",typeof(long)),("Title",typeof(string))},row.GetConstructors().Single(constructor=>constructor.GetParameters().Length==2).GetParameters().Select(parameter=>(parameter.Name!,parameter.ParameterType)));
                var options=Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=65536,SegmentSize=1048576,MemorySize=1048576};
                using var db=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(assembly.GetType("UserCode.ManagedDb")!,[options])!;
                var probeType=assembly.GetType("UserCode.ViewProbe")!;
                //重複(2,2)は集合として除かれる
                Assert.Equal(new long[]{1,2,3},Assert.IsType<long[]>(probeType.GetMethod("Run")!.Invoke(null,[db])).Order());
                Assert.Equal(new[]{"row1","row2","row3"},Assert.IsType<string[]>(probeType.GetMethod("Titles")!.Invoke(null,[db])));
                //中間の匿名型は置き換えず、最後の結果だけが行型になる
                Assert.Equal(new[]{15m,18m},Assert.IsType<decimal[]>(probeType.GetMethod("Twice")!.Invoke(null,[db])));
                //列を手書きした空のビューと、複数returnの本体
                Assert.Equal(0,probeType.GetMethod("FixedCount")!.Invoke(null,[db]));
                Assert.Equal(new[]{7},Assert.IsType<int[]>(probeType.GetMethod("Branches")!.Invoke(null,[db])));
            }finally{context.Unload();}
        }
    }
}
