using System.Reflection;
using System.Runtime.Loader;

namespace Seamless.Database.Tool;

internal sealed record DatabaseImportResult(
    long Rows,
    string Destination,
    string Manifest,
    string? Backup);

internal static class DatabaseImporter {
    private const int ManifestVersion=1;

    public static async Task<DatabaseImportResult> ImportAsync(
        GeneratedDatabase generated,
        string connectionString,
        string projectFile,
        string storageDirectory,
        string configuration,
        CancellationToken cancellationToken=default) {
        var assemblyPath=await BuildAndGetAssemblyAsync(projectFile,configuration,cancellationToken).ConfigureAwait(false);
        var destination=Path.GetFullPath(storageDirectory);
        var staging=$"{destination}.staging.{Guid.NewGuid():N}";
        var loadContext=new ProjectLoadContext(assemblyPath);
        var importedRows=0L;
        IReadOnlyDictionary<string,long> importedRowsByTable=new Dictionary<string,long>();
        string? backup=null;
        string? manifest=null;
        try {
            var assembly=loadContext.LoadFromAssemblyPath(assemblyPath);
            var databaseType=assembly.GetTypes().SingleOrDefault(type=>
                !type.IsAbstract
                &&typeof(global::Seamless.Database.DatabaseContainer).IsAssignableFrom(type)
                &&type.Name==generated.DatabaseName)
                ??throw new InvalidOperationException($"Generated database container '{generated.DatabaseName}' was not found in '{assemblyPath}'.");
            var fingerprint=databaseType.GetField(
                "GeneratedSchemaFingerprint",
                BindingFlags.Public|BindingFlags.Static)?.GetRawConstantValue() as string;
            if(fingerprint!=generated.SchemaFingerprint) {
                throw new InvalidOperationException(
                    $"Schema fingerprint mismatch. Generated: '{fingerprint??"<missing>"}', SQL Server: '{generated.SchemaFingerprint}'. Run generate before import.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
            var tableSizing=await SqlServerImportSizer.MeasureTablesAsync(
                connectionString,generated.Tables,cancellationToken).ConfigureAwait(false);
            var options=global::Seamless.Database.DatabaseContainerOptions.OnStorage(staging) with {
                TableStorage=tableSizing.ToDictionary(
                    static pair=>pair.Key,
                    static pair=>new global::Seamless.Database.DatabaseTableStorageOptions {
                        IndexSize=pair.Value.IndexSize,
                        PageSize=pair.Value.PageSize,
                        SegmentSize=pair.Value.SegmentSize,
                        MemorySize=pair.Value.MemorySize,
                    },
                    StringComparer.Ordinal),
            };
            var database=(global::Seamless.Database.DatabaseContainer?)Activator.CreateInstance(databaseType,[options])
                ??throw new InvalidOperationException($"Database container '{databaseType}' could not be created.");
            try {
                using var source=new SqlServerImportSource(connectionString,generated.Tables);
                Invoke(databaseType,database,"EnsureTablesCreated",[]);
                Invoke(databaseType,database,"ImportTables",[source]);
                importedRows=source.RowsRead;
                importedRowsByTable=new Dictionary<string,long>(source.RowsReadByTable,StringComparer.Ordinal);
            } finally {
                database.Dispose();
            }
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStaging(databaseType,options,generated.Tables,importedRowsByTable);
            manifest=WriteManifest(staging,generated,importedRowsByTable,tableSizing);
            cancellationToken.ThrowIfCancellationRequested();
            backup=ReplaceDirectory(staging,destination);
            manifest=Path.Combine(destination,Path.GetFileName(manifest));
        } finally {
            if(Directory.Exists(staging)) {
                Directory.Delete(staging,recursive:true);
            }
            loadContext.Unload();
        }
        return new(importedRows,destination,manifest!,backup);
    }

    private static void ValidateStaging(
        Type databaseType,
        global::Seamless.Database.DatabaseContainerOptions options,
        IReadOnlyList<GeneratedTableBinding> tables,
        IReadOnlyDictionary<string,long> expectedRows) {
        if(expectedRows.Count!=tables.Count||tables.Any(table=>!expectedRows.ContainsKey(table.ObjectName))) {
            throw new InvalidDataException("The SQL Server import did not enumerate every generated relation.");
        }
        using var database=(global::Seamless.Database.DatabaseContainer?)Activator.CreateInstance(databaseType,[options])
            ??throw new InvalidOperationException($"Database container '{databaseType}' could not be reopened for validation.");
        foreach(var table in tables) {
            object current=database;
            foreach(var segment in table.ObjectName.Split('.')) {
                var property=current.GetType().GetProperty(segment,BindingFlags.Instance|BindingFlags.Public)
                    ??throw new MissingMemberException(current.GetType().FullName,segment);
                current=property.GetValue(current)
                    ??throw new InvalidDataException($"Generated relation path '{table.ObjectName}' returned null at '{segment}'.");
            }
            var countProperty=current.GetType().GetProperty("LongCount",BindingFlags.Instance|BindingFlags.Public)
                ??throw new MissingMemberException(current.GetType().FullName,"LongCount");
            var actual=Convert.ToInt64(countProperty.GetValue(current),global::System.Globalization.CultureInfo.InvariantCulture);
            var expected=expectedRows[table.ObjectName];
            if(actual!=expected) {
                throw new InvalidDataException(
                    $"Imported relation '{table.ObjectName}' failed validation. Expected {expected} row(s), found {actual}.");
            }
        }
    }

    private static string WriteManifest(
        string staging,
        GeneratedDatabase generated,
        IReadOnlyDictionary<string,long> rows,
        IReadOnlyDictionary<string,SqlServerImportSizing> sizing) {
        var path=Path.Combine(staging,"seamless-import.json");
        var orderedRows=new SortedDictionary<string,long>(StringComparer.Ordinal);
        foreach(var row in rows){orderedRows.Add(row.Key,row.Value);}
        var manifest=new {
            Version=ManifestVersion,
            generated.SourceDatabaseName,
            GeneratedDatabaseName=generated.DatabaseName,
            generated.SchemaFingerprint,
            ImportedUtc=DateTimeOffset.UtcNow,
            Tables=orderedRows,
            Storage=sizing.OrderBy(static pair=>pair.Key,StringComparer.Ordinal).ToDictionary(
                static pair=>pair.Key,
                static pair=>pair.Value,
                StringComparer.Ordinal),
        };
        var json=JsonSerializer.Serialize(manifest,new JsonSerializerOptions {WriteIndented=true});
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);
        using(var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,leaveOpen:true)) {
            writer.Write(json);
            writer.Flush();
        }
        stream.Flush(flushToDisk:true);
        return path;
    }

