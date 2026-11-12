using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Theory]
    [InlineData("")]
    [InlineData("Sales")]
    public void AnonymousViewColumnsFollowRepeatedEdits(string schema){
        var definition=Designer.Empty("view-inference","View inference");
        if(schema.Length>0){definition=Designer.AddSchema(definition,schema);}
        const string first="return new[]{new{Id=1,Title=(string?)null}};";
        var design=new QueryDesign("Summary",schema,DatabaseQueryKind.View,[],[],"",first);
        definition=Designer.AssignStableIdentities(Designer.ApplyQuery(definition,design));
        //新しく書き出すファイルはOSに関係なくLFで作り、LFのファイルへ差し込む断片もLFにそろえる(WindowsでCRLFが混ざると両OSで成果物ハッシュが変わる)
        Assert.All(definition.Source.Files,file=>Assert.DoesNotContain('\r',file.Text));
        var before=Designer.ReadStableIdentities(definition);
        var query=Designer.ReadQuery(definition,design.Path);
        Assert.Equal(new[]{("Id","int"),("Title","string?")},query.Columns.Select(column=>(column.Name,column.Type)));
        Assert.Contains(first,query.Body);
        AssertFunctionResultCompilation(definition);

        const string second="/* body retained */ return new[]{new{Id=2L,Total=3m}};";
        definition=Designer.ApplyQuery(definition,query with{Body=second});
        var target=Designer.Objects(definition).Single(item=>item.Id==design.Path);
        var baseline=Designer.ReadObjectDefinition(definition,target);
        Assert.Null(baseline.ResultTypeInferenceReason);
        Assert.Equal(before.Object(design.Path),target.StableId);
        Assert.Equal(before.Column(design.Path,"Id"),Designer.ReadStableIdentities(definition).Column(design.Path,"Id"));
        Assert.Equal(new[]{("Id","long"),("Total","decimal")},baseline.Columns.Select(column=>(column.Name,column.Type)));
        AssertFunctionResultCompilation(definition);

        var part=Assert.Single(baseline.Parts);
        const string third="return new[]{new{Id=4,Count=5}};";
        var source=Designer.ApplyObjectSource(definition,baseline,part.Id,part.Text.Replace(second,third,StringComparison.Ordinal));
        Assert.True(source.Succeeded,string.Join("\n",source.Diagnostics.Select(diagnostic=>diagnostic.Message)));
        var updated=source.Definition!;
        Assert.Equal(new[]{("Id","int"),("Count","int")},Designer.ReadQuery(updated,design.Path).Columns.Select(column=>(column.Name,column.Type)));
        Assert.Contains(third,Designer.ReadQuery(updated,design.Path).Body);
        AssertFunctionResultCompilation(updated);

        baseline=Designer.ReadObjectDefinition(updated,target);
        part=Assert.Single(baseline.Parts);
        var invalid=Designer.ApplyObjectSource(updated,baseline,part.Id,part.Text.Replace(third,"return 42;",StringComparison.Ordinal));
        Assert.False(invalid.Succeeded);
        Assert.Equal("ViewResult",Assert.Single(invalid.Diagnostics).Code);
        Assert.Contains(third,Designer.ReadQuery(updated,design.Path).Body);
    }

    [Fact]
    public void AnonymousBodySwitchesAHandwrittenPartialViewToInferredColumns(){
        var definition=Designer.Empty("view-inference-partial","Partial view inference");
        const string body="return new[]{new{Id=7L,Name=\"example\"}};";
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,
            new("Views/Row.cs","namespace UserCode.Views{[global::System.Serializable] public readonly partial record struct Summary([property:global::System.ComponentModel.Description(\"kept\")] int Id);}"),
            new("Views/Query.cs","namespace UserCode.Views{public readonly partial record struct Summary{/* keep */public static global::System.Collections.Generic.IEnumerable<Summary> Query(global::UserCode.ManagedDb container){return [];}}}")
        ]}};
        var query=Designer.ReadQuery(definition,"Summary");
        Assert.Equal(new[]{("Id","int")},query.Columns.Select(column=>(column.Name,column.Type)));
        var updated=Designer.ApplyQuery(definition,query with{Body=body});
        //列の宣言は外れ、属性は型に残る。Queryは匿名型の列挙を返す形になる
        var row=updated.Source.Files.Single(file=>file.Path=="Views/Row.cs").Text;
        Assert.Contains("[global::System.Serializable] public readonly partial record struct Summary;",row);
        Assert.DoesNotContain("Description",row);
        var text=updated.Source.Files.Single(file=>file.Path=="Views/Query.cs").Text;
        Assert.Contains("/* keep */",text);
        Assert.Contains("IEnumerable<object> Query(",text);
        Assert.Equal(new[]{("Id","long"),("Name","string")},Designer.ReadQuery(updated,"Summary").Columns.Select(column=>(column.Name,column.Type)));
        Assert.Contains(body,Designer.ReadQuery(updated,"Summary").Body);
        AssertFunctionResultCompilation(updated);
    }

    [Fact]
    public void EditedAnonymousViewCompilesAndExpandsAtItsGeneratedGetter(){
        var definition=Designer.Empty("view-inference-runtime","View inference runtime");
        definition=Designer.ApplyQuery(definition,new("Summary","",DatabaseQueryKind.View,[],[],"","return new[]{new{Id=1}};"));
        var query=Designer.ReadQuery(definition,"Summary");
        definition=Designer.ApplyQuery(definition,query with{Body="return new[]{new{Id=7L},new{Id=7L},new{Id=8L}};"});
        const string probe="""
            using System.Linq;
            using Seamless.FLinq;
            namespace UserCode;
            public sealed class ProjectionProbe{
                [LinqIntercept(true)] private static long[] Read(ManagedDb db)=>db.Summary.Select(row=>row.Id).ToArray();
                public static long[] Run(ManagedDb db)=>Read(db);
            }
            """;
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("ProjectionProbe.cs",probe)]}};
        using var compiled=new Seamless.Management.Server.RuntimeCompiler().Compile(definition.Source);
        var errors=compiled.Result.Diagnostics.Where(diagnostic=>diagnostic.Severity=="Error").ToArray();
        Assert.True(errors.Length==0,string.Join("\n",errors.Select(diagnostic=>diagnostic.Code+" "+diagnostic.Message)));
        var plan=Assert.Single(compiled.Result.Plans,plan=>plan.MethodKey.Contains("ProjectionProbe.Read",StringComparison.Ordinal));
        Assert.Contains(plan.Nodes,node=>node.Label=="ViewDefinitionRoot"&&node.Properties.GetValueOrDefault("ExpansionStatus")=="ExpandedDefinitionWithSetBoundary");
        var context=new System.Runtime.Loader.AssemblyLoadContext("ViewResultSync",isCollectible:true);
        try{
            using var image=new MemoryStream(compiled.AssemblyBytes);
            var assembly=context.LoadFromStream(image);
            var options=Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=65536,SegmentSize=1048576,MemorySize=1048576};
            using var db=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(assembly.GetType("UserCode.ManagedDb")!,[options])!;
            using var read=db.BeginReadSession();
            var values=Assert.IsType<long[]>(assembly.GetType("UserCode.ProjectionProbe")!.GetMethod("Run")!.Invoke(null,[db]));
            Assert.Equal(new long[]{7,8},values.Order());
        }finally{context.Unload();}
    }

    [Fact]
    public void BodiesReturningTheRowKeepHandwrittenColumns(){
        var definition=Designer.ApplyQuery(Designer.Empty("view-manual-columns","Handwritten columns"),
            new("Summary","",DatabaseQueryKind.View,[new("Id","int",false)],[],"","return [];"));
        var query=Designer.ReadQuery(definition,"Summary");
        var updated=Designer.ApplyQuery(definition,query with{Body="return new[]{new Summary(123)};"});
        var target=Assert.Single(Designer.Objects(updated));
        Assert.Equal("ViewDeclaredResult",Designer.ReadObjectDefinition(updated,target).ResultTypeInferenceReason);
        Assert.Equal("int",Assert.Single(Designer.ReadQuery(updated,"Summary").Columns).Type);
        var text=updated.Source.Files.Single(file=>file.Path=="Views/Summary.cs").Text;
        Assert.Contains("record struct @Summary(int @Id)",text);
        Assert.Contains("IEnumerable<global::UserCode.Views.@Summary> Query(",text);
        Assert.Contains("new Summary(123)",text);
        AssertFunctionResultCompilation(updated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ViewBodyIsReadFromTheWholeDeclarationSymbol(bool splitPartial){
        var definition=Designer.Empty("view-body-reading","View body reading");
        const string query="""
            public static global::System.Collections.Generic.IEnumerable<Summary> Query(global::UserCode.ManagedDb container)=>/* before body */new Summary[]{new(7)}/* after body */;
            """;
        SourceFile[] files;
        if(splitPartial){
            files=[new("Views/A.Summary.cs","namespace UserCode.Views{public readonly partial record struct Summary(int Id);}"),
                new("Views/Z.Summary.Query.cs","namespace UserCode.Views{public readonly partial record struct Summary{"+query+"}}")];
        }else{
            files=[new("Views/Summary.cs","namespace UserCode.Views{public readonly struct Summary{public int Id{get;}public Summary(int id){this.Id=id;}"+query+"}}")];
        }
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,..files]}};
        AssertFunctionResultCompilation(definition);
        var before=definition.Source.Files.ToArray();
        var result=Designer.ReadQuery(definition,"Summary");
        Assert.Equal(DatabaseQueryKind.View,result.Kind);
        Assert.Equal("Summary",result.Name);
        Assert.Equal(new[]{new ColumnDesign("Id","int",false)},result.Columns);
        Assert.Contains("/* before body */",result.Body,StringComparison.Ordinal);
        Assert.Contains("new Summary[]{new(7)}",result.Body,StringComparison.Ordinal);
        Assert.Contains("/* after body */",result.Body,StringComparison.Ordinal);
        Assert.Equal(before,definition.Source.Files);
    }
}
