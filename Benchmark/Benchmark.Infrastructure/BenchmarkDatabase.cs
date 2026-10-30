using Microsoft.Data.Sqlite;

namespace Benchmark.Infrastructure;

public sealed class DatabaseVersionException(string path,int actual):InvalidOperationException($"測定DBの版が異なります。明示的な移行が必要です。Path={path} Expected=4 Actual={actual}"){
    public int ActualVersion{get;}=actual;
}

public sealed class BenchmarkDatabase{
    public const int Version=4;
    public const int TableCount=20;
    private static readonly Lazy<bool> Provider=new(static ()=>{SQLitePCL.Batteries_V2.Init();return true;});
    public BenchmarkDatabase(string path){
        _=Provider.Value;
        this.Path=System.IO.Path.GetFullPath(path);
    }
    public string Path{get;}
    public static string Resolve(string repositoryRoot)=>System.IO.Path.Combine(System.IO.Path.GetFullPath(repositoryRoot),"Work","Benchmark","Benchmark.db");
    public bool InitializeIfMissing(){
        using var gate=new Mutex(false,"SeamlessBenchmarkDB-"+Fingerprint.Of(this.Path.ToUpperInvariant()));
        var owns=false;
        try{
            try{owns=gate.WaitOne(TimeSpan.FromSeconds(30));}catch(AbandonedMutexException){owns=true;}
            if(!owns){throw new TimeoutException("測定DBの初期化待機がタイムアウトしました。");}
            return this.InitializeCore();
        }finally{if(owns){gate.ReleaseMutex();}}
    }
    private bool InitializeCore(){
        if(File.Exists(this.Path)){return false;}
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.Path)!);
        using var connection=this.Connect(SqliteOpenMode.ReadWriteCreate);
        using(var command=connection.CreateCommand()){
            command.CommandText="PRAGMA journal_mode=WAL;";command.ExecuteNonQuery();
        }
        using var transaction=connection.BeginTransaction();
        using var schema=connection.CreateCommand();
        schema.Transaction=transaction;
        schema.CommandText=ReadResource("Initial.sql");
        schema.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }
    public int ReadVersion(){
        if(!File.Exists(this.Path)){return 0;}
        using var connection=this.Connect(SqliteOpenMode.ReadOnly);
        using var command=connection.CreateCommand();command.CommandText="PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
    public SqliteConnection OpenRead()=>this.Open(SqliteOpenMode.ReadOnly);
    public SqliteConnection OpenWrite()=>this.Open(SqliteOpenMode.ReadWrite);
    private SqliteConnection Open(SqliteOpenMode mode){
        var connection=this.Connect(mode);
        try{
            using var command=connection.CreateCommand();command.CommandText="PRAGMA user_version;";
            var version=Convert.ToInt32(command.ExecuteScalar());
            if(version!=Version){throw new DatabaseVersionException(this.Path,version);}
            return connection;
        }catch{connection.Dispose();throw;}
    }
    private SqliteConnection Connect(SqliteOpenMode mode){
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=this.Path,Mode=mode,Pooling=false,DefaultTimeout=30}.ToString());
        connection.Open();
        using var command=connection.CreateCommand();command.CommandText="PRAGMA foreign_keys=ON;PRAGMA busy_timeout=30000;";command.ExecuteNonQuery();
        return connection;
    }
    /// <summary>アプリを停止した明示操作でのみ使う。元DBをSQLite Backup APIで保全し、同じスナップショットを移行する。</summary>
    public static string ImportDynamic(string sourcePath,string destinationPath,bool writersStopped){
        if(!writersStopped){throw new InvalidOperationException("DynamicとScalableの書き込みを停止してください。");}
        _=Provider.Value;
        sourcePath=System.IO.Path.GetFullPath(sourcePath);destinationPath=System.IO.Path.GetFullPath(destinationPath);
        if(sourcePath==destinationPath){throw new ArgumentException("移行元と移行先は別ファイルにしてください。");}
        if(File.Exists(destinationPath)){throw new IOException("移行先は既に存在します。既存の履歴は上書きしません。");}
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath)!);
        var backup=sourcePath+".backup-"+DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=sourcePath,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
        source.Open();
        using(var check=source.CreateCommand()){
            check.CommandText="PRAGMA user_version;";
            if(Convert.ToInt32(check.ExecuteScalar())!=3){throw new InvalidDataException("移行元はDynamic DB版3である必要があります。");}
        }
        using(var saved=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=backup,Pooling=false}.ToString())){
            saved.Open();source.BackupDatabase(saved);
        }
        File.Copy(backup,destinationPath,false);
        var destination=new BenchmarkDatabase(destinationPath);
        using var connection=destination.Connect(SqliteOpenMode.ReadWrite);
        using var transaction=connection.BeginTransaction();
        using(var migration=connection.CreateCommand()){
            migration.Transaction=transaction;migration.CommandText=ReadResource("Common.sql");migration.ExecuteNonQuery();
        }
        using(var imported=connection.CreateCommand()){
            imported.Transaction=transaction;
            imported.CommandText="INSERT INTO BenchmarkImports VALUES($source,$at,$backup);";
            imported.Parameters.AddWithValue("$source",sourcePath);imported.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));imported.Parameters.AddWithValue("$backup",backup);imported.ExecuteNonQuery();
        }
        using(var history=connection.CreateCommand()){
            history.Transaction=transaction;
            history.CommandText="""
                INSERT INTO BenchmarkRuns
                SELECT run_id,'Dynamic','Legacy',created_at,
                    json_object('ID',run_id,'Application','Dynamic','Status','Legacy','SettingsJSON',settings_json,
                        'EnvironmentJSON','{}','CodeFingerprint',worker_version,'CreatedAt',created_at,'CompletedAt',completed_at)
                FROM DynamicRuns;
                """;
            history.ExecuteNonQuery();
        }
        using(var check=connection.CreateCommand()){
            check.Transaction=transaction;check.CommandText="PRAGMA foreign_key_check;";
            using var reader=check.ExecuteReader();if(reader.Read()){throw new InvalidDataException("移行後の参照整合性が失われています。");}
        }
        transaction.Commit();
        return backup;
    }
    private static string ReadResource(string name){
        using var stream=typeof(BenchmarkDatabase).Assembly.GetManifestResourceStream("Benchmark.Infrastructure.Storage."+name)??throw new InvalidDataException("Missing schema: "+name);
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
}
