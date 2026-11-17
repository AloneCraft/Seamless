using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>一度の世代移行に閉じた型対応。一行内の参照対応は行ごとに破棄する。</summary>
internal sealed class MigrationValueConverter(Assembly before,Assembly after){
    private sealed class Plan{
        internal Field[] Fields=[];
        internal long Bytes,ElementBytes;
    }
    private sealed record Field(Func<object,object?> Read,Action<object,object?> Write,Type Target);
    private readonly Dictionary<(Type,Type),Plan> plans=[];
    private readonly Dictionary<Type,Type> targets=[];
    private readonly Dictionary<Type,object> defaults=[];

    internal static bool SameTypeName(Type source,Type target,Assembly before,Assembly after){
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(source==target){return true;}
        if(source.IsArray||target.IsArray){return source.IsArray&&target.IsArray&&source.IsSZArray==target.IsSZArray&&source.GetArrayRank()==target.GetArrayRank()&&SameTypeName(source.GetElementType()!,target.GetElementType()!,before,after);}
        if(source.IsConstructedGenericType||target.IsConstructedGenericType){
            if(!source.IsConstructedGenericType||!target.IsConstructedGenericType||!SameTypeName(source.GetGenericTypeDefinition(),target.GetGenericTypeDefinition(),before,after)){return false;}
            return source.GetGenericArguments().Zip(target.GetGenericArguments()).All(pair=>SameTypeName(pair.First,pair.Second,before,after));
        }
        return source.FullName==target.FullName&&(source.Assembly==before&&target.Assembly==after||source.Assembly.GetName().Name==target.Assembly.GetName().Name);
    }

