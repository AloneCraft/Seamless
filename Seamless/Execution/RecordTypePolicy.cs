using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Seamless.Execution;

internal sealed class RecordTypePolicy{
    private static readonly ConditionalWeakTable<Type,RecordTypePolicy> Policies=new();
    private static readonly Func<object,object> CloneObject=CreateCloner();
    internal bool IsScalar{get;private init;}
    internal int TypeHash{get;private init;}
    internal Func<object,object>? NormalizeScalar{get;private init;}
    internal Func<object,object,bool>? EqualScalars{get;private init;}
    internal Func<object,int>? HashScalar{get;private init;}
    internal Action<object,RecordValueContext>? NormalizeFields{get;private init;}
    internal Func<object,object,RecordValueContext,bool>? EqualFields{get;private init;}
    internal Func<object,RecordValueContext,int>? HashFields{get;private init;}
    internal Func<object,long> EstimatedBytes{get;private init;}=null!;
    internal FieldInfo[] Fields{get;private init;}=[];
    internal long ObjectBytes{get;private init;}
    internal Type? InlineElement{get;private init;}
    internal int InlineLength{get;private init;}
    internal static RecordTypePolicy Get(Type type)=>Policies.GetValue(type,Create);
    internal static object Clone(object value)=>CloneObject(value);

