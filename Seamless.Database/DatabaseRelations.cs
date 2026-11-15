using System.Collections;
using System.Reflection;
using System.Linq;
using Seamless.Transactions;

namespace Seamless.Database;

/// <summary>宣言された表の経路。カタログ列挙はgetterを呼ばず、実体化は明示操作に限定する。</summary>
public sealed record DatabaseTableDeclaration(string Path,PropertyInfo[] Access){
    public PropertyInfo Property=>this.Access[^1];
    public object GetTable(DatabaseContainer database){object current=database;foreach(var property in this.Access){current=property.GetValue(current)??throw new InvalidOperationException("Null schema/table: "+this.Path);}return current;}
    public static DatabaseTableDeclaration[] Read(Type type){var result=new List<DatabaseTableDeclaration>();Walk(type,"",[],new global::System.Collections.Generic.HashSet<Type>());return result.ToArray();
        void Walk(Type owner,string prefix,PropertyInfo[] access,global::System.Collections.Generic.HashSet<Type> ancestors){
            if(!ancestors.Add(owner)){throw new InvalidOperationException("Recursive schema type: "+owner.FullName);}
            try{foreach(var property in owner.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0)){
                var path=prefix+property.Name;var chain=access.Append(property).ToArray();var value=property.PropertyType;
                if(typeof(DatabaseSchema).IsAssignableFrom(value)){Walk(value,path+".",chain,ancestors);}
                else if(value.IsGenericType&&(value.GetGenericTypeDefinition()==typeof(Table<,>)||value.GetGenericTypeDefinition()==typeof(Table<>))){result.Add(new(path,chain));}
            }}finally{ancestors.Remove(owner);}
        }
    }
}

