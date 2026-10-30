using System.Collections;
using System.Reflection;

namespace Seamless.Database.Gateway.Hosting;

public sealed class InProcessDatabaseAdapter:IDatabaseAdapter{
    private readonly DatabaseContainer database;
    private readonly Dictionary<string,TableBinding>tables=new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TableMetadata>tableMetadataList=new();

    public InProcessDatabaseAdapter(DatabaseContainer database){
        ArgumentNullException.ThrowIfNull(database);
        this.database=database;
        this.DatabaseName=database.GetType().Name;
        this.InitializeTables();
    }

    public string DatabaseName{get;}

    public IReadOnlyList<TableMetadata>GetTables()=>this.tableMetadataList;

    public TableMetadata? FindTable(string name){
        if(this.tables.TryGetValue(name,out var binding)){
            return binding.Metadata;
        }else{
            return null;
        }
    }

    public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columnNames,Func<object,bool>? predicate,SessionContext session){
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

        var rows=new List<object?[]>();
        var enumerable=binding.GetRows(this.database);
        foreach(var item in enumerable){
            if(predicate is not null&&!predicate(item)){
                continue;
            }else{
            }
            var rowValues=new object?[selectedColumns.Count];
            for(var i=0;i<selectedColumns.Count;i++){
                rowValues[i]=columnExtractors[i](item);
            }
            rows.Add(rowValues);
        }

        return new QueryResult(selectedColumns,rows,rows.Count);
    }

    public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session){
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        var record=binding.CreateRecord(values);
        binding.InsertRecord(this.database,record);
        return 1;
    }

    public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session){
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        var targetRows=new List<object>();
        var enumerable=binding.GetRows(this.database);
        foreach(var item in enumerable){
            if(predicate is null||predicate(item)){
                targetRows.Add(item);
            }else{
            }
        }

        var count=0;
        foreach(var oldRow in targetRows){
            var updatedRow=binding.UpdateRecord(oldRow,values);
            binding.SetOrUpdateRecord(this.database,oldRow,updatedRow);
            count++;
        }
        return count;
    }

    public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session){
        if(!this.tables.TryGetValue(tableName,out var binding)){
            throw new InvalidOperationException($"Table '{tableName}' not found in database '{this.DatabaseName}'.");
        }else{
        }
        var targetRows=new List<object>();
        var enumerable=binding.GetRows(this.database);
        foreach(var item in enumerable){
            if(predicate is null||predicate(item)){
                targetRows.Add(item);
            }else{
            }
        }

        var count=0;
        foreach(var row in targetRows){
            binding.RemoveRecord(this.database,row);
            count++;
        }
        return count;
    }

    public void BeginTransaction(SessionContext session){
        var tx=this.database.BeginTransaction();
        session.AttachTransaction(tx);
    }

    public void CommitTransaction(SessionContext session){
        var tx=session.DetachTransaction();
        if(tx is DatabaseTransaction dbTx){
            dbTx.Commit();
            dbTx.Dispose();
        }else if(tx is not null){
            tx.Dispose();
        }else{
        }
    }

    public void RollbackTransaction(SessionContext session){
        var tx=session.DetachTransaction();
        if(tx is not null){
            tx.Dispose();
        }else{
        }
    }

    private void InitializeTables(){
        var dbType=this.database.GetType();
        var properties=dbType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
        foreach(var prop in properties){
            var propType=prop.PropertyType;
            if(!propType.IsGenericType){
                continue;
            }else{
            }
            var genericDef=propType.GetGenericTypeDefinition();
            var isKeyedTable=genericDef.Name.StartsWith("Table`2");
            var isUnkeyedTable=genericDef.Name.StartsWith("Table`1");
            if(!isKeyedTable&&!isUnkeyedTable){
                continue;
            }else{
            }

            var typeArgs=propType.GetGenericArguments();
            var recordType=isKeyedTable?typeArgs[1]:typeArgs[0];
            var keyType=isKeyedTable?typeArgs[0]:null;

            var columns=new List<ColumnMetadata>();
            var recordProps=recordType.GetProperties(BindingFlags.Public|BindingFlags.Instance);
            var ordinal=0;
            foreach(var rp in recordProps){
                var isPk=false;
                if(keyType is not null){
                    var keyProp=keyType.GetProperty(rp.Name);
                    if(keyProp is not null){
                        isPk=true;
                    }else{
                    }
                }else{
                }
                var sqlTypeName=this.GetSqlTypeName(rp.PropertyType);
                columns.Add(new ColumnMetadata(rp.Name,rp.PropertyType,sqlTypeName,false,isPk,ordinal));
                ordinal++;
            }

            var meta=new TableMetadata("dbo",prop.Name,columns);
            var binding=new TableBinding(prop,recordType,keyType,meta,columns);
            this.tables[prop.Name]=binding;
            this.tables[$"dbo.{prop.Name}"]=binding;
            this.tableMetadataList.Add(meta);
        }
    }

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
        }else if(underlying==typeof(string)){
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
        private readonly PropertyInfo property;
        private readonly ConstructorInfo constructor;
        private readonly MethodInfo addMethod;
        private readonly MethodInfo? removeMethod;
        private readonly PropertyInfo? indexerProperty;
        private readonly ConstructorInfo? keyConstructor;
        private readonly PropertyInfo[]? keyProperties;
        private readonly PropertyInfo[] recordProperties;
        private readonly Type? keyType;

        public TableBinding(PropertyInfo property,Type recordType,Type? keyType,TableMetadata metadata,IReadOnlyList<ColumnMetadata>columns){
            this.property=property;
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

            this.recordProperties=new PropertyInfo[columns.Count];
            for(var i=0;i<columns.Count;i++){
                var p=recordType.GetProperty(columns[i].Name)??throw new InvalidOperationException($"Property {columns[i].Name} not found on {recordType.Name}");
                this.recordProperties[i]=p;
            }

            var ctors=recordType.GetConstructors(BindingFlags.Public|BindingFlags.Instance);
            ConstructorInfo? matchedCtor=null;
            foreach(var ctor in ctors){
                var ps=ctor.GetParameters();
                if(ps.Length==columns.Count){
                    matchedCtor=ctor;
                    break;
                }else{
                }
            }
            this.constructor=matchedCtor??throw new InvalidOperationException($"Constructor not found on {recordType.Name}");

            var getters=new Func<object,object?>[columns.Count];
            for(var i=0;i<columns.Count;i++){
                var p=this.recordProperties[i];
                getters[i]=(obj)=>p.GetValue(obj);
            }
            this.ColumnGetters=getters;
        }

        public Type RecordType{get;}
        public TableMetadata Metadata{get;}
        public IReadOnlyList<Func<object,object?>>ColumnGetters{get;}

        public IEnumerable GetRows(DatabaseContainer db){
            var tableObj=this.property.GetValue(db);
            if(tableObj is IEnumerable en){
                return en;
            }else{
                return Array.Empty<object>();
            }
        }

        public void InsertRecord(DatabaseContainer db,object record){
            var tableObj=this.property.GetValue(db);
            this.addMethod.Invoke(tableObj,new[]{record});
        }

        public void RemoveRecord(DatabaseContainer db,object record){
            var tableObj=this.property.GetValue(db);
            if(this.keyType is not null&&this.removeMethod is not null){
                var key=this.CreateKeyFromRecord(record);
                this.removeMethod.Invoke(tableObj,new[]{key});
            }else if(this.removeMethod is not null){
                this.removeMethod.Invoke(tableObj,new[]{record});
            }else{
            }
        }

        public void SetOrUpdateRecord(DatabaseContainer db,object oldRecord,object updatedRecord){
            var tableObj=this.property.GetValue(db);
            if(this.indexerProperty is not null&&this.keyType is not null){
                var key=this.CreateKeyFromRecord(updatedRecord);
                this.indexerProperty.SetValue(tableObj,updatedRecord,new[]{key});
            }else{
                this.RemoveRecord(db,oldRecord);
                this.InsertRecord(db,updatedRecord);
            }
        }

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
                return Convert.ChangeType(val,underlying);
            }
        }
    }
}
