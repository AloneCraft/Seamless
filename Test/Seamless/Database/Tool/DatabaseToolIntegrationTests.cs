namespace Test.Seamless.Database.Tool;

public sealed class DatabaseToolIntegrationTests {
    [Fact,Trait("Category","Integration")]
    public async Task Generate_BuildsRealSqlServerSchemaThroughCli() {
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");
        if(string.IsNullOrWhiteSpace(connectionString)) {
            return;
        }
        var root=FindSolutionRoot();
        var tableName=$"SeamlessCliFixture_{Guid.NewGuid():N}";
        var work=Path.Combine(root,"Test","Work","DatabaseToolIntegration",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var projectFile=Path.Combine(work,"GeneratedDatabase.csproj");
        WriteProject(projectFile,root);
        await using var connection=new global::Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using(var create=new global::Microsoft.Data.SqlClient.SqlCommand(
            $"CREATE TABLE dbo.[{tableName}] (Id int NOT NULL,Name nvarchar(40) NULL,CONSTRAINT [PK_{tableName}] PRIMARY KEY (Id));",
            connection)) {
            await create.ExecuteNonQueryAsync();
        }
        try {
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
            startInfo.ArgumentList.Add("generate");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(projectFile);
            startInfo.ArgumentList.Add("--connection");
            startInfo.ArgumentList.Add("Integration");
            startInfo.Environment["ConnectionStrings__Integration"]=connectionString;
            using var process=global::System.Diagnostics.Process.Start(startInfo)!;
            var outputTask=process.StandardOutput.ReadToEndAsync();
            var errorTask=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output=await outputTask;
            var error=await errorTask;
            Assert.True(process.ExitCode==0,$"{output}{Environment.NewLine}{error}");
            var generatedRoot=Path.Combine(work,"Database.Generated","tempdb");
            Assert.True(File.Exists(Path.Combine(generatedRoot,"tempdb.cs")));
            Assert.True(File.Exists(Path.Combine(generatedRoot,"Records","dbo",$"{tableName}.cs")));
        } finally {
            await using var drop=new global::Microsoft.Data.SqlClient.SqlCommand($"DROP TABLE dbo.[{tableName}];",connection);
            await drop.ExecuteNonQueryAsync();
            if(Directory.Exists(work)) {
                Directory.Delete(work,recursive:true);
            }
        }
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
