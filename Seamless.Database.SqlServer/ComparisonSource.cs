using System.IO;
using System.Security.Cryptography;
using System.Text;
using Seamless.Database.Rdbms;

namespace Seamless.Database.SqlServer;

/// <summary>元サーバーの一覧取得に成功した場合だけ、完全な構築候補を作る。</summary>
public static class ComparisonSource{
    public static async Task<IReadOnlyList<string>> DiscoverAsync(string connectionString,CancellationToken cancellationToken=default){
        var settings=new SqlConnectionStringBuilder(connectionString){InitialCatalog="master"};await using var connection=new SqlConnection(settings.ConnectionString);await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command=connection.CreateCommand();command.CommandText="SELECT name,state_desc,HAS_DBACCESS(name) FROM sys.databases WHERE database_id>4 AND source_database_id IS NULL ORDER BY name;";
        var names=new List<string>();await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)){
            var name=reader.GetString(0);if(reader.GetString(1)!="ONLINE"||reader.IsDBNull(2)||reader.GetInt32(2)!=1){throw new InvalidOperationException($"Database '{name}' is unavailable; no deletion or cutover is allowed.");}names.Add(name);
        }
        if(names.Count==0){throw new InvalidOperationException("No accessible user databases were discovered; refusing an empty generation.");}return names;
    }
    public static Task<ComparisonGeneration> PrepareAsync(string connectionString,string directory,string productVersion,CancellationToken cancellationToken=default)=>PrepareCoreAsync(connectionString,directory,productVersion,null,cancellationToken);
    public static Task<ComparisonGeneration> PrepareSelectedAsync(string connectionString,string directory,string productVersion,IReadOnlyList<string> databaseNames,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(databaseNames);if(databaseNames.Count==0){throw new ArgumentException("At least one database must be selected.",nameof(databaseNames));}
        return PrepareCoreAsync(connectionString,directory,productVersion,databaseNames,cancellationToken);
    }
    private static async Task<ComparisonGeneration> PrepareCoreAsync(string connectionString,string directory,string productVersion,IReadOnlyList<string>?selectedDatabases,CancellationToken cancellationToken){
        directory=Path.GetFullPath(directory);if(Directory.Exists(directory)&&Directory.EnumerateFileSystemEntries(directory).Any(path=>Path.GetFileName(path)!=".seamless-generation-owner")){throw new IOException("A generation directory must be new and empty.");}
        var discovered=await DiscoverAsync(connectionString,cancellationToken).ConfigureAwait(false);var names=discovered;
        if(selectedDatabases is not null){
            var selected=new HashSet<string>(selectedDatabases,StringComparer.OrdinalIgnoreCase);
            if(selected.Count!=selectedDatabases.Count||selected.Any(name=>!discovered.Contains(name,StringComparer.OrdinalIgnoreCase))){throw new InvalidDataException("The selected databases must be unique and present in the discovered inventory.");}
            names=discovered.Where(selected.Contains).ToArray();
        }
        var databases=new List<ComparisonDatabase>();Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"source-inventory.json"),global::System.Text.Json.JsonSerializer.Serialize(discovered),cancellationToken).ConfigureAwait(false);
        foreach(var name in names){
            var settings=new SqlConnectionStringBuilder(connectionString){InitialCatalog=name};var catalog=await new SqlServerCatalogReader(settings.ConnectionString,analyzeRoutines:false).ReadAsync(cancellationToken).ConfigureAwait(false);
            var runtimeSql=name.Equals("Seamless_Comparison_Fixture",StringComparison.Ordinal);
            var generated=CSharpDatabaseGenerator.Generate(catalog,true,true,runtimeSql);
            foreach(var file in generated.Files){var path=ComparisonGeneration.ResolveManagedPath(directory,"generated/"+generated.DatabaseName+"/"+file.RelativePath);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllTextAsync(path,file.Content,new UTF8Encoding(false),cancellationToken).ConfigureAwait(false);}
            databases.Add(new(name,generated.DatabaseName,generated.SchemaFingerprint,!name.Equals("Seamless_Comparison_Fixture",StringComparison.Ordinal),catalog,generated.Tables,"d"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..12]){Routines=generated.Routines,GeneratedNamespace=generated.RootNamespace,RuntimeSqlEnabled=runtimeSql});
        }
        var generation=new ComparisonGeneration(Path.GetFileName(directory),productVersion,DateTimeOffset.UtcNow,databases,SourceServer:new SqlConnectionStringBuilder(connectionString).DataSource,SourceIntegratedSecurity:new SqlConnectionStringBuilder(connectionString).IntegratedSecurity){SourceDatabaseNames=discovered.ToArray()};generation.Write(directory);return generation;
    }
    public static async Task VerifySourceAsync(string connectionString,ComparisonGeneration generation,CancellationToken cancellationToken=default){
        var names=await DiscoverAsync(connectionString,cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string>expectedInventory=generation.SourceDatabaseNames;
        if(expectedInventory.Count==0){expectedInventory=generation.Databases.Select(database=>database.Name).ToArray();}
        if(!names.SequenceEqual(expectedInventory,StringComparer.Ordinal)){throw new InvalidDataException("The source database inventory changed during construction.");}
        foreach(var database in generation.Databases){var settings=new SqlConnectionStringBuilder(connectionString){InitialCatalog=database.Name};var catalog=await new SqlServerCatalogReader(settings.ConnectionString,analyzeRoutines:false).ReadAsync(cancellationToken).ConfigureAwait(false);if(CSharpDatabaseGenerator.Generate(catalog,true,true,database.RuntimeSqlEnabled).SchemaFingerprint!=database.Fingerprint){throw new InvalidDataException($"Source schema changed: {database.Name}.");}}
    }
}
