package dev.seamless.datagrip;

import com.intellij.database.Dbms;
import com.intellij.database.dialects.base.BaseDmlHelper;

/** Configure DataGrip's structured DML generator before SQL is produced. */
public final class SeamlessDmlHelper extends BaseDmlHelper {
    public SeamlessDmlHelper(Dbms dbms) { super(dbms); }
    @Override public boolean getNeedAliasInUpdateStatement() { return false; }
    @Override public boolean getNeedAliasInDeleteStatement() { return false; }
}
