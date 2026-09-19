namespace Seamless.Database.Tool;

internal static class GeneratedProjectWriter {
    private static readonly Encoding Utf8Bom=new UTF8Encoding(true);

    public static async Task<string> WriteAsync(
        GeneratedDatabase database,
        string projectFile,
        string configuration,
        CancellationToken cancellationToken=default)=>
        (await WriteBatchAsync([database],projectFile,configuration,cancellationToken).ConfigureAwait(false))[0];

    public static async Task<IReadOnlyList<string>> WriteBatchAsync(
        IReadOnlyList<GeneratedDatabase> databases,
        string projectFile,
        string configuration,
        CancellationToken cancellationToken=default) {
        ArgumentNullException.ThrowIfNull(databases);
        if(databases.Count==0) {
            return [];
        }
        var duplicate=databases
            .GroupBy(static database=>database.DatabaseName,StringComparer.Ordinal)
            .FirstOrDefault(static group=>group.Count()>1);
        if(duplicate is not null) {
            throw new ArgumentException($"Generated database '{duplicate.Key}' is duplicated.",nameof(databases));
        }
        var fullProjectFile=Path.GetFullPath(projectFile);
        if(!File.Exists(fullProjectFile)) {
            throw new FileNotFoundException("The target project was not found.",fullProjectFile);
        }
        var projectDirectory=Path.GetDirectoryName(fullProjectFile)!;
        var operationId=Guid.NewGuid().ToString("N");
        var stageRoot=Path.Combine(projectDirectory,"obj","Seamless.Database.Tool",operationId);
        var targetFile=Path.Combine(stageRoot,"CompileGeneratedDatabase.targets");
        try {
            var outputs=new List<StagedDatabase>(databases.Count);
            foreach(var database in databases) {
                var stageSources=Path.Combine(stageRoot,"generated",database.DatabaseName);
                var destination=Path.Combine(projectDirectory,"Database.Generated",database.DatabaseName);
                Directory.CreateDirectory(stageSources);
                foreach(var file in database.Files) {
                    var relative=file.RelativePath.Replace('/',Path.DirectorySeparatorChar);
                    if(Path.IsPathRooted(relative)||relative.Split(Path.DirectorySeparatorChar).Contains("..",StringComparer.Ordinal)) {
                        throw new InvalidOperationException($"Generated path '{file.RelativePath}' is invalid.");
                    }
                    var path=Path.GetFullPath(Path.Combine(stageSources,relative));
                    if(!path.StartsWith(stageSources+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidOperationException($"Generated path '{file.RelativePath}' escapes its output directory.");
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path,file.Content,Utf8Bom,cancellationToken).ConfigureAwait(false);
                }
                outputs.Add(new(stageSources,destination));
            }
            WriteBuildTargets(targetFile,fullProjectFile,outputs);
            await BuildAsync(fullProjectFile,targetFile,configuration,cancellationToken).ConfigureAwait(false);
            foreach(var output in outputs) {
                ReplaceDirectory(output.Staged,output.Destination,operationId);
            }
            return outputs.Select(static output=>output.Destination).ToArray();
        } finally {
            if(Directory.Exists(stageRoot)) {
                Directory.Delete(stageRoot,recursive:true);
            }
        }
    }

    private sealed record StagedDatabase(string Staged,string Destination);

    private static void WriteBuildTargets(
        string targetFile,
        string projectFile,
        IReadOnlyList<StagedDatabase> databases) {
        var itemGroup=new XElement("ItemGroup",new XAttribute("Condition",$"'$(MSBuildProjectFullPath)'=='{projectFile}'"));
        foreach(var database in databases) {
            itemGroup.Add(
                new XElement("Compile",new XAttribute("Remove",Path.Combine(database.Destination,"**","*.cs"))),
                new XElement("Compile",new XAttribute("Include",Path.Combine(database.Staged,"**","*.cs"))));
        }
        var document=new XDocument(
            new XElement("Project",
                itemGroup));
        document.Save(targetFile);
    }

    private static async Task BuildAsync(
        string projectFile,
        string targetFile,
        string configuration,
        CancellationToken cancellationToken) {
        var startInfo=new ProcessStartInfo("dotnet") {
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            WorkingDirectory=Path.GetDirectoryName(projectFile)!,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectFile);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--framework");
        startInfo.ArgumentList.Add("net10.0");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add($"-p:CustomAfterMicrosoftCommonTargets={targetFile}");
        using var process=Process.Start(startInfo)??throw new InvalidOperationException("Unable to start dotnet build.");
        var standardOutput=process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError=process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output=await standardOutput.ConfigureAwait(false);
        var error=await standardError.ConfigureAwait(false);
        if(process.ExitCode!=0) {
            throw new InvalidOperationException($"Generated database did not compile.\n{output}\n{error}");
        }
    }

    private static void ReplaceDirectory(string staged,string destination,string operationId) {
        var parent=Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        var backup=Path.Combine(parent,$".{Path.GetFileName(destination)}.backup.{operationId}");
        if(Directory.Exists(destination)) {
            Directory.Move(destination,backup);
        }
        try {
            Directory.Move(staged,destination);
            if(Directory.Exists(backup)) {
                Directory.Delete(backup,recursive:true);
            }
        } catch {
            if(!Directory.Exists(destination)&&Directory.Exists(backup)) {
                Directory.Move(backup,destination);
            }
            throw;
        }
    }
}
