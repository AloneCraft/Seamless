using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Theory]
    [InlineData(false,"return (long)id+1;","ScalarFunction","long")]
    [InlineData(false,"if(id<0){return 1L;}return id;","ScalarFunction","long")]
    [InlineData(false,"if(id<0){return (long?)null;}return id;","ScalarFunction","long?")]
    [InlineData(false,"return new[]{id,id+1};","TableFunction","global::System.Collections.Generic.IEnumerable<int>")]
    [InlineData(true,"return id+1;","ScalarFunction","int")]
    [InlineData(true,"return new global::UserCode.Other[]{new(id)};","TableFunction","global::System.Collections.Generic.IEnumerable<global::UserCode.Other>")]
    public void FunctionBodyDeterminesItsResultType(bool sequence,string body,string kind,string resultType){
        var definition=FunctionResultFixture(sequence);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(part=>part.Role=="Member");
        var old="return id;";if(sequence){old="return new global::UserCode.Functions.Result[]{new(id)};";}
        Assert.Contains(old,part.Text);
        var replacement=part.Text.Replace(old,body,StringComparison.Ordinal);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        var query=Designer.ReadQuery(changed,"Amount");
        Assert.Equal(kind,query.Kind.ToString());
        Assert.Equal(resultType,query.ReturnType);
        Assert.Contains("/* retained argument */",changed.Source.Files.Single(file=>file.Path=="Database.cs").Text);
        Assert.Equal(definition.Source.Files.Single(file=>file.Path=="Other.cs"),changed.Source.Files.Single(file=>file.Path=="Other.cs"));
        AssertFunctionResultCompilation(changed);
    }

    [Fact]
    public void QueryBodyFormUsesTheSameInferredResultType(){
        var definition=FunctionResultFixture(false);
        var query=Designer.ReadQuery(definition,"Amount");
        var changed=Designer.ApplyQuery(definition,query with{Body="return (long)id+1;"});
        Assert.Equal("long",Designer.ReadQuery(changed,"Amount").ReturnType);
        AssertFunctionResultCompilation(changed);
    }

    [Fact]
    public void TargetTypedBodyKeepsItsDeclaredType(){
        var definition=FunctionResultFixture(false);
        var query=Designer.ReadQuery(definition,"Amount");
        var changed=Designer.ApplyQuery(definition,query with{Body="return default;"});
        Assert.Equal("int",Designer.ReadQuery(changed,"Amount").ReturnType);
        AssertFunctionResultCompilation(changed);
    }

    [Fact]
    public void ParameterTypeEditsReinferTheFunctionResult(){
        var definition=FunctionResultFixture(false);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var edit=Designer.ApplyObjectStructure(definition,baseline,baseline.Columns,[new("id","long")],baseline.ReturnType);
        Assert.True(edit.Succeeded);
        Assert.Equal("long",Designer.ReadQuery(edit.Definition!,"Amount").ReturnType);
        AssertFunctionResultCompilation(edit.Definition!);
    }

    [Fact]
    public void NestedWrapperUpdatesOnlyItsOuterDeclaration(){
        var definition=FunctionBoundaryDefinition("global::Seamless.Database.ScalarFunction<int>","return this.ScalarFunction<int>(()=>id);","");
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("this.ScalarFunction<int>(()=>id)","this.ScalarFunction<long>(()=>(long)id)",StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded);
        Assert.Equal("global::Seamless.Database.ScalarFunction<long>",Designer.ReadQuery(edit.Definition!,"Amount").ReturnType);
        AssertFunctionResultCompilation(edit.Definition!);
    }

    private static Definition FunctionResultFixture(bool sequence){
        var definition=Designer.Empty("function-results","Function results");
        var kind=DatabaseQueryKind.ScalarFunction;var type="int";var body="return id;";ColumnDesign[] columns=[];
        if(sequence){kind=DatabaseQueryKind.TableFunction;type="IEnumerable<Result>";body="return new global::UserCode.Functions.Result[]{new(id)};";columns=[new("Value","int",false)];}
        definition=Designer.ApplyQuery(definition,new("Amount","",kind,columns,[new("id","int")],type,body));
        return definition with{Source=definition.Source with{Files=[..definition.Source.Files.Select(file=>{
            if(file.Path=="Database.cs"){return file with{Text=file.Text.Replace("int @id","int /* retained argument */ @id",StringComparison.Ordinal)};}
            return file;
        }),new("Other.cs","namespace UserCode{public readonly record struct Other(int Code);}")]}};
    }

    private static void AssertFunctionResultCompilation(Definition definition){
        var parse=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:definition.Source.Defines??[]);
        var compilation=CSharpCompilation.Create("FunctionResult_"+Guid.NewGuid().ToString("N"),definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,parse,file.Path)),global::Seamless.Management.SourceEditor.PlatformReferences(),new(OutputKind.DynamicallyLinkedLibrary));
        using var image=new MemoryStream();var result=compilation.Emit(image);
        Assert.True(result.Success,string.Join("\n",result.Diagnostics));
    }
}
