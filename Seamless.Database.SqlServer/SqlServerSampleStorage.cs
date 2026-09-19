namespace Seamless.Database.SqlServer;

public sealed class SqlServerSampleStorage {
    public SqlServerSampleStorage(string dataDirectory,string? logDirectory = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        this.DataDirectory=dataDirectory;
        this.LogDirectory=logDirectory is null
            ? dataDirectory
            : ValidateDirectory(logDirectory,nameof(logDirectory));
    }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    private static string ValidateDirectory(string directory,string parameterName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory,parameterName);
        return directory;
    }
}
