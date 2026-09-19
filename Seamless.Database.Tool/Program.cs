namespace Seamless.Database.Tool;

internal static class Program {
    public static async Task<int> Main(string[] args) {
        try {
            if(args.Length<3
                ||args[0]!="database"
                ||args[1]!="sqlserver"
                ||args[2] is not ("generate" or "import" or "prepare" or "samples")) {
                WriteUsage();
                return 2;
            }
            if(args[2]=="samples") {
                foreach(var sample in SqlServerSampleCatalog.All) {
                    Console.WriteLine(sample.Name);
                }
                return 0;
            }
            var options = ParseOptions(args[3..]);
            var projectFile = ResolveProject(Required(options,"project"));
            var connectionName = Required(options,"connection");
            var configuration = Optional(options,"configuration")??"Debug";
            var connectionString = ConnectionStringResolver.Resolve(projectFile,connectionName);
            if(args[2]=="prepare") {
                var sample = SqlServerSampleCatalog.Get(Required(options,"sample"));
                var storage = new SqlServerSampleStorage(
                    Required(options,"data"),
                    Optional(options,"log"));
                var cache = Optional(options,"cache")
                    ??Path.Combine(Path.GetDirectoryName(projectFile)!,"Work","DatabaseSamples");
                var replace = ReadBoolean(options,"replace");
                await new SqlServerSamplePreparer(connectionString,storage: storage)
                    .PrepareAsync(sample,cache,replace)
                    .ConfigureAwait(false);
                Console.WriteLine($"Prepared SQL Server sample database '{sample.Name}'.");
                return 0;
            }
            var databaseNames=Values(options,"database");
            if(args[2]=="generate"&&databaseNames.Count>0) {
                return await GenerateManyAsync(
                    databaseNames,
                    connectionString,
                    projectFile,
                    configuration).ConfigureAwait(false);
            }
            var generated = await SqlServerDatabaseGenerator.GenerateAsync(connectionString).ConfigureAwait(false);
            if(args[2]=="generate") {
                var output = await GeneratedProjectWriter.WriteAsync(generated,projectFile,configuration).ConfigureAwait(false);
                Console.WriteLine($"Generated '{generated.SourceDatabaseName}' as '{generated.DatabaseName}'.");
                Console.WriteLine($"Schema fingerprint: {generated.SchemaFingerprint}");
                Console.WriteLine($"Output: {output}");
            }
            else {
                var storage = Required(options,"storage");
                var result = await DatabaseImporter.ImportAsync(generated,connectionString,projectFile,storage,configuration).ConfigureAwait(false);
                Console.WriteLine($"Imported {result.Rows} row(s) from '{generated.SourceDatabaseName}' into '{result.Destination}'.");
                Console.WriteLine($"Validation manifest: {result.Manifest}");
                if(result.Backup is not null) {
                    Console.WriteLine($"Previous storage backup: {result.Backup}");
                }
            }
            return 0;
        }
        catch(Exception exception) {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static async Task<int> GenerateManyAsync(
        IReadOnlyList<string> databaseNames,
        string baseConnectionString,
        string projectFile,
        string configuration) {
        var generated=new List<GeneratedDatabase>(databaseNames.Count);
        var failed=false;
        foreach(var databaseName in databaseNames) {
            try {
                var connection=new global::Microsoft.Data.SqlClient.SqlConnectionStringBuilder(baseConnectionString) {
                    InitialCatalog=databaseName,
                };
                generated.Add(await SqlServerDatabaseGenerator.GenerateAsync(connection.ConnectionString).ConfigureAwait(false));
            } catch(Exception exception) {
                failed=true;
                Console.Error.WriteLine($"Database generation failed for '{databaseName}'.");
                Console.Error.WriteLine(exception);
            }
        }
        if(generated.Count>0) {
            var outputs=await GeneratedProjectWriter.WriteBatchAsync(
                generated,
                projectFile,
                configuration).ConfigureAwait(false);
            for(var index=0;index<generated.Count;index++) {
                Console.WriteLine($"Generated '{generated[index].SourceDatabaseName}' as '{generated[index].DatabaseName}'.");
                Console.WriteLine($"Schema fingerprint: {generated[index].SchemaFingerprint}");
                Console.WriteLine($"Output: {outputs[index]}");
            }
        }
        return failed?1:0;
    }

    private static Dictionary<string,List<string>> ParseOptions(string[] args) {
        var options = new Dictionary<string,List<string>>(StringComparer.Ordinal);
        for(var index = 0; index<args.Length; index+=2) {
            if(index+1>=args.Length||!args[index].StartsWith("--",StringComparison.Ordinal)) {
                throw new ArgumentException($"Invalid option near '{args[index]}'.");
            }
            var name=args[index][2..];
            if(!options.TryGetValue(name,out var values)) {
                values=[];
                options.Add(name,values);
            }
            values.Add(args[index+1]);
        }
        return options;
    }

    private static string Required(IReadOnlyDictionary<string,List<string>> options,string name) {
        var values=Values(options,name);
        return values.Count==1&&!string.IsNullOrWhiteSpace(values[0])
            ?values[0]
            :throw new ArgumentException($"Option '--{name}' must be specified exactly once.");
    }

    private static string? Optional(IReadOnlyDictionary<string,List<string>> options,string name) {
        var values=Values(options,name);
        return values.Count switch {
            0=>null,
            1=>values[0],
            _=>throw new ArgumentException($"Option '--{name}' may be specified at most once."),
        };
    }

    private static IReadOnlyList<string> Values(
        IReadOnlyDictionary<string,List<string>> options,
        string name)=>options.TryGetValue(name,out var values)?values:[];

    private static bool ReadBoolean(IReadOnlyDictionary<string,List<string>> options,string name) {
        var value=Optional(options,name);
        if(value is null) {
            return false;
        }
        return bool.TryParse(value,out var result)
            ? result
            : throw new ArgumentException($"Option '--{name}' must be 'true' or 'false'.");
    }

    private static string ResolveProject(string value) {
        var path = Path.GetFullPath(value);
        if(File.Exists(path)) {
            return path;
        }
        if(!Directory.Exists(path)) {
            throw new FileNotFoundException("The project path was not found.",path);
        }
        var projects = Directory.GetFiles(path,"*.csproj",SearchOption.TopDirectoryOnly);
        return projects.Length==1
            ? projects[0]
            : throw new InvalidOperationException($"Directory '{path}' must contain exactly one project file.");
    }

    private static void WriteUsage() {
        Console.Error.WriteLine("Usage: seamless database sqlserver generate --project <project.csproj> --connection <name> [--configuration Debug|Release]");
        Console.Error.WriteLine("       seamless database sqlserver generate --project <project.csproj> --connection <name> --database <name> [--database <name> ...] [--configuration Debug|Release]");
        Console.Error.WriteLine("       seamless database sqlserver import --project <project.csproj> --connection <name> --storage <directory> [--configuration Debug|Release]");
        Console.Error.WriteLine(
            "       seamless database sqlserver prepare --project <project.csproj> --connection <name> "
            +"--sample <name> --data <directory> [--log <directory>] [--cache <directory>] [--replace true|false]");
        Console.Error.WriteLine("       seamless database sqlserver samples");
    }
}
