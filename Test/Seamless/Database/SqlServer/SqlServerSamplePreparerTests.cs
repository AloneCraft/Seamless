namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerSamplePreparerTests {
    [Fact]
    public void Storage_UsesDataDirectoryForLogsWhenLogDirectoryIsOmitted() {
        var storage = new global::Seamless.Database.SqlServer.SqlServerSampleStorage(@"G:\DatabaseSamples");

        Assert.Equal(@"G:\DatabaseSamples",storage.DataDirectory);
        Assert.Equal(@"G:\DatabaseSamples",storage.LogDirectory);
    }

    [Fact]
    public void Storage_RejectsBlankDirectory() {
        Assert.Throws<ArgumentException>(() =>
            new global::Seamless.Database.SqlServer.SqlServerSampleStorage(" "));
    }

    [Fact]
    public void CreateRestoreCommand_UsesExplicitDataAndLogDirectories() {
        var files = new[] {
            new global::Seamless.Database.SqlServer.SqlServerSamplePreparer.BackupFile("SampleData",'D'),
            new global::Seamless.Database.SqlServer.SqlServerSamplePreparer.BackupFile("SampleLog",'L'),
        };

        var sql = global::Seamless.Database.SqlServer.SqlServerSamplePreparer.CreateRestoreCommand(
            "Sample",@"G:\Cache\Sample.bak",files,@"G:\DatabaseSamples\Data",@"G:\DatabaseSamples\Log");

        Assert.Contains("MOVE N'SampleData' TO N'G:\\DatabaseSamples\\Data\\Sample.mdf'",sql);
        Assert.Contains("MOVE N'SampleLog' TO N'G:\\DatabaseSamples\\Log\\Sample_1.ldf'",sql);
    }

    [Fact]
    public void CreateDatabaseCommand_UsesExplicitDataAndLogDirectories() {
        var sql = global::Seamless.Database.SqlServer.SqlServerSamplePreparer.CreateDatabaseCommand(
            "Northwind",@"G:\DatabaseSamples\Data",@"G:\DatabaseSamples\Log");

        Assert.Contains("FILENAME=N'G:\\DatabaseSamples\\Data\\Northwind.mdf'",sql);
        Assert.Contains("FILENAME=N'G:\\DatabaseSamples\\Log\\Northwind_1.ldf'",sql);
    }
}
