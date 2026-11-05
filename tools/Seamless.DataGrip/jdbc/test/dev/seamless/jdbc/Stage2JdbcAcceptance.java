package dev.seamless.jdbc;

import java.sql.*;
import java.util.*;

/** Observable JDBC contracts against a new, exclusively owned two-row fixture. */
public final class Stage2JdbcAcceptance {
    interface Probe { void run() throws Exception; }
    static String url;
    static final List<String> failures = new ArrayList<>();
    static Connection open() throws SQLException { return DriverManager.getConnection(url); }
    static void check(boolean value, String message) { if (!value) throw new AssertionError(message); }
    static void rejects(String state, Probe action) throws Exception {
        try { action.run(); } catch (SQLException e) { check(state == null || state.equals(e.getSQLState()), "expected " + state + ", received " + e.getSQLState()); return; }
        throw new AssertionError("Expected SQLException " + state);
    }
    static void probe(String name, Probe action) {
        try { action.run(); System.out.println("PASS " + name); }
        catch (Throwable error) { failures.add(name + ": " + error); System.out.println("FAIL " + name + ": " + error); }
    }
    static String name(Connection c) throws SQLException {
        try (Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT Name FROM dbo.Stage1Items WHERE Id=1")) { check(r.next(), "fixture row"); return r.getString(1); }
    }
    static int update(Connection c, String sql) throws SQLException { try (Statement s = c.createStatement()) { return s.executeUpdate(sql); } }
    public static void main(String[] args) throws Exception {
        Class.forName("dev.seamless.jdbc.SeamlessDriver"); url = args[0];
        probe("missing parameter keeps connection usable", () -> {
            try (Connection c = open(); PreparedStatement s = c.prepareStatement("SELECT ? + ? AS Value")) {
                s.setInt(1, 7); rejects("07001", s::executeQuery);
                check("日本語".equals(name(c)), "local validation must not disconnect");
                s.setInt(2, 4); try (ResultSet r = s.executeQuery()) { check(r.next() && r.getInt(1) == 11, "rebind after failure"); }
                s.clearParameters(); rejects("07001", s::executeQuery);
            }
        });
        probe("parameter index and lexical boundaries", () -> {
            try (Connection c = open(); PreparedStatement s = c.prepareStatement("SELECT ? AS Actual, '?' AS Literal /* ? /* ? */ ? */ -- ?\n")) {
                rejects("07009", () -> s.setInt(2, 9)); s.setInt(1, 7);
                try (ResultSet r = s.executeQuery()) { check(r.next() && r.getInt(1) == 7 && "?".equals(r.getString(2)), "literal/comment placeholders"); }
            }
        });
        probe("typed scalar parameters and conversion errors", () -> {
            try (Connection c = open(); PreparedStatement s = c.prepareStatement("SELECT ? AS TextValue, ? AS IntValue, ? AS Missing, ? AS AtValue")) {
                s.setObject(1, "型付き", JDBCType.NVARCHAR); s.setObject(2, 42L, Types.INTEGER); s.setObject(3, null, Types.INTEGER);
                var at = java.time.LocalDateTime.of(2026, 11, 2, 12, 34, 56, 123456700); s.setObject(4, at, Types.TIMESTAMP);
                try (ResultSet r = s.executeQuery()) { check(r.getMetaData().getColumnType(2) == Types.INTEGER && r.next() && "型付き".equals(r.getString(1)) && r.getInt(2) == 42 && r.getObject(3) == null && r.wasNull() && at.equals(r.getTimestamp(4).toLocalDateTime()), "typed values and requested integer type"); }
                rejects("22018", () -> s.setObject(2, "invalid", Types.INTEGER)); rejects("22003", () -> s.setObject(2, Long.MAX_VALUE, Types.INTEGER));
                check("日本語".equals(name(c)), "conversion errors are local and leave connection usable");
            }
        });
        probe("prepared statement rejects SQL-text overload", () -> {
            try (Connection c = open(); PreparedStatement s = c.prepareStatement("SELECT 1")) { rejects(null, () -> s.execute("SELECT 2")); }
        });
        probe("closing old statement preserves newer result", () -> {
            try (Connection c = open(); Statement a = c.createStatement(); Statement b = c.createStatement()) {
                a.executeQuery("SELECT Id FROM dbo.Stage1Items"); ResultSet r = b.executeQuery("SELECT Name FROM dbo.Stage1Items ORDER BY Id");
                try (Statement idle = c.createStatement()) { check(!idle.getMoreResults(), "idle statement cannot consume another request"); }
                a.close(); check(r.next() && "日本語".equals(r.getString(1)), "newer result ownership"); r.close();
            }
        });
        probe("mixed results and implicit result close", () -> {
            try (Connection c = open(); Statement s = c.createStatement()) {
                c.setAutoCommit(false);
                check(s.execute("SELECT 1 AS A; UPDATE dbo.Stage1Items SET Name=N'temporary' WHERE Id=1; SELECT Name FROM dbo.Stage1Items WHERE Id=1"), "first result");
                ResultSet first = s.getResultSet(); check(first == s.getResultSet(), "stable ResultSet identity"); check(first.next() && first.getInt(1) == 1, "first value");
                check(!s.getMoreResults() && first.isClosed() && s.getUpdateCount() == 1, "update and implicit close");
                check(s.getMoreResults(), "last result"); ResultSet last = s.getResultSet(); check(last.next() && "temporary".equals(last.getString(1)), "read own update");
                check(!s.getMoreResults() && last.isClosed() && s.getUpdateCount() == -1 && !s.getMoreResults(), "terminal state"); c.rollback();
            }
        });
        probe("savepoint names and IDs", () -> {
            try (Connection c = open()) {
                c.setAutoCommit(false); Savepoint numbered = c.setSavepoint(); check(numbered.getSavepointId() > 0, "numbered ID"); rejects("3B000", numbered::getSavepointName);
                Savepoint named = c.setSavepoint("利用者名"); check("利用者名".equals(named.getSavepointName()), "named point"); rejects("3B000", named::getSavepointId); c.rollback();
            }
        });
        probe("foreign savepoint rejected locally", () -> {
            try (Connection a = open(); Connection b = open()) {
                a.setAutoCommit(false); b.setAutoCommit(false); a.setSavepoint(); Savepoint foreign = b.setSavepoint();
                rejects("3B001", () -> a.rollback(foreign)); a.rollback(); b.rollback();
            }
        });
        probe("savepoint rollback and invalidation", () -> {
            try (Connection c = open()) {
                c.setAutoCommit(false); Savepoint before = c.setSavepoint(); update(c, "UPDATE dbo.Stage1Items SET Name=N'first' WHERE Id=1");
                Savepoint later = c.setSavepoint(); update(c, "UPDATE dbo.Stage1Items SET Name=N'second' WHERE Id=1"); c.rollback(before);
                check("日本語".equals(name(c)), "rollback to earlier point"); rejects("3B001", () -> c.rollback(later));
                c.releaseSavepoint(before); rejects("3B001", () -> c.rollback(before)); c.commit();
                rejects("3B001", () -> c.rollback(before));
            }
        });
        probe("CRUD commit and reconnect", () -> {
            try (Connection c = open()) {
                c.setAutoCommit(false);
                try (PreparedStatement s = c.prepareStatement("INSERT dbo.Stage1Items(Id,Name,Enabled) VALUES(?,?,?)")) { s.setInt(1, 1001); s.setNString(2, "追加"); s.setBoolean(3, true); check(s.executeUpdate() == 1, "insert count"); }
                c.commit();
                try (Connection reader = open(); PreparedStatement s = reader.prepareStatement("SELECT Name FROM dbo.Stage1Items WHERE Id=?")) { s.setInt(1, 1001); try (ResultSet r = s.executeQuery()) { check(r.next() && "追加".equals(r.getString(1)), "committed insert visible"); } }
                try (PreparedStatement s = c.prepareStatement("UPDATE dbo.Stage1Items SET Name=? WHERE Id=?")) { s.setNString(1, "変更"); s.setInt(2, 1001); check(s.executeUpdate() == 1, "update count"); }
                check(update(c, "DELETE FROM dbo.Stage1Items WHERE Id=1001") == 1, "delete count"); c.commit();
                check("日本語".equals(name(c)), "original row preserved");
            }
        });
        probe("connection close closes statements", () -> {
            Connection c = open(); Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT 1"); c.close();
            check(s.isClosed() && r.isClosed(), "dependent resources closed"); rejects("07000", () -> s.execute("SELECT 2")); s.close();
        });
        probe("network timeout independent of query timeout", () -> {
            try (Connection c = open(); Statement s = c.createStatement()) {
                c.setNetworkTimeout(Runnable::run, 1234); s.setQueryTimeout(2); try (ResultSet r = s.executeQuery("SELECT 1")) { check(r.next(), "query"); }
                check(c.getNetworkTimeout() == 1234, "network timeout retained");
            }
        });
        probe("unsupported isolation and batch fail explicitly", () -> {
            try (Connection c = open(); Statement s = c.createStatement()) {
                check(!c.getMetaData().supportsTransactionIsolationLevel(Connection.TRANSACTION_READ_COMMITTED), "do not advertise ignored isolation");
                rejects("0A000", () -> c.setTransactionIsolation(Connection.TRANSACTION_READ_COMMITTED)); rejects("0A000", () -> s.addBatch("SELECT 1"));
            }
        });
        if (!failures.isEmpty()) throw new AssertionError(String.join("\n", failures));
        System.out.println("Passed: 13 JDBC lifecycle, binding, ownership, CRUD and transaction probes");
    }
}
