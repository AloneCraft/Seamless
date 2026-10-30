using System.Reflection;
using System.Text;

namespace Seamless.Database.SqlServer;

public sealed class SqlServerImportSource:global::Seamless.Database.IDatabaseImportSource,IDisposable {
    private readonly SqlConnection connection;
    private readonly IReadOnlySet<(string Schema,string Table,string Column)> encryptedColumns;
    private readonly IReadOnlySet<(string Schema,string Table)> memoryOptimizedTables;
    private readonly SqlTransaction transaction;
    private readonly IReadOnlyDictionary<string,GeneratedTableBinding> tables;
    private readonly Dictionary<string,long> rowsReadByTable=new(StringComparer.Ordinal);
    private bool disposed;
    private bool completed;

    public long RowsRead { get; private set; }
    public IReadOnlyDictionary<string,long> RowsReadByTable=>this.rowsReadByTable;

    public SqlServerImportSource(string connectionString,IEnumerable<GeneratedTableBinding> tables,bool consistentRead=false) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tables);
        this.tables=tables.ToDictionary(static table=>table.ObjectName,StringComparer.Ordinal);
        var settings=new SqlConnectionStringBuilder(connectionString){ColumnEncryptionSetting=SqlConnectionColumnEncryptionSetting.Disabled};
        this.connection=new(settings.ConnectionString);
        this.connection.Open();
        try {
            this.encryptedColumns=ReadEncryptedColumns(this.connection);
            this.memoryOptimizedTables=ReadMemoryOptimizedTables(this.connection);
            var isolation=SelectIsolationLevel(this.memoryOptimizedTables.Count>0);
            if(consistentRead){using var check=this.connection.CreateCommand();check.CommandText="SELECT snapshot_isolation_state FROM sys.databases WHERE database_id=DB_ID();";var snapshot=Convert.ToInt32(check.ExecuteScalar())==1;isolation=IsolationLevel.Serializable;if(snapshot&&this.memoryOptimizedTables.Count==0){isolation=IsolationLevel.Snapshot;}}
            this.transaction=this.connection.BeginTransaction(isolation);
            if(consistentRead&&this.memoryOptimizedTables.Count>0){
                // Hekatonはsession SNAPSHOTを拒否する。全disk表のS lock取得後、mem表は同一txのSNAPSHOTで読む。
                foreach(var table in this.tables.Values.Where(t=>!this.memoryOptimizedTables.Contains((t.SourceSchema,t.SourceTable))).OrderBy(t=>t.SourceSchema,StringComparer.Ordinal).ThenBy(t=>t.SourceTable,StringComparer.Ordinal)){
                    using var hold=this.connection.CreateCommand();hold.Transaction=this.transaction;hold.CommandTimeout=0;hold.CommandText="SELECT COUNT_BIG(*) FROM "+Quote(table.SourceSchema)+"."+Quote(table.SourceTable)+" WITH (TABLOCK,HOLDLOCK);";hold.ExecuteScalar();
                }
            }
        } catch {
            this.connection.Dispose();
            throw;
        }
    }

    public IEnumerable<TRecord> ReadTable<TRecord>(global::Seamless.Database.System.Keys.DatabaseObject key) {
        ObjectDisposedException.ThrowIf(this.disposed,this);
        if(!this.tables.TryGetValue(key.Name,out var table)) {
            throw new KeyNotFoundException($"SQL Server table binding '{key.Name}' was not found.");
        }
        return this.Read<TRecord>(table);
    }

    private IEnumerable<TRecord> Read<TRecord>(GeneratedTableBinding table) {
        if(!this.rowsReadByTable.TryAdd(table.ObjectName,0)) {
            throw new InvalidOperationException($"SQL Server table '{table.ObjectName}' was read more than once during one import.");
        }
        var constructor=FindConstructor<TRecord>(table.Columns.Count);
        using var command=this.connection.CreateCommand();
        command.Transaction=this.transaction;
        command.CommandText=CreateSelect(
            table,
            this.memoryOptimizedTables.Contains((table.SourceSchema,table.SourceTable)),
            this.encryptedColumns);
        using var reader=command.ExecuteReader(CommandBehavior.SequentialAccess);
        var parameters=constructor.GetParameters();
        var rowId=0L;
        while(reader.Read()) {
            var values=new object?[table.Columns.Count];
            var sourceIndex=0;
            for(var index=0;index<values.Length;index++) {
                if(table.Columns[index].IsSynthetic){values[index]=checked(++rowId);continue;}
                var readerIndex=sourceIndex++;
                if(reader.IsDBNull(readerIndex)) {
                    values[index]=null;
                    continue;
                }
                var value=reader.GetValue(readerIndex);
                try {
                    values[index]=ConvertValue(value,parameters[index].ParameterType);
                } catch(Exception exception) when(exception is InvalidCastException or FormatException or OverflowException) {
                    throw new InvalidOperationException(
                        $"SQL Server value '{table.SourceSchema}.{table.SourceTable}.{table.Columns[index].SourceName}' "
                        +$"could not be converted from '{value.GetType()}' to '{parameters[index].ParameterType}'.",
                        exception);
                }
            }
            var record=(TRecord)constructor.Invoke(values);
            this.RowsRead++;
            this.rowsReadByTable[table.ObjectName]++;
            yield return record;
        }
    }

    public IEnumerable<object?[]> ReadVerificationRows(GeneratedTableBinding table)=>this.ReadVerificationSql(CreateSelect(table,this.memoryOptimizedTables.Contains((table.SourceSchema,table.SourceTable)),this.encryptedColumns));
    public IEnumerable<object?[]> ReadVerificationSql(string sql){
        ObjectDisposedException.ThrowIf(this.disposed,this);using var command=this.connection.CreateCommand();command.Transaction=this.transaction;command.CommandText=sql;command.CommandTimeout=0;using var reader=command.ExecuteReader(CommandBehavior.SequentialAccess);
        while(reader.Read()){var row=new object?[reader.FieldCount];for(var i=0;i<row.Length;i++){if(!reader.IsDBNull(i)){row[i]=reader.GetValue(i);}}yield return row;}
    }

    private static ConstructorInfo FindConstructor<TRecord>(int parameterCount) {
        var constructors=typeof(TRecord).GetConstructors(BindingFlags.Instance|BindingFlags.Public);
        return constructors.SingleOrDefault(constructor=>constructor.GetParameters().Length==parameterCount)
            ??throw new InvalidOperationException(
                $"Record type '{typeof(TRecord)}' does not have one public constructor with {parameterCount} parameters.");
    }

    internal static IsolationLevel SelectIsolationLevel(bool containsMemoryOptimizedTables)=>
        containsMemoryOptimizedTables?IsolationLevel.ReadCommitted:IsolationLevel.Snapshot;

    internal static string CreateSelect(GeneratedTableBinding table,bool isMemoryOptimized)=>
        CreateSelect(table,isMemoryOptimized,new HashSet<(string Schema,string Table,string Column)>());

    internal static string CreateSelect(
        GeneratedTableBinding table,
        bool isMemoryOptimized,
        IReadOnlySet<(string Schema,string Table,string Column)> encryptedColumns) {
        var builder=new StringBuilder("SELECT ");
        var selected=0;
        foreach(var field in table.Columns){
            if(field.IsSynthetic){continue;}
            if(selected++>0){builder.Append(',');}
            var column=Quote(field.SourceName);
            var isEncrypted=encryptedColumns.Contains((table.SourceSchema,table.SourceTable,field.SourceName));
            if(field.Type.Kind==DatabaseTypeKind.Binary&&!isEncrypted){builder.Append($"CONVERT(varbinary(max),{column})");}else{builder.Append(column);}
        }
        builder.Append(" FROM ");
        builder.Append(Quote(table.SourceSchema));
        builder.Append('.');
        builder.Append(Quote(table.SourceTable));
        if(isMemoryOptimized) {
            builder.Append(" WITH (SNAPSHOT)");
        }
        builder.Append(';');
        return builder.ToString();
    }

    private static HashSet<(string Schema,string Table,string Column)> ReadEncryptedColumns(SqlConnection connection) {
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT SCHEMA_NAME(table_definition.schema_id),table_definition.name,column_definition.name
            FROM sys.tables AS table_definition
            INNER JOIN sys.columns AS column_definition
                ON column_definition.object_id=table_definition.object_id
            WHERE column_definition.encryption_type IS NOT NULL;
            """;
        using var reader=command.ExecuteReader();
        var columns=new HashSet<(string Schema,string Table,string Column)>();
        while(reader.Read()) {
            columns.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2)));
        }
        return columns;
    }

    private static HashSet<(string Schema,string Table)> ReadMemoryOptimizedTables(SqlConnection connection) {
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT SCHEMA_NAME(schema_id),name
            FROM sys.tables
            WHERE is_memory_optimized=1;
            """;
        using var reader=command.ExecuteReader();
        var tables=new HashSet<(string Schema,string Table)>();
        while(reader.Read()) {
            tables.Add((reader.GetString(0),reader.GetString(1)));
        }
        return tables;
    }

    private static string Quote(string value)=>$"[{value.Replace("]","]]",StringComparison.Ordinal)}]";

    private static object ConvertValue(object value,Type destinationType) {
        var type=Nullable.GetUnderlyingType(destinationType)??destinationType;
        if(type.IsInstanceOfType(value)) {
            return value;
        }
        if(type==typeof(global::Seamless.Database.SqlServer.Types.Bit)){return new global::Seamless.Database.SqlServer.Types.Bit(Convert.ToInt32(value,global::System.Globalization.CultureInfo.InvariantCulture));}
        if(type==typeof(DateOnly)&&value is DateTime dateTime) {
            return DateOnly.FromDateTime(dateTime);
        }
        if(type==typeof(TimeOnly)&&value is TimeSpan timeSpan) {
            return TimeOnly.FromTimeSpan(timeSpan);
        }
        return Convert.ChangeType(value,type,global::System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Complete(){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.completed){throw new InvalidOperationException("The source transaction is already complete.");}this.transaction.Commit();this.completed=true;}

    public void Dispose() {
        if(this.disposed) {
            return;
        }
        this.disposed=true;
        try {
            if(!this.completed){this.transaction.Rollback();}
        } finally {
            this.transaction.Dispose();
            this.connection.Dispose();
        }
    }
}
