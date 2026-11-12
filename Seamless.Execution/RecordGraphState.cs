using System.Reflection;
using System.Runtime.Loader;

namespace Seamless.Execution;

internal sealed class RecordGraphState:IDisposable{
    private readonly ResourceReservation memory;
    private ResourceReservation? output;
    private readonly CancellationToken cancellationToken;
    private readonly AssemblyLoadContext loadContext;
    internal Dictionary<object,int> WrittenReferences{get;}
    internal List<object> ReadReferences{get;}
    internal Dictionary<Type,int> WrittenTypes{get;}
    internal Dictionary<int,Type> ReadTypes{get;}
    internal int NextTypeId{get;set;}=1;
    internal long OutputBytes{get;private set;}

    internal RecordGraphState(Type root,ResourceBudget? budget,CancellationToken cancellationToken,bool ownOutput=false){
        cancellationToken.ThrowIfCancellationRequested();
        this.cancellationToken=cancellationToken;
        this.memory=(budget??RecordValueContext.DefaultBudget).ReserveMemory(1024);
        try{
            if(ownOutput){this.output=(budget??RecordValueContext.DefaultBudget).ReserveMemory(0);}
            this.loadContext=AssemblyLoadContext.GetLoadContext(root.Assembly)??AssemblyLoadContext.Default;
            this.WrittenReferences=new(ReferenceEqualityComparer.Instance);this.ReadReferences=[];
            this.WrittenTypes=[];this.ReadTypes=[];
        }catch{this.output?.Dispose();this.memory.Dispose();throw;}
    }

    internal void Check()=>this.cancellationToken.ThrowIfCancellationRequested();
    internal void Reserve(long bytes){this.Check();this.memory.Resize(checked(this.memory.Bytes+bytes));}
    internal void CountOutput(long bytes)=>this.OutputBytes=checked(this.OutputBytes+bytes);
    internal void ReserveOutput(long bytes){
        this.Check();this.CountOutput(bytes);
        if(this.output is null){this.Reserve(bytes);}else{this.output.Resize(this.OutputBytes);}
    }
    internal OwnedValue<T> TransferOutput<T>(T value){
        if(this.output is null){throw new InvalidOperationException("This operation does not own its output.");}
        this.ReserveOutput(checked(OwnedValue<T>.HeaderBytes+(long)global::System.Runtime.CompilerServices.Unsafe.SizeOf<T>()));
        var owner=new OwnedValue<T>(value,this.output);this.output=null;return owner;
    }

    internal Module ResolveModule(Guid id){
        // 型名による assembly load は行わず、root の文脈と共有 runtime から同じ版を探す。
        foreach(var assembly in this.loadContext.Assemblies){foreach(var module in assembly.Modules){if(module.ModuleVersionId==id){return module;}}}
        if(this.loadContext!=AssemblyLoadContext.Default){
            foreach(var assembly in AssemblyLoadContext.Default.Assemblies){foreach(var module in assembly.Modules){if(module.ModuleVersionId==id){return module;}}}
        }
        throw new InvalidDataException("The record graph references an unavailable module version.");
    }

    public void Dispose(){try{this.output?.Dispose();}finally{this.memory.Dispose();}}
}
