using System.Reflection;

namespace Seamless.Database.Rdbms;

/// <summary>SQLビューの実行結果を生成済みの型付きレコードとして逐次公開する。</summary>
public static class GeneratedSqlViewReader{
    public static IEnumerable<T> Read<T>(Func<string,IEnumerable<object?[]>>? source,string name){
        if(source is null){throw new InvalidOperationException($"Bind the SQL view provider before reading '{name}'.");}
        global::System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        foreach(var row in source(name)){
            var constructor=Constructors<T>.Values.SingleOrDefault(c=>c.GetParameters().Length==row.Length)??throw new InvalidOperationException("The generated view record does not match its result schema.");
            var parameters=constructor.GetParameters();var values=new object?[row.Length];
            for(var i=0;i<row.Length;i++){values[i]=ConvertValue(row[i],parameters[i].ParameterType);}
            yield return (T)constructor.Invoke(values);
        }
    }
    private static object? ConvertValue(object? value,Type type){
        if(value is null||value is DBNull){return null;}type=Nullable.GetUnderlyingType(type)??type;
        if(type.IsInstanceOfType(value)){return value;}
        if(type.FullName=="Seamless.Database.SqlServer.Types.Bit"){return Activator.CreateInstance(type,Convert.ToInt32(value));}
        if(type==typeof(Guid)){return Guid.Parse(value.ToString()!);}
        if(type==typeof(DateOnly)&&value is DateTime date){return DateOnly.FromDateTime(date);}
        if(type==typeof(TimeOnly)&&value is TimeSpan time){return TimeOnly.FromTimeSpan(time);}
        return Convert.ChangeType(value,type,global::System.Globalization.CultureInfo.InvariantCulture);
    }
    private static class Constructors<T>{internal static readonly ConstructorInfo[] Values=typeof(T).GetConstructors(BindingFlags.Public|BindingFlags.Instance);}
}