    private static void Invoke(Type type,object instance,string name,object?[] arguments) {
        var method=type.GetMethod(name,BindingFlags.Instance|BindingFlags.Public)
            ??throw new MissingMethodException(type.FullName,name);
        try {
            method.Invoke(instance,arguments);
        } catch(TargetInvocationException exception) when(exception.InnerException is not null) {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static async Task<string> BuildAndGetAssemblyAsync(
        string projectFile,
        string configuration,
        CancellationToken cancellationToken) {
        await RunDotNetAsync(
            Path.GetDirectoryName(projectFile)!,
            ["build",projectFile,"--configuration",configuration,"--framework","net10.0","--nologo"],
            cancellationToken).ConfigureAwait(false);
        var targetPath=await RunDotNetAsync(
            Path.GetDirectoryName(projectFile)!,
            ["msbuild",projectFile,"--nologo",$"-p:Configuration={configuration}","-p:TargetFramework=net10.0","-getProperty:TargetPath"],
            cancellationToken).ConfigureAwait(false);
        var path=targetPath.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Last();
        if(!Path.IsPathRooted(path)) {
            path=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectFile)!,path));
        }
        return path;
    }

    private static async Task<string> RunDotNetAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) {
        var startInfo=new ProcessStartInfo("dotnet") {
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            WorkingDirectory=workingDirectory,
        };
        foreach(var argument in arguments) {
            startInfo.ArgumentList.Add(argument);
        }
        using var process=Process.Start(startInfo)??throw new InvalidOperationException("Unable to start dotnet.");
        var outputTask=process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask=process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output=await outputTask.ConfigureAwait(false);
        var error=await errorTask.ConfigureAwait(false);
        if(process.ExitCode!=0) {
            throw new InvalidOperationException($"dotnet command failed.\n{output}\n{error}");
        }
        return output;
    }

    private static string? ReplaceDirectory(string staging,string destination) {
        var parent=Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);
        if(File.Exists(destination)) {
            throw new IOException($"The import destination '{destination}' is a file.");
        }
        var timestamp=DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffZ",global::System.Globalization.CultureInfo.InvariantCulture);
        var backup=$"{destination}.backup.{timestamp}.{Guid.NewGuid():N}";
        var hadDestination=Directory.Exists(destination);
        if(hadDestination) {
            Directory.Move(destination,backup);
        }
        try {
            Directory.Move(staging,destination);
            return hadDestination?backup:null;
        } catch {
            if(!Directory.Exists(destination)&&Directory.Exists(backup)) {
                Directory.Move(backup,destination);
            }
            throw;
        }
    }

    private sealed class ProjectLoadContext(string mainAssemblyPath):AssemblyLoadContext(isCollectible:true) {
        private readonly AssemblyDependencyResolver resolver=new(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName) {
            var shared=Default.Assemblies.FirstOrDefault(assembly=>assembly.GetName().Name==assemblyName.Name);
            if(shared is not null) {
                return shared;
            }
            var path=this.resolver.ResolveAssemblyToPath(assemblyName);
            return path is null?null:this.LoadFromAssemblyPath(path);
        }
    }
}
