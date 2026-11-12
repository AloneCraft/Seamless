using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InferredReferenceResultKeepsNullableAnnotation(bool sequence){
        var definition=FunctionBoundaryDefinition("string","return \"initial\";","");
        var originalBody="return \"initial\";";
        var replacementBody="if(id<0){return(string?)null;}return \"text\";";
        if(sequence){
            definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>file with{Text=file.Text.Replace("ScalarFunction<string>","TableFunction<string>",StringComparison.Ordinal).Replace(originalBody,"return new string[]{\"initial\"};",StringComparison.Ordinal)}).ToArray()}};
            originalBody="return new string[]{\"initial\"};";
            replacementBody="return new string?[]{null,\"text\"};";
        }
        definition=definition with{Source=definition.Source with{Files=definition.Source.Files.Select(file=>{
            if(file.Path=="Database.cs"){return file with{Text="#nullable enable\n"+file.Text};}
            return file;
        }).ToArray()}};
        AssertFunctionResultCompilation(definition);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace(originalBody,replacementBody,StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        var options=new CSharpParseOptions(LanguageVersion.CSharp14,preprocessorSymbols:changed.Source.Defines??[]);
        var compilation=CSharpCompilation.Create("NullableFunctionResult",changed.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,options,file.Path)),global::Seamless.Management.SourceEditor.PlatformReferences());
        var method=compilation.GetTypeByMetadataName(changed.EntryType)!.GetMembers("Amount").OfType<IMethodSymbol>().Single();
        var wrapper=Assert.IsAssignableFrom<INamedTypeSymbol>(method.ReturnType);
        Assert.Equal(NullableAnnotation.Annotated,Assert.Single(wrapper.TypeArguments).NullableAnnotation);
        var declaration=(MethodDeclarationSyntax)method.DeclaringSyntaxReferences.Single().GetSyntax();
        var call=(InvocationExpressionSyntax)declaration.ExpressionBody!.Expression;
        var factory=Assert.IsAssignableFrom<IMethodSymbol>(compilation.GetSemanticModel(call.SyntaxTree).GetSymbolInfo(call).Symbol);
        Assert.Equal(NullableAnnotation.Annotated,Assert.Single(factory.TypeArguments).NullableAnnotation);
        var query=Designer.ReadQuery(changed,"Amount");
        var expected="string?";
        if(sequence){expected="global::System.Collections.Generic.IEnumerable<string?>";}
        Assert.Equal(expected,query.ReturnType);
        AssertFunctionResultCompilation(changed);
    }

    [Fact]
    public void ReturnedDelegateKeepsItsImplicitUserResultConversion(){
        var definition=FunctionBoundaryDefinition("global::System.Func<global::UserCode.Converted>","return ()=>new global::UserCode.Input(id);","""
            namespace UserCode{
             public static class Effects{public static int Count;public static bool Fail;}
             public readonly record struct Converted(int Value);
             public readonly record struct Input(int Value){
              public static implicit operator Converted(Input value){Effects.Count++;if(Effects.Fail){throw new System.InvalidOperationException("nested conversion");}return new(value.Value);}
             }
            }
            """);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);
        var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("new global::UserCode.Input(id)","new global::UserCode.Input(id+1)",StringComparison.Ordinal);
        Assert.NotEqual(part.Text,replacement);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.True(edit.Succeeded,string.Join("\n",edit.Diagnostics.Select(item=>item.Message)));
        var changed=edit.Definition!;
        Assert.Equal("global::System.Func<global::UserCode.Converted>",Designer.ReadQuery(changed,"Amount").ReturnType);
        Assert.False(string.IsNullOrWhiteSpace(Designer.ReadObjectDefinition(changed,target).ResultTypeInferenceReason));
        using var result=RunFunctionBoundary(changed,"""
            namespace UserCode{public static class BoundaryRun{
             public static string Run(){using var db=new ManagedDb(global::Seamless.Database.DatabaseContainerOptions.InMemory);
              var function=db.Amount(4).Value;var before=Effects.Count;var value=function().Value;var after=Effects.Count;Effects.Fail=true;string? error=null;
              try{_=function();}catch(System.InvalidOperationException exception){error=exception.Message;}
              return System.Text.Json.JsonSerializer.Serialize(new{before,value,after,total=Effects.Count,error});
             }
            }}
            """);
        Assert.Equal(0,result.RootElement.GetProperty("before").GetInt32());
        Assert.Equal(5,result.RootElement.GetProperty("value").GetInt32());
        Assert.Equal(1,result.RootElement.GetProperty("after").GetInt32());
        Assert.Equal(2,result.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("nested conversion",result.RootElement.GetProperty("error").GetString());
    }
}
