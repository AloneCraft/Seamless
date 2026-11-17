using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class DatabaseRegistry{
    private readonly object schemaGate=new();
    private readonly Dictionary<string,(string Stamp,long Version)>schemaVersions=new(StringComparer.OrdinalIgnoreCase);
    private long nextSchemaVersion;
    // 一時表は接続ごとに形が違い得るので、使っている接続では実行計画を共有しない。
    public bool CanCachePlans=>sqlSession.Value is not {TemporaryTables.Count:>0}&&sqlSession.Value?.Transaction?.Participants.Values.Any(static participant=>participant.Candidate is not null)!=true&&!this.databases.Values.Any(registration=>registration.Adapter is InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer runtime}&&runtime.RuntimeSql.HasActiveTransaction);
    public long GetGeneration(string database){
        this.RejectForeignDatabaseTransaction(database);
        // msdb などの値を持たないシステムデータベースは、登録が無くても形だけ引ける。
        if(!this.databases.ContainsKey(database)&&Sql.SqlServerSystemShape.IsSystemDatabase(database)){return this.Generation;}
        var registration=this.Resolve(database);
        if(registration.Adapter is InProcessDatabaseAdapter managed&&this.GatewayTransactions?.GenerationEpoch(managed) is{} epoch){return epoch;}
        if(registration.Adapter is not InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer}){return this.Generation;}
        var state=RuntimeState((InProcessDatabaseAdapter)registration.Adapter);var stamp=state.Revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture)+":"+state.Structure;
        lock(this.schemaGate){
            if(this.schemaVersions.TryGetValue(registration.Name,out var previous)&&previous.Stamp==stamp){return previous.Version;}
            var version=checked(++this.nextSchemaVersion);this.schemaVersions[registration.Name]=(stamp,version);return version;
        }
    }
    private void RejectForeignDatabaseTransaction(string database){
        this.databases.TryGetValue(database,out var target);
        foreach(var registration in this.databases.Values){if(registration.Adapter is InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer runtime}&&runtime.Options.GatewayProfile is null&&runtime.RuntimeSql.HasActiveTransaction&&!string.Equals(registration.Name,database,StringComparison.OrdinalIgnoreCase)&&!(target?.Adapter is InProcessDatabaseAdapter destination&&ReferenceEquals(destination.Database,runtime))){throw new SqlQueryException("A SQL write transaction cannot reference another database.",40515);}}
    }
    internal static string EffectiveSchemaFingerprint(IDatabaseAdapter adapter,string original){
        if(adapter is not InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer}){return original;}
        var state=RuntimeState((InProcessDatabaseAdapter)adapter);if(state.Empty){return original;}
        return Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(original+"|RuntimeSqlV1|"+state.Structure)));
    }
    private static (long Revision,string Structure,bool Empty)RuntimeState(InProcessDatabaseAdapter adapter){
        var database=(RuntimeSqlDatabaseContainer)adapter.Database;using var read=ReadSqlDatabase(adapter);var revision=database.RuntimeSql.SchemaRevision;
        // Metadata is bounded at 1,024 hashes; table rows remain streamed.
        var hashes=new List<(long Id,byte[] Hash)>();
        foreach(var table in database.RuntimeSql.Tables){if(hashes.Count>=1024){throw new SqlQueryException("Runtime metadata exceeds the table limit.",40515);}hashes.Add((table.ObjectId,global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(table))));}
        hashes.Sort(static (first,second)=>first.Id.CompareTo(second.Id));using var hash=global::System.Security.Cryptography.IncrementalHash.CreateHash(global::System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach(var entry in hashes){hash.AppendData(entry.Hash);}
        return(revision,Convert.ToHexString(hash.GetHashAndReset()),hashes.Count==0);
    }
}
