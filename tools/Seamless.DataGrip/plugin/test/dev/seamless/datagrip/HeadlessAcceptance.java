package dev.seamless.datagrip;

import com.intellij.database.Dbms;
import com.intellij.database.datagrid.GridColumn;
import com.intellij.database.datagrid.mutating.ColumnQueryData;
import com.intellij.database.dialects.DatabaseDialect;
import com.intellij.database.dialects.DatabaseDialectEx;
import com.intellij.database.model.*;
import com.intellij.database.script.generator.dml.*;
import com.intellij.database.util.DdlBuilder;
import com.intellij.openapi.application.ApplicationStarter;
import com.intellij.util.containers.JBIterable;
import java.lang.reflect.Proxy;
import java.nio.file.*;
import java.sql.Types;
import java.util.*;

/** Test-only command in a separate IDE profile. Never installed in the user's IDE. */
public final class HeadlessAcceptance implements ApplicationStarter {
    @Override public boolean isHeadless() { return true; }
    @Override public int getRequiredModality() { return NOT_IN_EDT; }
    @Override public void main(List<String> args) {
        try {
            if (args.size() != 2) throw new IllegalArgumentException("Expected an output SQL path");
            Path output = Path.of(args.get(1)).toAbsolutePath().normalize();
            if (!output.toString().replace('\\', '/').startsWith("F:/CodexTemp/datagrip-stage1-")) throw new IllegalArgumentException("Dedicated F root required");
            Dbms dbms = Dbms.byName("SEAMLESS");
            if (dbms == null || dbms.isMicrosoft()) throw new AssertionError("Seamless DBMS registration");
            var connection = new RawConnectionConfig() {
                public String getName() { return "Seamless native acceptance"; }
                public String getDriverClass() { return "dev.seamless.jdbc.SeamlessDriver"; }
                public String getUrl() { return "jdbc:seamless://127.0.0.1:53153/DataGripStage1"; }
            };
            if (Dbms.forConnection(connection) != dbms) throw new AssertionError("JDBC URL detection");
            DmlHelper helper = DmlHelper.Companion.getEP().forDbms(dbms);
            if (!(helper instanceof SeamlessDmlHelper)) throw new AssertionError("Seamless DML extension selection");
            var schema = model(DasNamespace.class, "dbo", ObjectKind.SCHEMA, null);
            var table = model(DasTable.class, "Stage1Items", ObjectKind.TABLE, schema);
            var changed = new ColumnQueryData(column("Name", Types.NVARCHAR, "nvarchar", 1), "GUI確認");
            var key = new ColumnQueryData(column("Id", Types.INTEGER, "int", 0), 1);
            var builder = new DdlBuilder().withDialect((DatabaseDialectEx)DatabaseDialect.EP.forDbms(dbms));
            var result = helper.generate(new UpdateRowTask(table, List.of(changed), List.of(key), builder, "t"));
            if (result.isLeft()) throw new AssertionError(result.getLeft());
            var query = result.getRight();
            if (query.size() != 2 || !"GUI確認".equals(query.getColumnsData().get(0).getObject()) || !Integer.valueOf(1).equals(query.getColumnsData().get(1).getObject())) throw new AssertionError("Generated bind values/order");
            Files.writeString(output, query.getSql(), StandardOpenOption.CREATE_NEW);
            System.out.println("Passed: real DataGrip startup, plugin registration, DML extension selection, generated UPDATE and bind order");
            System.out.println(query.getSql());
            System.exit(0);
        } catch (Throwable error) { error.printStackTrace(); System.exit(1); }
    }
    static GridColumn column(String name, int type, String typeName, int index) {
        return (GridColumn)Proxy.newProxyInstance(GridColumn.class.getClassLoader(), new Class<?>[]{GridColumn.class}, (self, method, args) -> switch (method.getName()) {
            case "getName" -> name; case "getType" -> type; case "getTypeName" -> typeName; case "getColumnNumber" -> index; case "getAttributes" -> Set.of();
            case "hashCode" -> System.identityHashCode(self); case "equals" -> self == args[0]; case "toString" -> name;
            default -> throw new UnsupportedOperationException(method.toString());
        });
    }
    static <T> T model(Class<T> type, String name, ObjectKind kind, DasObject parent) {
        return type.cast(Proxy.newProxyInstance(type.getClassLoader(), new Class<?>[]{type}, (self, method, args) -> switch (method.getName()) {
            case "getName" -> name; case "getKind" -> kind; case "getDasParent" -> parent; case "getDasChildren", "getDbChildren" -> JBIterable.empty();
            case "isQuoted", "isSystem", "isTemporary" -> false; case "getComment" -> null; case "getColumnAttrs" -> Set.of();
            case "hashCode" -> System.identityHashCode(self); case "equals" -> self == args[0]; case "toString" -> name;
            default -> throw new UnsupportedOperationException(method.toString());
        }));
    }
}
