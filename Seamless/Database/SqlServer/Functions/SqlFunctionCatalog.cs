using System.Collections.Frozen;
using System.Reflection;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// <see cref="SqlFunctionAttribute"/> の付いたメソッドを、SQL 名から引く一覧。
/// 型の読み込み時に1回だけ集める。翻訳器は引数の型からこの中の1つを選ぶ。
/// </summary>
public static class SqlFunctionCatalog{
    private static readonly FrozenDictionary<string,MethodInfo[]> functions=Collect();
    private static FrozenDictionary<string,MethodInfo[]> Collect(){
        var found=new Dictionary<string,List<MethodInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach(var type in typeof(SqlFunctionCatalog).Assembly.GetTypes()){
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Static)){
                foreach(var attribute in method.GetCustomAttributes<SqlFunctionAttribute>()){
                    if(!found.TryGetValue(attribute.Name,out var list)){
                        list=[];
                        found.Add(attribute.Name,list);
                    }
                    list.Add(method);
                }
            }
        }
        return found.ToFrozenDictionary(static pair=>pair.Key,static pair=>pair.Value.ToArray(),StringComparer.OrdinalIgnoreCase);
    }
    /// <summary>実装のある SQL 名の一覧。</summary>
    public static IEnumerable<string> Names=>functions.Keys;
    /// <summary>SQL 名に付いたメソッド。無ければ空。</summary>
    public static IReadOnlyList<MethodInfo> Find(string name){
        if(functions.TryGetValue(name,out var methods)){
            return methods;
        }
        return [];
    }
}
