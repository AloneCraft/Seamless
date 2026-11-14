using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Fact]public void ViewsAndFunctionsCompileAndExecuteAcrossMixedSchemas(){
        var definition=Seamless.Management.DatabaseDesigner.Empty("queries","Queries");
        definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo0");definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo1");
        foreach(var schema in new[]{"","dbo0","dbo1"}){
            // 本体は生成される入口(スキーマの I<スキーマ>Reader)を q で読む。
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items",schema,[new("Id","int",true),new("Value","decimal",false)],[]));
            var view=new QueryDesign("ItemList",schema,DatabaseQueryKind.View,[new("Id","int",false),new("Value","decimal",false)],[],"","return q.Items.Union(q.Items).Select(row=>new ItemList(row.Id,row.Value));");
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,view);
            var read=Seamless.Management.DatabaseDesigner.ReadQuery(definition,view.Path);Assert.Contains("Union",read.Body);
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,read with{Body=read.Body+"\n// preserve this body"});
            Assert.Contains("// preserve this body",Seamless.Management.DatabaseDesigner.ReadQuery(definition,view.Path).Body);
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Amount",schema,DatabaseQueryKind.ScalarFunction,[],[new("id","int")],"decimal","return q.Items.Where(row=>row.Id==id).Sum(row=>row.Value);"));
            definition=Seamless.Management.DatabaseDesigner.ApplyQuery(definition,new("Find",schema,DatabaseQueryKind.TableFunction,[new("Id","int",false)],[new("minimum","decimal")],"ISet<FindResult>","return q.Items.Where(row=>row.Value>=minimum).Select(row=>new FindResult(row.Id));"));
            Assert.Contains("ISet",Seamless.Management.DatabaseDesigner.ReadQuery(definition,string.Join(".",new[]{schema,"Find"}.Where(value=>value.Length>0))).ReturnType);
        }
        Assert.Equal(12,Seamless.Management.DatabaseDesigner.Objects(definition).Length);
        const string probe="""
            using System.Linq;
            namespace UserCode;
            public static class Probe{
                // 行の投入は試験側の取引で行う(管理サーバーの中の利用者コードは取引を開けない)。
                public static void Seed(ManagedDb database){database.Items.Add(new(1,10m));database.dbo0.Items.Add(new(1,20m));database.dbo1.Items.Add(new(1,30m));
                }
                public static bool Run(ManagedDb database){
                    using var r=database.BeginRead();
                    return r.ItemList.Single().Value==10m&&r.dbo0.ItemList.Single().Value==20m&&r.dbo1.ItemList.Single().Value==30m
                        &&r.ItemList.Count==1&&r.ItemList.Contains(new(1,10m))
                        &&r.Amount(1)==10m&&r.dbo0.Amount(1)==20m&&r.dbo1.Amount(1)==30m
                        &&r.Find(5m).Single().Id==1&&r.dbo0.Find(15m).Single().Id==1&&r.dbo1.Find(25m).Single().Id==1;
                }
            }
            """;
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Probe.cs",probe)]}};
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        var context=new ManagedLoadContext();
        try{
            using var image=new MemoryStream(compiled.AssemblyBytes);
            var assembly=context.LoadFromStream(image);
            using var database=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(assembly.GetType("UserCode.ManagedDb")!,[Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=65536,SegmentSize=1048576,MemorySize=1048576}])!;
            var type=assembly.GetType("UserCode.Probe")!;
            using(var write=database.BeginTransaction()){type.GetMethod("Seed")!.Invoke(null,[database]);write.Commit();}
            Assert.Equal(true,type.GetMethod("Run")!.Invoke(null,[database]));
            // 名前で呼ぶ入口(Manager の実行が使う)からも同じ値になる。
            using var amount=database.Queries.Single(query=>query.Name=="dbo1.Amount").Execute(database,1);
            Assert.Equal(30m,amount.Value);
        }finally{context.Unload();}
    }
    [Fact]public void EmptySchemasPersistAndCoexistWithRootTables(){
        var original=Seamless.Management.DatabaseDesigner.Empty("mixed","Mixed");
        var definition=Seamless.Management.DatabaseDesigner.AddSchema(original,"dbo0");
        definition=Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo1");
        Assert.Equal(new[]{"dbo0","dbo1"},Seamless.Management.DatabaseDesigner.Schemas(definition));
        foreach(var schema in new[]{"","dbo0","dbo1"}){
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Items",schema,[new("Id","int",true)],[]));
        }
        Assert.Equal(new[]{"Items","dbo0.Items","dbo1.Items"},Seamless.Management.DatabaseDesigner.Tables(definition).Order(StringComparer.Ordinal));
        using var compiled=new RuntimeCompiler().Compile(definition.Source);
        Assert.DoesNotContain(compiled.Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.AddSchema(definition,"Items")).Code);
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.AddSchema(definition,"dbo0")).Code);
        Assert.Equal("Conflict",Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(definition,new("Other","Items",[new("Id","int",true)],[]))).Code);
        var restored=System.Text.Json.JsonSerializer.Deserialize<Definition>(System.Text.Json.JsonSerializer.Serialize(definition,ManagementJson.Options),ManagementJson.Options)!;
        Assert.Equal(new[]{"dbo0","dbo1"},Seamless.Management.DatabaseDesigner.Schemas(restored));
    }
    private static ColumnDesign Rename(ColumnDesign column,string previous,string next){var name=column.Name;if(name==previous){name=next;}return column with{Name=name};}
    [Fact]public void PreviewIsActualSourceAndKeepsUnrelatedCode(){
        var original=Seamless.Management.DatabaseDesigner.Empty("new","Example");
        original=original with{Source=original.Source with{Files=original.Source.Files.Select(file=>file with{Text=file.Text+"\n// keep this comment\n"}).ToArray()}};
        var first=Seamless.Management.DatabaseDesigner.Apply(original,new("Customers","Sales",[new("Id","int",true),new("Name","string",false)],[]));
        Assert.Contains("Sales.Customers",Seamless.Management.DatabaseDesigner.Tables(first));
        Assert.Contains(first.Source.Files,file=>file.Text.Contains("// keep this comment"));
        Assert.DoesNotContain(new RuntimeCompiler().Compile(first.Source).Result.Diagnostics,item=>item.Severity=="Error");
        var read=Seamless.Management.DatabaseDesigner.Read(first,"Sales.Customers");Assert.Equal(new[]{"Id","Name"},read.Columns.Select(column=>column.Name));
        var second=Seamless.Management.DatabaseDesigner.Apply(first,read with{Name="Clients",Columns=read.Columns.Select(column=>Rename(column,"Name","DisplayName")).ToArray()});
        Assert.Contains("Sales.Clients",Seamless.Management.DatabaseDesigner.Tables(second));Assert.DoesNotContain(new RuntimeCompiler().Compile(second.Source).Result.Diagnostics,item=>item.Severity=="Error");
        Assert.Empty(second.Migration!.Tables);Assert.Equal("Sales.Clients",Assert.Single(second.Migration.NewTables!));
    }
    [Fact]public void RepeatedEditsMapToOriginalPublishedColumns(){
        var original=NativeWorkflow.Database();var read=Seamless.Management.DatabaseDesigner.Read(original,"Rows");
        var first=Seamless.Management.DatabaseDesigner.Apply(original,read with{Columns=read.Columns.Select(column=>Rename(column,"Value","Amount")).ToArray()});
        var next=Seamless.Management.DatabaseDesigner.Read(first,"Rows");var second=Seamless.Management.DatabaseDesigner.Apply(first,next with{Columns=next.Columns.Select(column=>Rename(column,"Amount","Total")).ToArray()});
        Assert.Equal("Value",Assert.Single(second.Migration!.Tables).Columns.Single(column=>column.Target=="Total").Source);
        Assert.DoesNotContain(new RuntimeCompiler().Compile(second.Source).Result.Diagnostics,item=>item.Severity=="Error");
    }
    [Fact]public void MovingTableAndRenamingRelationshipPreservesSourceMeaning(){
        var original=NativeWorkflow.Database();var design=Seamless.Management.DatabaseDesigner.Read(original,"Rows");var moved=Seamless.Management.DatabaseDesigner.Apply(original,design with{Schema="Sales",Name="Entries"});
        Assert.Equal("Sales.Entries",Assert.Single(Seamless.Management.DatabaseDesigner.Tables(moved)));Assert.Equal("Rows",Assert.Single(moved.Migration!.Tables).Source);Assert.DoesNotContain(new RuntimeCompiler().Compile(moved.Source).Result.Diagnostics,item=>item.Severity=="Error");
        var related=new Definition("relations","Relations",DefinitionKind.Database,"Relations.Db","",new("1",[new("relations.cs",NativeRelations.Source)],[]));var next=Seamless.Management.DatabaseDesigner.Apply(related,Seamless.Management.DatabaseDesigner.Read(related,"As") with{Name="Renamed"});
        Assert.Equal("Renamed",Assert.Single(Seamless.Management.DatabaseDesigner.Read(next,"Bs").ForeignKeys).ReferencedTable);Assert.DoesNotContain(new RuntimeCompiler().Compile(next.Source).Result.Diagnostics,item=>item.Severity=="Error");
    }
    [Fact]public void SharedRecordTypesAreNotSilentlyChanged(){
        var original=NativeWorkflow.Database();original=original with{Source=original.Source with{Files=original.Source.Files.Select(file=>file with{Text=file.Text.Replace("public Table<Keys.Row,Records.Row> Rows=>","public Table<Keys.Row,Records.Row> Other=>this.Table<Keys.Row,Records.Row>(row=>new(row.Id));public Table<Keys.Row,Records.Row> Rows=>")}).ToArray()}};
        var design=Seamless.Management.DatabaseDesigner.Read(original,"Rows");var error=Assert.Throws<ManagementException>(()=>Seamless.Management.DatabaseDesigner.Apply(original,design with{Columns=design.Columns.Append(new("Added","int",false)).ToArray()}));Assert.Equal("Unsupported",error.Code);
    }
}
