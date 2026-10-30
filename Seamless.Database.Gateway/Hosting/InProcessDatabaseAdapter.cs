using System.Collections;
using System.Reflection;
using System.Linq.Expressions;
using Seamless.Database.Rdbms;
using Seamless.Database.Query;
using System.Globalization;
using Seamless.Execution;
using Seamless.Linq;
using MemoryPack;

namespace Seamless.Database.Gateway.Hosting;

public sealed class InProcessDatabaseAdapter:IDatabaseAdapter{
    private readonly DatabaseContainer database;
    private readonly Dictionary<string,TableBinding>tables=new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TableMetadata>tableMetadataList=new();
    private readonly HashSet<string>ambiguousNames=new(StringComparer.OrdinalIgnoreCase);

    public InProcessDatabaseAdapter(DatabaseContainer database,IReadOnlyList<GeneratedTableBinding>? bindings=null,DatabaseCatalog? catalog=null){
        ArgumentNullException.ThrowIfNull(database);
        this.database=database;
        this.DatabaseName=database.GetType().Name;
        this.InitializeTables(bindings,catalog);
    }

    public DatabaseContainer Database=>this.database;
    internal IDisposable AcquireReadSession(SessionContext? session){if(session is null){return this.database.BeginReadSession();}return session.AcquireLock(token=>this.database.BeginReadSession(token));}
    public string DatabaseName{get;}

    private IReadOnlyList<DatabaseViewDefinition>views=[];
    private IReadOnlyList<DatabaseRoutineDefinition>routines=[];
    public IReadOnlyList<TableMetadata>GetTables()=>this.tableMetadataList.Concat(this.Runtime?.Tables.Select(RuntimeMetadata)??[]).ToArray();
    public IReadOnlyList<DatabaseViewDefinition>GetViews()=>this.views;
    internal void SetViews(IReadOnlyList<DatabaseViewDefinition>views){this.views=views;}
    public IReadOnlyList<DatabaseRoutineDefinition>GetRoutines()=>this.routines;
    internal void SetRoutines(IReadOnlyList<DatabaseRoutineDefinition>routines){this.routines=routines;}

    public TableMetadata? FindTable(string name){
        var dynamic=this.FindRuntime(name);if(dynamic is not null){return RuntimeMetadata(dynamic);}
        if(this.ambiguousNames.Contains(name)){throw new InvalidOperationException($"Ambiguous relation name '{name}'. Use a quoted schema and table name.");}
        if(this.tables.TryGetValue(name,out var binding)){
            return binding.Metadata;
        }else{
            return null;
        }
    }

