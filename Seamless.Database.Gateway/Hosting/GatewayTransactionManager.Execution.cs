using Seamless.Execution;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    internal ExecutionOptions QueryExecutionOptions(CancellationToken cancellationToken,DateTime? statementTimestamp=null)=>new(){ParentBudget=this.budget,TemporaryDirectory=Path.Combine(this.directory,"query-spool"),CancellationToken=cancellationToken,StatementTimestamp=statementTimestamp};
    internal ExecutionOptions LinkExecutionOptions(ExecutionOptions options){
        if(options.ParentBudget is null){return options with{ParentBudget=this.budget,TemporaryDirectory=Path.Combine(this.directory,"http-spool")};}
        for(var parent=options.ParentBudget;parent is not null;parent=parent.Parent){if(ReferenceEquals(parent,this.budget)){return options;}}
        throw new ArgumentException("The HTTP execution budget must descend from the owning Gateway budget.",nameof(options));
    }
}
