using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Seamless.Database.SqlServer;

public sealed class SqlServerSamplePreparer {
    private static readonly HttpClient SharedHttpClient = new();
    private readonly string masterConnectionString;
    private readonly HttpClient httpClient;
    private readonly SqlServerSampleStorage? storage;

    public SqlServerSamplePreparer(
        string connectionString,
        HttpClient? httpClient = null,
        SqlServerSampleStorage? storage = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new SqlConnectionStringBuilder(connectionString) {
            InitialCatalog="master",
        };
        this.masterConnectionString=builder.ConnectionString;
        this.httpClient=httpClient??SharedHttpClient;
        this.storage=storage;
    }

    public async Task<string> PrepareAsync(
        SqlServerSampleDatabase sample,
        string cacheDirectory,
        bool replace = false,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        var cachePath = Path.GetFullPath(cacheDirectory);
        Directory.CreateDirectory(cachePath);
        var packagePath = Path.Combine(cachePath,sample.PackageFileName);
        await this.DownloadAsync(sample.DownloadUri,packagePath,cancellationToken).ConfigureAwait(false);
        switch(sample.PackageKind) {
            case SqlServerSamplePackageKind.Backup:
                await this.RestoreBackupAsync(sample.Name,packagePath,replace,cancellationToken).ConfigureAwait(false);
                break;
            case SqlServerSamplePackageKind.Script:
                await this.ExecuteScriptAsync(sample.Name,packagePath,replace,cancellationToken).ConfigureAwait(false);
                break;
            case SqlServerSamplePackageKind.SelfExtractingBackup:
                var backupPath = await ExtractBackupAsync(sample,packagePath,cachePath,cancellationToken).ConfigureAwait(false);
                await this.RestoreBackupAsync(sample.Name,backupPath,replace,cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(sample));
        }
        return sample.Name;
    }

