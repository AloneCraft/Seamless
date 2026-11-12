using System.Reflection;
using System.Reflection.Emit;

namespace Seamless.Database.Query;

public delegate TNew RowConverter<TOld,TNew>(in TOld source,MigrationContext context) where TOld:struct where TNew:struct;

/// <summary>一度だけ reflection で bind し、移行中は型付き getter / constructor を呼ぶ。</summary>
public static class RowConverterCompiler{
    public static RowConverter<TOld,TNew> Compile<TOld,TNew>(MigrationPlan plan) where TOld:struct where TNew:struct{
        ArgumentNullException.ThrowIfNull(plan);
        var source=Bind(typeof(TOld),plan.SourceColumns);var target=Bind(typeof(TNew),plan.TargetColumns);
        var constructor=typeof(TNew).GetConstructor(target.Select(static property=>property.PropertyType).ToArray())??throw new InvalidDataException("The target record has no constructor matching the migration column order.");
        var parameters=constructor.GetParameters();
        for(var ordinal=0;ordinal<target.Length;ordinal++){if(!string.Equals(parameters[ordinal].Name,target[ordinal].Name,StringComparison.Ordinal)){throw new InvalidDataException("The target constructor member order disagrees with the migration plan: "+target[ordinal].Name);}}
        var method=new DynamicMethod("migrate_"+plan.TableId,typeof(TNew),[typeof(MigrationPlan),typeof(TOld).MakeByRefType(),typeof(MigrationContext)],typeof(RowConverterCompiler).Module,false);
        var il=method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Callvirt,typeof(MigrationContext).GetMethod(nameof(MigrationContext.ThrowIfCancellationRequested))!);
        for(var ordinal=0;ordinal<plan.TargetColumns.Count;ordinal++){
            var targetColumn=plan.TargetColumns[ordinal];var sourceOrdinal=-1;
            for(var index=0;index<plan.SourceColumns.Count;index++){if(plan.SourceColumns[index].Id==targetColumn.Id){sourceOrdinal=index;break;}}
            var targetType=target[ordinal].PropertyType;
            if(sourceOrdinal<0){
                il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4,ordinal);il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call,typeof(MigrationPlan).GetMethod("AddedColumn",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(targetType));
                continue;
            }
            var sourceColumn=plan.SourceColumns[sourceOrdinal];var sourceProperty=source[sourceOrdinal];var sourceType=sourceProperty.PropertyType;
            if(sourceType==targetType&&sourceColumn.Type==targetColumn.Type&&sourceColumn.IsNullable==targetColumn.IsNullable){
                il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);continue;
            }
            if(sourceType==typeof(int)&&targetType==typeof(long)&&sourceColumn.Type.Name.Equals("int",StringComparison.OrdinalIgnoreCase)&&targetColumn.Type.Name.Equals("bigint",StringComparison.OrdinalIgnoreCase)){
                il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);il.Emit(OpCodes.Conv_I8);continue;
            }
            il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);il.Emit(OpCodes.Ldc_I4,ordinal);
            il.Emit(OpCodes.Call,typeof(MigrationPlan).GetMethod("ConvertColumn",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(sourceType,targetType));
        }
        il.Emit(OpCodes.Newobj,constructor);il.Emit(OpCodes.Ret);
        return(RowConverter<TOld,TNew>)method.CreateDelegate(typeof(RowConverter<TOld,TNew>),plan);
    }
    private static PropertyInfo[] Bind(Type type,IReadOnlyList<MigrationColumn> columns){
        var result=new PropertyInfo[columns.Count];
        for(var index=0;index<columns.Count;index++){
            var column=columns[index];var property=type.GetProperty(column.MemberName,BindingFlags.Instance|BindingFlags.Public);
            if(property?.GetMethod is not{IsPublic:true}||property.GetIndexParameters().Length!=0){throw new InvalidDataException("Migration member is unavailable: "+type.FullName+"."+column.MemberName);}
            var actual=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
            if(actual!=column.Type.ClrType){throw new InvalidDataException("Migration member type disagrees with SQL metadata: "+column.MemberName);}
            if(!column.IsNullable&&Nullable.GetUnderlyingType(property.PropertyType) is not null){throw new InvalidDataException("A non-nullable migration column has a nullable member: "+column.MemberName);}
            if(column.IsNullable&&property.PropertyType.IsValueType&&Nullable.GetUnderlyingType(property.PropertyType) is null){throw new InvalidDataException("A nullable migration column requires a nullable value member: "+column.MemberName);}
            result[index]=property;
        }
        return result;
    }
}
