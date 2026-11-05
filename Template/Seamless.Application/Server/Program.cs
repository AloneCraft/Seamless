using System.Reflection;
using System.Runtime.Loader;

namespace Seamless.Application.Server;

// ホストは利用者DBの具体的な型を参照せず、モジュールの入口だけを呼ぶ。
internal static class Program{
    public static async Task<int> Main(string[] args){
        var path=Path.Combine(AppContext.BaseDirectory,"Seamless.Application.ServerSchema.dll");
        var context=new ModuleContext(path);
        try{
            var assembly=context.LoadFromAssemblyPath(path);
            var method=assembly.GetType("Seamless.Application.ServerSchema.TemplateEntryPoint",throwOnError:true)!.GetMethod("RunAsync",BindingFlags.Public|BindingFlags.Static)!;
            return await(Task<int>)method.Invoke(null,[args])!;
        }catch(TargetInvocationException error)when(error.InnerException is not null){
            Console.Error.WriteLine(error.InnerException);
            return 1;
        }finally{context.Unload();}
    }

    private sealed class ModuleContext(string path):AssemblyLoadContext(isCollectible:true){
        private readonly string directory=Path.GetDirectoryName(path)!;
        protected override Assembly? Load(AssemblyName name){
            var candidate=Path.Combine(this.directory,name.Name+".dll");
            if(File.Exists(candidate)){return this.LoadFromAssemblyPath(candidate);}
            return null;
        }
    }
}
