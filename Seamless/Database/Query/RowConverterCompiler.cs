using System.Reflection;
using System.Reflection.Emit;

namespace Seamless.Database.Query;

public delegate TNew RowConverter<TOld,TNew>(in TOld source,MigrationContext context) where TOld:struct where TNew:struct;

/// <summary>一度だけ reflection で bind し、移行中は型付き getter / constructor を呼ぶ。</summary>
public static class RowConverterCompiler{
    public static RowConverter<TOld,TNew> Compile<TOld,TNew>(MigrationPlan plan) where TOld:struct where TNew:struct{
        ArgumentNullException.ThrowIfNull(plan);
        var source=Bind(typeof(TOld),plan.SourceColumns);var target=Bind(typeof(TNew),plan.TargetColumns);
        // [行の作り方] 生成した行は生成コンストラクターの列名の一覧に従う(キーの型を先に作る)。手書きの行は、列と同じ型・名前の引数を列の順に並べたコンストラクター。
        var shape=GeneratedRecordShape.TryCreate(typeof(TNew));
        ConstructorInfo? constructor=null;
        if(shape is null){
            constructor=typeof(TNew).GetConstructor(target.Select(static property=>property.PropertyType).ToArray())??throw new InvalidDataException("The target record has no constructor matching the migration column order.");
            var parameters=constructor.GetParameters();
            for(var ordinal=0;ordinal<target.Length;ordinal++){if(!string.Equals(parameters[ordinal].Name,target[ordinal].Name,StringComparison.Ordinal)){throw new InvalidDataException("The target constructor member order disagrees with the migration plan: "+target[ordinal].Name);}}
        }else if(!shape.Columns.Order(StringComparer.Ordinal).SequenceEqual(target.Select(static property=>property.Name).Order(StringComparer.Ordinal),StringComparer.Ordinal)){
            throw new InvalidDataException("The target record columns disagree with the migration plan: "+typeof(TNew).FullName);
        }
        var method=new DynamicMethod("migrate_"+plan.TableId,typeof(TNew),[typeof(MigrationPlan),typeof(TOld).MakeByRefType(),typeof(MigrationContext)],typeof(RowConverterCompiler).Module,false);
        var il=method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_2);il.Emit(OpCodes.Callvirt,typeof(MigrationContext).GetMethod(nameof(MigrationContext.ThrowIfCancellationRequested))!);
        // 列の値は計画の順に局所変数へ入れ、最後に行の作り方の順で読み出す。
        var locals=target.Select(property=>il.DeclareLocal(property.PropertyType)).ToArray();
        for(var ordinal=0;ordinal<plan.TargetColumns.Count;ordinal++){
            EmitColumn(il,plan,source,target,ordinal);
            il.Emit(OpCodes.Stloc,locals[ordinal]);
        }
        if(shape is null){
            foreach(var local in locals){il.Emit(OpCodes.Ldloc,local);}
            il.Emit(OpCodes.Newobj,constructor!);
        }else{
            var ordinals=target.Select(static(property,index)=>(property.Name,index)).ToDictionary(static item=>item.Name,static item=>item.index,StringComparer.Ordinal);
            // 値の位置は、列の型と名前を持つ仮の引数で表す(コンストラクターの引数の型の照合を通すため)。
            var built=shape.New(name=>global::System.Linq.Expressions.Expression.Parameter(target[ordinals[name]].PropertyType,name));
            EmitNew(il,built,locals,ordinals);
        }
        il.Emit(OpCodes.Ret);
        return(RowConverter<TOld,TNew>)method.CreateDelegate(typeof(RowConverter<TOld,TNew>),plan);
    }
    // 行を作る式(GeneratedRecordShape.New。値の位置は列の名前の仮の引数)を、局所変数を読む IL に置き換えて書く。
    private static void EmitNew(ILGenerator il,global::System.Linq.Expressions.Expression expression,LocalBuilder[] locals,Dictionary<string,int> ordinals){
        switch(expression){
            case global::System.Linq.Expressions.NewExpression created:
                foreach(var argument in created.Arguments){EmitNew(il,argument,locals,ordinals);}
                il.Emit(OpCodes.Newobj,created.Constructor!);return;
            case global::System.Linq.Expressions.ParameterExpression column:
                il.Emit(OpCodes.Ldloc,locals[ordinals[column.Name!]]);return;
            default:
                throw new InvalidDataException("Unsupported generated record construction: "+expression.NodeType);
        }
    }
    private static void EmitColumn(ILGenerator il,MigrationPlan plan,PropertyInfo[] source,PropertyInfo[] target,int ordinal){
        var targetColumn=plan.TargetColumns[ordinal];var sourceOrdinal=-1;
        for(var index=0;index<plan.SourceColumns.Count;index++){if(plan.SourceColumns[index].Id==targetColumn.Id){sourceOrdinal=index;break;}}
        var targetType=target[ordinal].PropertyType;
        if(sourceOrdinal<0){
            il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldc_I4,ordinal);il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Call,typeof(MigrationPlan).GetMethod("AddedColumn",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(targetType));
            return;
        }
        var sourceColumn=plan.SourceColumns[sourceOrdinal];var sourceProperty=source[sourceOrdinal];var sourceType=sourceProperty.PropertyType;
        if(sourceType==targetType&&sourceColumn.Type==targetColumn.Type&&sourceColumn.IsNullable==targetColumn.IsNullable){
            il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);return;
        }
        if(sourceType==typeof(int)&&targetType==typeof(long)&&sourceColumn.Type.Name.Equals("int",StringComparison.OrdinalIgnoreCase)&&targetColumn.Type.Name.Equals("bigint",StringComparison.OrdinalIgnoreCase)){
            il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);il.Emit(OpCodes.Conv_I8);return;
        }
        il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Ldarg_1);il.Emit(OpCodes.Call,sourceProperty.GetMethod!);il.Emit(OpCodes.Ldc_I4,ordinal);
        il.Emit(OpCodes.Call,typeof(MigrationPlan).GetMethod("ConvertColumn",BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(sourceType,targetType));
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
