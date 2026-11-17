using Seamless.Database.Rdbms;
using Seamless.Execution;
using Seamless.Transactions;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>新規の Gateway 保存プロファイル。全世代を復旧してから接続用 registry を返す。</summary>
public sealed class GatewayDatabaseHost:IDisposable{
    private GatewayDatabaseHost(GatewayTransactionManager transactions,DatabaseRegistry databases,GatewayGenerationStore generations){this.Transactions=transactions;this.Databases=databases;this.Generations=generations;}
    public GatewayTransactionManager Transactions{get;}
    public DatabaseRegistry Databases{get;}
    public GatewayGenerationStore Generations{get;}
    public static async Task<GatewayDatabaseHost> OpenAsync(string directory,GeneratedAssemblyBuilder builder,DatabaseContainerOptions databaseOptions,IReadOnlyList<DatabaseCatalog>? initialCatalogs=null,WalOptions? walOptions=null,ResourceBudget? budget=null,Action<GatewayCommitPhase,int>? failureInjection=null,CancellationToken cancellationToken=default,Func<DatabaseCatalog,GeneratedDatabase,IDatabaseImportSource>? initialDataSource=null){
        if(!Path.IsPathFullyQualified(directory)){throw new ArgumentException("Gateway storage requires an absolute directory.",nameof(directory));}
        budget??=new(256L<<20,16L<<30);var transactions=new GatewayTransactionManager(Path.Combine(directory,"transactions"),walOptions,budget,failureInjection,databaseOptions);
        try{Func<GatewayDatabaseProfile,GatewayVersionedBinding>? binding=null;if(transactions.IsVersioned){binding=transactions.CreateVersionedBinding;}var generations=new GatewayGenerationStore(Path.Combine(directory,"generations"),builder,databaseOptions with{ParentBudget=budget},budget,binding);var registry=new DatabaseRegistry(transactions:transactions);transactions.ConfigureGenerations(generations,registry);transactions.ConfigureNativeQueries(new(Path.Combine(directory,"native"),builder,budget));if(transactions.NeedsBootstrap){await transactions.BootstrapAsync(initialCatalogs??throw new InvalidOperationException("A new Gateway requires initial database catalogs."),cancellationToken,initialDataSource).ConfigureAwait(false);}transactions.Recover();transactions.CompleteStartupCollection();return new(transactions,registry,generations);}
        catch{transactions.Dispose();throw;}
    }
    public void Dispose()=>this.Transactions.Dispose();
}
