using System.Xml.Linq;
using Seamless.Management;
using Seamless.Management.Server;

namespace Management.Tests;

public sealed partial class SourceProjectBuildInspection{
    [Fact]
    public async Task SdkCustomColumnsAndChangedPrivateDependencyPublishAndRecover(){
        var root=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,"sdk-values-"+Guid.NewGuid().ToString("N"));
        var storeRoot=Path.Combine(root,"store");
        using(var host=InProcessManagementHost.Create(storeRoot)){
            var definition=Seamless.Management.DatabaseDesigner.Empty("values","BuiltValues");
            definition=definition with{Source=definition.Source with{Files=[..definition.Source.Files,new("Payload.cs","namespace UserCode;public readonly record struct Payload(decimal Amount);")]}};
            definition=Seamless.Management.DatabaseDesigner.Apply(definition,new("Rows","",[new("Id","int",true),new("Value","global::UserCode.Payload",false)],[]));
            var draft=await host.SaveDraftAsync(new(definition,0,0));var preview=await host.PreviewAsync(new(definition.Id,draft.Revision));
            Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
            Assert.Equal(OperationState.Succeeded,(await host.ApplyAsync(new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()))).State);
            var published=await host.GetDefinitionAsync(definition.Id);
            await host.CommitRowsAsync(new(definition.Database,published.Version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Insert,Values:ManagementJson.Element(new{Id=1,Value=new{Amount=7m}}))]));
            var document=await host.CreateSourceProjectAsync(new(Path.Combine(root,"project"),"BuiltValues",new(published.Definition,published.Version,0)));
            var design=Seamless.Management.DatabaseDesigner.Read(published.Definition,"Rows");
            var changed=Seamless.Management.DatabaseDesigner.Apply(published.Definition,design with{Columns=[..design.Columns,new("Price","global::ExternalBuildHelper.Price",false)],OriginalPath="Rows"});
            document=await host.SynchronizeSourceProjectAsync(new(document.Id,document.Source!,changed.Source with{Defines=document.Source!.Defines}));
            var helper=Path.Combine(root,"helper");Directory.CreateDirectory(helper);
            var helperProject=Path.Combine(helper,"ExternalBuildHelper.csproj");var helperSource=Path.Combine(helper,"Price.cs");
            await File.WriteAllTextAsync(helperProject,"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
            await File.WriteAllTextAsync(helperSource,"namespace ExternalBuildHelper;public readonly record struct Price(decimal Amount){public static int Generation=>1;}");
            var project=new XElement("Project",new XAttribute("Sdk","Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",new XElement("TargetFramework","net10.0"),new XElement("LangVersion","14"),new XElement("Nullable","enable"),new XElement("DebugType","portable"),
                    new XElement("EnableNETAnalyzers","false")),
                new XElement("ItemGroup",ReferencePaths().Select(path=>new XElement("Reference",new XAttribute("Include",Path.GetFileNameWithoutExtension(path)),new XElement("HintPath",path),new XElement("Private","true"))),
                    new XElement("ProjectReference",new XAttribute("Include",helperProject)),new XElement("Analyzer",new XAttribute("Include",Path.Combine(AppContext.BaseDirectory,"Seamless.Database.Generator.dll")))));
            await File.WriteAllTextAsync(document.ProjectPath,project.ToString());
            var configuration="Release";
#if DEBUG
            configuration="Debug";
#endif
            document=await host.OpenSourceProjectAsync(new(document.ProjectPath){Configuration=configuration});
            Assert.Contains(configuration.ToUpperInvariant(),document.Source!.Defines!);
            string? previousDependency=null;
            for(var generation=1;generation<=2;generation++){
                if(generation==2){
                    var files=document.Source!.Files.Select(file=>file with{Text=file.Text+"\n// rebuilt generation 2\n"}).ToArray();
                    document=await host.SynchronizeSourceProjectAsync(new(document.Id,document.Source!,document.Source! with{Files=files}));
                    await File.WriteAllTextAsync(helperSource,"namespace ExternalBuildHelper;public readonly record struct Price(decimal Amount){public static int Generation=>2;}");
                    await File.WriteAllTextAsync(helperProject,"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework><AssemblyVersion>2.0.0.0</AssemblyVersion></PropertyGroup></Project>");
                }
                draft=await host.SaveSourceProjectDraftAsync(document.Id);
                var artifacts=Path.Combine(root,"build-"+generation);await Build(document.ProjectPath,artifacts,configuration);
                var image=Path.Combine(artifacts,"bin","BuiltValues",configuration.ToLowerInvariant(),"BuiltValues.dll");
                var request=new SourceProjectBuildRequest(document.Id,image,draft.Definition.Source.Hash());var inspected=await host.InspectSourceProjectBuildAsync(request);
                var dependency=Assert.Single(inspected.Dependencies,item=>item.Name=="ExternalBuildHelper");Assert.False(dependency.Shared);
                if(generation==2){Assert.Equal("2.0.0.0",dependency.Version);}
                if(previousDependency is not null){Assert.NotEqual(previousDependency,dependency.Sha256);}previousDependency=dependency.Sha256;
                await host.ImportSourceProjectBuildAsync(new(request,inspected.BundleHash));
                var selected=new DraftKey(definition.Id,draft.Revision){BundleHash=inspected.BundleHash};preview=await host.PreviewAsync(selected);
                Assert.True(preview.CanApply,string.Join("\n",preview.Diagnostics.Select(item=>item.Message)));
                var operation=await host.ApplyAsync(new(definition.Id,draft.Revision,preview.Token,Guid.NewGuid()){BundleHash=inspected.BundleHash});Assert.True(operation.State==OperationState.Succeeded,operation.Error);
                published=await host.GetDefinitionAsync(definition.Id);Assert.Equal(inspected.BundleHash,published.ArtifactHash);
                var row=Assert.Single((await host.ReadRowsAsync(new(definition.Database,"Rows"))).Rows);Assert.Equal(7m,row.Values.GetProperty("value").GetProperty("amount").GetDecimal());
                var expected=0m;if(generation==2){expected=13m;}Assert.Equal(expected,row.Values.GetProperty("price").GetProperty("amount").GetDecimal());
                if(generation==1){await host.CommitRowsAsync(new(definition.Database,published.Version,Guid.NewGuid(),[new("Rows",TableRowChangeKind.Replace,row.Key,row.Stamp,ManagementJson.Element(new{Id=1,Value=new{Amount=7m},Price=new{Amount=13m}}))]));}
                document=await host.OpenSourceProjectAsync(new(document.ProjectPath){Configuration=configuration});
            }
        }
        using var reopened=InProcessManagementHost.Open(storeRoot);var restored=Assert.Single((await reopened.ReadRowsAsync(new("BuiltValues","Rows"))).Rows);
        Assert.Equal(7m,restored.Values.GetProperty("value").GetProperty("amount").GetDecimal());Assert.Equal(13m,restored.Values.GetProperty("price").GetProperty("amount").GetDecimal());
    }
}
