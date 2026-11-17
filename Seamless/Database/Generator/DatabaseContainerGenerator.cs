using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
namespace Seamless.Database.Generator;

[Generator]
public sealed class DatabaseContainerGenerator:IIncrementalGenerator{
    public void Initialize(IncrementalGeneratorInitializationContext context)=>new global::Shared.Database.Generation.DatabaseContainerGeneration().Initialize(context);
}
