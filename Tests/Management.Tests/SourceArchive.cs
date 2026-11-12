using Seamless.Management;

namespace Management.Tests;

public sealed class SourceArchive{
    [Fact]public void ExportImport(){
        var directory=Path.Combine(Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_TEST_ROOT")!,Guid.NewGuid().ToString("N"));var definition=NativeWorkflow.Database();Seamless.Management.SourceArchive.Export(directory,new(definition,8,12));
        var path=Path.Combine(directory,"schema.cs");File.AppendAllText(path,"\n// edited externally");var imported=Seamless.Management.SourceArchive.Import(Path.Combine(directory,"definition.seamless.json"));Assert.Equal(8,imported.BaseVersion);Assert.Equal(12,imported.DraftRevision);Assert.EndsWith("// edited externally",imported.Definition.Source.Files[0].Text);Assert.Throws<IOException>(()=>Seamless.Management.SourceArchive.Export(directory,new(definition,8,12)));
        Assert.Throws<ManagementException>(()=>Seamless.Management.SourceEditor.Validate(definition.Source with{Files=[new("../outside.cs","")]}));Assert.Throws<ManagementException>(()=>Seamless.Management.SourceEditor.Validate(definition.Source with{Files=[new("C:outside.cs","")]}));
    }
}
