using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

public sealed partial class NativeQueryServer{
    private static void ValidateRecordDefinition(INativeQueryModule module,NativeQueryDefinition definition){
        if(module is INativeRecordQueryModule records){records.GetContract(definition.Id).ValidateDefinition(definition);}
    }
    private static IEnumerable<byte[]> ExecuteRows(INativeQueryModule module,NativeQueryDefinition definition,ReadOnlyMemory<byte> arguments,INativeQueryContext context){
        NativeRecordQueryContract? contract=null;
        if(module is INativeRecordQueryModule records){
            contract=records.GetContract(definition.Id);
            contract.ValidateDefinition(definition);
            try{
                contract.ValidateArguments(arguments.Span,context.Budget,context.CancellationToken);
            }catch(Exception ex){
                throw new NativeQueryException(ex.Message,isRecoverable:true);
            }
        }
        foreach(var row in module.Execute(definition.Id,arguments,context)){
            context.CancellationToken.ThrowIfCancellationRequested();contract?.ValidateResult(row,context.Budget,context.CancellationToken);yield return row;
        }
    }
}
