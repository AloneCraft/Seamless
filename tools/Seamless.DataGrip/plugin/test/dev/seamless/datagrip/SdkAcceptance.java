package dev.seamless.datagrip;

/** Runs against the installed SDK; this is separate from loading the plugin in the IDE. */
public final class SdkAcceptance {
    public static void main(String[] args) {
        var helper = new SeamlessDmlHelper(SeamlessDbms.INSTANCE);
        if (!"SEAMLESS".equals(helper.getDbms().getName())) throw new AssertionError("independent DBMS identity");
        if (helper.getDbms().isMicrosoft() || helper.getDbms().isTransactSql()) throw new AssertionError("must not impersonate another DBMS");
        if (helper.getNeedAliasInUpdateStatement() || helper.getNeedAliasInDeleteStatement()) throw new AssertionError("T-SQL-compatible alias policy");
        System.out.println("Passed: installed SDK links, independent DBMS identity, UPDATE/DELETE alias policy");
    }
}
