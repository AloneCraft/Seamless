namespace Seamless.Database.SqlServer;

public sealed record SqlServerSampleDatabase(
    string Name,
    Uri DownloadUri,
    SqlServerSamplePackageKind PackageKind,
    string PackageFileName,
    string? BackupFileName=null);
