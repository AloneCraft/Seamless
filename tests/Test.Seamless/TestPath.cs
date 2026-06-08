using System.Diagnostics;
using System.Reflection;

namespace Test.Seamless;

internal static class TestPath {
    public static string Directory(object owner,params string[] segments) =>
        Directory(GetRootType(owner.GetType()),segments);

    public static string Directory<TTest>(params string[] segments) =>
        Directory(GetRootType(typeof(TTest)),segments);

    public static string Directory(string className,string methodName,params string[] segments) {
        var path = Path.Combine(Environment.CurrentDirectory,className,methodName);
        foreach(var segment in segments) {
            if(string.IsNullOrWhiteSpace(segment)) continue;
            path=Path.Combine(path,segment);
        }

        if(System.IO.Directory.Exists(path)) {
            try {
                System.IO.Directory.Delete(path,recursive: true);
            }
            catch(IOException) {
            }
            catch(UnauthorizedAccessException) {
            }
        }

        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    private static string Directory(Type ownerType,params string[] segments) =>
        Directory(ownerType.Name,ResolveTestMethodName(ownerType),segments);

    private static string ResolveTestMethodName(Type ownerType) {
        string? fallback = null;
        var frames = new StackTrace(skipFrames: 1,fNeedFileInfo: false).GetFrames()?? [];

        foreach(var frame in frames) {
            if(frame.GetMethod() is not MethodInfo method) continue;

            var declaringType = method.DeclaringType;
            if(declaringType is null) continue;
            if(declaringType==typeof(TestPath)) continue;
            if(!IsRelevantType(ownerType,declaringType)) continue;

            fallback??=method.Name;
            if(IsTestMethod(method)) return method.Name;
        }

        return fallback??"UnknownTest";
    }

    private static bool IsRelevantType(Type ownerType,Type declaringType) {
        var rootDeclaringType = GetRootType(declaringType);
        return rootDeclaringType.IsAssignableFrom(ownerType)
            ||ownerType.IsAssignableFrom(rootDeclaringType);
    }

    private static bool IsTestMethod(MethodInfo method) {
        foreach(var attribute in method.GetCustomAttributes(inherit: true)) {
            var fullName = attribute.GetType().FullName;
            if(fullName=="Xunit.FactAttribute"||fullName=="Xunit.TheoryAttribute") {
                return true;
            }
        }

        return false;
    }

    private static Type GetRootType(Type type) {
        while(type.DeclaringType is not null) {
            type=type.DeclaringType;
        }

        return type;
    }
}
