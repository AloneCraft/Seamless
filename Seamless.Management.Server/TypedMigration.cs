using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Seamless.Management.Server;

internal sealed class MigrationLoadContext(Assembly before,Assembly after):AssemblyLoadContext(isCollectible:true){
    protected override Assembly? Load(AssemblyName name){
        foreach(var assembly in new[]{before,after}){if(AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name)){return assembly;}}
        var shared=AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));
        if(shared is not null){return shared;}
        return AssemblyLoadContext.GetLoadContext(before)?.LoadFromAssemblyName(name);
    }
}
