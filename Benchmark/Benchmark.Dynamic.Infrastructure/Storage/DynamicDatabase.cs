using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Infrastructure;

public sealed class DynamicDatabaseVersionMismatchException:InvalidOperationException{
    public DynamicDatabaseVersionMismatchException(string databasePath,int expectedVersion,int actualVersion)
        :base($"Benchmark.Dynamic DBの版が一致しないため書き込めません。Path={databasePath} Expected={expectedVersion} Actual={actualVersion}"){
        this.DatabasePath=databasePath;
        this.ExpectedVersion=expectedVersion;
        this.ActualVersion=actualVersion;
    }

    public string DatabasePath{get;}
    public int ExpectedVersion{get;}
    public int ActualVersion{get;}
}

public sealed class DynamicDatabase{
    private const string SchemaResourceName="Benchmark.Dynamic.Infrastructure.Storage.Schema.001_initial.sql";
    private static int providerInitialized;
    private readonly object initializationGate=new();

    public DynamicDatabase(string databasePath){
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        EnsureProviderInitialized();
        this.DatabasePath=Path.GetFullPath(databasePath);
    }

    public string DatabasePath{get;}

    public bool InitializeIfMissing(){
        lock(this.initializationGate){
            if(File.Exists(this.DatabasePath)){
                return false;
            }

            var directory=Path.GetDirectoryName(this.DatabasePath);
            if(!string.IsNullOrWhiteSpace(directory)){
                Directory.CreateDirectory(directory);
            }

            using var connection=this.CreateAndOpenConnection(SqliteOpenMode.ReadWriteCreate);
            ConfigureConnection(connection,true,enableWal:true);
            using var transaction=connection.BeginTransaction();
            using var command=connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText=LoadInitialSchema();
            command.ExecuteNonQuery();
            transaction.Commit();
            this.VerifyWritableVersion(connection);
            this.VerifyInitialShape(connection);
            return true;
        }
    }

    public int ReadVersion(){
        if(!File.Exists(this.DatabasePath)){
            return 0;
        }
        using var connection=this.CreateAndOpenConnection(SqliteOpenMode.ReadOnly);
        return ReadUserVersion(connection);
    }

    public SqliteConnection OpenReadConnection(){
        this.InitializeIfMissing();
        var connection=this.CreateAndOpenConnection(SqliteOpenMode.ReadOnly);
        ConfigureConnection(connection,false);
        return connection;
    }

    public SqliteConnection OpenWritableConnection(){
        this.InitializeIfMissing();
        var connection=this.CreateAndOpenConnection(SqliteOpenMode.ReadWrite);
        try{
            this.VerifyWritableVersion(connection);
            // Do not change journal mode on an existing database. Changing WAL mode
            // can produce SQLITE_READONLY while readers or sidecar files are present.
            ConfigureConnection(connection,true,enableWal:false);
            return connection;
        }catch{
            connection.Dispose();
            throw;
        }
    }

    internal void VerifyWritableVersion(SqliteConnection connection){
        var actualVersion=ReadUserVersion(connection);
        if(actualVersion!=DynamicInfrastructureSchema.CurrentVersion){
            throw new DynamicDatabaseVersionMismatchException(
                this.DatabasePath,
                DynamicInfrastructureSchema.CurrentVersion,
                actualVersion);
        }

        int schemaVersion;
        try{
            using var command=connection.CreateCommand();
            command.CommandText="SELECT \"version\" FROM \"DynamicSchemaInfo\" ORDER BY \"version\" DESC LIMIT 1;";
            schemaVersion=Convert.ToInt32(command.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture);
        }catch(SqliteException){
            schemaVersion=0;
        }
        if(schemaVersion!=DynamicInfrastructureSchema.CurrentVersion){
            throw new DynamicDatabaseVersionMismatchException(
                this.DatabasePath,
                DynamicInfrastructureSchema.CurrentVersion,
                schemaVersion);
        }
    }

    private SqliteConnection CreateAndOpenConnection(SqliteOpenMode mode){
        var builder=new SqliteConnectionStringBuilder{
            DataSource=this.DatabasePath,
            Mode=mode,
            // Keep read-only and read-write handles out of a shared connection pool.
            Pooling=false,
            DefaultTimeout=30,
        };
        var connection=new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private void VerifyInitialShape(SqliteConnection connection){
        using var command=connection.CreateCommand();
        command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        var count=Convert.ToInt32(command.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture);
        if(count!=DynamicInfrastructureSchema.UserTableCount){
            throw new InvalidDataException(
                $"Benchmark.Dynamic DBの初期テーブル数が不正です。Path={this.DatabasePath} Expected={DynamicInfrastructureSchema.UserTableCount} Actual={count}");
        }
    }

    private static void ConfigureConnection(SqliteConnection connection,bool writable,bool enableWal=false){
        using(var command=connection.CreateCommand()){
            command.CommandText="PRAGMA busy_timeout=30000;PRAGMA foreign_keys=ON;";
            command.ExecuteNonQuery();
        }
        if(writable&&enableWal){
            using var command=connection.CreateCommand();
            command.CommandText="PRAGMA journal_mode=WAL;PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();
        }else if(writable){
            using var command=connection.CreateCommand();
            command.CommandText="PRAGMA synchronous=NORMAL;";
            command.ExecuteNonQuery();
        }
    }

    private static int ReadUserVersion(SqliteConnection connection){
        using var command=connection.CreateCommand();
        command.CommandText="PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string LoadInitialSchema(){
        using var stream=typeof(DynamicDatabase).Assembly.GetManifestResourceStream(SchemaResourceName)
            ??throw new InvalidDataException($"埋め込みDBスキーマが見つかりません: {SchemaResourceName}");
        using var reader=new StreamReader(stream,System.Text.Encoding.UTF8,true);
        return reader.ReadToEnd();
    }

    private static void EnsureProviderInitialized(){
        if(Interlocked.Exchange(ref providerInitialized,1)==0){
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
