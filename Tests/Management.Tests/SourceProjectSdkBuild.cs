using System.Security.Cryptography;
using System.Xml.Linq;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

// 段階7(2)：DLL の場所を空にして検査を頼むと、Manager がソースプロジェクトを SDK でビルドし、その出力(IDE のビルドと同じ TargetPath)を検査する。
// 設計：Document/refactoring-designs/20261115-Managerの設計と実装の照合.md の段階7。
public sealed partial class SourceProjectBuildInspection{
    [Fact]
    public async Task EmptyAssemblyPathBuildsTheProjectWithTheSdkThenInspectsIt(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"sdk-build-"+Guid.NewGuid().ToString("N"));
        using var host=InProcessManagementHost.Create(Path.Combine(root,"store"));
        IManagementOperations operations=host;IManagementSourceOperations sources=host;
        Assert.Contains("OwnerSourceSdkBuildV1",(await operations.InfoAsync()).Capabilities);
        var definition=Seamless.Management.DatabaseDesigner.Empty("sdkbuilt","SdkBuilt");
        definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true),new("Value","int",false)],[]));
        var draft=await operations.SaveDraftAsync(new(definition,0,0));
        var preview=await operations.PreviewAsync(new(definition.Id,draft.Revision));
        Assert.Equal(OperationState.Succeeded,(await operations.ApplyAsync(new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()))).State);
        var published=await operations.GetDefinitionAsync(definition.Id);
        var document=await sources.CreateSourceProjectAsync(new(Path.Combine(root,"project"),"SdkBuilt",new(published.Definition,published.Version,0)));
        draft=await sources.SaveSourceProjectDraftAsync(document.Id);
        // パッケージの置き場に頼らず、試験の部品を直接参照する(既存の検査の試験と同じ)。
        var references=Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll").Select(path=>
            new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","true")));
        var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
            new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),
                new XElement("DebugType","portable"),new XElement("EnableNETAnalyzers","false"),new XElement("NuGetAudit","false"),new XElement("DefineConstants",string.Join(";",document.Source!.Defines??[]))),
            new XElement("ItemGroup",references,new XElement("Analyzer",new XAttribute("Include",Path.Combine(AppContext.BaseDirectory,"Seamless.Database.Generator.dll")))));
        await File.WriteAllTextAsync(document.ProjectPath,project.ToString());
        var request=new SourceProjectBuildRequest(document.Id,"",draft.Definition.Source.Hash());
        var result=await sources.InspectSourceProjectBuildAsync(request);
        Assert.Equal(definition.Id,result.DefinitionId);
        Assert.Equal(draft.Revision,result.DraftRevision);
        var image=Path.Combine(Path.GetDirectoryName(document.ProjectPath)!,"bin","Debug","net10.0","SdkBuilt.dll");
        Assert.True(File.Exists(image),"SDK のビルドの出力がありません: "+image);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(image))),result.AssemblySha256);
        // 取り込みも同じく空の場所で頼める(ビルドし直しても同じ DLL なので同じ成果物になる)。
        var imported=await sources.ImportSourceProjectBuildAsync(new(request,result.BundleHash));
        Assert.Equal(result.BundleHash,imported.Build.BundleHash);
        // ビルドが通らなければ、SDK の出力を付けて止める(ソースは変えず、プロジェクトにわざと失敗する手順を足す)。
        var failing=new XElement(project);failing.Add(new XElement("Target",new XAttribute("Name","SeamlessIntentionalFailure"),new XAttribute("BeforeTargets","CoreCompile"),
            new XElement("Error",new XAttribute("Text","seamless-intentional-build-failure"))));
        await File.WriteAllTextAsync(document.ProjectPath,failing.ToString());
        try{
            var error=await Assert.ThrowsAsync<ManagementException>(()=>sources.InspectSourceProjectBuildAsync(request));
            Assert.Equal("BuildFailed",error.Code);
            Assert.Contains("seamless-intentional-build-failure",error.Message);
        }finally{await File.WriteAllTextAsync(document.ProjectPath,project.ToString());}
    }
}
