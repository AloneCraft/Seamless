using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;
using PlanDocument=Seamless.Management.ExecutionPlanDocument;

namespace Management.Tests;

public sealed class QueryPlanSdkPaths{
    [Theory]
    [InlineData("C:\\build\\Queries\\dbo0\\Shared.cs",true)]
    [InlineData("C:\\build\\QUERIES\\DBO0\\SHARED.cs",true)]
    [InlineData("\\\\server\\build\\Queries\\dbo0\\Shared.cs",true)]
    [InlineData("/srv/build/Queries/dbo0/Shared.cs",true)]
    [InlineData("/srv/build/queries/dbo0/Shared.cs",false)]
    [InlineData("/srv/build/Other/dbo0/Shared.cs",false)]
    [InlineData("/srv/build/Other/Shared.cs",false)]
    [InlineData("relative/Queries/dbo0/Shared.cs",false)]
    [InlineData("Queries\\dbo0\\Shared.cs",true)]
    [InlineData("/srv/build/../Queries/dbo0/Shared.cs",false)]
    public void SdkPathsMatchTheWholeUniqueSourcePath(string path,bool matches){
        var(definition,plan)=Fixture("dbo0.Shared");
        var selection=Designer.SelectQueryPlans(definition,"dbo0.Shared",[plan with{CallSite=plan.CallSite! with{Path=path}}]);
        Assert.Equal(matches,selection.Plans.Length==1);
    }

    [Fact]
    public void SdkPathMappingKeepsScopeCallerSpanAndMethodHashes(){
        var(definition,original)=Fixture("dbo0.Shared");
        var plan=original with{CallSite=original.CallSite! with{Path="C:/build/Queries/dbo0/Shared.cs"}};
        Assert.Single(Designer.SelectQueryPlans(definition,"dbo0.Shared",[plan]).Plans);
        Assert.Empty(Designer.SelectQueryPlans(definition,"Shared",[plan]).Plans);
        Assert.Empty(Designer.SelectQueryPlans(definition,"dbo1.Shared",[plan]).Plans);
        foreach(var rejected in new[]{
            plan with{CallSite=plan.CallSite! with{Caller="Another.Caller()"}},
            plan with{CallSite=plan.CallSite! with{Start=plan.CallSite.Start+1}},
            plan with{CallSite=plan.CallSite! with{Length=plan.CallSite.Length-1}},
            plan with{SourceHash=new string('0',64)},
            plan with{MethodKey="PlanPaths.AnotherHelper.Rows()"}
        }){
            Assert.Empty(Designer.SelectQueryPlans(definition,"dbo0.Shared",[rejected]).Plans);
        }
    }

    [Fact]
    public void AmbiguousSdkSuffixIsRejectedWhileExactRelativePathStillMatches(){
        var(definition,plan)=Fixture("dbo0.Shared");
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Shared.cs","namespace Extra;public readonly record struct Other(int Value);")]}};
        Assert.Single(Designer.SelectQueryPlans(definition,"dbo0.Shared",[plan]).Plans);
        var absolute=plan with{CallSite=plan.CallSite! with{Path="C:/build/Queries/dbo0/Shared.cs"}};
        Assert.Empty(Designer.SelectQueryPlans(definition,"dbo0.Shared",[absolute]).Plans);
    }

    [Fact]
    public void SourceFileResolutionKeepsTheOriginalSnapshotPathAndText(){
        var file=new SourceFile("Views\\dbo0\\Shared.cs","// exact source text");
        var source=Designer.Empty("resolver","Resolver").Source with{Files=[file]};
        Assert.Same(file,Designer.ResolvePlanSourceFile(source,"C:/build/views/dbo0/shared.cs"));
        Assert.Same(file,Designer.ResolvePlanSourceFile(source,"Views/dbo0/Shared.cs"));
        Assert.Null(Designer.ResolvePlanSourceFile(source,"/build/views/dbo0/shared.cs"));
        Assert.Null(Designer.ResolvePlanSourceFile(source,"Other/Shared.cs"));
    }

    private static (Definition Definition,PlanDocument Plan) Fixture(string objectId){
        var definition=Designer.AddSchema(Designer.AddSchema(Designer.Empty("sdk-paths","SDK paths"),"dbo0"),"dbo1");
        foreach(var schema in new[]{"","dbo0","dbo1"}){
            definition=Designer.ApplyQuery(definition,new("Shared",schema,DatabaseQueryKind.View,[new("Id","int",false)],[],"",
                "return global::PlanPaths.SharedHelper.Rows().Select(value=>new Shared(value));"));
        }
        definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Helper.cs","using System.Linq;namespace PlanPaths;public sealed class SharedHelper{public static int[] Rows()=>new[]{1,2}.Where(value=>value>0).ToArray();}")]}};
        var trees=definition.Source.Files.Select(file=>CSharpSyntaxTree.ParseText(file.Text,path:file.Path)).ToArray();
        var compilation=CSharpCompilation.Create("SdkPaths",trees,global::Seamless.Management.SourceEditor.PlatformReferences());
        var helperTree=trees.Single(tree=>tree.FilePath=="Helper.cs");
        var method=helperTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var methodKey=compilation.GetSemanticModel(helperTree).GetDeclaredSymbol(method)!.ToDisplayString();
        var sourceHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(method.ToFullString())));
        var path="Queries/"+objectId.Replace('.','/')+".cs";
        var tree=trees.Single(tree=>tree.FilePath==path);
        // ビューは IReader への拡張プロパティ。呼び出し元は getter。
        var query=tree.GetRoot().DescendantNodes().OfType<AccessorDeclarationSyntax>().Single();
        var call=query.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node=>node.Expression.ToString()=="global::PlanPaths.SharedHelper.Rows");
        var caller=compilation.GetSemanticModel(tree).GetDeclaredSymbol(query)!.ToDisplayString();
        var plan=new PlanDocument(2,methodKey,"test",sourceHash,new string('1',64),[],JsonSerializer.SerializeToElement(new{})){
            CallSite=new(path,call.SpanStart,call.Span.Length,caller)
        };
        return(definition,plan);
    }
}