    private static RecordTypePolicy Create(Type type){
        if(RecordScalarValue.IsScalar(type)){return Factory(type,nameof(CreateScalar));}
        if(type.IsArray){return Factory(type.GetElementType()!,nameof(CreateArray));}
        var finalizer=type.GetMethod("Finalize",BindingFlags.Instance|BindingFlags.NonPublic);
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(OwnedValue<>)||typeof(Delegate).IsAssignableFrom(type)||typeof(MemberInfo).IsAssignableFrom(type)
            ||typeof(SafeHandle).IsAssignableFrom(type)||type.IsByRefLike||type.IsCOMObject
            ||finalizer is not null&&finalizer.DeclaringType!=typeof(object)){
            throw new NotSupportedException($"Record data cannot contain runtime-owned type '{type}'.");
        }
        var hierarchy=new Stack<Type>();
        for(var current=type;current is not null&&current!=typeof(object);current=current.BaseType){hierarchy.Push(current);}
        var fields=hierarchy.SelectMany(static current=>current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
            .OrderBy(static field=>field.MetadataToken)).ToArray();
        if(fields.Any(static field=>field.FieldType.IsPointer||field.FieldType.IsFunctionPointer||field.FieldType.IsByRefLike)){
            throw new NotSupportedException($"Record data contains a pointer or byref-like field in '{type}'.");
        }
        var inline=type.GetCustomAttribute<InlineArrayAttribute>();
        if(inline is not null){
            if(fields.Length!=1||inline.Length<=0){throw new NotSupportedException($"Invalid inline record array '{type}'.");}
            return typeof(RecordTypePolicy).GetMethod(nameof(CreateInlineArray),BindingFlags.Static|BindingFlags.NonPublic)!
                .MakeGenericMethod(type,fields[0].FieldType).CreateDelegate<Func<RecordTypePolicy>>()();
        }
        if(fields.Any(static field=>field.IsDefined(typeof(FixedBufferAttribute),false))){
            throw new NotSupportedException($"Fixed buffers require a dedicated record value adapter in '{type}'.");
        }
        var bytes=fields.Aggregate(32L,static (sum,field)=>checked(sum+Math.Max(8,SizeOf(field.FieldType))));
        bytes=Math.Max(bytes,checked(32+hierarchy.Sum(static current=>(long)(current.StructLayoutAttribute?.Size??0))));
        if(type.IsValueType){bytes=Math.Max(bytes,32L+SizeOf(type));}
        foreach(var group in fields.Where(static field=>field.DeclaringType!.IsExplicitLayout).GroupBy(static field=>field.DeclaringType)){
            long end=0;
            foreach(var field in group.OrderBy(static field=>field.GetCustomAttribute<FieldOffsetAttribute>()!.Value)){
                var offset=field.GetCustomAttribute<FieldOffsetAttribute>()!.Value;
                if(offset<end){throw new NotSupportedException($"Overlapping record fields require a dedicated value adapter in '{type}'.");}
                end=checked((long)offset+SizeOf(field.FieldType));
            }
            bytes=Math.Max(bytes,32+end);
        }
        return new(){Fields=fields,ObjectBytes=bytes,TypeHash=RecordHash.Text(PersistedTypeIdentity.Name(type)),EstimatedBytes=_=>bytes,
            NormalizeFields=BuildNormalizer(type,fields),EqualFields=BuildComparer(type,fields),HashFields=BuildHasher(type,fields)};
    }

    private static RecordTypePolicy Factory(Type type,string method)=>typeof(RecordTypePolicy)
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(type)
        .CreateDelegate<Func<RecordTypePolicy>>()();

    private static RecordTypePolicy CreateScalar<T>()=>new(){
        IsScalar=true,TypeHash=RecordHash.Text(PersistedTypeIdentity.Name(typeof(T))),ObjectBytes=32L+Unsafe.SizeOf<T>(),EstimatedBytes=static _=>32L+Unsafe.SizeOf<T>(),
        NormalizeScalar=static value=>RecordScalarValue<T>.Normalize((T)value)!,
        EqualScalars=static (first,second)=>RecordScalarValue<T>.Equal((T)first,(T)second),
        HashScalar=static value=>RecordScalarValue<T>.Hash((T)value)
    };

    private static int SizeOf(Type type)=>typeof(RecordTypePolicy).GetMethod(nameof(SizeOfValue),BindingFlags.Static|BindingFlags.NonPublic)!
        .MakeGenericMethod(type).CreateDelegate<Func<int>>()();
    private static int SizeOfValue<T>()=>Unsafe.SizeOf<T>();

    private static RecordTypePolicy CreateInlineArray<T,TElement>() where T:struct{
        var length=typeof(T).GetCustomAttribute<InlineArrayAttribute>()!.Length;
        if(Unsafe.SizeOf<T>()!=checked(Unsafe.SizeOf<TElement>()*length)){throw new NotSupportedException($"Invalid inline record array layout '{typeof(T)}'.");}
        return new(){InlineElement=typeof(TElement),InlineLength=length,ObjectBytes=32L+Unsafe.SizeOf<T>(),
            TypeHash=RecordHash.Text(PersistedTypeIdentity.Name(typeof(T))),EstimatedBytes=static _=>32L+Unsafe.SizeOf<T>(),
            NormalizeFields=(object value,RecordValueContext context)=>{
                ref var element=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(value));
                for(var index=0;index<length;index++){Unsafe.Add(ref element,index)=context.Normalize(Unsafe.Add(ref element,index));}
            },
            EqualFields=(object first,object second,RecordValueContext context)=>{
                ref var left=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(first));
                ref var right=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(second));
                for(var index=0;index<length;index++){if(!context.Equal(Unsafe.Add(ref left,index),Unsafe.Add(ref right,index))){return false;}}
                return true;
            },
            HashFields=(object value,RecordValueContext context)=>{
                ref var element=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(value));var hash=RecordHash.Seed;
                for(var index=0;index<length;index++){hash=RecordHash.Add(hash,context.Hash(Unsafe.Add(ref element,index)));}
                return hash;
            }
        };
    }

    private static Func<object,object> CreateCloner(){
        var method=new DynamicMethod("CloneRecordNode",typeof(object),[typeof(object)],typeof(RecordTypePolicy).Module,true);
        var il=method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call,typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)!);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<object,object>>();
    }

    private static MethodInfo ContextMethod(string name,Type type)=>typeof(RecordValueContext)
        .GetMethods(BindingFlags.Instance|BindingFlags.Public).Single(method=>method.Name==name&&method.IsGenericMethodDefinition).MakeGenericMethod(type);

    private static void LoadFieldOwner(ILGenerator il,short argument,Type owner){
        il.Emit(OpCodes.Ldarg,argument);
        il.Emit(owner.IsValueType?OpCodes.Unbox:OpCodes.Castclass,owner);
    }

    private static Action<object,RecordValueContext> BuildNormalizer(Type type,FieldInfo[] fields){
        var method=new DynamicMethod("NormalizeRecordFields",typeof(void),[typeof(object),typeof(RecordValueContext)],type,true);
        var il=method.GetILGenerator();
        foreach(var field in fields){
            LoadFieldOwner(il,0,field.DeclaringType!);
            il.Emit(OpCodes.Ldarg_1);
            LoadFieldOwner(il,0,field.DeclaringType!);
            il.Emit(OpCodes.Ldfld,field);
            il.Emit(OpCodes.Callvirt,ContextMethod(nameof(RecordValueContext.Normalize),field.FieldType));
            il.Emit(OpCodes.Stfld,field);
        }
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Action<object,RecordValueContext>>();
    }

    private static Func<object,object,RecordValueContext,bool> BuildComparer(Type type,FieldInfo[] fields){
        var method=new DynamicMethod("CompareRecordFields",typeof(bool),[typeof(object),typeof(object),typeof(RecordValueContext)],type,true);
        var il=method.GetILGenerator();
        var different=il.DefineLabel();
        foreach(var field in fields){
            il.Emit(OpCodes.Ldarg_2);
            LoadFieldOwner(il,0,field.DeclaringType!);
            il.Emit(OpCodes.Ldfld,field);
            LoadFieldOwner(il,1,field.DeclaringType!);
            il.Emit(OpCodes.Ldfld,field);
            il.Emit(OpCodes.Callvirt,ContextMethod(nameof(RecordValueContext.Equal),field.FieldType));
            il.Emit(OpCodes.Brfalse,different);
        }
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(different);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<object,object,RecordValueContext,bool>>();
    }

    private static Func<object,RecordValueContext,int> BuildHasher(Type type,FieldInfo[] fields){
        var method=new DynamicMethod("HashRecordFields",typeof(int),[typeof(object),typeof(RecordValueContext)],type,true);
        var il=method.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4,RecordHash.Seed);
        foreach(var field in fields){
            il.Emit(OpCodes.Ldarg_1);
            LoadFieldOwner(il,0,field.DeclaringType!);
            il.Emit(OpCodes.Ldfld,field);
            il.Emit(OpCodes.Callvirt,ContextMethod(nameof(RecordValueContext.Hash),field.FieldType));
            il.Emit(OpCodes.Call,typeof(RecordHash).GetMethod(nameof(RecordHash.Add),BindingFlags.Static|BindingFlags.NonPublic)!);
        }
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<object,RecordValueContext,int>>();
    }

    private static RecordTypePolicy CreateArray<T>()=>new(){
        TypeHash=RecordHash.Text(PersistedTypeIdentity.Name(typeof(T[]))),
        EstimatedBytes=static value=>checked(64L+((Array)value).Rank*16L+((Array)value).LongLength*Unsafe.SizeOf<T>()),
        NormalizeFields=NormalizeArray<T>,EqualFields=EqualArrays<T>,HashFields=HashArray<T>
    };

    private static void NormalizeArray<T>(object value,RecordValueContext context){
        if(value is T[] vector){
            for(var index=0;index<vector.Length;index++){vector[index]=context.Normalize(vector[index]);}
            return;
        }
        var array=(Array)value;
        var indices=global::System.Linq.Enumerable.Range(0,array.Rank).Select(array.GetLowerBound).ToArray();
        for(long index=0;index<array.LongLength;index++){
            array.SetValue(context.Normalize((T)array.GetValue(indices)!),indices);
            for(var dimension=array.Rank-1;dimension>=0;dimension--){
                if(indices[dimension]<array.GetUpperBound(dimension)){indices[dimension]++;break;}
                indices[dimension]=array.GetLowerBound(dimension);
            }
        }
    }

    private static bool EqualArrays<T>(object first,object second,RecordValueContext context){
        var left=(Array)first;
        var right=(Array)second;
        if(left.Rank!=right.Rank){return false;}
        for(var dimension=0;dimension<left.Rank;dimension++){
            if(left.GetLength(dimension)!=right.GetLength(dimension)||left.GetLowerBound(dimension)!=right.GetLowerBound(dimension)){return false;}
        }
        if(first is T[] leftVector&&second is T[] rightVector){
            for(var index=0;index<leftVector.Length;index++){if(!context.Equal(leftVector[index],rightVector[index])){return false;}}
            return true;
        }
        var other=right.GetEnumerator();
        foreach(T item in left){other.MoveNext();if(!context.Equal(item,(T)other.Current!)){return false;}}
        return true;
    }

    private static int HashArray<T>(object value,RecordValueContext context){
        var array=(Array)value;
        var hash=RecordHash.Add(RecordHash.Seed,array.Rank);
        for(var dimension=0;dimension<array.Rank;dimension++){
            hash=RecordHash.Add(hash,array.GetLength(dimension));
            hash=RecordHash.Add(hash,array.GetLowerBound(dimension));
        }
        if(value is T[] vector){foreach(var item in vector){hash=RecordHash.Add(hash,context.Hash(item));}}
        else{foreach(T item in array){hash=RecordHash.Add(hash,context.Hash(item));}}
        return hash;
    }
}
