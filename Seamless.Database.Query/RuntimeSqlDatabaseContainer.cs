using System.Security.Cryptography;
using System.Text;
using MemoryPack;
using Seamless.Database;

namespace Seamless.Database.Query;

[MemoryPackable]internal partial record struct RuntimeCatalogKey(long Id);
[MemoryPackable]internal partial record struct RuntimeCatalogRecord(RuntimeCatalogKey Key,byte[] Payload);
[MemoryPackable]internal partial record struct RuntimeRowKey(long ObjectId,long RowId);
[MemoryPackable]internal partial record struct RuntimeRowRecord(RuntimeRowKey Key,byte[] Payload);

/// <summary>新規 SharedV2 領域へ動的 SQL 用の固定 backing を事前登録する。</summary>
public abstract class RuntimeSqlDatabaseContainer:DatabaseContainer{
    private readonly Table<RuntimeCatalogKey,RuntimeCatalogRecord> catalog;
    private readonly Table<RuntimeRowKey,RuntimeRowRecord> rows;
    protected RuntimeSqlDatabaseContainer(DatabaseContainerOptions options,string schemaIdentity):base(ValidateProfile(options,schemaIdentity)){
        try{
            this.catalog=this.Table<RuntimeCatalogKey,RuntimeCatalogRecord>(static value=>value.Key,"~runtime-sql-v1.catalog");
            this.rows=this.Table<RuntimeRowKey,RuntimeRowRecord>(static value=>value.Key,"~runtime-sql-v1.rows");
            this.RuntimeSql=new RuntimeSqlCatalog(this,this.catalog,this.rows);
        }catch(Exception error){
            try{this.Dispose();}catch(Exception cleanup){throw new AggregateException(error,cleanup);}throw;
        }
    }
    public RuntimeSqlCatalog RuntimeSql{get;}
    private static DatabaseContainerOptions ValidateProfile(DatabaseContainerOptions options,string schemaIdentity){
        ArgumentNullException.ThrowIfNull(options);ArgumentException.ThrowIfNullOrWhiteSpace(schemaIdentity);
        if(options.Transactions!=DatabaseTransactionMode.SharedV2){throw new InvalidOperationException("Runtime SQL requires SharedV2.");}
        if(options.StorageMode==DatabaseStorageMode.Memory){return options;}
        var path=options.StorageDirectory;if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)){throw new InvalidOperationException("Runtime SQL requires an explicit absolute storage directory.");}
        path=Path.GetFullPath(path);var marker=Path.Combine(path,"runtime-sql-v1.profile");var identity="RuntimeSqlV1|"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schemaIdentity)))+"\n";
        if(Directory.Exists(path)){
            if(File.Exists(marker)){if(File.ReadAllText(marker,Encoding.UTF8)!=identity){throw new InvalidDataException("Runtime SQL storage profile does not match.");}return options;}
            if(Directory.EnumerateFileSystemEntries(path).Any()){throw new InvalidDataException("Existing storage has no RuntimeSqlV1 profile.");}
        }else{Directory.CreateDirectory(path);}
        using var file=new FileStream(marker,FileMode.CreateNew,FileAccess.Write,FileShare.None);var bytes=Encoding.UTF8.GetBytes(identity);file.Write(bytes);file.Flush(true);return options;
    }
}
