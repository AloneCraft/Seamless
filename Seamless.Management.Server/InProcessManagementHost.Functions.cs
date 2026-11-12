namespace Seamless.Management.Server;

public sealed partial class InProcessManagementHost{
    public CatalogPage<FunctionSummary> Functions(CatalogRequest request)=>this.Invoke((service,user)=>service.Functions(user,request));
    public FunctionDetails GetFunctionDetails(FunctionTarget target)=>this.Invoke((service,user)=>service.GetFunctionDetails(user,target));
    public ObjectExecutionResult ExecuteFunction(FunctionExecutionRequest request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.Invoke((service,user)=>service.ExecuteFunctionAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken);
    }
}
