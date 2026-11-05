using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seamless.Management.Server;

/// <summary>生成型を共有JSONメタデータへ登録せず、安定した値の木へ投影してから送信する。</summary>
internal static class RuntimeValueJson{
    internal static JsonElement Serialize(object? value)=>JsonSerializer.SerializeToElement(Project(value,new HashSet<object>(ReferenceEqualityComparer.Instance),0),ManagementJson.Options);
    internal static object? Deserialize(JsonElement value,Type type,int depth=0){
        if(depth>64){throw new ManagementException("Validation","引数の入れ子が深すぎます。");}
        if(!type.IsCollectible){return value.Deserialize(type,ManagementJson.Options);}
        if(value.ValueKind==JsonValueKind.Null){if(type.IsValueType&&Nullable.GetUnderlyingType(type) is null){throw new ManagementException("Validation","nullにできない型です: "+type.Name);}return null;}
        if(Nullable.GetUnderlyingType(type) is{} nullable){return Deserialize(value,nullable,depth+1);}
        if(type.IsEnum){if(value.TryGetInt64(out var number)){return Enum.ToObject(type,number);}return Enum.ToObject(type,value.GetUInt64());}
        if(type.IsArray){if(type.GetArrayRank()!=1){throw new ManagementException("Unsupported","多次元配列の引数は対応範囲外です。");}var elements=value.EnumerateArray().ToArray();if(elements.Length>10000){throw new ManagementException("Limit","引数配列が大きすぎます。");}var element=type.GetElementType()!;var array=Array.CreateInstance(element,elements.Length);for(var index=0;index<elements.Length;index++){array.SetValue(Deserialize(elements[index],element,depth+1),index);}return array;}
        if(type.IsInterface&&type.IsGenericType){var generic=type.GetGenericTypeDefinition();if(generic==typeof(IEnumerable<>)||generic==typeof(IReadOnlyList<>)||generic==typeof(IList<>)||generic==typeof(ICollection<>)){type=typeof(List<>).MakeGenericType(type.GenericTypeArguments);}else if(generic==typeof(IDictionary<,>)||generic==typeof(IReadOnlyDictionary<,>)){type=typeof(Dictionary<,>).MakeGenericType(type.GenericTypeArguments);}}
        if(value.ValueKind==JsonValueKind.Array&&typeof(IList).IsAssignableFrom(type)){var list=(IList)Activator.CreateInstance(type)!;var element=type.GenericTypeArguments[0];foreach(var item in value.EnumerateArray()){if(list.Count>=10000){throw new ManagementException("Limit","引数配列が大きすぎます。");}list.Add(Deserialize(item,element,depth+1));}return list;}
        if(value.ValueKind==JsonValueKind.Object&&typeof(IDictionary).IsAssignableFrom(type)){var dictionary=(IDictionary)Activator.CreateInstance(type)!;var types=type.GenericTypeArguments;foreach(var item in value.EnumerateObject()){if(dictionary.Count>=10000){throw new ManagementException("Limit","引数辞書が大きすぎます。");}object key;if(types[0]==typeof(string)){key=item.Name;}else if(types[0]==typeof(Guid)){key=Guid.Parse(item.Name);}else if(types[0].IsEnum){key=Enum.Parse(types[0],item.Name);}else{key=Convert.ChangeType(item.Name,types[0],CultureInfo.InvariantCulture);}dictionary.Add(key,Deserialize(item.Value,types[1],depth+1));}return dictionary;}
        if(value.ValueKind!=JsonValueKind.Object){throw new ManagementException("Validation","オブジェクトの引数が必要です: "+type.Name);}
        var values=new Dictionary<string,JsonElement>(StringComparer.OrdinalIgnoreCase);foreach(var item in value.EnumerateObject()){if(!values.TryAdd(item.Name,item.Value)){throw new ManagementException("Validation","引数の列名が重複しています: "+item.Name);}}
        var properties=type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.GetIndexParameters().Length==0).ToArray();var fields=type.GetFields(BindingFlags.Public|BindingFlags.Instance);var constructors=type.GetConstructors();var constructor=constructors.FirstOrDefault(item=>item.GetCustomAttribute<JsonConstructorAttribute>() is not null)??constructors.OrderByDescending(item=>item.GetParameters().Length).FirstOrDefault();var parameters=constructor?.GetParameters()??[];var arguments=new object?[parameters.Length];
        for(var index=0;index<parameters.Length;index++){var parameter=parameters[index];MemberInfo? member=properties.FirstOrDefault(item=>string.Equals(item.Name,parameter.Name,StringComparison.OrdinalIgnoreCase));member??=fields.FirstOrDefault(item=>string.Equals(item.Name,parameter.Name,StringComparison.OrdinalIgnoreCase));var name=parameter.Name!;if(member is not null){name=Name(member);}if(values.Remove(name,out var item)){arguments[index]=Deserialize(item,parameter.ParameterType,depth+1);}else if(parameter.HasDefaultValue){arguments[index]=parameter.DefaultValue;}else{throw new ManagementException("Validation","値が必要な列です: "+name);}}
        object result;if(constructor is not null){result=constructor.Invoke(arguments);}else{result=Activator.CreateInstance(type)??throw new ManagementException("Validation","引数を構築できません。");}
        foreach(var item in values){var property=properties.FirstOrDefault(candidate=>string.Equals(Name(candidate),item.Key,StringComparison.OrdinalIgnoreCase));if(property?.SetMethod?.IsPublic==true){property.SetValue(result,Deserialize(item.Value,property.PropertyType,depth+1));continue;}var field=fields.FirstOrDefault(candidate=>string.Equals(Name(candidate),item.Key,StringComparison.OrdinalIgnoreCase));if(field is not null&&!field.IsInitOnly){field.SetValue(result,Deserialize(item.Value,field.FieldType,depth+1));continue;}throw new ManagementException("Validation","不明または書き込めない列です: "+item.Key);}
        return result;
    }
    private static object? Project(object? value,HashSet<object> path,int depth){
        if(depth>64){throw new ManagementException("ResultLimit","結果の入れ子が深すぎます。");}
        if(value is null||value is string or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Guid or byte[] or JsonElement){return value;}
        var type=value.GetType();if(type.IsEnum){return Convert.ChangeType(value,Enum.GetUnderlyingType(type),CultureInfo.InvariantCulture);}
        if(!type.IsValueType&&!path.Add(value)){throw new ManagementException("Validation","結果に循環参照があります。");}
        try{
            if(value is IDictionary dictionary){var result=new Dictionary<string,object?>();foreach(DictionaryEntry item in dictionary){var key=Convert.ToString(item.Key,CultureInfo.InvariantCulture)??throw new ManagementException("Validation","JSONのキーがありません。");result.Add(key,Project(item.Value,path,depth+1));}return result;}
            if(value is IEnumerable sequence){var result=new List<object?>();foreach(var item in sequence){if(result.Count>=10000){throw new ManagementException("ResultLimit","1セルの配列は10000要素以下にしてください。");}result.Add(Project(item,path,depth+1));}return result;}
            var record=new Dictionary<string,object?>();
            foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance)){if(property.GetMethod is null||property.GetIndexParameters().Length>0||property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition==JsonIgnoreCondition.Always){continue;}record.Add(Name(property),Project(property.GetValue(value),path,depth+1));}
            foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance)){if(field.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition==JsonIgnoreCondition.Always){continue;}record.Add(Name(field),Project(field.GetValue(value),path,depth+1));}
            return record;
        }finally{if(!type.IsValueType){path.Remove(value);}}
    }
    private static string Name(MemberInfo member)=>member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name??ManagementJson.Options.PropertyNamingPolicy?.ConvertName(member.Name)??member.Name;
}
