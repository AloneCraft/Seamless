using System.Text.Json;
using Microsoft.Data.Sqlite;
using Verification.Core;
namespace Verification.Infrastructure;

public sealed class VerificationRepository:IVerificationRepository{
    private readonly string path;
    public VerificationRepository(string path){
        this.path=Path.GetFullPath(path);
        if(!File.Exists(this.path)){throw new FileNotFoundException("先に seamless-verify init を実行してください。",this.path);}
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText="PRAGMA user_version";
        if(Convert.ToInt32(command.ExecuteScalar())!=1){throw new InvalidDataException("未対応の検証DBです。自動移行は行いません。");}
    }
    public static void Initialize(string path){
        path=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){}
        try{
            using var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadWrite}.ToString());
            connection.Open();
            using var command=connection.CreateCommand();
            command.CommandText="""
                PRAGMA journal_mode=WAL;
                CREATE TABLE Runs(Id TEXT PRIMARY KEY,ParentId TEXT,StartedAt TEXT NOT NULL,Status TEXT NOT NULL,Plan TEXT NOT NULL);
                CREATE TABLE Results(RunId TEXT NOT NULL,CaseId TEXT NOT NULL,Payload TEXT NOT NULL,PRIMARY KEY(RunId,CaseId),FOREIGN KEY(RunId) REFERENCES Runs(Id));
                CREATE TABLE Settings(Name TEXT PRIMARY KEY,Value TEXT NOT NULL);
                PRAGMA user_version=1;
                """;
            command.ExecuteNonQuery();
        }catch{
            throw;
        }
    }
    private SqliteConnection Open(){
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=this.path,Mode=SqliteOpenMode.ReadWrite,ForeignKeys=true,DefaultTimeout=30}.ToString());
        connection.Open();
        return connection;
    }
    public void CreateRun(VerificationRun run){
        this.Execute("INSERT INTO Runs VALUES($id,$parent,$time,$status,$plan)",("$id",run.Id),("$parent",run.ParentId),("$time",run.StartedAt.ToString("O")),("$status",run.Status.ToString()),("$plan",JsonSerializer.Serialize(run.Plan)));
    }
    public void SaveResult(string runId,CaseResult result){
        this.Execute("INSERT INTO Results VALUES($run,$case,$payload) ON CONFLICT(RunId,CaseId) DO UPDATE SET Payload=excluded.Payload",("$run",runId),("$case",result.CaseId),("$payload",JsonSerializer.Serialize(result)));
    }
    public void CompleteRun(string runId,CaseStatus status)=>this.Execute("UPDATE Runs SET Status=$status WHERE Id=$id",("$id",runId),("$status",status.ToString()));
    public VerificationRun GetRun(string runId){
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT Id,ParentId,StartedAt,Status,Plan FROM Runs WHERE Id=$id";
        command.Parameters.AddWithValue("$id",runId);
        using var reader=command.ExecuteReader();
        if(!reader.Read()){throw new KeyNotFoundException("実行が見つかりません: "+runId);}
        return ReadRun(reader);
    }
    public IReadOnlyList<VerificationRun> GetRuns(int limit=100){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT Id,ParentId,StartedAt,Status,Plan FROM Runs ORDER BY StartedAt DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit",Math.Min(limit,1000));
        using var reader=command.ExecuteReader();
        var results=new List<VerificationRun>();
        while(reader.Read()){results.Add(ReadRun(reader));}
        return results;
    }
    public int RecoverInterruptedRuns(string artifactRoot){
        artifactRoot=Path.GetFullPath(artifactRoot);
        var recovered=0;
        foreach(var run in this.GetRuns(1000).Where(static run=>run.Status==CaseStatus.Running)){
            var directory=Path.GetFullPath(Path.Combine(artifactRoot,run.Id));
            if(!directory.StartsWith(artifactRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){continue;}
            FileStream? lease=null;
            try{
                Directory.CreateDirectory(directory);
                lease=new FileStream(Path.Combine(directory,"run.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            }catch(IOException){continue;}
            using(lease){
                foreach(var result in this.GetResults(run.Id).Where(static result=>result.Status==CaseStatus.Running)){
                    this.SaveResult(run.Id,result with{
                        Status=CaseStatus.Interrupted,
                        Diagnostic="検証プロセスが完了記録を残さず終了しました。resume でこのケースを測り直してください。",
                    });
                }
                this.CompleteRun(run.Id,CaseStatus.Interrupted);
                recovered++;
            }
        }
        return recovered;
    }
    private static VerificationRun ReadRun(SqliteDataReader reader){
        string? parent=null;
        if(!reader.IsDBNull(1)){parent=reader.GetString(1);}
        return new(reader.GetString(0),parent,DateTimeOffset.Parse(reader.GetString(2),System.Globalization.CultureInfo.InvariantCulture),Enum.Parse<CaseStatus>(reader.GetString(3)),JsonSerializer.Deserialize<VerificationPlan>(reader.GetString(4))!);
    }
    public IReadOnlyList<CaseResult> GetResults(string runId){
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT Payload FROM Results WHERE RunId=$id ORDER BY CaseId";
        command.Parameters.AddWithValue("$id",runId);
        using var reader=command.ExecuteReader();
        var results=new List<CaseResult>();
        while(reader.Read()){results.Add(JsonSerializer.Deserialize<CaseResult>(reader.GetString(0))!);}
        return results;
    }
    public void SetBaseline(string runId){
        var run=this.GetRun(runId);
        var results=this.GetResults(runId);
        if(run.Status!=CaseStatus.Passed||results.Count!=run.Plan.Cases.Length||results.Count==0||results.Any(static x=>x.Status!=CaseStatus.Passed)){
            throw new InvalidOperationException("全ケースが完了した成功実行だけを基準に設定できます。");
        }
        this.Execute("INSERT INTO Settings VALUES('Baseline',$id) ON CONFLICT(Name) DO UPDATE SET Value=excluded.Value",("$id",runId));
    }
    public string? GetBaseline(){
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT Value FROM Settings WHERE Name='Baseline'";
        return command.ExecuteScalar() as string;
    }
    private void Execute(string sql,params (string Name,object? Value)[] parameters){
        using var connection=this.Open();
        using var command=connection.CreateCommand();
        command.CommandText=sql;
        foreach(var parameter in parameters){command.Parameters.AddWithValue(parameter.Name,parameter.Value??DBNull.Value);}
        command.ExecuteNonQuery();
    }
}
