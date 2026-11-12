using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Seamless.Management;
using System.Xml.Linq;

namespace Management.Tests;

public sealed partial class SourceProjectBuildInspection{
    [Fact]public async Task TypeCompletionUsesTheResolvedProjectReferences(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"completion-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dependency=Path.Combine(root,"CompletionDependency.dll");
        var compilation=CSharpCompilation.Create("CompletionDependency",[CSharpSyntaxTree.ParseText("namespace CompletionDependency; public class External<T>{ public class Nested{} } internal class Hidden{}")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],new(OutputKind.DynamicallyLinkedLibrary));
        var result=compilation.Emit(dependency);Assert.True(result.Success,string.Join("\n",result.Diagnostics));
        var artifacts=Path.Combine(root,"artifacts");var project=Path.Combine(root,"CompletionConsumer.csproj");
        new XDocument(new XElement("Project",new XElement("PropertyGroup",new XElement("UseArtifactsOutput","true"),new XElement("ArtifactsPath",artifacts)))).Save(Path.Combine(root,"Directory.Build.props"));
        new XDocument(new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",new XElement("TargetFramework","net10.0")),
            new XElement("ItemGroup",new XElement("Reference",new XAttribute("Include","CompletionDependency"),new XElement("HintPath",dependency))))).Save(project);
        File.WriteAllText(Path.Combine(root,"Db.cs"),"namespace CompletionSample; internal static partial class DatabaseGenerator; public class LocalType{}");
        await Build(project,artifacts);
        using var service=new SourceProjectService();
        var document=await service.OpenSourceProjectAsync(new(project));
        Assert.Null(document.TypeCompletionDiagnostic);
        Assert.Contains("CompletionDependency.External<T>",document.ReferenceTypes);
        Assert.Contains("CompletionDependency.External<T>.Nested",document.ReferenceTypes);
        Assert.DoesNotContain("CompletionDependency.Hidden",document.ReferenceTypes);
        Assert.Contains("global::CompletionSample.LocalType",SourceStructure.Read(document.Source!).Types);
    }
}
