package dev.seamless.jdbc;

import java.sql.*;

/** Reproduces the observed Generic SQL limitation and verifies the same edit in T-SQL. */
public final class Stage1GuiProbe {
    static String name(Connection c) throws Exception {
        try (Statement s = c.createStatement()) {
            s.setQueryTimeout(5);
            try (ResultSet r = s.executeQuery("SELECT Name FROM dbo.Stage1Items WHERE Id=1")) {
                if (!r.next()) throw new AssertionError("Fixture row missing");
                return r.getString(1);
            }
        }
    }
    public static void main(String[] args) throws Exception {
        Class.forName("dev.seamless.jdbc.SeamlessDriver");
        try (Connection c = DriverManager.getConnection(args[0])) {
            c.setNetworkTimeout(null, 5000);
            if (!"日本語".equals(name(c))) throw new AssertionError("GUI failure changed persisted data");
            try (Statement s = c.createStatement()) {
                try {
                    s.executeUpdate("UPDATE dbo.Stage1Items t SET t.Name='GUI確認' WHERE t.Id=1");
                    throw new AssertionError("Generic UPDATE alias is now supported; update the recorded limitation");
                } catch (SQLException expected) {
                    if (expected.getErrorCode() != 102) throw expected;
                    System.out.println("Confirmed limitation: Generic UPDATE alias rejected with error 102");
                }
            }
            if (!"日本語".equals(name(c))) throw new AssertionError("Rejected SQL changed persisted data");
            c.setAutoCommit(false);
            try {
                System.out.println("Starting prepared T-SQL update in an explicit transaction");
                try (PreparedStatement s = c.prepareStatement("UPDATE dbo.Stage1Items SET Name=? WHERE Id=?")) {
                    s.setNString(1, "GUI確認"); s.setInt(2, 1);
                    if (s.executeUpdate() != 1) throw new AssertionError("Expected one changed row");
                }
                System.out.println("Prepared UPDATE completed");
                if (!"GUI確認".equals(name(c))) throw new AssertionError("T-SQL UPDATE did not change the transaction view");
            } finally { System.out.println("Rolling back the probe transaction"); c.rollback(); c.setAutoCommit(true); }
            if (!"日本語".equals(name(c))) throw new AssertionError("Rollback did not restore the fixture value");
            System.out.println("Passed: prepared T-SQL UPDATE, read-own-write, rollback, rejected SQL leaves data unchanged");
        }
    }
}
