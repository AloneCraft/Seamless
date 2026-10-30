using Microsoft.Data.Sqlite;

namespace Benchmark.Infrastructure.Tests;

public sealed class BenchmarkDatabase{
    [Fact]
    public void ImportDynamic(){
        var folder=Path.Combine(Path.GetTempPath(),"seamless-migrate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var source=Path.Combine(folder,"old.db");var target=Path.Combine(folder,"new.db");
        try{
            _=new Infrastructure.BenchmarkDatabase(source);
            var assembly=typeof(Infrastructure.BenchmarkDatabase).Assembly;
            using(var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(name=>name.EndsWith("Initial.sql")))!)
            using(var reader=new StreamReader(stream))
            using(var connection=new SqliteConnection($"Data Source={source};Pooling=False")){
                connection.Open();using var command=connection.CreateCommand();
                command.CommandText=reader.ReadToEnd().Split("CREATE TABLE BenchmarkSchema",StringSplitOptions.None)[0];command.ExecuteNonQuery();
                command.CommandText="INSERT INTO DynamicRuns(run_id,status,seed,profile,settings_json,created_at,updated_at) VALUES('legacy','Completed',42,'Balanced-v1','{}','2026-10-22T00:00:00Z','2026-10-22T00:00:00Z');";command.ExecuteNonQuery();
            }
            Assert.Throws<InvalidOperationException>(()=>Infrastructure.BenchmarkDatabase.ImportDynamic(source,target,false));
            var backup=Infrastructure.BenchmarkDatabase.ImportDynamic(source,target,true);
            Assert.True(File.Exists(backup));
            Assert.Equal(3,new Infrastructure.BenchmarkDatabase(source).ReadVersion());
            Assert.Equal(3,new Infrastructure.BenchmarkDatabase(backup).ReadVersion());
            var database=new Infrastructure.BenchmarkDatabase(target);Assert.Equal(4,database.ReadVersion());
            var legacy=Assert.Single(new Infrastructure.BenchmarkRepository(database).GetRuns());
            Assert.Equal("legacy",legacy.ID);Assert.Equal("Legacy",legacy.Status);Assert.Equal("{}",legacy.EnvironmentJSON);
            using var read=database.OpenRead();using var check=read.CreateCommand();check.CommandText="PRAGMA integrity_check;";Assert.Equal("ok",check.ExecuteScalar());
            check.CommandText="SELECT COUNT(*) FROM DynamicRuns;";Assert.Equal(1L,check.ExecuteScalar());
            check.CommandText="PRAGMA foreign_key_check;";using(var violations=check.ExecuteReader()){Assert.False(violations.Read());}
            Assert.Throws<IOException>(()=>Infrastructure.BenchmarkDatabase.ImportDynamic(source,target,true));
        }finally{Directory.Delete(folder,true);}
    }
    [Fact]
    public async Task ConcurrentWriters(){
        var folder=Path.Combine(Path.GetTempPath(),"seamless-writers-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try{
            var path=Path.Combine(folder,"both.db");
            await Task.WhenAll(Enumerable.Range(0,8).Select(index=>Task.Run(()=>{
                var repository=new Infrastructure.BenchmarkRepository(new(path));
                var app="Dynamic";if(index%2==0){app="Scalable";}
                repository.SaveRun(new(index.ToString(),app,"Running","{}","{}","test",DateTimeOffset.UtcNow));
            })));
            Assert.Equal(8,new Infrastructure.BenchmarkRepository(new(path)).GetRuns().Count);
        }finally{Directory.Delete(folder,true);}
    }
}