    internal Type Prepare(Type target){
        var source=this.InContext(target,before);
        this.GetPlan(source,target);
        return source;
    }
    internal void PrepareDefault(Type target){
        if(target.IsValueType&&Nullable.GetUnderlyingType(target) is null){this.defaults.TryAdd(target,RuntimeHelpers.GetUninitializedObject(target));}
    }
    internal IEnumerable<object[]> ConvertRows(IEnumerable<object[]> rows,Type[] parameters,ResourceBudget budget,CancellationToken cancellation){
        if(parameters.All(type=>Scalar(Nullable.GetUnderlyingType(type)??type))){return rows;}
        return this.ConvertValues(rows,parameters,budget,cancellation);
    }
    private IEnumerable<object[]> ConvertValues(IEnumerable<object[]> rows,Type[] parameters,ResourceBudget budget,CancellationToken cancellation){
        foreach(var values in rows){
            using var memory=budget.ReserveMemory(checked(1024L+values.Length*IntPtr.Size));
            var references=new Dictionary<object,object>(ReferenceEqualityComparer.Instance);var result=new object[values.Length];
            for(var index=0;index<values.Length;index++){
                result[index]=this.Copy(values[index],parameters[index],references,memory,cancellation)!;
            }
            yield return result;
        }
    }
    private object? Copy(object? source,Type target,Dictionary<object,object> references,ResourceReservation memory,CancellationToken cancellation){
        cancellation.ThrowIfCancellationRequested();RuntimeHelpers.EnsureSufficientExecutionStack();
        if(source is null){if(this.defaults.TryGetValue(target,out var value)){return value;}return null;}
        var sourceType=source.GetType();target=Nullable.GetUnderlyingType(target)??target;
        if(!this.targets.TryGetValue(sourceType,out var actual)){
            actual=this.InContext(sourceType,after);this.targets.Add(sourceType,actual);
        }
        if(!target.IsAssignableFrom(actual)){throw Incompatible(sourceType,target);}
        if(Scalar(sourceType)&&sourceType==actual){return source;}
        if(references.TryGetValue(source,out var previous)){return previous;}
        var plan=this.GetPlan(sourceType,actual);
        memory.Resize(checked(memory.Bytes+plan.Bytes+256));
        if(sourceType.IsEnum){var value=Enum.ToObject(actual,System.Convert.ChangeType(source,Enum.GetUnderlyingType(sourceType))!);references.Add(source,value);return value;}
        object result;
        if(source is Array array){
            var lengths=new int[array.Rank];var indices=new int[array.Rank];var bounds=new int[array.Rank];
            for(var dimension=0;dimension<array.Rank;dimension++){
                lengths[dimension]=array.GetLength(dimension);indices[dimension]=bounds[dimension]=array.GetLowerBound(dimension);
            }
            var element=actual.GetElementType()!;
            memory.Resize(checked(memory.Bytes+array.LongLength*plan.ElementBytes));
            Array copy;
            if(actual.IsSZArray){copy=Array.CreateInstance(element,lengths[0]);}
            else{copy=Array.CreateInstance(element,lengths,bounds);}
            result=copy;references.Add(source,result);
            foreach(var value in array){
                copy.SetValue(this.Copy(value,element,references,memory,cancellation),indices);
                for(var dimension=indices.Length-1;dimension>=0;dimension--){
                    if((long)indices[dimension]-bounds[dimension]+1<lengths[dimension]){indices[dimension]++;break;}
                    indices[dimension]=bounds[dimension];
                }
            }
            return result;
        }
        result=RuntimeHelpers.GetUninitializedObject(actual);references.Add(source,result);
        foreach(var field in plan.Fields){field.Write(result,this.Copy(field.Read(source),field.Target,references,memory,cancellation));}
        return result;
    }
    private Plan GetPlan(Type source,Type target){
        if(this.plans.TryGetValue((source,target),out var existing)){return existing;}
        if(!SameTypeName(source,target,before,after)||source.IsValueType!=target.IsValueType||source.IsAbstract!=target.IsAbstract||source.IsInterface!=target.IsInterface){throw Incompatible(source,target);}
        var plan=new Plan();this.plans.Add((source,target),plan);
        if(Scalar(source)&&source==target){plan.Bytes=Size(target);return plan;}
        if(source.IsEnum&&target.IsEnum){
            if(Enum.GetUnderlyingType(source)!=Enum.GetUnderlyingType(target)){throw Incompatible(source,target);}
            plan.Bytes=Size(target);return plan;
        }
        if(source.IsArray&&target.IsArray){
            this.GetPlan(source.GetElementType()!,target.GetElementType()!);plan.Bytes=64L+target.GetArrayRank()*16L;plan.ElementBytes=Size(target.GetElementType()!);return plan;
        }
        ValidateDataType(source);ValidateDataType(target);
        var previous=Fields(source);var next=Fields(target).ToDictionary(Identity,StringComparer.Ordinal);
        if(previous.Length!=next.Count){throw Incompatible(source,target);}
        var fields=new List<Field>();
        foreach(var field in previous){
            if(!next.TryGetValue(Identity(field),out var replacement)){throw Incompatible(source,target);}
            this.GetPlan(field.FieldType,replacement.FieldType);
            fields.Add(new(Getter(field),Setter(replacement),replacement.FieldType));
        }
        plan.Fields=fields.ToArray();plan.Bytes=32+next.Values.Sum(field=>Size(field.FieldType));
        if(target.IsValueType){plan.Bytes=Math.Max(plan.Bytes,32+Size(target));}
        return plan;
    }
    private Type InContext(Type type,Assembly root){
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(type.IsArray){var element=this.InContext(type.GetElementType()!,root);if(type.IsSZArray){return element.MakeArrayType();}return element.MakeArrayType(type.GetArrayRank());}
        var definition=type;if(type.IsGenericType){definition=type.GetGenericTypeDefinition();}
        var assembly=definition.Assembly;
        if(assembly==before&&root==after){assembly=after;}
        else if(assembly==after&&root==before){assembly=before;}
        else if(assembly.IsCollectible){
            var context=AssemblyLoadContext.GetLoadContext(root)!;
            var name=assembly.GetName().Name!;
            try{
                assembly=context.Assemblies.FirstOrDefault(candidate=>string.Equals(candidate.GetName().Name,name,StringComparison.OrdinalIgnoreCase))
                    ??context.LoadFromAssemblyName(new AssemblyName(name));
            }catch(Exception error)when(error is FileNotFoundException or FileLoadException or TypeLoadException){
                throw new ManagementException("MigrationRequired","列値に対応する世代の依存DLLがありません: "+name+" · "+error.Message);
            }
        }
        var mapped=assembly.GetType(definition.FullName!,false)??throw new ManagementException("MigrationRequired","対応する移行先・移行元の列型がDLLにありません: "+type.FullName);
        if(type.IsGenericType){
            try{mapped=mapped.MakeGenericType(type.GetGenericArguments().Select(argument=>this.InContext(argument,root)).ToArray());}
            catch(ArgumentException error){throw new ManagementException("MigrationRequired","移行先のジェネリック型制約が異なります: "+type+" · "+error.Message);}
        }
        return mapped;
    }
    private static bool Scalar(Type type)=>type.IsPrimitive||type==typeof(string)||type==typeof(decimal)||type==typeof(DateTime)||type==typeof(DateTimeOffset)||type==typeof(TimeSpan)||type==typeof(Guid)||type==typeof(DateOnly)||type==typeof(TimeOnly)||type==typeof(Int128)||type==typeof(UInt128);
    private static void ValidateDataType(Type type){
        var finalizer=type.GetMethod("Finalize",BindingFlags.Instance|BindingFlags.NonPublic);
        if(type.IsPointer||type.IsFunctionPointer||type.IsByRefLike||type.IsCOMObject||typeof(Delegate).IsAssignableFrom(type)||typeof(MemberInfo).IsAssignableFrom(type)||typeof(SafeHandle).IsAssignableFrom(type)
            ||finalizer is not null&&finalizer.DeclaringType!=typeof(object)||type.IsExplicitLayout||type.IsDefined(typeof(InlineArrayAttribute),false)){
            throw new ManagementException("MigrationRequired","列値の移行に専用の変換が必要です: "+type.FullName);
        }
    }
    private static FieldInfo[] Fields(Type type){
        var fields=new List<FieldInfo>();
        for(var current=type;current is not null&&current!=typeof(object);current=current.BaseType){
            fields.AddRange(current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
        }
        if(fields.Any(field=>field.IsDefined(typeof(FixedBufferAttribute),false))){throw new ManagementException("MigrationRequired","固定バッファの移行に専用の変換が必要です: "+type.FullName);}
        return fields.ToArray();
    }
    private static string Identity(FieldInfo field){
        var owner=field.DeclaringType!;if(owner.IsGenericType){owner=owner.GetGenericTypeDefinition();}
        return owner.FullName+"/"+field.Name;
    }
    private static long Size(Type type){
        if(!type.IsValueType){return IntPtr.Size;}
        return typeof(MigrationValueConverter).GetMethod(nameof(ValueSize),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(type).CreateDelegate<Func<int>>()();
    }
    private static int ValueSize<T>()=>Unsafe.SizeOf<T>();
    private static ManagementException Incompatible(Type source,Type target)=>new("MigrationRequired","列値の型・保存フィールドが異なります。明示的な変換が必要です: "+source+" → "+target);
    private static Func<object,object?> Getter(FieldInfo field){
        var method=new DynamicMethod("ReadMigrationField",typeof(object),[typeof(object)],typeof(MigrationValueConverter).Module,true);var il=method.GetILGenerator();
        Owner(il,field.DeclaringType!);il.Emit(OpCodes.Ldfld,field);
        if(field.FieldType.IsValueType){il.Emit(OpCodes.Box,field.FieldType);}
        il.Emit(OpCodes.Ret);return method.CreateDelegate<Func<object,object?>>();
    }
    private static Action<object,object?> Setter(FieldInfo field){
        var method=new DynamicMethod("WriteMigrationField",typeof(void),[typeof(object),typeof(object)],typeof(MigrationValueConverter).Module,true);var il=method.GetILGenerator();
        Owner(il,field.DeclaringType!);il.Emit(OpCodes.Ldarg_1);
        if(field.FieldType.IsValueType){il.Emit(OpCodes.Unbox_Any,field.FieldType);}else{il.Emit(OpCodes.Castclass,field.FieldType);}
        il.Emit(OpCodes.Stfld,field);il.Emit(OpCodes.Ret);return method.CreateDelegate<Action<object,object?>>();
    }
    private static void Owner(ILGenerator il,Type type){il.Emit(OpCodes.Ldarg_0);if(type.IsValueType){il.Emit(OpCodes.Unbox,type);}else{il.Emit(OpCodes.Castclass,type);}}
}
