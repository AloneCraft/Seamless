namespace Seamless.Database.SqlServer;

public static class SqlServerSampleCatalog {
    private const string AdventureWorksRelease =
        "https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks/";

    private static readonly IReadOnlyList<SqlServerSampleDatabase> Samples = Array.AsReadOnly([
        Backup("AdventureWorks2025"),
        Backup("AdventureWorksDW2025"),
        Backup("AdventureWorks2022"),
        Backup("AdventureWorksDW2022"),
        Backup("AdventureWorks2019"),
        Backup("AdventureWorksDW2019"),
        Backup("AdventureWorks2017"),
        Backup("AdventureWorksDW2017"),
        Backup("AdventureWorks2016"),
        Backup("AdventureWorksDW2016"),
        Backup("AdventureWorks2016_EXT"),
        Backup("AdventureWorksDW2016_EXT"),
        Backup("AdventureWorks2014"),
        Backup("AdventureWorksDW2014"),
        Backup("AdventureWorks2012"),
        Backup("AdventureWorksDW2012"),
        new(
            "AdventureWorks2008R2",
            new("https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks2008r2/adventure-works-2008r2-oltp.bak"),
            SqlServerSamplePackageKind.Backup,
            "adventure-works-2008r2-oltp.bak"),
        new(
            "AdventureWorksDW2008R2",
            new("https://github.com/Microsoft/sql-server-samples/releases/download/adventureworks2008r2/adventure-works-2008r2-dw.bak"),
            SqlServerSamplePackageKind.Backup,
            "adventure-works-2008r2-dw.bak"),
        new(
            "WideWorldImporters",
            new("https://github.com/Microsoft/sql-server-samples/releases/download/wide-world-importers-v1.0/WideWorldImporters-Standard.bak"),
            SqlServerSamplePackageKind.Backup,
            "WideWorldImporters-Standard.bak"),
        new(
            "WideWorldImportersDW",
            new("https://github.com/Microsoft/sql-server-samples/releases/download/wide-world-importers-v1.0/WideWorldImportersDW-Standard.bak"),
            SqlServerSamplePackageKind.Backup,
            "WideWorldImportersDW-Standard.bak"),
        Script("Northwind","instnwnd.sql"),
        Script("pubs","instpubs.sql"),
        new(
            "ContosoRetailDW",
            new("https://download.microsoft.com/download/2/b/9/2b93be7f-2f7b-4202-ae66-878ed8813956/ContosoBIdemoBAK.exe"),
            SqlServerSamplePackageKind.SelfExtractingBackup,
            "ContosoBIdemoBAK.exe",
            "ContosoRetailDW.bak"),
    ]);

    public static IReadOnlyList<SqlServerSampleDatabase> All=>Samples;

    public static bool TryGet(string name,out SqlServerSampleDatabase sample) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        sample=Samples.SingleOrDefault(value=>string.Equals(value.Name,name,StringComparison.OrdinalIgnoreCase))!;
        return sample is not null;
    }

    public static SqlServerSampleDatabase Get(string name)=>
        TryGet(name,out var sample)
            ?sample
            :throw new KeyNotFoundException($"SQL Server sample database '{name}' is not registered.");

    private static SqlServerSampleDatabase Backup(string name)=>new(
        name,
        new($"{AdventureWorksRelease}{name}.bak"),
        SqlServerSamplePackageKind.Backup,
        $"{name}.bak");

    private static SqlServerSampleDatabase Script(string name,string fileName)=>new(
        name,
        new($"https://raw.githubusercontent.com/microsoft/sql-server-samples/master/samples/databases/northwind-pubs/{fileName}"),
        SqlServerSamplePackageKind.Script,
        fileName);
}
