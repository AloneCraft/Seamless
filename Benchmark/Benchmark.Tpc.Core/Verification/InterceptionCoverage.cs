using System.Reflection;
namespace Benchmark.Tpc.Core;
/// <summary>
/// FLinq の組み合わせで、取引の問い合わせ(指定した名前空間の、名前が *Queries の型の public メソッド)がすべて FLinq のインターセプタに置き換わっているかを調べる。
/// FLinq は置き換えられないメソッドを診断なしに System.Linq のまま動かすので、それを見逃さないための検査。
/// </summary>
public static class InterceptionCoverage{
    /// <summary>置き換えの無い問い合わせの一覧(「型名.メソッド名」)。[LinqIntercept(false)] で止めたものは <paramref name="allowed"/> に入れて返す。</summary>
    public static IReadOnlyList<string> FindMissing(Assembly variant,string transactionsNamespace,out IReadOnlyList<string> allowed){
        var intercepted=new HashSet<(Type,string)>();
        foreach(var type in SafeTypes(variant)){
            if(type.Namespace is null||!(type.Namespace=="Intercept"||type.Namespace.StartsWith("Intercept.",StringComparison.Ordinal))){
                continue;
            }
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){
                var parameters=method.GetParameters();
                if(parameters.Length==0||!method.Name.StartsWith("OptimizedLinq_",StringComparison.Ordinal)){
                    continue;
                }
                var target=parameters[0].ParameterType;
                var rest=method.Name["OptimizedLinq_".Length..];
                var end=rest.IndexOf('_');
                if(end<0){
                    continue;
                }
                intercepted.Add((target,rest[..end]));
            }
        }
        var missing=new List<string>();
        var stopped=new List<string>();
        foreach(var type in SafeTypes(variant)){
            if(type.Namespace!=transactionsNamespace||!type.Name.EndsWith("Queries",StringComparison.Ordinal)){
                continue;
            }
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)){
                var name=type.Name+"."+method.Name;
                if(IsStopped(method)){
                    stopped.Add(name);
                    continue;
                }
                if(!intercepted.Contains((type,method.Name))){
                    missing.Add(name);
                }
            }
        }
        missing.Sort(StringComparer.Ordinal);
        stopped.Sort(StringComparer.Ordinal);
        allowed=stopped;
        return missing;
    }
    /// <summary>アセンブリにある FLinq のインターセプタの数(標準 LINQ の組み合わせでは 0 のはず)。</summary>
    public static int CountInterceptors(Assembly assembly){
        var count=0;
        foreach(var type in SafeTypes(assembly)){
            if(type.Namespace is null||!(type.Namespace=="Intercept"||type.Namespace.StartsWith("Intercept.",StringComparison.Ordinal))){
                continue;
            }
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)){
                if(method.Name.StartsWith("OptimizedLinq_",StringComparison.Ordinal)){
                    count++;
                }
            }
        }
        return count;
    }
    /// <summary>[LinqIntercept(false)] が付いているか(FLinq の属性は利用側へ internal で生成されるので名前で見る)。</summary>
    private static bool IsStopped(MethodInfo method){
        foreach(var attribute in method.GetCustomAttributesData()){
            if(attribute.AttributeType.Name!="LinqInterceptAttribute"){
                continue;
            }
            if(attribute.ConstructorArguments.Count>0&&attribute.ConstructorArguments[0].Value is false){
                return true;
            }
        }
        return false;
    }
    private static IEnumerable<Type> SafeTypes(Assembly assembly){
        try{
            return assembly.GetTypes();
        }catch(ReflectionTypeLoadException error){
            return error.Types.OfType<Type>();
        }
    }
}
