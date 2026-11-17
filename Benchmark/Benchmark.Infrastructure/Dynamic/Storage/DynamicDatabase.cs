using Microsoft.Data.Sqlite;
using Benchmark.Infrastructure;

namespace Benchmark.Dynamic.Infrastructure;

public sealed class DynamicDatabaseVersionMismatchException(string databasePath,int expectedVersion,int actualVersion)
    :InvalidOperationException($"測定DBの版が異なります。明示移行が必要です。Path={databasePath} Expected={expectedVersion} Actual={actualVersion}"){
    public string DatabasePath{get;}=databasePath;
    public int ExpectedVersion{get;}=expectedVersion;
    public int ActualVersion{get;}=actualVersion;
}

/// <summary>共有DB上のDynamic固有の詳細記録へ接続する。</summary>
public sealed class DynamicDatabase{
    private readonly BenchmarkDatabase database;
    public DynamicDatabase(string databasePath){this.database=new(databasePath);}
    public string DatabasePath=>this.database.Path;
    public bool InitializeIfMissing()=>this.database.InitializeIfMissing();
    public int ReadVersion()=>this.database.ReadVersion();
    public SqliteConnection OpenReadConnection()=>this.Open(false);
    public SqliteConnection OpenWritableConnection()=>this.Open(true);
    internal void VerifyWritableVersion(SqliteConnection connection){
        using var command=connection.CreateCommand();
        command.CommandText="PRAGMA user_version;";
        var version=Convert.ToInt32(command.ExecuteScalar());
        if(version!=DynamicInfrastructureSchema.CurrentVersion){throw new DynamicDatabaseVersionMismatchException(this.DatabasePath,DynamicInfrastructureSchema.CurrentVersion,version);}
    }
    private SqliteConnection Open(bool writable){
        this.InitializeIfMissing();
        try{
            if(writable){return this.database.OpenWrite();}
            return this.database.OpenRead();
        }catch(DatabaseVersionException exception){
            throw new DynamicDatabaseVersionMismatchException(this.DatabasePath,DynamicInfrastructureSchema.CurrentVersion,exception.ActualVersion);
        }
    }
}
