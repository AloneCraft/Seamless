using System.Text;
using System.Text.Json;
using Seamless.Database;

namespace Seamless.Database.Query;

public sealed record RuntimeSqlColumn(string Name,string SqlTypeName,bool IsNullable,int? Length=null,byte? Precision=null,byte? Scale=null,bool IsPrimaryKey=false,string? Collation=null);
public sealed record RuntimeSqlTable(long ObjectId,string Schema,string Name,IReadOnlyList<RuntimeSqlColumn> Columns);
public sealed record RuntimeSqlRow(long RowId,object?[] Values);

/// <summary>同一 SharedV2 取引に参加する限定された動的 SQL カタログ。</summary>
public sealed class RuntimeSqlCatalog{
    private const int MaxTables=1024,MaxColumns=128,MaxPayload=16*1024*1024;
    private static readonly UTF8Encoding Utf8=new(false,true);
    private readonly AsyncLocal<RuntimeSqlTransaction?> current=new();
    private readonly RuntimeSqlDatabaseContainer owner;
    private readonly Table<RuntimeCatalogKey,RuntimeCatalogRecord> catalog;
    private readonly Table<RuntimeRowKey,RuntimeRowRecord> rows;
    private sealed record State(int Version,long NextObjectId,long NextRowId,long Revision);
    private sealed record Definition(int Version,long ObjectId,string Schema,string Name,RuntimeSqlColumn[] Columns,bool Dropped);
    internal RuntimeSqlCatalog(RuntimeSqlDatabaseContainer owner,Table<RuntimeCatalogKey,RuntimeCatalogRecord> catalog,Table<RuntimeRowKey,RuntimeRowRecord> rows){this.owner=owner;this.catalog=catalog;this.rows=rows;}
    public bool HasActiveTransaction=>this.current.Value is {IsDisposed:false};
    public bool IsRollbackOnly=>this.current.Value is {Finished:false,RollbackOnly:true};
    public void MarkRollbackOnly(){var scope=this.current.Value;if(scope is null||scope.Finished){throw new InvalidOperationException("An active RuntimeSql transaction is required.");}scope.MarkRollbackOnly();}
    public long SchemaRevision{get{using var scope=this.owner.BeginReadSession();return this.ReadState().Revision;}}
    public IEnumerable<RuntimeSqlTable> Tables=>this.EnumerateTables();
    private IEnumerable<RuntimeSqlTable> EnumerateTables(){using var scope=this.owner.BeginReadSession();_=this.ReadState();foreach(var record in this.catalog){if(record.Key.Id==0){continue;}var definition=DecodeDefinition(record);if(!definition.Dropped){yield return Public(definition);}}}
    public RuntimeSqlTable? FindTable(string schema,string name){ValidateName(schema);ValidateName(name);using var scope=this.owner.BeginReadSession();_=this.ReadState();var found=this.Lookup(schema,name);return found is null?null:Public(found);}
    public RuntimeSqlTable CreateTable(string schema,string name,IReadOnlyList<RuntimeSqlColumn> columns){
        this.RequireTransaction();ValidateName(schema);ValidateName(name);var validated=ValidateColumns(columns);if(this.Lookup(schema,name) is not null){throw new InvalidOperationException("Table already exists.");}
        var count=0;foreach(var item in this.catalog){if(item.Key.Id!=0&&!DecodeDefinition(item).Dropped&&++count>=MaxTables){throw new InvalidOperationException("Runtime table limit reached.");}}
        var state=this.ReadState();var id=state.NextObjectId;if(id>1_000_000_000L){throw new InvalidOperationException("Runtime SQL object ID limit reached.");}var definition=new Definition(1,id,schema,name,validated,false);var next=state with{NextObjectId=checked(id+1),Revision=checked(state.Revision+1)};this.ApplyMutation(()=>{this.PutDefinition(definition);this.PutState(next);});return Public(definition);
    }
    public void AddNullableColumn(string schema,string name,RuntimeSqlColumn column){
        this.RequireTransaction();var definition=this.Get(schema,name);if(column is null||!column.IsNullable||column.IsPrimaryKey){throw new InvalidOperationException("Only nullable non-key columns may be added.");}
        var columns=ValidateColumns(definition.Columns.Append(column).ToArray());var state=this.ReadState();var next=state with{Revision=checked(state.Revision+1)};this.ApplyMutation(()=>{this.PutDefinition(definition with{Columns=columns});this.PutState(next);});
    }
    public void DropTable(string schema,string name){this.RequireTransaction();var definition=this.Get(schema,name);var state=this.ReadState();var next=state with{Revision=checked(state.Revision+1)};this.ApplyMutation(()=>{this.PutDefinition(definition with{Dropped=true});this.PutState(next);});}
    public long InsertRow(string schema,string name,IReadOnlyList<object?> values){
        this.RequireTransaction();var definition=this.Get(schema,name);var encoded=Encode(values,definition.Columns);this.ValidatePrimaryKey(definition,values,null);var state=this.ReadState();var id=state.NextRowId;var next=state with{NextRowId=checked(id+1)};
        this.ApplyMutation(()=>{if(!this.rows.Add(new(new(definition.ObjectId,id),encoded))){throw new InvalidDataException("Runtime row ID collision.");}this.PutState(next);});return id;
    }
    public void UpdateRow(string schema,string name,long rowId,IReadOnlyList<object?> values){
        this.RequireTransaction();var definition=this.Get(schema,name);var key=new RuntimeRowKey(definition.ObjectId,rowId);if(rowId<=0||!this.rows.ContainsKey(key)){throw new KeyNotFoundException("Runtime row not found.");}
        var encoded=Encode(values,definition.Columns);this.ValidatePrimaryKey(definition,values,rowId);this.ApplyMutation(()=>this.rows[key]=new(key,encoded));
    }
    public void DeleteRow(string schema,string name,long rowId){this.RequireTransaction();var definition=this.Get(schema,name);var key=new RuntimeRowKey(definition.ObjectId,rowId);if(rowId<=0||!this.rows.ContainsKey(key)){throw new KeyNotFoundException("Runtime row not found.");}this.ApplyMutation(()=>{if(!this.rows.Remove(key)){throw new InvalidDataException("Runtime row disappeared.");}});}
    public IEnumerable<RuntimeSqlRow> ReadRows(string schema,string name,long expectedSchemaRevision){
        using var scope=this.owner.BeginReadSession();if(this.ReadState().Revision!=expectedSchemaRevision){throw new InvalidOperationException("Runtime schema revision changed.");}
        var definition=this.Get(schema,name);foreach(var row in this.rows){if(this.ReadState().Revision!=expectedSchemaRevision){throw new InvalidOperationException("Runtime schema revision changed.");}if(row.Key.ObjectId==definition.ObjectId){yield return new(row.Key.RowId,Decode(row.Payload,definition.Columns));}}if(this.ReadState().Revision!=expectedSchemaRevision){throw new InvalidOperationException("Runtime schema revision changed.");}
    }
    public RuntimeSqlRow ReadRow(string schema,string name,long rowId,long expectedSchemaRevision){using var scope=this.owner.BeginReadSession();if(this.ReadState().Revision!=expectedSchemaRevision){throw new InvalidOperationException("Runtime schema revision changed.");}var definition=this.Get(schema,name);if(!this.rows.TryGetValue(new(definition.ObjectId,rowId),out var row)){throw new KeyNotFoundException("Runtime row not found.");}return new(row.Key.RowId,Decode(row.Payload,definition.Columns));}
    public RuntimeSqlTransaction BeginTransaction()=>this.BeginTransaction(CancellationToken.None);
    public RuntimeSqlTransaction BeginTransaction(CancellationToken cancellationToken){
        if(this.current.Value is not null){throw new InvalidOperationException("A runtime SQL transaction is already active.");}
        var scope=new RuntimeSqlTransaction(this,this.owner.BeginTransaction(cancellationToken));this.current.Value=scope;return scope;
    }
    private void RequireTransaction(){var scope=this.current.Value;if(scope is null||scope.Finished||scope.RollbackOnly){throw new InvalidOperationException("An active RuntimeSql transaction is required.");}}
    private void ApplyMutation(Action action){try{action();}catch{this.current.Value!.AbortAfterWriteFailure();throw;}}
    public sealed class RuntimeSqlTransaction:IDisposable{
        private readonly RuntimeSqlCatalog catalog;private readonly DatabaseTransaction transaction;private bool completed;private bool disposed;private bool closed;private bool rollbackOnly;
        internal RuntimeSqlTransaction(RuntimeSqlCatalog catalog,DatabaseTransaction transaction){this.catalog=catalog;this.transaction=transaction;}
        internal bool IsDisposed=>this.closed;
        internal bool Finished=>this.completed||this.disposed;
        internal bool RollbackOnly=>this.rollbackOnly;
        internal void MarkRollbackOnly(){this.Check();this.transaction.Rollback();this.rollbackOnly=true;}
        private void Check(){if(this.Finished||!ReferenceEquals(this.catalog.current.Value,this)){throw new InvalidOperationException("Runtime SQL transaction is not active in this context.");}}
        public void Commit(){this.Check();if(this.rollbackOnly){throw new InvalidOperationException("Transaction is rollback-only.");}this.transaction.Commit();this.completed=true;}
        public void Rollback(){this.Check();this.transaction.Rollback();this.completed=true;}
        internal void AbortAfterWriteFailure(){this.transaction.Rollback();this.rollbackOnly=true;}
        public void Dispose(){if(this.disposed){return;}if(!ReferenceEquals(this.catalog.current.Value,this)){throw new InvalidOperationException("Runtime SQL transaction does not belong to this context.");}this.disposed=true;try{this.transaction.Dispose();}finally{this.closed=true;this.catalog.current.Value=null;}}
    }
    private Definition Get(string schema,string name){ValidateName(schema);ValidateName(name);return this.Lookup(schema,name)??throw new KeyNotFoundException("Runtime table not found.");}
    private Definition? Lookup(string schema,string name){foreach(var item in this.catalog){if(item.Key.Id==0){continue;}var definition=DecodeDefinition(item);if(!definition.Dropped&&string.Equals(definition.Schema,schema,StringComparison.OrdinalIgnoreCase)&&string.Equals(definition.Name,name,StringComparison.OrdinalIgnoreCase)){return definition;}}return null;}
    private State ReadState(){if(!this.catalog.TryGetValue(new(0),out var record)){if(this.catalog.Any()||this.rows.Any()){throw new InvalidDataException("Runtime state is missing.");}return new(1,1,1,0);}if(record.Payload is null||record.Payload.Length>4096){throw new InvalidDataException("Invalid runtime state.");}var state=JsonSerializer.Deserialize<State>(record.Payload)??throw new InvalidDataException("Missing runtime state.");if(state.Version!=1||state.NextObjectId<1||state.NextRowId<1||state.Revision<0){throw new InvalidDataException("Invalid runtime state.");}return state;}
    private void PutState(State state)=>this.catalog[new(0)]=new(new(0),JsonSerializer.SerializeToUtf8Bytes(state));
    private void PutDefinition(Definition value){var bytes=JsonSerializer.SerializeToUtf8Bytes(value);if(bytes.Length>1024*1024){throw new InvalidOperationException("Runtime definition is too large.");}var key=new RuntimeCatalogKey(value.ObjectId);this.catalog[key]=new(key,bytes);}
    private static Definition DecodeDefinition(RuntimeCatalogRecord record){if(record.Key.Id<=0||record.Payload is null||record.Payload.Length>1024*1024){throw new InvalidDataException("Invalid runtime definition.");}var value=JsonSerializer.Deserialize<Definition>(record.Payload)??throw new InvalidDataException("Missing runtime definition.");if(value.Version!=1||value.ObjectId!=record.Key.Id){throw new InvalidDataException("Invalid runtime definition identity.");}try{ValidateName(value.Schema);ValidateName(value.Name);value=value with{Columns=ValidateColumns(value.Columns)};}catch(Exception e)when(e is ArgumentException or InvalidOperationException){throw new InvalidDataException("Invalid runtime definition.",e);}return value;}
    private static RuntimeSqlTable Public(Definition definition)=>new(definition.ObjectId,definition.Schema,definition.Name,Array.AsReadOnly(definition.Columns.ToArray()));
    private static void ValidateName(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>128||name.Contains('\0')){throw new ArgumentException("Invalid SQL identifier.");}}
    private static RuntimeSqlColumn[] ValidateColumns(IReadOnlyList<RuntimeSqlColumn> columns){
        ArgumentNullException.ThrowIfNull(columns);if(columns.Count is <1 or >MaxColumns){throw new InvalidOperationException("Runtime column count is outside the supported limit.");}
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new RuntimeSqlColumn[columns.Count];
        for(var i=0;i<columns.Count;i++){var c=columns[i]??throw new ArgumentException("Null column.");ValidateName(c.Name);if(!names.Add(c.Name)){throw new InvalidOperationException("Duplicate column name.");}var type=c.SqlTypeName?.ToLowerInvariant();
            switch(type){
                case "int":case "bigint":case "bit":if(c.Length is not null||c.Precision is not null||c.Scale is not null){throw new InvalidOperationException("Invalid type attributes.");}break;
                case "nvarchar":case "varbinary":var max=type=="nvarchar"?4000:8000;if(c.Length is null||c.Length is 0 or < -1||c.Length>max||c.Precision is not null||c.Scale is not null){throw new InvalidOperationException("Invalid type length.");}if(c.IsPrimaryKey){throw new InvalidOperationException("Character and binary primary keys are not supported.");}break;
                case "decimal":if(c.Length is not null||c.Precision is null or 0 or >28||c.Scale is null||c.Scale>c.Precision){throw new InvalidOperationException("Invalid decimal type.");}break;
                default:throw new InvalidOperationException("Unsupported runtime SQL type.");
            }
            if(c.Collation is not null&&(type!="nvarchar"||c.Collation.ToUpperInvariant() is not ("SQL_LATIN1_GENERAL_CP1_CI_AS" or "JAPANESE_CI_AS" or "LATIN1_GENERAL_100_CI_AS" or "LATIN1_GENERAL_BIN2" or "LATIN1_GENERAL_100_BIN2" or "LATIN1_GENERAL_100_BIN2_UTF8"))){throw new InvalidOperationException("Unsupported column collation.");}
            if(c.IsPrimaryKey&&c.IsNullable){throw new InvalidOperationException("Primary key columns cannot be nullable.");}result[i]=c with{SqlTypeName=type!};
        }return result;
    }
    private void ValidatePrimaryKey(Definition table,IReadOnlyList<object?> values,long? excluded){
        if(!table.Columns.Any(c=>c.IsPrimaryKey)){return;}
        foreach(var row in this.rows){if(row.Key.ObjectId!=table.ObjectId||row.Key.RowId==excluded){continue;}var existing=Decode(row.Payload,table.Columns);var match=true;for(var i=0;i<table.Columns.Length;i++){if(table.Columns[i].IsPrimaryKey&&!Equals(existing[i],values[i])){match=false;break;}}if(match){throw new InvalidOperationException("Duplicate primary key.");}}
    }
    private static byte[] Encode(IReadOnlyList<object?> values,RuntimeSqlColumn[] columns){
        ArgumentNullException.ThrowIfNull(values);if(values.Count!=columns.Length){throw new ArgumentException("Row column count differs from schema.");}long length=8;var textBytes=new int[columns.Length];
        for(var i=0;i<columns.Length;i++){var value=values[i];var column=columns[i];length++;if(value is null||value is DBNull){if(!column.IsNullable){throw new InvalidOperationException("NULL is not allowed.");}continue;}
            switch(column.SqlTypeName){
                case "int" when value is int:length+=4;break;case "bigint" when value is long:length+=8;break;case "bit" when value is bool:length++;break;
                case "decimal" when value is decimal number:ValidateDecimal(number,column);length+=16;break;
                case "nvarchar" when value is string text:if(column.Length!=-1&&text.Length>column.Length){throw new InvalidOperationException("String truncation.");}textBytes[i]=Utf8.GetByteCount(text);length+=4L+textBytes[i];break;
                case "varbinary" when value is byte[] binary:if(column.Length!=-1&&binary.Length>column.Length){throw new InvalidOperationException("Binary truncation.");}length+=4L+binary.Length;break;
                default:throw new InvalidOperationException("Value type does not match runtime SQL type.");
            }if(length>MaxPayload){throw new InvalidOperationException("Runtime row exceeds 16 MiB.");}
        }
        using var stream=new MemoryStream(checked((int)length));using var writer=new BinaryWriter(stream,Utf8,true);writer.Write(1);writer.Write(columns.Length);
        for(var i=0;i<columns.Length;i++){var value=values[i];if(value is null||value is DBNull){writer.Write((byte)0);continue;}writer.Write((byte)1);switch(columns[i].SqlTypeName){case "int":writer.Write((int)value);break;case "bigint":writer.Write((long)value);break;case "bit":writer.Write((byte)((bool)value?1:0));break;case "decimal":writer.Write((decimal)value);break;case "nvarchar":var bytes=Utf8.GetBytes((string)value);writer.Write(bytes.Length);writer.Write(bytes);break;case "varbinary":var binary=(byte[])value;writer.Write(binary.Length);writer.Write(binary);break;}}
        return stream.ToArray();
    }
    private static object?[] Decode(byte[] payload,RuntimeSqlColumn[] columns){
        if(payload is null||payload.Length>MaxPayload){throw new InvalidDataException("Invalid runtime row length.");}
        try{using var stream=new MemoryStream(payload,false);using var reader=new BinaryReader(stream,Utf8,true);if(reader.ReadInt32()!=1){throw new InvalidDataException("Unknown runtime row format.");}var count=reader.ReadInt32();if(count<1||count>columns.Length){throw new InvalidDataException("Invalid runtime row column count.");}var result=new object?[columns.Length];
            for(var i=0;i<count;i++){var tag=reader.ReadByte();if(tag==0){if(!columns[i].IsNullable){throw new InvalidDataException("Unexpected NULL.");}continue;}if(tag!=1){throw new InvalidDataException("Invalid runtime row tag.");}result[i]=columns[i].SqlTypeName switch{"int"=>reader.ReadInt32(),"bigint"=>reader.ReadInt64(),"bit"=>ReadBit(reader),"decimal"=>reader.ReadDecimal(),"nvarchar"=>Utf8.GetString(ReadBytes(reader,stream)),"varbinary"=>ReadBytes(reader,stream),_=>throw new InvalidDataException("Unsupported stored SQL type.")};}
            for(var i=count;i<columns.Length;i++){if(!columns[i].IsNullable){throw new InvalidDataException("Missing non-null column.");}}if(stream.Position!=stream.Length){throw new InvalidDataException("Trailing runtime row data.");}
            _ = Encode(result,columns);return result;
        }catch(Exception e)when(e is EndOfStreamException or DecoderFallbackException or ArgumentException or OverflowException or InvalidOperationException){throw new InvalidDataException("Malformed runtime row.",e);}
    }
    private static bool ReadBit(BinaryReader reader)=>reader.ReadByte() switch{0=>false,1=>true,_=>throw new InvalidDataException("Invalid bit value.")};
    private static byte[] ReadBytes(BinaryReader reader,Stream stream){var length=reader.ReadInt32();if(length<0||length>MaxPayload||length>stream.Length-stream.Position){throw new InvalidDataException("Invalid value length.");}var bytes=reader.ReadBytes(length);if(bytes.Length!=length){throw new EndOfStreamException();}return bytes;}
    private static void ValidateDecimal(decimal value,RuntimeSqlColumn column){var scale=column.Scale!.Value;if(decimal.Round(value,scale)!=value){throw new InvalidOperationException("Decimal scale conversion would round.");}var integerDigits=column.Precision!.Value-scale;decimal limit=1;for(var i=0;i<integerDigits;i++){limit*=10;}if(decimal.Abs(value)>=limit){throw new InvalidOperationException("Decimal precision overflow.");}}
}
