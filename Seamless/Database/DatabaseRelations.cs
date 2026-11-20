using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using System.Collections;
using System.Reflection;
using System.Linq;
using Seamless.Transactions;

namespace Seamless.Database;

internal enum RelationValidationWork{ParentLookup,ChildScan}

/// <summary>宣言された表の経路。カタログ列挙はgetterを呼ばず、実体化は明示操作に限定する。</summary>
public sealed record DatabaseTableDeclaration(string Path,PropertyInfo[] Access){
    public PropertyInfo Property=>this.Access[^1];
    public object GetTable(DatabaseContainer database){object current=database;foreach(var property in this.Access){current=property.GetValue(current)??throw new InvalidOperationException("Null schema/table: "+this.Path);}return current;}
    public static DatabaseTableDeclaration[] Read(Type type)=>ReadMatching(type,static definition=>definition==typeof(Table<,>)||definition==typeof(Table<>));
    /// <summary>取引を通さない表(db.X、<see cref="Set{TKey,TRecord}"/>・<see cref="Set{TRecord}"/>)の宣言の経路。</summary>
    public static DatabaseTableDeclaration[] ReadDirectSets(Type type)=>ReadMatching(type,static definition=>definition==typeof(Set<,>)||definition==typeof(Set<>));
    private static DatabaseTableDeclaration[] ReadMatching(Type type,Func<Type,bool> matches){var result=new List<DatabaseTableDeclaration>();Walk(type,"",[],new global::System.Collections.Generic.HashSet<Type>());return result.ToArray();
        void Walk(Type owner,string prefix,PropertyInfo[] access,global::System.Collections.Generic.HashSet<Type> ancestors){
            if(!ancestors.Add(owner)){throw new InvalidOperationException("Recursive schema type: "+owner.FullName);}
            try{foreach(var property in owner.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0)){
                var path=prefix+property.Name;var chain=access.Append(property).ToArray();var value=property.PropertyType;
                if(typeof(DatabaseSchema).IsAssignableFrom(value)){Walk(value,path+".",chain,ancestors);}
                else if(value.IsGenericType&&matches(value.GetGenericTypeDefinition())){result.Add(new(path,chain));}
            }}finally{ancestors.Remove(owner);}
        }
    }
}

