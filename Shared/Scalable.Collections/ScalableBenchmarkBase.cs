using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Scalable.Collections;

public abstract class ScalableBenchmarkBase{
    public string CaptureBenchmarkMethodResult(string methodName,bool invokeImpl){
        return this.CaptureBenchmarkMethodResult(methodName,invokeImpl,[]);
    }

    public string CaptureBenchmarkMethodResult(string methodName,bool invokeImpl,object?[] arguments){
        if(string.IsNullOrWhiteSpace(methodName)){
            throw new ArgumentException("Method name is required.",nameof(methodName));
        }

        var method=invokeImpl
            ? FindInstanceMethodInHierarchy(this.GetType(),methodName+"_Impl",nonPublic:true)
            : FindBenchmarkMethod(this.GetType(),methodName);
        object? returnValue;
        try{
            returnValue=method.Invoke(this,arguments);
        }catch(TargetInvocationException ex) when(ex.InnerException is not null){
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw new InvalidOperationException("Unreachable.");
        }

        return CreateSingleDigest(Normalize(returnValue));
    }

    private static string CreateSingleDigest(string normalized){
        var bytes=Encoding.UTF8.GetBytes(normalized);
        var lengthBytes=new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(lengthBytes,bytes.Length);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(lengthBytes);
        hash.AppendData(bytes);
        var hashBytes=hash.GetHashAndReset();
        var hashText=Convert.ToHexString(hashBytes);
        return "count=1;sha256="+hashText+";first=["+normalized+"];last=["+normalized+"]";
    }

    private static string Normalize(object? value){
        if(value is null){
            return "<null>";
        }
        if(value is string text){
            return text;
        }
        if(TryGetKey(value,out var key)&&value is System.Collections.IEnumerable keyedEnumerable){
            return "{"+Normalize(key)+":"+NormalizeEnumerable(keyedEnumerable)+"}";
        }
        if(value is System.Collections.IDictionary dictionary){
            return NormalizeDictionary(dictionary);
        }
        if(value is System.Collections.IEnumerable enumerable){
            return NormalizeEnumerable(enumerable);
        }
        if(value is IFormattable formattable){
            return formattable.ToString(null,System.Globalization.CultureInfo.InvariantCulture);
        }
        return value.ToString()??string.Empty;
    }

    private static string NormalizeEnumerable(System.Collections.IEnumerable enumerable){
        var values=new List<string>();
        foreach(var item in enumerable){
            values.Add(Normalize(item));
        }
        return "["+string.Join(",",values)+"]";
    }

    private static string NormalizeDictionary(System.Collections.IDictionary dictionary){
        var values=new List<string>();
        foreach(System.Collections.DictionaryEntry entry in dictionary){
            values.Add(Normalize(entry.Key)+"="+Normalize(entry.Value));
        }
        values.Sort(StringComparer.Ordinal);
        return "["+string.Join(",",values)+"]";
    }

    private static bool TryGetKey(object instance,out object? key){
        var property=instance.GetType().GetProperty("Key");
        if(property is null){
            key=null;
            return false;
        }

        key=property.GetValue(instance);
        return true;
    }

    private static MethodInfo FindBenchmarkMethod(Type type,string methodName){
        var method=FindInstanceMethodInHierarchy(type,methodName,nonPublic:false);
        if(!HasAttribute(method,"BenchmarkAttribute")){
            throw new MissingMethodException(type.FullName,methodName+" [Benchmark]");
        }
        return method;
    }

    private static MethodInfo FindInstanceMethodInHierarchy(Type type,string methodName,bool nonPublic){
        var flags=BindingFlags.Instance
            |BindingFlags.DeclaredOnly
            |(nonPublic?BindingFlags.NonPublic:BindingFlags.Public);
        for(var current=type;current is not null;current=current.BaseType!){
            var method=current.GetMethod(methodName,flags);
            if(method is null){
                continue;
            }
            return method;
        }

        throw new MissingMethodException(type.FullName,methodName);
    }

    private static bool HasAttribute(MemberInfo member,string attributeName){
        foreach(var attribute in member.CustomAttributes){
            if(attribute.AttributeType.Name==attributeName){
                return true;
            }
        }
        return false;
    }
}
