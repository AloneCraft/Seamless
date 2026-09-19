namespace Test.Seamless.Database.Tool;

public sealed class DatabaseToolImportIntegrationTests {
    [Fact,Trait("Category","Integration")]
    public async Task Import_LoadsRowsThroughGeneratedAssembly() {
        var configured=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");
        if(string.IsNullOrWhiteSpace(configured)) {
            return;
        }
        var root=FindSolutionRoot();
        var databaseName=$"SeamlessImportFixture_{Guid.NewGuid():N}";
        var work=Path.Combine(root,"Test","Work","DatabaseToolImportIntegration",Guid.NewGuid().ToString("N"));
        var projectFile=Path.Combine(work,"GeneratedDatabase.csproj");
        var storage=Path.Combine(work,"storage");
        Directory.CreateDirectory(work);
        WriteProject(projectFile,root);
        var sourceBuilder=new global::Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configured) {
            InitialCatalog=databaseName,
        };
        var masterBuilder=new global::Microsoft.Data.SqlClient.SqlConnectionStringBuilder(configured) {
            InitialCatalog="master",
        };
        await using var master=new global::Microsoft.Data.SqlClient.SqlConnection(masterBuilder.ConnectionString);
        await master.OpenAsync();
        await ExecuteAsync(master,$"CREATE DATABASE [{databaseName}]; ALTER DATABASE [{databaseName}] SET ALLOW_SNAPSHOT_ISOLATION ON;");
        try {
            await using(var source=new global::Microsoft.Data.SqlClient.SqlConnection(sourceBuilder.ConnectionString)) {
                await source.OpenAsync();
                await ExecuteAsync(source,"CREATE TABLE dbo.Items (Id int NOT NULL,Name nvarchar(40) NULL,CONSTRAINT PK_Items PRIMARY KEY (Id)); INSERT dbo.Items (Id,Name) VALUES (1,N'Apple'),(2,NULL);");
            }
            var generate=await RunToolAsync(root,projectFile,sourceBuilder.ConnectionString,["generate"]);
            Assert.True(generate.ExitCode==0,$"{generate.Output}{Environment.NewLine}{generate.Error}");
            var import=await RunToolAsync(root,projectFile,sourceBuilder.ConnectionString,["import","--storage",storage]);
            Assert.True(import.ExitCode==0,$"{import.Output}{Environment.NewLine}{import.Error}");
            Assert.Contains("Imported 2 row(s)",import.Output,StringComparison.Ordinal);
            Assert.True(Directory.Exists(Path.Combine(storage,"dbo.Items")));
            var manifestPath=Path.Combine(storage,"seamless-import.json");
            Assert.True(File.Exists(manifestPath));
            using(var manifest=global::System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath))) {
                Assert.Equal(1,manifest.RootElement.GetProperty("Version").GetInt32());
                Assert.Equal(databaseName,manifest.RootElement.GetProperty("SourceDatabaseName").GetString());
                Assert.Equal(2,manifest.RootElement.GetProperty("Tables").GetProperty("dbo.Items").GetInt64());
                Assert.True(manifest.RootElement.GetProperty("Storage").TryGetProperty("dbo.Items",out _));
            }

            var secondImport=await RunToolAsync(root,projectFile,sourceBuilder.ConnectionString,["import","--storage",storage]);
            Assert.True(secondImport.ExitCode==0,$"{secondImport.Output}{Environment.NewLine}{secondImport.Error}");
            Assert.Contains("Previous storage backup:",secondImport.Output,StringComparison.Ordinal);
            var backups=Directory.GetDirectories(work,"storage.backup.*",SearchOption.TopDirectoryOnly);
            var backup=Assert.Single(backups);
            Assert.True(File.Exists(Path.Combine(backup,"seamless-import.json")));
            Assert.True(File.Exists(Path.Combine(storage,"seamless-import.json")));
        } finally {
            if(Directory.Exists(work)) {
                Directory.Delete(work,recursive:true);
            }
            await ExecuteAsync(master,$"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];");
        }
    }

    private static async Task<(int ExitCode,string Output,string Error)> RunToolAsync(
        string root,
        string projectFile,
        string connectionString,
        IReadOnlyList<string> command) {
        var startInfo=new global::System.Diagnostics.ProcessStartInfo("dotnet") {
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            UseShellExecute=false,
            WorkingDirectory=root,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(Path.Combine(root,"Seamless.Database.Tool","Seamless.Database.Tool.csproj"));
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("database");
        startInfo.ArgumentList.Add("sqlserver");
        foreach(var argument in command) {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectFile);
        startInfo.ArgumentList.Add("--connection");
        startInfo.ArgumentList.Add("Integration");
        startInfo.Environment["ConnectionStrings__Integration"]=connectionString;
        using var process=global::System.Diagnostics.Process.Start(startInfo)!;
        var output=process.StandardOutput.ReadToEndAsync();
        var error=process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode,await output,await error);
    }

    private static async Task ExecuteAsync(global::Microsoft.Data.SqlClient.SqlConnection connection,string sql) {
        await using var command=new global::Microsoft.Data.SqlClient.SqlCommand(sql,connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void WriteProject(string projectFile,string root) {
        var document=new global::System.Xml.Linq.XDocument(
            new global::System.Xml.Linq.XElement("Project",new global::System.Xml.Linq.XAttribute("Sdk","Microsoft.NET.Sdk"),
                new global::System.Xml.Linq.XElement("PropertyGroup",
                    new global::System.Xml.Linq.XElement("TargetFramework","net10.0"),
                    new global::System.Xml.Linq.XElement("LangVersion","14"),
                    new global::System.Xml.Linq.XElement("Nullable","enable")),
                new global::System.Xml.Linq.XElement("ItemGroup",
                    new global::System.Xml.Linq.XElement("ProjectReference",new global::System.Xml.Linq.XAttribute("Include",Path.Combine(root,"Seamless.Database","Seamless.Database.csproj"))),
                    new global::System.Xml.Linq.XElement("ProjectReference",new global::System.Xml.Linq.XAttribute("Include",Path.Combine(root,"Seamless.Database.Generator","Seamless.Database.Generator.csproj"))),
                    new global::System.Xml.Linq.XElement("Analyzer",new global::System.Xml.Linq.XAttribute("Include",typeof(global::Seamless.Database.Generator.DatabaseContainerGenerator).Assembly.Location)))));
        document.Save(projectFile);
    }

    private static string FindSolutionRoot() {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(File.Exists(Path.Combine(directory.FullName,"Seamless.slnx"))) {
                return directory.FullName;
            }
            directory=directory.Parent;
        }
        throw new DirectoryNotFoundException("Seamless solution root was not found.");
    }
}