public abstract partial class DatabaseContainer{
    private sealed record RelationValidator(string Child,string Parent,Action<DatabaseContainer,TransactionScope?,DatabaseTransactionState?,Dictionary<object,ChildScan>?> Validate);
    private sealed record ChildScan(IEnumerable Rows,List<Action<object>> Checks);
    private RelationValidator[]? relationValidators;
    internal Action<RelationValidationWork>? ObserveRelationValidation{get;set;}
    private RelationValidator[] RelationValidators=>this.relationValidators??=BuildRelationValidators(this.GetType());
    public static void ValidateRelationDeclarations(Type databaseType){_ = BuildRelationValidators(databaseType);}
    private static RelationValidator[] BuildRelationValidators(Type databaseType){
        var tables=DatabaseTableDeclaration.Read(databaseType);var result=new List<RelationValidator>();var names=new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach(var child in tables){foreach(var relation in child.Property.GetCustomAttributes<ForeignKeyAttribute>()){
            if(string.IsNullOrWhiteSpace(relation.Name)){throw new InvalidOperationException("A foreign key name is required.");}
            if(!names.Add(relation.Name)){throw new InvalidOperationException("Duplicate foreign key: "+relation.Name);}
            var parent=tables.SingleOrDefault(table=>table.Path==relation.ReferencedTable)??throw new InvalidOperationException("Referenced table not found: "+relation.ReferencedTable);
            var childTypes=child.Property.PropertyType.GenericTypeArguments;var parentTypes=parent.Property.PropertyType.GenericTypeArguments;
            if(childTypes.Length!=2||parentTypes.Length!=2){throw new NotSupportedException("Foreign keys require keyed tables: "+relation.Name);}
            var method=typeof(DatabaseContainer).GetMethod(nameof(CreateRelationValidator),BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(childTypes[0],childTypes[1],parentTypes[0],parentTypes[1]);
            try{result.Add((RelationValidator)method.Invoke(null,[child,parent,relation])!);}catch(TargetInvocationException error)when(error.InnerException is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }}return result.ToArray();
    }
    private static RelationValidator CreateRelationValidator<TChildKey,TChild,TParentKey,TParent>(DatabaseTableDeclaration child,DatabaseTableDeclaration parent,ForeignKeyAttribute relation)
        where TChildKey:struct where TChild:struct where TParentKey:struct where TParent:struct{
        var keyFields=typeof(TParentKey).GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(field=>field.GetIndexParameters().Length==0).ToArray();
        if(relation.Columns.Length==0||relation.Columns.Length!=relation.ReferencedColumns.Length||relation.Columns.Distinct().Count()!=relation.Columns.Length||relation.ReferencedColumns.Length!=keyFields.Length||relation.ReferencedColumns.Distinct().Count()!=keyFields.Length||!keyFields.All(field=>relation.ReferencedColumns.Contains(field.Name,StringComparer.Ordinal))){throw new InvalidOperationException("Foreign key must reference the complete primary key: "+relation.Name);}
        var fields=relation.Columns.Select(name=>typeof(TChild).GetProperty(name)??throw new InvalidOperationException("Foreign key column not found: "+name)).ToArray();
        // 通常のビルドで生成したキーのコンストラクターは引数名が value0… なので、列名は生成器が付ける GeneratedRecordConstructorAttribute から読む。
        static IReadOnlyList<string> Members(ConstructorInfo item)=>item.GetCustomAttribute<GeneratedRecordConstructorAttribute>()?.Members??(IReadOnlyList<string>)item.GetParameters().Select(parameter=>parameter.Name??"").ToArray();
        var constructor=typeof(TParentKey).GetConstructors().SingleOrDefault(item=>item.GetParameters().Length==keyFields.Length&&Members(item).All(member=>keyFields.Any(field=>field.Name.Equals(member,StringComparison.OrdinalIgnoreCase))))??throw new NotSupportedException("Primary key constructor not found: "+parent.Path);
        var positions=Members(constructor).Select(member=>Array.FindIndex(relation.ReferencedColumns,name=>name.Equals(member,StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach(var field in keyFields){var index=Array.IndexOf(relation.ReferencedColumns,field.Name);if((Nullable.GetUnderlyingType(fields[index].PropertyType)??fields[index].PropertyType)!=field.PropertyType){throw new InvalidOperationException("Foreign key types differ: "+relation.Name);}}
        TParentKey? Reference(TChild row){
            var values=new object?[positions.Length];
            for(var i=0;i<values.Length;i++){
                values[i]=fields[positions[i]].GetValue(row);
                if(values[i] is null){return null;}
            }
            return (TParentKey)constructor.Invoke(values);
        }
        ForeignKeyViolationException Violation()=>new(relation.Name,"Foreign key '"+relation.Name+"' has no referenced row: "+child.Path+" -> "+parent.Path);
        return new(child.Path,parent.Path,(database,scope,legacy,scans)=>{
            var childTable=(StoredTable<TChildKey,TChild>)child.GetTable(database);var parentTable=(StoredTable<TParentKey,TParent>)parent.GetTable(database);
            var childCollection=scope is null?null:database.GetTransactionCollection(scope,childTable);
            var parentCollection=scope is null?null:database.GetTransactionCollection(scope,parentTable);
            IEnumerable<TChild> rows=childCollection is null?childTable:childCollection;
            Func<TParentKey,bool> contains=parentCollection is null?parentTable.ContainsKey:parentCollection.ContainsKey;
            if(scans is null){
                foreach(var row in rows){if(Reference(row) is{} key&&!contains(key)){throw Violation();}}
                return;
            }
            var comparer=parentTable.GatewayKeyComparer??database.executionResources.Codecs.GetEqualityComparer<TParentKey>();
            var references=new global::System.Collections.Generic.HashSet<TParentKey>(comparer);
            void VisitChild(TChildKey _,(bool Found,TChild Row) before,(bool Found,TChild Row) after){
                if(!after.Found||Reference(after.Row) is not{} target){return;}
                if(!before.Found||Reference(before.Row) is not{} original||!comparer.Equals(target,original)){references.Add(target);}
            }
            var deleted=new global::System.Collections.Generic.HashSet<TParentKey>(comparer);
            void VisitParent(TParentKey key,(bool Found,TParent Row) before,(bool Found,TParent Row) after){
                if(before.Found&&!after.Found){deleted.Add(key);}
            }
            childCollection?.VisitFinalChanges(VisitChild);
            parentCollection?.VisitFinalChanges(VisitParent);
            if(legacy is not null){
                if(legacy.Overlays.TryGetValue(childTable,out var childChanges)){((TableTransactionOverlay<TChildKey,TChild>)childChanges).VisitFinalChanges(VisitChild);}
                if(legacy.Overlays.TryGetValue(parentTable,out var parentChanges)){((TableTransactionOverlay<TParentKey,TParent>)parentChanges).VisitFinalChanges(VisitParent);}
            }
            foreach(var key in references){
                database.ObserveRelationValidation?.Invoke(RelationValidationWork.ParentLookup);
                if(!contains(key)){throw Violation();}
            }
            if(deleted.Count==0){return;}
            if(!scans.TryGetValue(childTable,out var scan)){scans.Add(childTable,scan=new(rows,[]));}
            scan.Checks.Add(row=>{if(Reference((TChild)row) is{} key&&deleted.Contains(key)){throw Violation();}});
        });
    }
    /// <summary>未公開の構築結果も通常取引と同じ検査を使用する。</summary>
    public void ValidateRelations(){foreach(var relation in this.RelationValidators){relation.Validate(this,null,null,null);}}
    private void ValidateRelations(TransactionScope scope){
        if(this.ambientTransaction.Value is null){this.ValidateRelationChanges(scope,null);}
    }
    private void ValidateRelationChanges(TransactionScope? scope,DatabaseTransactionState? legacy){
        var scans=new Dictionary<object,ChildScan>(global::System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var planned=scope??legacy?.SharedScope;
        foreach(var relation in this.RelationValidators){
            if(planned?.HasTablePlan==true&&!planned.CanWriteTable(this.ResolveParticipant(relation.Child))&&!planned.CanWriteTable(this.ResolveParticipant(relation.Parent))){continue;}
            relation.Validate(this,scope,legacy,scans);
        }
        foreach(var scan in scans.Values){
            this.ObserveRelationValidation?.Invoke(RelationValidationWork.ChildScan);
            foreach(var row in scan.Rows){foreach(var check in scan.Checks){check(row!);}}
        }
    }
    private IEnumerable<string> RelationDependencies(global::System.Collections.Generic.HashSet<string> writing){
        foreach(var relation in this.RelationValidators){
            if(writing.Contains(relation.Child)){yield return relation.Parent;}
            if(writing.Contains(relation.Parent)){yield return relation.Child;}
        }
    }
    // 取引の外の書き込みは暗黙の取引(確定時に外部キーを検査する)で行うので、従来の取引で包むのはそれ以外の経路だけ。
    internal bool RequiresRelationTransaction=>!this.UsesImplicitTransaction&&this.ambientTransaction.Value is null&&this.Options.GatewayProfile is null&&this.RelationValidators.Length>0;
    internal TResult RelationWrite<TResult>(Func<TResult> operation){using var transaction=this.BeginTransaction();var result=operation();transaction.Commit();return result;}
    protected DatabaseTransaction? BeginRelationImport(){if(this.Options.GatewayProfile is not null||this.RelationValidators.Length==0){return null;}return this.BeginTransaction();}
}