    public async Task RestoreBackupAsync(
        string databaseName,
        string backupPath,
        bool replace = false,
        CancellationToken cancellationToken = default) {
        ValidateDatabaseName(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        backupPath=Path.GetFullPath(backupPath);
        if(!File.Exists(backupPath)) {
            throw new FileNotFoundException("The SQL Server backup file was not found.",backupPath);
        }
        await using var connection = new SqlConnection(this.masterConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTargetAvailableAsync(connection,databaseName,replace,cancellationToken).ConfigureAwait(false);
        var files = await ReadBackupFilesAsync(connection,backupPath,cancellationToken).ConfigureAwait(false);
        var storage = this.storage;
        var (dataDirectory,logDirectory)=storage is null
            ? await ReadServerDirectoriesAsync(connection,cancellationToken).ConfigureAwait(false)
            : (storage.DataDirectory,storage.LogDirectory);
        var restore = CreateRestoreCommand(databaseName,backupPath,files,dataDirectory,logDirectory);
        await ExecuteAsync(connection,restore,cancellationToken).ConfigureAwait(false);
        await EnableSnapshotIsolationAsync(connection,databaseName,cancellationToken).ConfigureAwait(false);
    }

    public async Task ExecuteScriptAsync(
        string databaseName,
        string scriptPath,
        bool replace = false,
        CancellationToken cancellationToken = default) {
        ValidateDatabaseName(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);
        scriptPath=Path.GetFullPath(scriptPath);
        if(!File.Exists(scriptPath)) {
            throw new FileNotFoundException("The SQL Server script file was not found.",scriptPath);
        }
        await using var connection = new SqlConnection(this.masterConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTargetAvailableAsync(connection,databaseName,replace,cancellationToken).ConfigureAwait(false);
        var storage = this.storage;
        var createDatabase = storage is null
            ? $"CREATE DATABASE {QuoteIdentifier(databaseName)};"
            : CreateDatabaseCommand(databaseName,storage.DataDirectory,storage.LogDirectory);
        await ExecuteAsync(
            connection,
            createDatabase,
            cancellationToken).ConfigureAwait(false);
        connection.ChangeDatabase(databaseName);
        var script = await File.ReadAllTextAsync(scriptPath,cancellationToken).ConfigureAwait(false);
        foreach(var batch in SqlServerBatchParser.Split(script)) {
            await ExecuteAsync(connection,batch,cancellationToken).ConfigureAwait(false);
        }
        await EnableSnapshotIsolationAsync(connection,databaseName,cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadAsync(Uri uri,string destination,CancellationToken cancellationToken) {
        if(File.Exists(destination)) {
            return;
        }
        var partial = $"{destination}.{Guid.NewGuid():N}.partial";
        try {
            using var response = await this.httpClient.GetAsync(
                uri,HttpCompletionOption.ResponseHeadersRead,cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using(var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false)) {
                await using var output = new FileStream(
                    partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous);
                await input.CopyToAsync(output,cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            File.Move(partial,destination,overwrite: false);
        }
        catch {
            if(File.Exists(partial)) {
                File.Delete(partial);
            }
            throw;
        }
    }

    private static async Task<string> ExtractBackupAsync(
        SqlServerSampleDatabase sample,
        string packagePath,
        string cacheDirectory,
        CancellationToken cancellationToken) {
        if(!OperatingSystem.IsWindows()) {
            throw new PlatformNotSupportedException("The Contoso self-extracting backup package requires Windows.");
        }
        var backupFileName = sample.BackupFileName
            ??throw new InvalidOperationException($"Sample '{sample.Name}' does not define its extracted backup name.");
        var extractDirectory = Path.Combine(cacheDirectory,$"{Path.GetFileNameWithoutExtension(packagePath)}.extracted");
        var existing = Directory.Exists(extractDirectory)
            ? Directory.GetFiles(extractDirectory,backupFileName,SearchOption.AllDirectories).SingleOrDefault()
            : null;
        if(existing is not null) {
            return existing;
        }
        Directory.CreateDirectory(extractDirectory);
        var startInfo = new ProcessStartInfo(packagePath) {
            UseShellExecute=false,
            CreateNoWindow=true,
        };
        startInfo.ArgumentList.Add("/Q");
        startInfo.ArgumentList.Add($"/T:{extractDirectory}");
        using var process = Process.Start(startInfo)
            ??throw new InvalidOperationException($"Could not start package '{packagePath}'.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if(process.ExitCode!=0) {
            throw new InvalidOperationException($"Package extraction failed with exit code {process.ExitCode}.");
        }
        return Directory.GetFiles(extractDirectory,backupFileName,SearchOption.AllDirectories).SingleOrDefault()
            ??throw new FileNotFoundException($"Package '{packagePath}' did not extract '{backupFileName}'.");
    }

    private static async Task EnsureTargetAvailableAsync(
        SqlConnection connection,
        string databaseName,
        bool replace,
        CancellationToken cancellationToken) {
        await using var query = new SqlCommand("SELECT CASE WHEN DB_ID(@name) IS NULL THEN 0 ELSE 1 END;",connection);
        query.Parameters.Add(new("@name",SqlDbType.NVarChar,128) { Value=databaseName });
        var exists = Convert.ToInt32(await query.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            global::System.Globalization.CultureInfo.InvariantCulture)!=0;
        if(!exists) {
            return;
        }
        if(!replace) {
            throw new InvalidOperationException(
                $"Database '{databaseName}' already exists. Use '--replace true' to replace it explicitly.");
        }
        await ExecuteAsync(
            connection,
            $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {QuoteIdentifier(databaseName)};",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<BackupFile>> ReadBackupFilesAsync(
        SqlConnection connection,
        string backupPath,
        CancellationToken cancellationToken) {
        await using var command = new SqlCommand($"RESTORE FILELISTONLY FROM DISK={Literal(backupPath)};",connection) {
            CommandTimeout=0,
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var logicalName = reader.GetOrdinal("LogicalName");
        var type = reader.GetOrdinal("Type");
        var files = new List<BackupFile>();
        while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            files.Add(new(reader.GetString(logicalName),reader.GetString(type)[0]));
        }
        return files;
    }

    private static async Task<(string Data,string Log)> ReadServerDirectoriesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken) {
        const string query = """
            SELECT
                COALESCE(CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)),
                    (SELECT TOP(1) physical_name FROM master.sys.database_files WHERE type=0)),
                COALESCE(CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)),
                    (SELECT TOP(1) physical_name FROM master.sys.database_files WHERE type=1));
            """;
        await using var command = new SqlCommand(query,connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if(!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) {
            throw new InvalidOperationException("SQL Server did not return its data directories.");
        }
        return (DirectoryName(reader.GetString(0)),DirectoryName(reader.GetString(1)));
    }

    internal static string CreateRestoreCommand(
        string databaseName,
        string backupPath,
        IReadOnlyList<BackupFile> files,
        string dataDirectory,
        string logDirectory) {
        if(files.Count==0) {
            throw new InvalidOperationException($"Backup '{backupPath}' contains no database files.");
        }
        var builder = new StringBuilder();
        builder.Append("RESTORE DATABASE ").Append(QuoteIdentifier(databaseName))
            .Append(" FROM DISK=").Append(Literal(backupPath)).Append(" WITH RECOVERY,STATS=5");
        var dataIndex = 0;
        var logIndex = 0;
        var streamIndex = 0;
        foreach(var file in files) {
            string destination;
            switch(file.Type) {
                case 'L':
                    destination=CombineServerPath(logDirectory,$"{databaseName}_{++logIndex}.ldf");
                    break;
                case 'S':
                    destination=CombineServerPath(dataDirectory,$"{databaseName}_stream_{++streamIndex}");
                    break;
                default:
                    destination=CombineServerPath(dataDirectory,
                        dataIndex++==0 ? $"{databaseName}.mdf" : $"{databaseName}_{dataIndex}.ndf");
                    break;
            }
            builder.Append(",MOVE ").Append(Literal(file.LogicalName)).Append(" TO ").Append(Literal(destination));
        }
        builder.Append(';');
        return builder.ToString();
    }

    internal static string CreateDatabaseCommand(
        string databaseName,
        string dataDirectory,
        string logDirectory) {
        ValidateDatabaseName(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        var dataFile = CombineServerPath(dataDirectory,$"{databaseName}.mdf");
        var logFile = CombineServerPath(logDirectory,$"{databaseName}_1.ldf");
        return $"CREATE DATABASE {QuoteIdentifier(databaseName)} ON PRIMARY "
            +$"(NAME={Literal(databaseName)},FILENAME={Literal(dataFile)}) "
            +$"LOG ON (NAME={Literal($"{databaseName}_log")},FILENAME={Literal(logFile)});";
    }

    private static async Task EnableSnapshotIsolationAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken) => await ExecuteAsync(
            connection,
            $"ALTER DATABASE {QuoteIdentifier(databaseName)} SET ALLOW_SNAPSHOT_ISOLATION ON;",
            cancellationToken).ConfigureAwait(false);

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string commandText,
        CancellationToken cancellationToken) {
        await using var command = new SqlCommand(commandText,connection) { CommandTimeout=0 };
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string DirectoryName(string path) {
        path=path.TrimEnd('\\','/');
        var lastSeparator = Math.Max(path.LastIndexOf('\\'),path.LastIndexOf('/'));
        var extension = lastSeparator<0 ? path : path[(lastSeparator+1)..];
        return extension.Contains('.',StringComparison.Ordinal)&&lastSeparator>=0 ? path[..lastSeparator] : path;
    }

    private static string CombineServerPath(string directory,string name) =>
        $"{directory.TrimEnd('\\','/')}{(directory.Contains('\\',StringComparison.Ordinal) ? '\\' : '/')}{name}";

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]","]]",StringComparison.Ordinal)}]";
    private static string Literal(string value) => $"N'{value.Replace("'","''",StringComparison.Ordinal)}'";

    private static void ValidateDatabaseName(string databaseName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        if(databaseName.Length>128) {
            throw new ArgumentOutOfRangeException(nameof(databaseName),"A SQL Server database name cannot exceed 128 characters.");
        }
    }

    internal sealed record BackupFile(string LogicalName,char Type);
}
