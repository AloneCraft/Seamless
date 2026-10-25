using System.Diagnostics;
using System.Reflection;

namespace Seamless.TestKit;

public static class TestPath {
    /// <summary>ストレージテストのランナーが指定した保存先。未指定時は従来の作業フォルダ。</summary>
    public static string Root{
        get{
            var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT");
            if(string.IsNullOrWhiteSpace(root)){return Environment.CurrentDirectory;}
            if(!Path.IsPathFullyQualified(root)){throw new InvalidOperationException("SEAMLESS_TEST_DATA_ROOT must be an absolute path.");}
            return Path.GetFullPath(root);
        }
    }
    public static string Directory(object owner,params string[] segments) =>
        Directory(GetRootType(owner.GetType()),segments);

    public static string Directory<TTest>(params string[] segments) =>
        Directory(GetRootType(typeof(TTest)),segments);

    public static string Directory(string className,string methodName,params string[] segments) {
        var path = Path.Combine(Root,className,methodName);
        foreach(var segment in segments) {
            if(string.IsNullOrWhiteSpace(segment)) continue;
            path=Path.Combine(path,segment);
        }

        if(global::System.IO.Directory.Exists(path)) {
            try {
                global::System.IO.Directory.Delete(path,recursive: true);
            } catch(IOException) {
            } catch(UnauthorizedAccessException) {
            }
        }

        global::System.IO.Directory.CreateDirectory(path);
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

