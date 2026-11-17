namespace Database.Verification.Tests;

public sealed class ModuleBoundaries{
    [Fact]public void References(){
        var database=typeof(Seamless.Database.Compilation.NativeDatabaseCompiler).Assembly;
        Assert.Same(database,typeof(Seamless.Database.Transfer.DatabaseTransferAdapter).Assembly);
        var databaseReferences=database.GetReferencedAssemblies().Select(assembly=>assembly.Name).ToArray();
        Assert.Contains("Microsoft.Data.SqlClient",databaseReferences);
        Assert.DoesNotContain("Database.Verification",databaseReferences);
        Assert.DoesNotContain("Client",databaseReferences);
        Assert.DoesNotContain("Server",databaseReferences);
        var clientReferences=typeof(Seamless.Database.Client.NativeQueryClient).Assembly.GetReferencedAssemblies().Select(assembly=>assembly.Name).ToArray();
        Assert.DoesNotContain("Microsoft.Data.SqlClient",clientReferences);
        Assert.DoesNotContain("Database",clientReferences);
        Assert.DoesNotContain("Server",clientReferences);
        Assert.DoesNotContain(clientReferences,name=>name?.StartsWith("Microsoft.AspNetCore.",StringComparison.Ordinal)==true);
        var verification=typeof(global::Database.Verification.DatabaseConsistencyVerifier).Assembly.GetReferencedAssemblies().Select(assembly=>assembly.Name).ToArray();
        Assert.DoesNotContain("Server",verification);
        Assert.DoesNotContain("Client",verification);
    }
}