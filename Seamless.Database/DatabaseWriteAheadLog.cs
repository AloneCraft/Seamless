using System.Text.Json;

namespace Seamless.Database;

internal sealed record DatabaseWalOperation(
    int EncodingVersion,
    string TableName,
    bool Deleted,
    string KeyData,
    string? RecordData,
    global::Seamless.Execution.ResourceReservation? Reservation=null):IDisposable {
    public void Dispose()=>this.Reservation?.Dispose();
}

internal sealed record DatabaseWalRecord(
    int Version,
    string Kind,
    Guid TransactionId,
    string? TableName=null,
    bool Deleted=false,
    string? KeyData=null,
    string? RecordData=null);

internal sealed class DatabaseWriteAheadLog(string path) {
    private const int CurrentVersion=2;
    private readonly string path=path;

    public void Append(IEnumerable<DatabaseWalOperation> operations) {
        using var iterator=operations.GetEnumerator();
        if(!iterator.MoveNext()){return;}
        DatabaseWalOperation? current=iterator.Current;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
            var transactionId=Guid.NewGuid();
            using var stream=new FileStream(
                this.path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize:4096,
                FileOptions.WriteThrough);
            using var writer=new StreamWriter(stream,new UTF8Encoding(false),4096,leaveOpen:true);
            this.Write(writer,new(CurrentVersion,"begin",transactionId));
            while(current is not null) {
                using(current) {
                    this.Write(writer,new(current.EncodingVersion,"operation",transactionId,current.TableName,current.Deleted,current.KeyData,current.RecordData));
                }
                current=null;
                if(iterator.MoveNext()){current=iterator.Current;}
            }
            this.Write(writer,new(CurrentVersion,"commit",transactionId));
            writer.Flush();
            stream.Flush(flushToDisk:true);
        } finally {
            current?.Dispose();
        }
    }

    public IEnumerable<DatabaseWalOperation> Read(string tableName) {
        if(!File.Exists(this.path)){yield break;}
        var begun=new global::System.Collections.Generic.HashSet<Guid>();
        var committed=new global::System.Collections.Generic.HashSet<Guid>();
        foreach(var record in this.ReadRecords()) {
            if(record.Kind=="begin"){begun.Add(record.TransactionId);}
            if(record.Kind=="commit"&&begun.Contains(record.TransactionId)){committed.Add(record.TransactionId);}
        }
        foreach(var record in this.ReadRecords()) {
            if(record.Kind!="operation"||!committed.Contains(record.TransactionId)||record.TableName!=tableName){continue;}
            if(record.KeyData is null){throw new InvalidDataException("A committed WAL operation has no key.");}
            yield return new(record.Version,record.TableName,record.Deleted,record.KeyData,record.RecordData);
        }
    }

    private IEnumerable<DatabaseWalRecord> ReadRecords() {
        using var stream=new FileStream(this.path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=new StreamReader(stream,Encoding.UTF8,detectEncodingFromByteOrderMarks:true);
        var lineNumber=0;
        while(reader.ReadLine() is { }line) {
            lineNumber++;
            if(string.IsNullOrWhiteSpace(line)){continue;}
            DatabaseWalRecord? record;
            try{record=JsonSerializer.Deserialize<DatabaseWalRecord>(line);}
            catch(JsonException) when(reader.EndOfStream){yield break;}
            catch(JsonException error){throw new InvalidDataException($"Invalid database WAL record at line {lineNumber}: {this.path}",error);}
            if(record is null){throw new InvalidDataException($"Null database WAL record at line {lineNumber}: {this.path}");}
            if(record.Version is not (1 or CurrentVersion)){throw new InvalidDataException($"Unsupported database WAL version {record.Version}.");}
            if(record.Kind is not ("begin" or "operation" or "commit")){throw new InvalidDataException($"Unknown database WAL record kind '{record.Kind}'.");}
            yield return record;
        }
    }

    private void Write(StreamWriter writer,DatabaseWalRecord record){
        writer.WriteLine(JsonSerializer.Serialize(record));
    }

    public bool CanArchiveAfterCheckpoint(IReadOnlySet<string> checkpointedTables) {
        if(!File.Exists(this.path)){return true;}
        var begun=new global::System.Collections.Generic.HashSet<Guid>();
        var committed=new global::System.Collections.Generic.HashSet<Guid>();
        foreach(var record in this.ReadRecords()) {
            if(record.Kind=="begin"){begun.Add(record.TransactionId);}
            if(record.Kind=="commit"&&begun.Contains(record.TransactionId)){committed.Add(record.TransactionId);}
        }
        foreach(var record in this.ReadRecords()) {
            if(record.Kind=="operation"&&committed.Contains(record.TransactionId)
                &&record.TableName is not null&&!checkpointedTables.Contains(record.TableName)){
                return false;
            }
        }
        return true;
    }

    public string? ArchiveAfterCheckpoint() {
        if(!File.Exists(this.path)||new FileInfo(this.path).Length==0){return null;}
        var archiveDirectory=this.path+".archive";
        Directory.CreateDirectory(archiveDirectory);
        string hash;
        using(var input=File.OpenRead(this.path)){hash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(input));}
        var archivePath=Path.Combine(archiveDirectory,hash+".wal");
        var stagedArchive=archivePath+".new-"+Guid.NewGuid().ToString("N");
        var next=this.path+".next-"+Guid.NewGuid().ToString("N");
        try {
            if(!File.Exists(archivePath)) {
                File.Copy(this.path,stagedArchive,overwrite:false);
                using(var archive=new FileStream(stagedArchive,FileMode.Open,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.WriteThrough)){
                    archive.Flush(flushToDisk:true);
                    archive.Position=0;
                    var copiedHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(archive));
                    if(!copiedHash.Equals(hash,StringComparison.Ordinal)){
                        throw new IOException("The archived WAL hash does not match the active WAL.");
                    }
                }
                File.Move(stagedArchive,archivePath,overwrite:false);
            } else {
                using var existing=File.OpenRead(archivePath);
                var existingHash=Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(existing));
                if(!existingHash.Equals(hash,StringComparison.Ordinal)){
                    throw new IOException("The existing archived WAL does not match its content hash.");
                }
            }
            using(var stream=new FileStream(next,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){
                stream.Flush(flushToDisk:true);
            }
            File.Move(next,this.path,overwrite:true);
            return archivePath;
        } finally {
            if(File.Exists(stagedArchive)){File.Delete(stagedArchive);}
            if(File.Exists(next)){File.Delete(next);}
        }
    }
}
