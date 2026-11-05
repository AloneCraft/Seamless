namespace Database.Verification.Tests;

public sealed class ModuleBoundaries{
    [Fact]public void References(){
        var conversion=typeof(Seamless.Database.Compilation.NativeDatabaseCompiler).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("Database.Verification",conversion);
        Assert.DoesNotContain("Seamless.Database.Transfer",conversion);
        Assert.DoesNotContain("Microsoft.Data.SqlClient",conversion);
        var transfer=typeof(Seamless.Database.Transfer.DatabaseTransferAdapter).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("Database.Verification",transfer);
        Assert.DoesNotContain("Seamless.Database.Compilation",transfer);
        var verification=typeof(global::Database.Verification.DatabaseConsistencyVerifier).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
        Assert.DoesNotContain("Seamless.Database.Compilation",verification);
        Assert.DoesNotContain("Seamless.Database.Transfer",verification);
    }
}
