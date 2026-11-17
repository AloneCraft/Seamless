using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MemoryPack;

namespace Seamless.Database.Client;

/// <summary>型名、列順、型、formatter 属性と unmanaged layout から Native の wire 契約を識別する。</summary>
public static class NativeWireContract{
    public static string For<T>()=>Cache<T>.Hash;
    private static class Cache<T>{internal static readonly string Hash=For(typeof(T));}
    public static string For(Type type){
        var text=new StringBuilder("seamless-native-contract-v1|MemoryPack:").Append(typeof(MemoryPackSerializer).Assembly.GetName().Version).Append('|');
        Describe(type,text,[],0);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
    private static void Describe(Type type,StringBuilder text,HashSet<Type> path,int depth){
        if(depth>64||text.Length>4*1024*1024){throw new NotSupportedException("Native contract graph exceeds the supported bounds.");}
        text.Append(Name(type));if(type.IsPrimitive||type==typeof(string)||type==typeof(decimal)||type==typeof(DateTime)||type==typeof(DateTimeOffset)||type==typeof(TimeSpan)||type==typeof(DateOnly)||type==typeof(TimeOnly)||type==typeof(Guid)){return;}
        if(!path.Add(type)){text.Append("(recursive)");return;}
        try{
            if(type.IsArray){text.Append("[rank=").Append(type.GetArrayRank()).Append(':');Describe(type.GetElementType()!,text,path,depth+1);text.Append(']');return;}
            if(type.IsEnum){text.Append("<enum:");Describe(Enum.GetUnderlyingType(type),text,path,depth+1);foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Static).OrderBy(static field=>field.MetadataToken)){text.Append('|').Append(field.Name).Append('=').Append(Convert.ToString(field.GetRawConstantValue(),CultureInfo.InvariantCulture));}text.Append('>');return;}
            if(type.IsGenericType){text.Append('<');foreach(var argument in type.GetGenericArguments()){Describe(argument,text,path,depth+1);text.Append(';');}text.Append('>');if(type.Namespace?.StartsWith("System",StringComparison.Ordinal)==true){return;}}
            Attributes(type.CustomAttributes,text,path,depth);
            if(type.IsValueType&&!ContainsReferences(type,[])){
                var layout=type.StructLayoutAttribute;text.Append("{layout:").Append(layout?.Value).Append(',').Append(layout?.Pack).Append(',').Append(layout?.Size).Append(",pointer:").Append(IntPtr.Size);
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(static field=>field.MetadataToken)){text.Append('|').Append(field.Name).Append(':');Describe(field.FieldType,text,path,depth+1);Attributes(field.CustomAttributes,text,path,depth);}text.Append('}');return;
            }
            var hierarchy=new Stack<Type>();for(var current=type;current is not null&&current!=typeof(object);current=current.BaseType){hierarchy.Push(current);}
            while(hierarchy.TryPop(out var declaring)){
                text.Append('{').Append(Name(declaring));var members=declaring.GetMembers(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly).Where(Serializable).ToArray();
                var explicitOrder=members.Any(static member=>Order(member) is not null);
                if(explicitOrder&&members.Any(static member=>Order(member) is null)||!explicitOrder&&members.Any(static member=>member is FieldInfo)&&members.Any(static member=>member is PropertyInfo)){throw new NotSupportedException("Mixed Native field/property contracts require explicit MemoryPackOrder on all members: "+Name(declaring));}
                foreach(var member in members.OrderBy(member=>explicitOrder?Order(member)!.Value:member.MetadataToken)){
                    text.Append('|').Append(member.Name).Append(':');var nullability=new NullabilityInfoContext();Nullable(member is FieldInfo nullableField?nullability.Create(nullableField):nullability.Create((PropertyInfo)member),text);Describe(member is FieldInfo field?field.FieldType:((PropertyInfo)member).PropertyType,text,path,depth+1);Attributes(member.CustomAttributes,text,path,depth);
                }
                text.Append('}');
            }
        }finally{path.Remove(type);}
    }
    private static string Name(Type type)=>type.IsArray?"array-rank-"+type.GetArrayRank().ToString(CultureInfo.InvariantCulture):type.IsGenericType?type.GetGenericTypeDefinition().FullName??type.Name:type.FullName??type.Name;
    private static void Nullable(NullabilityInfo info,StringBuilder text){text.Append('~').Append(info.ReadState).Append('/').Append(info.WriteState);if(info.ElementType is{} element){Nullable(element,text);}foreach(var argument in info.GenericTypeArguments){Nullable(argument,text);}}
    private static bool Serializable(MemberInfo member){
        if(member.CustomAttributes.Any(static attribute=>attribute.AttributeType.FullName=="MemoryPack.MemoryPackIgnoreAttribute")){return false;}
        var included=member.CustomAttributes.Any(static attribute=>attribute.AttributeType.FullName=="MemoryPack.MemoryPackIncludeAttribute");
        return member is FieldInfo field&&(field.IsPublic||included)||member is PropertyInfo property&&property.GetIndexParameters().Length==0&&(property.GetMethod?.IsPublic==true||included);
    }
    private static int? Order(MemberInfo member)=>member.CustomAttributes.FirstOrDefault(static attribute=>attribute.AttributeType.FullName=="MemoryPack.MemoryPackOrderAttribute") is{} order?(int)order.ConstructorArguments[0].Value!:null;
    private static bool ContainsReferences(Type type,HashSet<Type> path){
        if(!type.IsValueType){return true;}if(type.IsPrimitive||type.IsEnum||!path.Add(type)){return false;}
        try{return type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Any(field=>ContainsReferences(field.FieldType,path));}finally{path.Remove(type);}
    }
    private static void Attributes(IEnumerable<CustomAttributeData> attributes,StringBuilder text,HashSet<Type> path,int depth){
        foreach(var attribute in attributes.Where(static attribute=>attribute.AttributeType.Namespace=="MemoryPack").OrderBy(static attribute=>attribute.AttributeType.FullName,StringComparer.Ordinal)){
            text.Append('@').Append(Name(attribute.AttributeType));foreach(var argument in attribute.ConstructorArguments){Argument(argument,text,path,depth);}foreach(var argument in attribute.NamedArguments.OrderBy(static argument=>argument.MemberName,StringComparer.Ordinal)){text.Append(argument.MemberName).Append('=');Argument(argument.TypedValue,text,path,depth);}
        }
    }
    private static void Argument(CustomAttributeTypedArgument argument,StringBuilder text,HashSet<Type> path,int depth){
        text.Append('(');if(argument.Value is Type type){Describe(type,text,path,depth+1);}else if(argument.Value is IEnumerable<CustomAttributeTypedArgument> array){foreach(var item in array){Argument(item,text,path,depth+1);}}else{text.Append(Convert.ToString(argument.Value,CultureInfo.InvariantCulture));}text.Append(')');
    }
}