public abstract partial class DatabaseContainer{
    private Action<TransactionScope?>[]? relationValidators;
    private Action<TransactionScope?>[] RelationValidators=>this.relationValidators??=this.CreateRelationValidators();
    private Action<TransactionScope?>[] CreateRelationValidators()=>BuildRelationValidators(this.GetType()).Select(validate=>(Action<TransactionScope?>)(scope=>validate(this,scope))).ToArray();
    public static void ValidateRelationDeclarations(Type databaseType){_ = BuildRelationValidators(databaseType);}
    private static Action<DatabaseContainer,TransactionScope?>[] BuildRelationValidators(Type databaseType){
        var tables=DatabaseTableDeclaration.Read(databaseType);var result=new List<Action<DatabaseContainer,TransactionScope?>>();var names=new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var child in tables){foreach(var relation in child.Property.GetCustomAttributes<ForeignKeyAttribute>()){
            if(string.IsNullOrWhiteSpace(relation.Name)){throw new InvalidOperationException("A foreign key name is required.");}
            if(!names.Add(relation.Name)){throw new InvalidOperationException("Duplicate foreign key: "+relation.Name);}
            var parent=tables.SingleOrDefault(table=>table.Path==relation.ReferencedTable)??throw new InvalidOperationException("Referenced table not found: "+relation.ReferencedTable);
            var childTypes=child.Property.PropertyType.GenericTypeArguments;var parentTypes=parent.Property.PropertyType.GenericTypeArguments;
            if(childTypes.Length!=2||parentTypes.Length!=2){throw new NotSupportedException("Foreign keys require keyed tables: "+relation.Name);}
            var method=typeof(DatabaseContainer).GetMethod(nameof(CreateRelationValidator),BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(childTypes[0],childTypes[1],parentTypes[0],parentTypes[1]);
            try{result.Add((Action<DatabaseContainer,TransactionScope?>)method.Invoke(null,[child,parent,relation])!);}catch(TargetInvocationException error)when(error.InnerException is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }}return result.ToArray();
    }
    private static Action<DatabaseContainer,TransactionScope?> CreateRelationValidator<TChildKey,TChild,TParentKey,TParent>(DatabaseTableDeclaration child,DatabaseTableDeclaration parent,ForeignKeyAttribute relation)
        where TChildKey:struct where TChild:struct where TParentKey:struct where TParent:struct{
        var keyFields=typeof(TParentKey).GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(field=>field.GetIndexParameters().Length==0).ToArray();
        if(relation.Columns.Length==0||relation.Columns.Length!=relation.ReferencedColumns.Length||relation.Columns.Distinct().Count()!=relation.Columns.Length||relation.ReferencedColumns.Length!=keyFields.Length||relation.ReferencedColumns.Distinct().Count()!=keyFields.Length||!keyFields.All(field=>relation.ReferencedColumns.Contains(field.Name,StringComparer.Ordinal))){throw new InvalidOperationException("Foreign key must reference the complete primary key: "+relation.Name);}
        var fields=relation.Columns.Select(name=>typeof(TChild).GetProperty(name)??throw new InvalidOperationException("Foreign key column not found: "+name)).ToArray();
        // 通常のビルドで生成したキーのコンストラクターは引数名が value0… なので、列名は生成器が付ける GeneratedRecordConstructorAttribute から読む。
        static IReadOnlyList<string> Members(ConstructorInfo item)=>item.GetCustomAttribute<GeneratedRecordConstructorAttribute>()?.Members??(IReadOnlyList<string>)item.GetParameters().Select(parameter=>parameter.Name??"").ToArray();
        var constructor=typeof(TParentKey).GetConstructors().SingleOrDefault(item=>item.GetParameters().Length==keyFields.Length&&Members(item).All(member=>keyFields.Any(field=>field.Name.Equals(member,StringComparison.OrdinalIgnoreCase))))??throw new NotSupportedException("Primary key constructor not found: "+parent.Path);
        var positions=Members(constructor).Select(member=>Array.FindIndex(relation.ReferencedColumns,name=>name.Equals(member,StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach(var field in keyFields){var index=Array.IndexOf(relation.ReferencedColumns,field.Name);if((Nullable.GetUnderlyingType(fields[index].PropertyType)??fields[index].PropertyType)!=field.PropertyType){throw new InvalidOperationException("Foreign key types differ: "+relation.Name);}}
        return (database,scope)=>{
            var childTable=(Table<TChildKey,TChild>)child.GetTable(database);var parentTable=(Table<TParentKey,TParent>)parent.GetTable(database);
            IEnumerable<TChild> rows=childTable;Func<TParentKey,bool> contains=parentTable.ContainsKey;
            if(scope is not null){rows=database.GetTransactionCollection(scope,childTable);contains=database.GetTransactionCollection(scope,parentTable).ContainsKey;}
            foreach(var row in rows){
                var values=fields.Select(field=>field.GetValue(row)).ToArray();if(values.Any(value=>value is null)){continue;}
                var key=(TParentKey)constructor.Invoke(positions.Select(position=>values[position]).ToArray());
                if(!contains(key)){throw new ForeignKeyViolationException(relation.Name,"Foreign key '"+relation.Name+"' has no referenced row: "+child.Path+" -> "+parent.Path);}
            }
        };
    }
    /// <summary>未公開の構築結果も通常取引と同じ検査を使用する。</summary>
    public void ValidateRelations(){foreach(var validate in this.RelationValidators){validate(null);}}
    private void ValidateRelations(TransactionScope scope){foreach(var validate in this.RelationValidators){if(this.ambientTransaction.Value is not null){validate(null);}else{validate(scope);}}}
    internal bool RequiresRelationTransaction=>this.ambientTransaction.Value is null&&this.Options.GatewayProfile is null&&this.RelationValidators.Length>0;
    internal TResult RelationWrite<TResult>(Func<TResult> operation){using var transaction=this.BeginTransaction();var result=operation();transaction.Commit();return result;}
    protected DatabaseTransaction? BeginRelationImport(){if(this.Options.GatewayProfile is not null||this.RelationValidators.Length==0){return null;}return this.BeginTransaction();}
}
