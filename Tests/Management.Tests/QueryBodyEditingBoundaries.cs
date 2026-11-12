using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Management;
using Seamless.Management.Server;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class QueryBodyEditingBoundaries{
    [Theory][InlineData(false)][InlineData(true)]
    public void KindChangesReplaceTheDefinitionArgumentWithoutMovingOrReevaluatingTheName(bool sequence){
        var definition=Fixture();var member="public global::Seamless.Database.ScalarFunction<int> Amount(int id=41)=>this.ScalarFunction<int>(name:this.NextName(),definition:[Keep(\"lambda\")] ()=>id+1);";var originalOperation="database.Amount().Value";
        if(sequence){definition=WithFile(definition,"Result.cs","namespace UserCode.Functions{public readonly record struct Result(int Value);}");member="public global::Seamless.Database.TableFunction<global::UserCode.Functions.Result> Amount(int id=41)=>this.TableFunction<global::UserCode.Functions.Result>(name:this.NextName(),definition:[Keep(\"lambda\")] ()=>new global::UserCode.Functions.Result[]{new(id+1)});";originalOperation="database.Amount().Single().Value";}
        definition=WithMember(definition,member);Assert.Equal(new[]{42,1},Execute(WithProbe(definition,originalOperation)));var query=Designer.ReadQuery(definition,"Amount");
        var next=DatabaseQueryKind.TableFunction;var type="IEnumerable<NewResult>";ColumnDesign[] columns=[new("Value","int",false)];var body="return new global::UserCode.Functions.NewResult[]{new(id+2)};";var operation="database.Amount().Single().Value";
        if(sequence){next=DatabaseQueryKind.ScalarFunction;type="int";columns=[];body="return id+2;";operation="database.Amount().Value";}
        var updated=Designer.ApplyQuery(definition,query with{Kind=next,ReturnType=type,Columns=columns,Body=body});var text=updated.Source.Files.Single(file=>file.Path=="Database.cs").Text;Assert.Contains("name:this.NextName(),definition:[Keep(\"lambda\")]",text);Assert.Contains("int id=41",text);Assert.Equal(new[]{43,1},Execute(WithProbe(updated,operation)));
    }
    [Theory]
    [InlineData("ScalarFunction",false)][InlineData("ScalarFunction",true)]
    [InlineData("TableFunction",false)][InlineData("TableFunction",true)]
    public void BodyChangesKeepLambdaAttributesLineCommentsAndArgumentEvaluation(string kind,bool nameFirst){
        var definition=Fixture();var receiver="this";var expression="id+1";var operation="database.Amount().Value";var name="Amount";
        var member="public global::Seamless.Database.ScalarFunction<int> Amount([Keep(\"parameter\")] int id=41)=>this.ScalarFunction<int>(ARGUMENTS);";
        if(kind=="TableFunction"){
            expression="new global::UserCode.Functions.Result[]{new(id+1)}";operation="database.Find().Single().Value";name="Find";
            member="public global::Seamless.Database.TableFunction<global::UserCode.Functions.Result> Find([Keep(\"parameter\")] int id=41)=>this.TableFunction<global::UserCode.Functions.Result>(ARGUMENTS);";
            definition=WithFile(definition,"Result.cs","namespace UserCode.Functions{public readonly record struct Result(int Value);}");
        }
        var lambda="[global::UserCode.Keep(\"lambda\")] () => // before expression\r\n"+expression+" // after expression\r\n";
        var arguments="definition:"+lambda+",name:"+receiver+".NextName()";
        if(nameFirst){arguments="name:"+receiver+".NextName(),definition:"+lambda;}
        definition=WithMember(definition,member.Replace("ARGUMENTS",arguments,StringComparison.Ordinal)+" /* outside invocation */");
        definition=WithProbe(definition,operation);Assert.Equal(new[]{42,1},Execute(definition));
        var query=Designer.ReadQuery(definition,name);var updated=Designer.ApplyQuery(definition,query with{Body=query.Body.Replace("+1","+2",StringComparison.Ordinal)});
        var text=string.Join("\n",updated.Source.Files.Select(file=>file.Text));Assert.Contains("[global::UserCode.Keep(\"lambda\")]",text);Assert.Contains("// before expression",text);Assert.Contains("// after expression",text);Assert.Contains("/* outside invocation */",text);Assert.Contains("name:"+receiver+".NextName()",text);
        Assert.Contains("[Keep(\"parameter\")] int id=41",text);
        Assert.Equal(new[]{43,1},Execute(updated));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ConditionalContainerDeclarationsRemainInsideTheirBranches(bool enabled){
        var definition=Fixture();if(enabled){definition=definition with{Source=definition.Source with{Defines=["ALT"]}};}
        const string branches="#if ALT\nvar container=this;\n#else\nvar container=this;\n#endif";
        definition=WithMember(definition,"public int Value=>42;public global::Seamless.Database.ScalarFunction<int> Amount()=>this.ScalarFunction<int>(()=>{\n"+branches+"\nreturn container.Value;\n});");definition=WithProbe(definition,"database.Amount().Value");Assert.Equal(new[]{42,0},Execute(definition));
        var query=Designer.ReadQuery(definition,"Amount");var updated=Designer.ApplyQuery(definition,query with{Body=query.Body.Replace("container.Value","container.Value+1",StringComparison.Ordinal)});
        Assert.Contains(branches,updated.Source.Files.Single(file=>file.Path=="Database.cs").Text);Assert.Equal(new[]{43,0},Execute(updated));
    }

    [Fact]public void ParamsModifierAndArrayArgumentKeepTheirMeaningWhenOnlyTheBodyChanges(){
        var definition=WithProbe(WithMember(Fixture(),"public global::Seamless.Database.ScalarFunction<int> Amount(params int[] values)=>this.ScalarFunction<int>(()=>values[0]+1);"),"database.Amount(41,7).Value");Assert.Equal(new[]{42,0},Execute(definition));
        var query=Designer.ReadQuery(definition,"Amount");var updated=Designer.ApplyQuery(definition,query with{Body=query.Body.Replace("+1","+2",StringComparison.Ordinal)});Assert.Contains("params int[] values",updated.Source.Files.Single(file=>file.Path=="Database.cs").Text);Assert.Equal(new[]{43,0},Execute(updated));
    }

    private static Definition Fixture()=>WithMember(WithFile(Designer.Empty("boundaries","Boundaries"),"Keep.cs","namespace UserCode{public sealed class KeepAttribute(string value):global::System.Attribute{}}"),"public int Calls;public string NextName(){this.Calls++;return \"kept\";}");
    private static Definition WithMember(Definition definition,string text)=>definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{if(file.Path=="Database.cs"){return file with{Text=file.Text.Insert(file.Text.LastIndexOf('}'),text+"\n")};}return file;}).ToArray()}};
    private static Definition WithFile(Definition definition,string path,string text)=>definition with{Source=definition.Source with{Files=[..definition.Source.Files,new(path,text)]}};
    private static Definition WithProbe(Definition definition,string expression)=>WithFile(definition,"Probe.cs","using System.Linq;namespace UserCode{public static class BoundaryProbe{public static int[] Run(ManagedDb database){var value="+expression+";return new[]{value,database.Calls};}}}");
    private static int[] Execute(Definition definition){
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:definition.Source.Defines??[]);
        var compilation=CSharpCompilation.Create("BodyBoundary_"+Guid.NewGuid().ToString("N"),definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,parse,file.Path)),global::Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        GeneratorDriver driver=CSharpGeneratorDriver.Create([new Seamless.Database.Generator.DatabaseSchemaGenerator().AsSourceGenerator(),new Seamless.Database.Generator.DatabaseContainerGenerator().AsSourceGenerator()],parseOptions:parse);driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);Assert.DoesNotContain(diagnostics,item=>item.Severity==DiagnosticSeverity.Error);
        using var image=new MemoryStream();var result=output.Emit(image);Assert.True(result.Success,string.Join("\n",result.Diagnostics));image.Position=0;var context=new ManagedLoadContext();
        try{var assembly=context.LoadFromStream(image);using var database=(Seamless.Database.DatabaseContainer)Activator.CreateInstance(assembly.GetType("UserCode.ManagedDb")!,[Seamless.Database.DatabaseContainerOptions.InMemory with{IndexSize=1024,PageSize=65536,SegmentSize=1048576,MemorySize=1048576}])!;using var read=database.BeginReadSession();return(int[])assembly.GetType("UserCode.BoundaryProbe")!.GetMethod("Run")!.Invoke(null,[database])!;}finally{context.Unload();}
    }
}