    public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session){
        var dynamic=this.FindRuntime(tableName);if(dynamic is not null){return this.SelectRuntime(dynamic,columnNames,predicate);}
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }

        var selectedColumns=new List<ColumnMetadata>();
        var columnExtractors=new List<Func<object,object?>>();
        if(columnNames is null||columnNames.Count==0||(columnNames.Count==1&&columnNames[0]=="*")){
            for(var i=0;i<binding.Metadata.Columns.Count;i++){
                var col=binding.Metadata.Columns[i];
                selectedColumns.Add(col);
                columnExtractors.Add(binding.ColumnGetters[i]);
            }
        }else{
            foreach(var colName in columnNames){
                var foundIndex=-1;
                for(var i=0;i<binding.Metadata.Columns.Count;i++){
                    if(string.Equals(binding.Metadata.Columns[i].Name,colName,StringComparison.OrdinalIgnoreCase)){
                        foundIndex=i;
                        break;
                    }else{
                    }
                }
                if(foundIndex>=0){
                    selectedColumns.Add(binding.Metadata.Columns[foundIndex]);
                    columnExtractors.Add(binding.ColumnGetters[foundIndex]);
                }else{
                    throw new InvalidOperationException($"Column '{colName}' not found in table '{tableName}'.");
                }
            }
        }

        return new QueryResult(selectedColumns,ReadRows(),-1);
        IEnumerable<object?[]> ReadRows(){
            foreach(var item in binding.GetRows(this.database)){
                if(predicate is not null&&!predicate(item)){continue;}
                var values=new object?[selectedColumns.Count];
                for(var i=0;i<values.Length;i++){values[i]=NormalizeValue(columnExtractors[i](item));}
                yield return values;
            }
        }
    }

    public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session){
        var dynamic=this.FindRuntime(tableName);if(dynamic is not null){this.Runtime!.InsertRow(dynamic.Schema,dynamic.Name,RuntimeValues(dynamic,values));return 1;}
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        var record=binding.CreateRecord(binding.MapValues(values));
        binding.InsertRecord(this.database,record);
        return 1;
    }

    public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session){
        var dynamic=this.FindRuntime(tableName);if(dynamic is not null){return this.UpdateRuntime(dynamic,values.ToDictionary(p=>p.Key,p=>(Func<object,object?>)(_=>p.Value),StringComparer.OrdinalIgnoreCase),predicate,session);}
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        using var scope=new ExecutionScope();
        using var targetRows=scope.Query(binding.GetRows(this.database).Cast<object>()).Where(row=>predicate is null||predicate(row)).Select(binding.Encode).ToStorage();

        var count=0;
        foreach(var encoded in targetRows){
            var oldRow=binding.Decode(encoded);
            var updatedRow=binding.UpdateRecord(oldRow,binding.MapValues(values));
            binding.SetOrUpdateRecord(this.database,oldRow,updatedRow);
            count++;
        }
        return count;
    }

    public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session){
        var dynamic=this.FindRuntime(tableName);if(dynamic is not null){return this.DeleteRuntime(dynamic,predicate,session);}
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        using var scope=new ExecutionScope();
        using var targetRows=scope.Query(binding.GetRows(this.database).Cast<object>()).Where(row=>predicate is null||predicate(row)).Select(binding.Encode).ToStorage();

        var count=0;
        foreach(var encoded in targetRows){
            var row=binding.Decode(encoded);
            binding.RemoveRecord(this.database,row);
            count++;
        }
        return count;
    }

    public object? GetRecordValue(string table,string column,object row){var dynamic=this.FindRuntime(table);if(dynamic is not null){var runtimeIndex=Index(dynamic,column);return ((RuntimeSqlRow)row).Values[runtimeIndex];}var binding=this.tables[table];var index=binding.Metadata.Columns.ToList().FindIndex(c=>c.Name.Equals(column,StringComparison.OrdinalIgnoreCase));if(index<0){throw new InvalidOperationException("Column mapping is missing.");}return NormalizeValue(binding.ColumnGetters[index](row));}
    public int ExecuteUpdateExpressions(string table,IReadOnlyDictionary<string,Func<object,object?>> values,Func<object,bool>? predicate,SessionContext session){
        var dynamic=this.FindRuntime(table);if(dynamic is not null){return this.UpdateRuntime(dynamic,values,predicate,session);}
        var binding=this.tables[table];using var scope=new ExecutionScope(new(){CancellationToken=session.CancellationToken});using var rows=scope.Query(binding.GetRows(this.database).Cast<object>()).Where(row=>predicate is null||predicate(row)).Select(binding.Encode).ToStorage();var count=0;
        foreach(var encoded in rows){session.CancellationToken.ThrowIfCancellationRequested();var old=binding.Decode(encoded);var evaluated=values.ToDictionary(p=>p.Key,p=>p.Value(old),StringComparer.OrdinalIgnoreCase);var updated=binding.UpdateRecord(old,binding.MapValues(evaluated));binding.SetOrUpdateRecord(this.database,old,updated);count++;}return count;
    }

    public void BeginTransaction(SessionContext session){
        if(session.IsInTransaction){throw new InvalidOperationException("Nested SQL transactions are not supported in this session.");}
        var tx=session.AcquireLock<IDisposable>(token=>{if(this.Runtime is{}runtime){return runtime.BeginTransaction(token);}return this.database.BeginTransaction(token);});
        session.AttachTransaction(tx);
    }

    public void CommitTransaction(SessionContext session){
        if(!session.IsInTransaction){throw new InvalidOperationException("There is no active transaction.");}
        if(this.Runtime?.IsRollbackOnly==true){throw new SqlQueryException("Transaction is rollback-only; ROLLBACK is required.",3930);}
        var tx=session.DetachTransaction();
        if(tx is RuntimeSqlCatalog.RuntimeSqlTransaction runtimeTx){
            runtimeTx.Commit();runtimeTx.Dispose();
        }else if(tx is DatabaseTransaction dbTx){
            dbTx.Commit();
            dbTx.Dispose();
        }else if(tx is not null){
            tx.Dispose();
        }else{
        }
    }

    public void RollbackTransaction(SessionContext session){
        if(!session.IsInTransaction){throw new InvalidOperationException("There is no active transaction.");}
        var tx=session.DetachTransaction();
        if(tx is not null){
            tx.Dispose();
        }else{
        }
    }

    public void MarkTransactionFailed(){this.Runtime?.MarkRollbackOnly();}
    private RuntimeSqlCatalog? Runtime=>(this.database as RuntimeSqlDatabaseContainer)?.RuntimeSql;
    public void ExecuteDdl(SqlDdlCommand command){
        var runtime=this.Runtime??throw new SqlQueryException("Runtime SQL is not enabled.",40515);
        var key=new TableMetadata(command.Schema,command.Name,[]).IdentifierKey;
        if(command is SqlCreateTableCommand create){
            if(this.tables.ContainsKey(key)||runtime.FindTable(command.Schema,command.Name) is not null||this.views.Any(v=>v.Schema.Equals(command.Schema,StringComparison.OrdinalIgnoreCase)&&v.Name.Equals(command.Name,StringComparison.OrdinalIgnoreCase))||this.routines.Any(r=>r.Schema.Equals(command.Schema,StringComparison.OrdinalIgnoreCase)&&r.Name.Equals(command.Name,StringComparison.OrdinalIgnoreCase))){throw new SqlQueryException("An object with that name already exists.",2714);}
            runtime.CreateTable(command.Schema,command.Name,create.Columns);return;
        }
        if(runtime.FindTable(command.Schema,command.Name) is null){if(this.tables.ContainsKey(key)){throw new SqlQueryException("DDL on imported typed tables is not supported.",40515);}throw new SqlQueryException("Table not found.",208);}
        if(command is SqlAddColumnCommand add){runtime.AddNullableColumn(command.Schema,command.Name,add.Column);return;}
        if(command is SqlDropTableCommand){runtime.DropTable(command.Schema,command.Name);return;}throw new SqlQueryException("Unsupported DDL command.",40515);
    }
    private RuntimeSqlTable? FindRuntime(string name){
        if(this.Runtime is not{}runtime){return null;}RuntimeSqlTable? found=null;
        foreach(var item in runtime.Tables){var meta=RuntimeMetadata(item);if(meta.IdentifierKey.Equals(name,StringComparison.OrdinalIgnoreCase)||meta.FullName.Equals(name,StringComparison.OrdinalIgnoreCase)||item.Schema.Equals("dbo",StringComparison.OrdinalIgnoreCase)&&item.Name.Equals(name,StringComparison.OrdinalIgnoreCase)){if(found is not null){throw new SqlQueryException("Ambiguous table name.",40515);}found=item;}}
        return found;
    }
    private static TableMetadata RuntimeMetadata(RuntimeSqlTable table){
        var columns=table.Columns.Select((c,i)=>new ColumnMetadata(c.Name,c.SqlTypeName switch{"int"=>typeof(int),"bigint"=>typeof(long),"bit"=>typeof(bool),"nvarchar"=>typeof(string),"varbinary"=>typeof(byte[]),"decimal"=>typeof(decimal),_=>throw new InvalidDataException("Unsupported runtime type.")},c.SqlTypeName,c.IsNullable,c.IsPrimaryKey,i,c.Length,c.Precision,c.Scale,c.Collation)).ToArray();
        return new(table.Schema,table.Name,columns,null,checked((int)(1_000_000_000L+table.ObjectId)));
    }
    private QueryResult SelectRuntime(RuntimeSqlTable table,IReadOnlyList<string>? names,Func<object,bool>? predicate){
        var all=RuntimeMetadata(table);var indices=names is null||names.Count==0||names.Count==1&&names[0]=="*"?global::System.Linq.Enumerable.Range(0,table.Columns.Count).ToArray():names.Select(name=>Index(table,name)).ToArray();var revision=this.Runtime!.SchemaRevision;
        IEnumerable<object?[]> Read(){foreach(var row in this.Runtime.ReadRows(table.Schema,table.Name,revision)){if(predicate is null||predicate(row)){yield return indices.Select(index=>row.Values[index]).ToArray();}}}
        return new(indices.Select(i=>all.Columns[i]).ToArray(),Read(),-1);
    }
    private static int Index(RuntimeSqlTable table,string name){for(var i=0;i<table.Columns.Count;i++){if(table.Columns[i].Name.Equals(name,StringComparison.OrdinalIgnoreCase)){return i;}}throw new SqlQueryException("Unknown column: "+name,207);}
    private static object?[] RuntimeValues(RuntimeSqlTable table,IReadOnlyDictionary<string,object?> values,object?[]? existing=null){
        var output=existing is null?new object?[table.Columns.Count]:(object?[])existing.Clone();foreach(var entry in values){var i=Index(table,entry.Key);output[i]=RuntimeValue(table.Columns[i],entry.Value);}return output;
    }
    private static object? RuntimeValue(RuntimeSqlColumn column,object? value){
        if(value is null or DBNull){return null;}try{return column.SqlTypeName switch{
            "int"=>checked((int)Integral(value)),"bigint"=>checked((long)Integral(value)),"bit"=>value is bool flag?flag:Integral(value) switch{0=>false,1=>true,_=>throw new InvalidOperationException("Invalid bit value.")},
            "decimal"=>value is decimal number?number:Integral(value),"nvarchar" when value is string text=>text,"varbinary" when value is byte[] bytes=>bytes,_=>throw new InvalidOperationException("Unsupported value conversion.")};
        }catch(OverflowException){throw new SqlQueryException("Numeric overflow.",8115);}
        static decimal Integral(object value){if(value is not (byte or short or int or long or decimal)){throw new InvalidOperationException("Unsupported numeric conversion.");}var n=Convert.ToDecimal(value,CultureInfo.InvariantCulture);if(decimal.Truncate(n)!=n){throw new InvalidOperationException("Numeric conversion would truncate.");}return n;}
    }
    private int UpdateRuntime(RuntimeSqlTable table,IReadOnlyDictionary<string,Func<object,object?>> values,Func<object,bool>? predicate,SessionContext session){
        var runtime=this.Runtime!;var revision=runtime.SchemaRevision;using var scope=new ExecutionScope(new(){CancellationToken=session.CancellationToken});
        using var ids=scope.Query(runtime.ReadRows(table.Schema,table.Name,revision)).Where(row=>predicate is null||predicate(row)).Select(row=>row.RowId).ToStorage();var count=0;
        foreach(var id in ids){session.CancellationToken.ThrowIfCancellationRequested();var row=runtime.ReadRow(table.Schema,table.Name,id,revision);var changes=values.ToDictionary(p=>p.Key,p=>p.Value(row),StringComparer.OrdinalIgnoreCase);runtime.UpdateRow(table.Schema,table.Name,id,RuntimeValues(table,changes,row.Values));count++;}return count;
    }
    private int DeleteRuntime(RuntimeSqlTable table,Func<object,bool>? predicate,SessionContext session){
        var runtime=this.Runtime!;var revision=runtime.SchemaRevision;using var scope=new ExecutionScope(new(){CancellationToken=session.CancellationToken});
        using var ids=scope.Query(runtime.ReadRows(table.Schema,table.Name,revision)).Where(row=>predicate is null||predicate(row)).Select(row=>row.RowId).ToStorage();var count=0;
        foreach(var id in ids){session.CancellationToken.ThrowIfCancellationRequested();runtime.DeleteRow(table.Schema,table.Name,id);count++;}return count;
    }

    private void InitializeTables(IReadOnlyList<GeneratedTableBinding>? bindings,DatabaseCatalog? catalog){
        var nullability=new NullabilityInfoContext();
        foreach(var member in Relations(this.database,new List<PropertyInfo>())){
            var prop=member.Path[^1];
            var generic=prop.PropertyType.GetGenericTypeDefinition();
            var keyed=generic.Name.StartsWith("Table`2",StringComparison.Ordinal);
            var args=prop.PropertyType.GetGenericArguments();
            var recordType=keyed?args[1]:args[0];
            var keyType=keyed?args[0]:null;
            global::System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(recordType.TypeHandle);
            if(keyType is not null){global::System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(keyType.TypeHandle);}
            var path=string.Join(".",member.Path.Select(p=>p.Name));
            var source=bindings?.FirstOrDefault(b=>b.ObjectName==path);
            var schema=source?.SourceSchema??member.Schema;
            var name=source?.SourceTable??prop.Name;
            var sourceTable=catalog?.Schemas.FirstOrDefault(s=>string.Equals(s.Name,schema,StringComparison.OrdinalIgnoreCase))?.Tables.FirstOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase));
            if(catalog is not null&&source is not null&&sourceTable is null){throw new InvalidDataException($"Source table missing: {schema}.{name}.");}
            var properties=recordType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
            var columns=new List<ColumnMetadata>();
            var propertyNames=new List<string>();
            foreach(var rp in properties){
                var bound=source?.Columns.FirstOrDefault(c=>c.PropertyName==rp.Name);
                if(source is not null&&bound is null){continue;}
                if(bound?.IsSynthetic==true){continue;}
                var actual=Nullable.GetUnderlyingType(rp.PropertyType)??rp.PropertyType;
                var clr=actual.FullName=="Seamless.Database.SqlServer.Types.Bit"?typeof(bool):actual;
                var nullable=bound?.Type.IsNullable??(Nullable.GetUnderlyingType(rp.PropertyType) is not null||(!actual.IsValueType&&nullability.Create(rp).ReadState!=NullabilityState.NotNull));
                var primary=sourceTable is not null?sourceTable.PrimaryKey?.Columns.Any(c=>string.Equals(c.Name,bound?.SourceName??rp.Name,StringComparison.OrdinalIgnoreCase))==true:keyType?.GetProperty(rp.Name) is not null;
                columns.Add(new(bound?.SourceName??rp.Name,clr,bound?.Type.ResultSqlTypeName??bound?.Type.SqlTypeName??this.GetSqlTypeName(clr),nullable,primary,columns.Count,bound?.Type.ResultMaximumLength??bound?.Type.MaximumLength,bound?.Type.Precision,bound?.Type.Scale,bound?.Type.Collation,bound?.Type.SourceMaximumLength,bound?.Type.SourcePrecision,bound?.Type.SourceScale,sourceSqlTypeName:bound?.Type.SqlTypeName));
                propertyNames.Add(rp.Name);
            }
            object GetTable(DatabaseContainer db){object current=db;foreach(var item in member.Path){current=item.GetValue(current)??throw new InvalidOperationException("A schema relation returned null.");}return current;}
            var metadata=new TableMetadata(schema,name,columns,sourceTable?.PrimaryKey);
            var binding=new TableBinding(prop,GetTable,recordType,keyType,metadata,columns,propertyNames);
            if(!this.tables.TryAdd(metadata.IdentifierKey,binding)){throw new InvalidOperationException($"Duplicate relation '{metadata.IdentifierKey}'.");}
            this.tableMetadataList.Add(metadata);
        }
        // 旧形式のキーは複数の関係に一致するとき使用しない。
        var aliases=new Dictionary<string,List<TableBinding>>(StringComparer.OrdinalIgnoreCase);
        foreach(var binding in this.tables.Values.ToArray()){
            AddAlias(binding.Metadata.FullName,binding);
            if(string.Equals(binding.Metadata.SchemaName,"dbo",StringComparison.OrdinalIgnoreCase)){AddAlias(binding.Metadata.TableName,binding);}
        }
        foreach(var alias in aliases){if(alias.Value.Distinct().Count()==1){this.tables.TryAdd(alias.Key,alias.Value[0]);}else{this.ambiguousNames.Add(alias.Key);}}
        void AddAlias(string name,TableBinding binding){if(!aliases.TryGetValue(name,out var matches)){matches=[];aliases.Add(name,matches);}matches.Add(binding);}
        static IEnumerable<(PropertyInfo[] Path,string Schema)> Relations(object owner,List<PropertyInfo> path){
            foreach(var prop in owner.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
                if(prop.GetIndexParameters().Length>0){continue;}
                var next=new List<PropertyInfo>(path){prop};
                if(typeof(DatabaseSchema).IsAssignableFrom(prop.PropertyType)){
                    if(prop.GetValue(owner) is DatabaseSchema child){foreach(var item in Relations(child,next)){yield return item;}}
                }else if(prop.PropertyType.IsGenericType&&(prop.PropertyType.GetGenericTypeDefinition().Name is "Table`1" or "Table`2")){
                    var schema=owner is DatabaseSchema named?named.Name:"dbo";
                    yield return(next.ToArray(),schema);
                }
            }
        }
    }
    private static object? NormalizeValue(object? value){
        if(value?.GetType().FullName=="Seamless.Database.SqlServer.Types.Bit"){return Convert.ToByte(value.GetType().GetProperty("Value")!.GetValue(value))!=0;}
        return value;
    }
    private static byte[] EncodeRecord<T>(object record)=>MemoryPackSerializer.Serialize((T)record);
    private static object DecodeRecord<T>(byte[] bytes)=>MemoryPackSerializer.Deserialize<T>(bytes)!;

    private string GetSqlTypeName(Type clrType){
        var underlying=Nullable.GetUnderlyingType(clrType)??clrType;
        if(underlying==typeof(int)){
            return "int";
        }else if(underlying==typeof(long)){
            return "bigint";
        }else if(underlying==typeof(short)){
            return "smallint";
        }else if(underlying==typeof(byte)){
            return "tinyint";
        }else if(underlying==typeof(bool)){
            return "bit";
        }else if(underlying==typeof(Guid)){return "uniqueidentifier";}else if(underlying==typeof(DateOnly)){return "date";}else if(underlying==typeof(TimeOnly)||underlying==typeof(TimeSpan)){return "time";}else if(underlying==typeof(DateTimeOffset)){return "datetimeoffset";}else if(underlying==typeof(string)){
            return "nvarchar";
        }else if(underlying==typeof(DateTime)){
            return "datetime2";
        }else if(underlying==typeof(double)){
            return "float";
        }else if(underlying==typeof(float)){
            return "real";
        }else if(underlying==typeof(decimal)){
            return "decimal";
        }else if(underlying==typeof(byte[])){
            return "varbinary";
        }else{
            return "nvarchar";
        }
    }

    private sealed class TableBinding{
        private readonly Func<DatabaseContainer,object> getTable;
        private readonly ConstructorInfo constructor;
        private readonly MethodInfo addMethod;
        private readonly MethodInfo? removeMethod;
        private readonly PropertyInfo? indexerProperty;
        private readonly ConstructorInfo? keyConstructor;
        private readonly PropertyInfo[]? keyProperties;
        private readonly PropertyInfo[] recordProperties;
        private readonly Type? keyType;
        private readonly IReadOnlyList<string> propertyNames;

        public TableBinding(PropertyInfo property,Func<DatabaseContainer,object> getTable,Type recordType,Type? keyType,TableMetadata metadata,IReadOnlyList<ColumnMetadata>columns,IReadOnlyList<string> propertyNames){
            this.getTable=getTable;this.propertyNames=propertyNames;
            this.Encode=typeof(InProcessDatabaseAdapter).GetMethod("EncodeRecord",BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(recordType).CreateDelegate<Func<object,byte[]>>();
            this.Decode=typeof(InProcessDatabaseAdapter).GetMethod("DecodeRecord",BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(recordType).CreateDelegate<Func<byte[],object>>();
            this.RecordType=recordType;
            this.keyType=keyType;
            this.Metadata=metadata;

            var tableObjType=property.PropertyType;
            this.addMethod=tableObjType.GetMethod("Add",new[]{recordType})??throw new InvalidOperationException($"Add method not found on {tableObjType.Name}");

            if(keyType is not null){
                this.removeMethod=tableObjType.GetMethod("Remove",new[]{keyType});
                this.indexerProperty=tableObjType.GetProperty("Item",new[]{keyType});
                var kProps=keyType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
                this.keyProperties=new PropertyInfo[kProps.Length];
                for(var i=0;i<kProps.Length;i++){
                    this.keyProperties[i]=recordType.GetProperty(kProps[i].Name)??throw new InvalidOperationException($"Key property {kProps[i].Name} not found on {recordType.Name}");
                }
                var kCtors=keyType.GetConstructors(BindingFlags.Public|BindingFlags.Instance);
                foreach(var c in kCtors){
                    if(c.GetParameters().Length==kProps.Length){
                        this.keyConstructor=c;
                        break;
                    }else{
                    }
                }
            }else{
                this.removeMethod=tableObjType.GetMethod("Remove",new[]{recordType});
                this.indexerProperty=null;
                this.keyProperties=null;
                this.keyConstructor=null;
            }

            this.recordProperties=recordType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
            for(var i=0;i<columns.Count;i++){
                var p=recordType.GetProperty(propertyNames[i])??throw new InvalidOperationException($"Property {columns[i].Name} not found on {recordType.Name}");
                this.recordProperties[i]=p;
            }

            var ctors=recordType.GetConstructors(BindingFlags.Public|BindingFlags.Instance);
            ConstructorInfo? matchedCtor=null;
            foreach(var ctor in ctors){
                var ps=ctor.GetParameters();
                if(ps.Length==this.recordProperties.Length){
                    matchedCtor=ctor;
                    break;
                }else{
                }
            }
            this.constructor=matchedCtor??throw new InvalidOperationException($"Constructor not found on {recordType.Name}");

            var getters=new Func<object,object?>[columns.Count];
            for(var i=0;i<columns.Count;i++){
                var p=recordType.GetProperty(propertyNames[i])!;
                var obj=Expression.Parameter(typeof(object));
                getters[i]=Expression.Lambda<Func<object,object?>>(Expression.Convert(Expression.Property(Expression.Convert(obj,recordType),p),typeof(object)),obj).Compile();
            }
            this.ColumnGetters=getters;
        }

        public Func<object,byte[]> Encode{get;}
        public Func<byte[],object> Decode{get;}
        public Type RecordType{get;}
        public TableMetadata Metadata{get;}
        public IReadOnlyList<Func<object,object?>>ColumnGetters{get;}

        public IEnumerable GetRows(DatabaseContainer db){
            var tableObj=this.getTable(db);
            if(tableObj is IEnumerable en){
                return en;
            }else{
                return Array.Empty<object>();
            }
        }

        public void InsertRecord(DatabaseContainer db,object record){
            var tableObj=this.getTable(db);
            var added=this.addMethod.Invoke(tableObj,new[]{record});
            if(added is false){throw new InvalidOperationException("Duplicate primary key.");}
        }

        public void RemoveRecord(DatabaseContainer db,object record){
            var tableObj=this.getTable(db);
            if(this.keyType is not null&&this.removeMethod is not null){
                var key=this.CreateKeyFromRecord(record);
                this.removeMethod.Invoke(tableObj,new[]{key});
            }else if(this.removeMethod is not null){
                this.removeMethod.Invoke(tableObj,new[]{record});
            }else{
            }
        }

        public void SetOrUpdateRecord(DatabaseContainer db,object oldRecord,object updatedRecord){
            var tableObj=this.getTable(db);
            if(this.indexerProperty is not null&&this.keyType is not null){
                var key=this.CreateKeyFromRecord(updatedRecord);
                this.indexerProperty.SetValue(tableObj,updatedRecord,new[]{key});
            }else{
                this.RemoveRecord(db,oldRecord);
                this.InsertRecord(db,updatedRecord);
            }
        }

        public IReadOnlyDictionary<string,object?> MapValues(IReadOnlyDictionary<string,object?> values){var mapped=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);foreach(var pair in values){var index=this.Metadata.Columns.ToList().FindIndex(c=>c.Name.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));if(index<0){throw new InvalidOperationException("Unknown SQL column.");}mapped.Add(this.propertyNames[index],pair.Value);}return mapped;}

        public object CreateRecord(IReadOnlyDictionary<string,object?>values){
            var args=new object?[this.recordProperties.Length];
            for(var i=0;i<this.recordProperties.Length;i++){
                var p=this.recordProperties[i];
                if(values.TryGetValue(p.Name,out var val)){
                    args[i]=ConvertValue(val,p.PropertyType);
                }else{
                    args[i]=p.PropertyType.IsValueType?Activator.CreateInstance(p.PropertyType):null;
                }
            }
            return this.constructor.Invoke(args);
        }

        public object UpdateRecord(object existing,IReadOnlyDictionary<string,object?>newValues){
            var args=new object?[this.recordProperties.Length];
            for(var i=0;i<this.recordProperties.Length;i++){
                var p=this.recordProperties[i];
                if(newValues.TryGetValue(p.Name,out var val)){
                    args[i]=ConvertValue(val,p.PropertyType);
                }else{
                    args[i]=p.GetValue(existing);
                }
            }
            return this.constructor.Invoke(args);
        }

        private object CreateKeyFromRecord(object record){
            if(this.keyProperties is null||this.keyConstructor is null){
                throw new InvalidOperationException("Key metadata not available.");
            }else{
            }
            var keyArgs=new object?[this.keyProperties.Length];
            for(var i=0;i<this.keyProperties.Length;i++){
                keyArgs[i]=this.keyProperties[i].GetValue(record);
            }
            return this.keyConstructor.Invoke(keyArgs);
        }

        private static object? ConvertValue(object? val,Type targetType){
            if(val is null){
                return null;
            }else{
            }
            var underlying=Nullable.GetUnderlyingType(targetType)??targetType;
            if(underlying.IsInstanceOfType(val)){
                return val;
            }else{
                if(underlying.FullName=="Seamless.Database.SqlServer.Types.Bit"){return Activator.CreateInstance(underlying,Convert.ToInt32(val));}
                if(underlying==typeof(Guid)){return Guid.Parse(val.ToString()!);}
                if(underlying==typeof(DateOnly)&&val is DateTime date){return DateOnly.FromDateTime(date);}
                if(underlying==typeof(TimeOnly)&&val is TimeSpan time){return TimeOnly.FromTimeSpan(time);}
                return Convert.ChangeType(val,underlying,global::System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
