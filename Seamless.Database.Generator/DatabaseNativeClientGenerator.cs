namespace Seamless.Database.Generator;

[Generator]
public sealed class DatabaseNativeClientGenerator:IIncrementalGenerator{
    public void Initialize(IncrementalGeneratorInitializationContext context)=>new global::Shared.Database.Generation.DatabaseNativeClientGeneration().Initialize(context);
}
