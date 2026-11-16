using System.Reflection;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GeneratedTableMigration(string SourceObjectName,string TargetObjectName,MigrationPlan Plan);

/// <summary>source の論理読取を保持し、新規の非公開領域へ全表を型付きで逐次移行する。</summary>
public static class TypedGenerationMigrator{
    /// <summary>SQL Server などの既存の型付き読取元から、新しい保存プロファイルを逐次構築する。</summary>
    public static long Import(LoadedDatabaseGeneration target,IDatabaseImportSource source,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(source);using var snapshot=target.Database.BeginUnpublishedSnapshot();long count=0;foreach(var binding in target.Generated.Tables){var table=TableObject(target.Database,binding.ObjectName);var types=table.GetType().GetGenericArguments();var method=typeof(TypedGenerationMigrator).GetMethod(nameof(ImportTable),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(types[0],types[1]);try{count=checked(count+(long)method.Invoke(null,[table,binding.ObjectName,source,snapshot,cancellationToken])!);}catch(TargetInvocationException error)when(error.InnerException is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}}snapshot.Complete();return count;
    }
    private static long ImportTable<TKey,TRow>(object table,string name,IDatabaseImportSource source,DatabaseContainer.UnpublishedSnapshotBuilder snapshot,CancellationToken cancellationToken) where TKey:struct where TRow:struct{
        long count=0;foreach(var row in source.ReadTable<TRow>(new(name))){cancellationToken.ThrowIfCancellationRequested();snapshot.Insert((Table<TKey,TRow>)table,row);count=checked(count+1);}return count;
    }
    public static long Prepare(LoadedDatabaseGeneration source,LoadedDatabaseGeneration target,IReadOnlyList<GeneratedTableMigration> migrations,MigrationContext context,IReadOnlySet<string>? createdTables=null){
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(migrations);ArgumentNullException.ThrowIfNull(context);
        if(ReferenceEquals(source,target)){throw new ArgumentException("A migration needs a separate target generation.",nameof(target));}
        var names=new HashSet<string>(StringComparer.Ordinal);var ids=new HashSet<long>();var copies=new List<(object Source,object Target,Func<object,object,DatabaseContainer.UnpublishedSnapshotBuilder,MigrationContext,long> Copy)>();
        foreach(var migration in migrations){
            if(!names.Add(migration.TargetObjectName)||!ids.Add(migration.Plan.TableId)){throw new InvalidDataException("A target table is mapped more than once.");}
            if(!source.Generated.Tables.Any(table=>table.ObjectName==migration.SourceObjectName)||!target.Generated.Tables.Any(table=>table.ObjectName==migration.TargetObjectName)){throw new InvalidDataException("A migration refers to an unknown generated table.");}
            var oldTable=TableObject(source.Database,migration.SourceObjectName);var newTable=TableObject(target.Database,migration.TargetObjectName);
            var oldTypes=oldTable.GetType().GetGenericArguments();var newTypes=newTable.GetType().GetGenericArguments();
            var method=typeof(TypedGenerationMigrator).GetMethod(nameof(Bind),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(oldTypes[0],oldTypes[1],newTypes[0],newTypes[1]);
            Func<object,object,DatabaseContainer.UnpublishedSnapshotBuilder,MigrationContext,long> copy;
            try{copy=(Func<object,object,DatabaseContainer.UnpublishedSnapshotBuilder,MigrationContext,long>)method.Invoke(null,[migration.Plan])!;}
            catch(TargetInvocationException error)when(error.InnerException is not null){global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
            copies.Add((oldTable,newTable,copy));
        }
        foreach(var table in target.Generated.Tables){if(!names.Contains(table.ObjectName)&&createdTables?.Contains(table.ObjectName)!=true){throw new InvalidDataException("A target table has no migration mapping: "+table.ObjectName);}}
        if(createdTables is not null&&createdTables.Any(name=>names.Contains(name)||!target.Generated.Tables.Any(table=>table.ObjectName==name))){throw new InvalidDataException("The set of newly created tables is inconsistent.");}
        using var read=source.Database.BeginReadSession();using var snapshot=target.Database.BeginUnpublishedSnapshot();long count=0;
        foreach(var item in copies){context.ThrowIfCancellationRequested();count=checked(count+item.Copy(item.Source,item.Target,snapshot,context));}
        context.ThrowIfCancellationRequested();snapshot.Complete();return count;
    }
    private static object TableObject(DatabaseContainer database,string path){
        object current=database;
        foreach(var part in path.Split('.')){
            var property=current.GetType().GetProperty(part,BindingFlags.Public|BindingFlags.Instance);
            if(property?.GetMethod is not{IsPublic:true}||property.GetIndexParameters().Length!=0){throw new InvalidDataException("A generated table path is unavailable: "+path);}
            current=property.GetValue(current)??throw new InvalidDataException("A generated table path returned null: "+path);
        }
        var type=current.GetType();if(!type.IsGenericType||type.GetGenericTypeDefinition()!=typeof(StoredTable<,>)){throw new InvalidDataException("A migration target must be a keyed typed table.");}
        return current;
    }
    private static Func<object,object,DatabaseContainer.UnpublishedSnapshotBuilder,MigrationContext,long> Bind<TOldKey,TOld,TNewKey,TNew>(MigrationPlan plan)
        where TOldKey:struct where TOld:struct where TNewKey:struct where TNew:struct{
        var converter=RowConverterCompiler.Compile<TOld,TNew>(plan);
        return(source,target,snapshot,context)=>{
            var oldTable=(Table<TOldKey,TOld>)source;var newTable=(Table<TNewKey,TNew>)target;long count=0;
            foreach(var row in oldTable){try{snapshot.Insert(newTable,converter(in row,context));}catch(InvalidDataException error)when(error.Message.StartsWith("Duplicate key in the unpublished snapshot:",StringComparison.Ordinal)){throw new SqlQueryException("A duplicate key prevents the table migration.",2627);}count=checked(count+1);}
            return count;
        };
    }
}
