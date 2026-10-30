using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class DatabaseRegistry{
    private readonly object schemaGate=new();
    private readonly Dictionary<string,(string Stamp,long Version)>schemaVersions=new(StringComparer.OrdinalIgnoreCase);
    private long nextSchemaVersion;
    public bool CanCachePlans=>!this.databases.Values.Any(registration=>registration.Adapter is InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer runtime}&&runtime.RuntimeSql.HasActiveTransaction);
    public long GetGeneration(string database){
        this.RejectForeignDatabaseTransaction(database);var registration=this.Resolve(database);
        if(registration.Adapter is not InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer}){return this.Generation;}
        var state=RuntimeState((InProcessDatabaseAdapter)registration.Adapter);var stamp=state.Revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture)+":"+state.Structure;
        lock(this.schemaGate){
            if(this.schemaVersions.TryGetValue(registration.Name,out var previous)&&previous.Stamp==stamp){return previous.Version;}
            var version=checked(++this.nextSchemaVersion);this.schemaVersions[registration.Name]=(stamp,version);return version;
        }
    }
    private void RejectForeignDatabaseTransaction(string database){
        foreach(var registration in this.databases.Values){if(registration.Adapter is InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer runtime}&&runtime.RuntimeSql.HasActiveTransaction&&!string.Equals(registration.Name,database,StringComparison.OrdinalIgnoreCase)){throw new SqlQueryException("A SQL write transaction cannot reference another database.",40515);}}
    }
    internal static string EffectiveSchemaFingerprint(IDatabaseAdapter adapter,string original){
        if(adapter is not InProcessDatabaseAdapter{Database:RuntimeSqlDatabaseContainer}){return original;}
        var state=RuntimeState((InProcessDatabaseAdapter)adapter);if(state.Empty){return original;}
        return Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(original+"|RuntimeSqlV1|"+state.Structure)));
    }
    private static (long Revision,string Structure,bool Empty)RuntimeState(InProcessDatabaseAdapter adapter){
        var database=(RuntimeSqlDatabaseContainer)adapter.Database;using var read=database.BeginReadSession();var revision=database.RuntimeSql.SchemaRevision;
        // Metadata is bounded at 1,024 hashes; table rows remain streamed.
        var hashes=new List<(long Id,byte[] Hash)>();
        foreach(var table in database.RuntimeSql.Tables){if(hashes.Count>=1024){throw new SqlQueryException("Runtime metadata exceeds the table limit.",40515);}hashes.Add((table.ObjectId,global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(table))));}
        hashes.Sort(static (first,second)=>first.Id.CompareTo(second.Id));using var hash=global::System.Security.Cryptography.IncrementalHash.CreateHash(global::System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach(var entry in hashes){hash.AppendData(entry.Hash);}
        return(revision,Convert.ToHexString(hash.GetHashAndReset()),hashes.Count==0);
    }
}
