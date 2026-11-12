package dev.seamless.jdbc;

import java.math.BigDecimal;
import java.sql.*;
import java.util.*;

/** Exercises observable JDBC behavior against the isolated real Seamless fixture. */
public final class Stage1Acceptance {
    static void check(boolean value, String message) { if (!value) throw new AssertionError(message); }
    public static void main(String[] args) throws Exception {
        Class.forName("dev.seamless.jdbc.SeamlessDriver");
        try (Connection c = DriverManager.getConnection(args[0])) {
            DatabaseMetaData m = c.getMetaData();
            check(m.getDatabaseProductName().equals("SeamlessDB"), "product identity");
            try (ResultSet r = m.getTableTypes()) { check(r.next() && r.getString(1).equals("TABLE") && !r.next(), "table types"); }
            try (ResultSet r = m.getTables("missing", null, "%", null)) { check(!r.next(), "catalog filter"); }
            try (ResultSet r = m.getTables("DataGripStage1", "dbo", "Stage1Items", new String[]{"VIEW"})) { check(!r.next(), "table type filter"); }
            try (ResultSet r = m.getTables("DataGripStage1", "dbo", "Stage1Items", new String[]{"TABLE"})) { check(r.next() && r.getString("TABLE_NAME").equals("Stage1Items") && !r.next(), "table metadata"); }
            try (ResultSet r = m.getColumns(null, "dbo", "Stage1Items", "Na%")) { check(r.next() && r.getString("COLUMN_NAME").equals("Name") && r.getInt("NULLABLE") == 1 && !r.next(), "column filter and nullable"); }
            try (ResultSet r = m.getPrimaryKeys(null, "dbo", "Stage1Items")) { check(r.next() && r.getString("COLUMN_NAME").equals("Id") && r.getShort("KEY_SEQ") == 1 && !r.next(), "primary key"); }
            try (Statement s = c.createStatement(); ResultSet r = s.executeQuery("SELECT Id,Name,Amount,Enabled,Payload,At FROM dbo.Stage1Items ORDER BY Id")) {
                check(r.next() && r.getInt(1) == 1, "first row");
                check(r.getString(2).equals("日本語"), "unicode");
                check(r.getBigDecimal(3).equals(new BigDecimal("123.4567")), "decimal precision");
                check(r.getBoolean(4), "boolean");
                check(Arrays.equals(r.getBytes(5), new byte[]{0,1,(byte)255}), "binary");
                check(r.getTimestamp(6).getNanos() == 123456700, "datetime precision");
                check(r.next() && r.getInt(1) == 2 && r.getString(2) == null && r.wasNull(), "null string");
                check(r.getBigDecimal(3) == null && r.wasNull() && !r.next(), "null decimal and eof");
                check(!s.getMoreResults() && s.getUpdateCount() == -1 && !s.getMoreResults(), "repeat completion");
            }
            try (Statement s = c.createStatement()) {
                try { s.executeQuery("SELECT Missing FROM dbo.Stage1Items"); throw new AssertionError("expected query error"); } catch (SQLException expected) {}
                try (ResultSet r = s.executeQuery("SELECT COUNT(*) FROM dbo.Stage1Items")) { check(r.next() && r.getInt(1) == 2, "error recovery"); }
            }
            try (PreparedStatement s = c.prepareStatement("SELECT Name FROM dbo.Stage1Items WHERE Id=?")) {
                s.setInt(1, 1);
                try (ResultSet r = s.executeQuery()) { check(r.next() && r.getString(1).equals("日本語") && !r.next(), "bound parameter"); }
            }
            check(!m.supportsBatchUpdates(), "unsupported batch is not advertised");
        }
        System.out.println("Passed: real JDBC identity, metadata filters, PK, typed rows, NULL, completion, error recovery, parameter");
    }
}
