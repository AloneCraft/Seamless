package dev.seamless.jdbc;

import java.io.*;
import java.lang.reflect.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.math.BigDecimal;
import java.sql.*;
import java.time.*;
import java.util.*;
import java.util.logging.Logger;

/** JDBC adapter for the versioned Seamless tools protocol, without SQL Server/TDS emulation. */
public final class SeamlessDriver implements Driver {
    static final int MAX = 8 * 1024 * 1024;
    static { try { DriverManager.registerDriver(new SeamlessDriver()); } catch (SQLException e) { throw new ExceptionInInitializerError(e); } }
    public boolean acceptsURL(String url) { return url != null && url.startsWith("jdbc:seamless://"); }
    public Connection connect(String url, Properties info) throws SQLException {
        if (!acceptsURL(url)) return null;
        return new Conn(url).proxy();
    }
    public DriverPropertyInfo[] getPropertyInfo(String url, Properties info) { return new DriverPropertyInfo[0]; }
    public int getMajorVersion() { return 1; }
    public int getMinorVersion() { return 0; }
    public boolean jdbcCompliant() { return false; }
    public Logger getParentLogger() { return Logger.getLogger("dev.seamless.jdbc"); }

    interface Body { void write(DataOutputStream out) throws IOException; }
    static void integer(DataOutputStream out, int value) throws IOException { out.writeInt(Integer.reverseBytes(value)); }
    static int integer(DataInputStream in) throws IOException { return Integer.reverseBytes(in.readInt()); }
    static void longValue(DataOutputStream out, long value) throws IOException { out.writeLong(Long.reverseBytes(value)); }
    static long longValue(DataInputStream in) throws IOException { return Long.reverseBytes(in.readLong()); }
    static void text(DataOutputStream out, String value) throws IOException {
        if (value == null) { integer(out, -1); return; }
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8); integer(out, bytes.length); out.write(bytes);
    }
    static String text(DataInputStream in) throws IOException {
        int length = integer(in); if (length == -1) return null;
        if (length < 0 || length > MAX) throw new IOException("Invalid text length");
        byte[] bytes = in.readNBytes(length); if (bytes.length != length) throw new EOFException();
        return new String(bytes, StandardCharsets.UTF_8);
    }
    static Object value(DataInputStream in) throws IOException {
        return switch (in.readUnsignedByte()) {
            case 0 -> null; case 1 -> in.readBoolean(); case 2 -> integer(in); case 3 -> longValue(in);
            case 4 -> Float.intBitsToFloat(integer(in)); case 5 -> Double.longBitsToDouble(longValue(in));
            case 6 -> new BigDecimal(text(in)); case 7 -> text(in);
            case 8 -> { int length = integer(in); if (length < 0 || length > MAX) throw new IOException("Invalid binary length"); byte[] data = in.readNBytes(length); if (data.length != length) throw new EOFException(); yield data; }
            case 9 -> java.sql.Date.valueOf(text(in)); case 10 -> LocalTime.parse(text(in));
            case 11 -> Timestamp.valueOf(LocalDateTime.parse(text(in))); case 12 -> OffsetDateTime.parse(text(in));
            case 13 -> UUID.fromString(text(in)); default -> throw new IOException("Unknown value tag");
        };
    }
    static void value(DataOutputStream out, Object value) throws IOException {
        if (value == null) { out.writeByte(0); }
        else if (value instanceof Boolean flag) { out.writeByte(1); out.writeBoolean(flag); }
        else if (value instanceof Byte || value instanceof Short || value instanceof Integer) { out.writeByte(2); integer(out, ((Number)value).intValue()); }
        else if (value instanceof Long number) { out.writeByte(3); longValue(out, number); }
        else if (value instanceof Float number) { out.writeByte(4); integer(out, Float.floatToRawIntBits(number)); }
        else if (value instanceof Double number) { out.writeByte(5); longValue(out, Double.doubleToRawLongBits(number)); }
        else if (value instanceof BigDecimal number) { out.writeByte(6); text(out, number.toPlainString()); }
        else if (value instanceof byte[] bytes) { out.writeByte(8); integer(out, bytes.length); out.write(bytes); }
        else if (value instanceof java.sql.Date date) { out.writeByte(9); text(out, date.toLocalDate().toString()); }
        else if (value instanceof LocalDate date) { out.writeByte(9); text(out, date.toString()); }
        else if (value instanceof java.sql.Time time) { out.writeByte(10); text(out, time.toLocalTime().toString()); }
        else if (value instanceof LocalTime time) { out.writeByte(10); text(out, time.toString()); }
        else if (value instanceof Timestamp date) { out.writeByte(11); text(out, date.toLocalDateTime().toString()); }
        else if (value instanceof LocalDateTime date) { out.writeByte(11); text(out, date.toString()); }
        else if (value instanceof OffsetDateTime date) { out.writeByte(12); text(out, date.toString()); }
        else if (value instanceof UUID uuid) { out.writeByte(13); text(out, uuid.toString()); }
        else if (value instanceof String string) { out.writeByte(7); text(out, string); }
        else throw new IOException("Unsupported parameter class " + value.getClass().getName());
    }
    static SQLException unsupported(String method) { return new SQLFeatureNotSupportedException("Seamless JDBC does not support " + method, "0A000"); }
    static Object proxy(Class<?> type, InvocationHandler handler) { return java.lang.reflect.Proxy.newProxyInstance(SeamlessDriver.class.getClassLoader(), new Class<?>[]{type}, handler); }
    static Object common(Object instance, Method method, Object[] args) throws SQLException {
        return switch (method.getName()) {
            case "toString" -> "Seamless " + method.getDeclaringClass().getSimpleName();
            case "hashCode" -> System.identityHashCode(instance); case "equals" -> instance == args[0];
            case "isWrapperFor" -> ((Class<?>)args[0]).isInstance(instance);
            case "unwrap" -> { if (!((Class<?>)args[0]).isInstance(instance)) throw unsupported("unwrap " + args[0]); yield instance; }
            default -> throw unsupported(method.getName());
        };
    }
    static final class Link implements AutoCloseable {
        final URI address; final Socket socket = new Socket(); final DataInputStream in; final DataOutputStream out;
        String session; boolean done = true; Cursor current;
        Link(String url) throws SQLException {
            try {
                address = URI.create(url.substring(5)); if (!"seamless".equals(address.getScheme()) || address.getHost() == null || address.getPort() < 1) throw new IOException("Expected jdbc:seamless://host:port/database");
                if (!InetAddress.getByName(address.getHost()).isLoopbackAddress()) throw new IOException("Stage1 requires a loopback host"); socket.connect(new InetSocketAddress(address.getHost(), address.getPort()), 10000); socket.setTcpNoDelay(true); socket.setSoTimeout(60000);
                in = new DataInputStream(new BufferedInputStream(socket.getInputStream())); out = new DataOutputStream(new BufferedOutputStream(socket.getOutputStream()));
                send(1, writer -> { integer(writer, 1001); text(writer, database()); }); Frame frame = receive(); if (frame.kind != 1) throw new IOException("Missing native protocol greeting"); text(frame.input); text(frame.input); session = text(frame.input); receiveDone();
            } catch (IOException | IllegalArgumentException error) { try { socket.close(); } catch (IOException ignored) {} throw new SQLException("Native connection failed: " + error.getMessage(), "08001", error); }
        }
        String database() { return URLDecoder.decode(address.getPath().substring(1), StandardCharsets.UTF_8); }
        void send(int operation, Body body) throws IOException {
            ByteArrayOutputStream buffer = new ByteArrayOutputStream(); DataOutputStream writer = new DataOutputStream(buffer); writer.writeByte(operation); body.write(writer);
            if (buffer.size() > MAX) throw new IOException("Native request exceeds frame limit"); integer(out, buffer.size()); buffer.writeTo(out); out.flush(); done = false;
        }
        Frame receive() throws IOException, SQLException {
            int length = integer(in); if (length < 1 || length > MAX) throw new IOException("Invalid native response length"); byte[] bytes = in.readNBytes(length); if (bytes.length != length) throw new EOFException(); DataInputStream input = new DataInputStream(new ByteArrayInputStream(bytes)); int kind = input.readUnsignedByte();
            if (kind == 9) { done = true; int number = integer(input); String state = text(input); throw new SQLException(text(input), state, number); }
            return new Frame(kind, input);
        }
        void receiveDone() throws IOException, SQLException { Frame frame = receive(); if (frame.kind != 5) throw new IOException("Missing completion"); done = true; }
        void drain() throws IOException, SQLException { while (!done) { Frame frame = receive(); if (frame.kind == 5) done = true; } if (current != null) current.closed = true; current = null; }
        Cursor result() throws IOException, SQLException { if (done) return null; Frame frame = receive(); if (frame.kind == 5) { done = true; return null; } if (frame.kind != 2) throw new IOException("Missing result metadata"); current = new Cursor(this, frame.input); if (current.columns.length == 0) current.finish(); return current; }
        void transaction(int operation, String point) throws SQLException { try { drain(); send(4, writer -> { writer.writeByte(operation); text(writer, point); }); Cursor result = result(); if (result != null) result.finish(); receiveDone(); } catch (IOException error) { throw new SQLException(error.getMessage(), "08006", error); } }
        void cancel() throws SQLException {
            if (session == null) return;
            try (Socket control = new Socket()) { control.connect(new InetSocketAddress(address.getHost(), address.getPort()), 3000); control.setSoTimeout(3000); DataOutputStream output = new DataOutputStream(control.getOutputStream()); ByteArrayOutputStream buffer = new ByteArrayOutputStream(); DataOutputStream writer = new DataOutputStream(buffer); writer.writeByte(6); text(writer, session); integer(output, buffer.size()); buffer.writeTo(output); output.flush(); }
            catch (IOException error) { throw new SQLException("Cancel failed", "08006", error); }
        }
        public void close() { try { socket.close(); } catch (IOException ignored) { } }
    }
    record Frame(int kind, DataInputStream input) {}
    record Column(String name, String type, boolean nullable, int size, int precision, int scale, String schema, String table) {}
    static final class Cursor implements InvocationHandler {
        final Link link; final Column[] columns; List<Object[]> batch = List.of(); int offset; Object[] row; boolean end, closed, wasNull; long affected = -1; int rowNumber; ResultSet resultSet;
        Cursor(Link link, DataInputStream input) throws IOException { this.link = link; int count = integer(input); if (count < 0 || count > 4096) throw new IOException("Invalid column count"); columns = new Column[count]; for (int i = 0; i < count; i++) columns[i] = new Column(text(input), text(input), input.readBoolean(), integer(input), input.readUnsignedByte(), input.readUnsignedByte(), text(input), text(input)); }
        ResultSet proxy() { if (resultSet == null) resultSet = (ResultSet)SeamlessDriver.proxy(ResultSet.class, this); return resultSet; }
        boolean next() throws SQLException {
            if (closed) throw new SQLException("Result set closed", "24000"); if (end) return false;
            try { while (offset == batch.size()) { Frame frame = link.receive(); if (frame.kind == 4) { affected = longValue(frame.input); end = true; row = null; return false; } if (frame.kind != 3) throw new IOException("Invalid result frame"); int count = integer(frame.input); if (count < 0 || count > 5000 || count > frame.input.available() / Math.max(1, columns.length)) throw new IOException("Invalid batch count"); List<Object[]> rows = new ArrayList<>(count); for (int r = 0; r < count; r++) { Object[] values = new Object[columns.length]; for (int c = 0; c < values.length; c++) values[c] = value(frame.input); rows.add(values); } batch = rows; offset = 0; }
                row = batch.get(offset++); rowNumber++; return true;
            } catch (IOException error) { link.close(); throw new SQLException(error.getMessage(), "08006", error); }
        }
        void finish() throws SQLException { while (!end) next(); }
        int column(Object key) throws SQLException { if (key instanceof Integer number) { if (number < 1 || number > columns.length) throw new SQLException("Column index out of range", "07009"); return number - 1; } for (int i = 0; i < columns.length; i++) if (columns[i].name.equalsIgnoreCase((String)key)) return i; throw new SQLException("Unknown column " + key, "S0022"); }
        Object cell(Object key) throws SQLException { if (closed || row == null) throw new SQLException("Cursor is not on a row", "24000"); Object value = row[column(key)]; wasNull = value == null; return value; }
        ResultSetMetaData metadata() { return (ResultSetMetaData)SeamlessDriver.proxy(ResultSetMetaData.class, (instance, method, args) -> {
            if (method.getName().equals("getColumnCount")) return columns.length; if (args == null || args.length == 0 || !(args[0] instanceof Integer)) return common(instance, method, args); Column column = columns[column(args[0])];
            return switch (method.getName()) { case "getColumnName", "getColumnLabel" -> column.name; case "getColumnType" -> jdbcType(column.type); case "getColumnTypeName" -> column.type; case "getColumnClassName" -> className(column.type); case "isNullable" -> column.nullable ? ResultSetMetaData.columnNullable : ResultSetMetaData.columnNoNulls; case "getColumnDisplaySize" -> Math.max(column.size, column.precision); case "getPrecision" -> column.precision == 0 ? Math.max(0, column.size) : column.precision; case "getScale" -> column.scale; case "getSchemaName" -> column.schema == null ? "" : column.schema; case "getTableName" -> column.table == null ? "" : column.table; case "getCatalogName" -> link.database(); case "isSigned" -> Number.class.getName().equals(className(column.type)) || Set.of(Types.INTEGER, Types.BIGINT, Types.DECIMAL, Types.DOUBLE, Types.REAL, Types.SMALLINT).contains(jdbcType(column.type)); case "isReadOnly" -> false; case "isWritable", "isSearchable", "isCaseSensitive" -> true; case "isDefinitelyWritable", "isAutoIncrement", "isCurrency" -> false; default -> common(instance, method, args); };
        }); }
        public Object invoke(Object instance, Method method, Object[] args) throws Throwable {
            return switch (method.getName()) {
                case "next" -> next(); case "close" -> { if (!closed) { finish(); closed = true; } yield null; } case "isClosed" -> closed; case "getMetaData" -> metadata(); case "findColumn" -> column(args[0]) + 1;
                case "wasNull" -> wasNull; case "getRow" -> rowNumber; case "getType" -> ResultSet.TYPE_FORWARD_ONLY; case "getConcurrency" -> ResultSet.CONCUR_READ_ONLY; case "getHoldability" -> ResultSet.CLOSE_CURSORS_AT_COMMIT; case "getFetchDirection" -> ResultSet.FETCH_FORWARD; case "getFetchSize" -> 256;
                case "setFetchSize", "setFetchDirection", "clearWarnings" -> null; case "getWarnings", "getStatement" -> null;
                case "getObject" -> { Object value = cell(args[0]); if (args.length > 1 && args[1] instanceof Class<?> target && value != null && !target.isInstance(value)) { if (target == String.class) yield value.toString(); if (target == LocalDate.class && value instanceof java.sql.Date date) yield date.toLocalDate(); if (target == LocalDateTime.class && value instanceof Timestamp date) yield date.toLocalDateTime(); throw unsupported("getObject as " + target); } yield value; }
                case "getString", "getNString" -> { Object value = cell(args[0]); yield value == null ? null : value.toString(); }
                case "getInt" -> { Object value = cell(args[0]); yield value == null ? 0 : ((Number)value).intValue(); } case "getLong" -> { Object value = cell(args[0]); yield value == null ? 0L : ((Number)value).longValue(); } case "getShort" -> { Object value = cell(args[0]); yield value == null ? (short)0 : ((Number)value).shortValue(); } case "getByte" -> { Object value = cell(args[0]); yield value == null ? (byte)0 : ((Number)value).byteValue(); }
                case "getDouble" -> { Object value = cell(args[0]); yield value == null ? 0d : ((Number)value).doubleValue(); } case "getFloat" -> { Object value = cell(args[0]); yield value == null ? 0f : ((Number)value).floatValue(); }
                case "getBoolean" -> { Object value = cell(args[0]); yield value != null && (value instanceof Boolean flag ? flag : ((Number)value).intValue() != 0); }
                case "getBigDecimal" -> { Object value = cell(args[0]); yield value == null ? null : value instanceof BigDecimal number ? number : new BigDecimal(value.toString()); }
                case "getBytes" -> cell(args[0]); case "getDate" -> cell(args[0]); case "getTime" -> { Object value = cell(args[0]); yield value instanceof LocalTime time ? java.sql.Time.valueOf(time) : value; } case "getTimestamp" -> cell(args[0]);
                case "rowUpdated", "rowInserted", "rowDeleted" -> false;
                default -> common(instance, method, args);
            };
        }
    }
    static final class Conn implements InvocationHandler {
        final String url; Link link; Connection connection; boolean auto = true, transaction, closed, readOnly; int savepoint, generation, networkTimeout;
        final Set<Stmt> statements = Collections.newSetFromMap(new IdentityHashMap<>());
        final List<Point> points = new ArrayList<>();
        Conn(String url) throws SQLException { this.url = url; link = new Link(url); }
        Connection proxy() { if (connection == null) connection = (Connection)SeamlessDriver.proxy(Connection.class, this); return connection; }
        void begin() throws SQLException { if (!auto && !transaction) { link.transaction(1, null); transaction = true; generation++; } }
        void complete(boolean commit) throws SQLException { if (auto) throw new SQLException("Auto commit is enabled", "25000"); if (transaction) { link.transaction(commit ? 2 : 3, null); transaction = false; points.clear(); } }
        Point point(Object value) throws SQLException {
            if (!(value instanceof Point point) || point.owner != this || !transaction || auto || point.generation != generation || !points.contains(point)) throw new SQLException("Savepoint is not active on this connection", "3B001");
            return point;
        }
        public Object invoke(Object instance, Method method, Object[] args) throws Throwable {
            String name = method.getName(); if (closed && !Set.of("close", "isClosed", "isValid", "toString").contains(name)) throw new SQLException("Connection closed", "08003");
            return switch (name) {
                case "close", "abort" -> { if (!closed) { link.close(); closed = true; for (Stmt statement : statements) statement.closeLocally(); statements.clear(); points.clear(); } yield null; } case "isClosed" -> closed || link.socket.isClosed(); case "isValid" -> !closed && !link.socket.isClosed();
                case "createStatement" -> new Stmt(this, null).proxy(); case "prepareStatement" -> new Stmt(this, (String)args[0]).proxy(); case "getMetaData" -> metadata();
                case "setAutoCommit" -> { boolean next = (boolean)args[0]; if (next && !auto) complete(true); auto = next; yield null; } case "getAutoCommit" -> auto;
                case "commit" -> { complete(true); yield null; } case "rollback" -> {
                    if (args == null || args.length == 0) complete(false);
                    else { Point point = point(args[0]); link.transaction(5, point.wireName); points.removeIf(later -> later.id > point.id); }
                    yield null;
                }
                case "setSavepoint" -> {
                    if (auto) throw new SQLException("Auto commit is enabled", "25000");
                    String userName = args == null || args.length == 0 ? null : (String)args[0];
                    if (args != null && args.length != 0 && (userName == null || userName.isEmpty())) throw new SQLException("Missing savepoint name", "3B000");
                    begin(); Point point = new Point(this, generation, ++savepoint, userName); link.transaction(4, point.wireName); points.add(point); yield point;
                }
                case "releaseSavepoint" -> { Point point = point(args[0]); link.transaction(6, point.wireName); points.remove(point); yield null; }
                case "getCatalog" -> link.database(); case "setCatalog" -> { if (!Objects.equals(args[0], link.database())) throw unsupported("changing catalog; open a connection to the other database"); yield null; } case "getSchema" -> "dbo"; case "setSchema" -> { if (!"dbo".equals(args[0])) throw unsupported("default schema " + args[0]); yield null; }
                case "setReadOnly" -> { readOnly = (boolean)args[0]; yield null; } case "isReadOnly" -> readOnly; case "getTransactionIsolation" -> Connection.TRANSACTION_SERIALIZABLE; case "setTransactionIsolation" -> { if ((int)args[0] != Connection.TRANSACTION_SERIALIZABLE) throw unsupported("isolation level"); yield null; }
                case "nativeSQL" -> args[0]; case "getWarnings" -> null; case "clearWarnings" -> null; case "getHoldability" -> ResultSet.CLOSE_CURSORS_AT_COMMIT; case "setHoldability" -> { if ((int)args[0] != ResultSet.CLOSE_CURSORS_AT_COMMIT) throw unsupported("hold cursors over commit"); yield null; }
                case "getNetworkTimeout" -> networkTimeout; case "setNetworkTimeout" -> { if ((int)args[1] < 0) throw new SQLException("Invalid network timeout", "HY092"); networkTimeout = (int)args[1]; link.socket.setSoTimeout(networkTimeout); yield null; }
                case "getClientInfo" -> args == null || args.length == 0 ? new Properties() : null; case "setClientInfo" -> null;
                default -> common(instance, method, args);
            };
        }
        DatabaseMetaData metadata() { return (DatabaseMetaData)SeamlessDriver.proxy(DatabaseMetaData.class, (instance, method, args) -> {
            String name = method.getName();
            if (Set.of("getTableTypes", "getCatalogs", "getSchemas", "getTables", "getColumns", "getPrimaryKeys", "getIndexInfo", "getImportedKeys", "getExportedKeys", "getProcedures", "getFunctions", "getTypeInfo").contains(name)) {
                String kind = switch (name) { case "getTableTypes" -> "tableTypes"; case "getCatalogs" -> "catalogs"; case "getSchemas" -> "schemas"; case "getTables" -> "tables"; case "getColumns" -> "columns"; case "getPrimaryKeys" -> "primaryKeys"; case "getIndexInfo" -> "indexInfo"; case "getImportedKeys" -> "importedKeys"; case "getExportedKeys" -> "exportedKeys"; case "getProcedures" -> "procedures"; case "getFunctions" -> "functions"; default -> "typeInfo"; };
                String schema = args != null && args.length > 1 ? (String)args[1] : null; String table = args != null && args.length > 2 ? (String)args[2] : null;
                try { link.drain(); link.send(2, writer -> { text(writer, kind); text(writer, schema); text(writer, table); text(writer, args != null && args.length > 0 ? (String)args[0] : null); text(writer, name.equals("getColumns") ? (String)args[3] : null); text(writer, name.equals("getTables") && args[3] != null ? String.join("|", (String[])args[3]) : null); }); return link.result().proxy(); } catch (IOException error) { throw new SQLException(error.getMessage(), "08006", error); }
            }
            return switch (name) {
                case "storesMixedCaseIdentifiers", "storesMixedCaseQuotedIdentifiers", "supportsMixedCaseIdentifiers" -> true; case "getConnection" -> proxy(); case "getURL" -> url; case "getUserName" -> ""; case "getDatabaseProductName" -> "SeamlessDB"; case "getDatabaseProductVersion" -> "1.0"; case "getDriverName" -> "Seamless Native JDBC"; case "getDriverVersion" -> "1.0";
                case "getDriverMajorVersion", "getDatabaseMajorVersion" -> 1; case "getDriverMinorVersion", "getDatabaseMinorVersion", "getJDBCMinorVersion" -> 0; case "getJDBCMajorVersion" -> 4;
                case "getIdentifierQuoteString" -> "\""; case "getCatalogSeparator" -> "."; case "getCatalogTerm" -> "database"; case "getSchemaTerm" -> "schema"; case "getProcedureTerm" -> "routine"; case "getSearchStringEscape" -> "\\"; case "getSQLKeywords", "getNumericFunctions", "getStringFunctions", "getSystemFunctions", "getTimeDateFunctions", "getExtraNameCharacters" -> "";
                case "supportsTransactions", "supportsSavepoints", "supportsSchemasInDataManipulation", "supportsSchemasInTableDefinitions", "supportsCatalogsInDataManipulation", "supportsCatalogsInTableDefinitions", "supportsMultipleResultSets", "supportsMixedCaseQuotedIdentifiers", "isCatalogAtStart", "dataDefinitionCausesTransactionCommit" -> !name.equals("dataDefinitionCausesTransactionCommit");
                case "supportsDataDefinitionAndDataManipulationTransactions", "allTablesAreSelectable" -> true; case "getDefaultTransactionIsolation" -> Connection.TRANSACTION_SERIALIZABLE; case "getResultSetHoldability" -> ResultSet.CLOSE_CURSORS_AT_COMMIT; case "getSQLStateType" -> DatabaseMetaData.sqlStateSQL;
                case "supportsResultSetType" -> (int)args[0] == ResultSet.TYPE_FORWARD_ONLY; case "supportsResultSetConcurrency" -> (int)args[0] == ResultSet.TYPE_FORWARD_ONLY && (int)args[1] == ResultSet.CONCUR_READ_ONLY; case "supportsTransactionIsolationLevel" -> (int)args[0] == Connection.TRANSACTION_SERIALIZABLE;
                default -> { if (name.startsWith("supports") || method.getReturnType() == boolean.class) yield false; if (name.startsWith("getMax") && method.getReturnType() == int.class) yield 0; if (method.getReturnType() == String.class) yield ""; yield common(instance, method, args); }
            };
        }); }
    }
    static final class Point implements Savepoint {
        final Conn owner; final int generation, id; final String name, wireName;
        Point(Conn owner, int generation, int id, String name) { this.owner = owner; this.generation = generation; this.id = id; this.name = name; wireName = "jdbc_" + generation + "_" + id; }
        public int getSavepointId() throws SQLException { if (name != null) throw new SQLException("Named savepoint has no numeric ID", "3B000"); return id; }
        public String getSavepointName() throws SQLException { if (name == null) throw new SQLException("Unnamed savepoint has no name", "3B000"); return name; }
    }
    record Parameter(String type, Object value, int length, int precision, int scale) {}
    static final class Stmt implements InvocationHandler {
        final Conn connection; final String sql; final SqlTemplate template; final List<Parameter> parameters = new ArrayList<>(); Cursor cursor; boolean closed; int maxRows, timeout = 60;
        Stmt(Conn connection, String sql) throws SQLException { this.connection = connection; this.sql = sql; template = sql == null ? null : parse(sql); connection.statements.add(this); }
        void closeLocally() { closed = true; if (cursor != null) cursor.closed = true; }
        Object proxy() { return SeamlessDriver.proxy(sql == null ? Statement.class : PreparedStatement.class, this); }
        boolean execute(String text) throws SQLException {
            if (closed || connection.closed) throw new SQLException("Statement closed", "07000");
            if (template != null && (parameters.size() != template.count || parameters.contains(null))) throw new SQLException("Every positional parameter must be set", "07001");
            connection.begin();
            try { connection.link.drain(); int queryMillis = (int)Math.min(Integer.MAX_VALUE, timeout * 1000L); int networkMillis = connection.networkTimeout;
                connection.link.socket.setSoTimeout(queryMillis == 0 ? networkMillis : networkMillis == 0 ? queryMillis : Math.min(queryMillis, networkMillis)); String command = template == null ? text : template.text;
                connection.link.send(3, writer -> { SeamlessDriver.text(writer, command); integer(writer, maxRows); integer(writer, sql == null ? 0 : parameters.size()); if (sql != null) for (Parameter parameter : parameters) { if (parameter == null) throw new IOException("Unbound parameter"); SeamlessDriver.text(writer, parameter.type); integer(writer, parameter.length); writer.writeByte(parameter.precision); writer.writeByte(parameter.scale); value(writer, parameter.value); } }); cursor = connection.link.result(); return cursor != null && cursor.columns.length != 0;
            } catch (IOException error) { connection.link.close(); throw new SQLException(error.getMessage(), "08006", error); }
        }
        public Object invoke(Object instance, Method method, Object[] args) throws Throwable {
            String name = method.getName();
            if ((closed || connection.closed) && !Set.of("close", "isClosed", "toString", "hashCode", "equals").contains(name)) throw new SQLException("Statement closed", "07000");
            if (sql != null && Set.of("execute", "executeQuery", "executeUpdate", "executeLargeUpdate").contains(name) && args != null && args.length != 0) throw new SQLException("PreparedStatement cannot execute supplied SQL text", "07000");
            if (sql != null && name.startsWith("set") && args != null && args.length >= 2 && args[0] instanceof Integer && !Set.of("setFetchSize", "setQueryTimeout", "setMaxRows", "setMaxFieldSize", "setFetchDirection", "setPoolable").contains(name)) {
                int index = (int)args[0]; if (index < 1 || index > template.count || index > 2100) throw new SQLException("Invalid parameter index", "07009");
                if (args.length > 2 && !name.equals("setNull") && !(name.equals("setObject") && args.length == 3)) throw unsupported(name + " overload");
                Object value = name.equals("setNull") ? null : args[1]; String type = switch (name) { case "setNull" -> sqlName((int)args[1]); case "setInt" -> "int"; case "setLong" -> "bigint"; case "setShort" -> "smallint"; case "setByte" -> "tinyint"; case "setBoolean" -> "bit"; case "setFloat" -> "real"; case "setDouble" -> "float"; case "setBigDecimal" -> "decimal"; case "setString", "setNString" -> "nvarchar"; case "setBytes" -> "varbinary"; case "setDate" -> "date"; case "setTime" -> "time"; case "setTimestamp" -> "datetime2"; case "setObject" -> args.length == 3 ? sqlName(targetType(args[2])) : infer(value); default -> throw unsupported(name); };
                if (name.equals("setObject") && args.length == 3) value = convert(value, type);
                int precision = value instanceof BigDecimal number ? Math.max(number.precision() + Math.max(0, -number.scale()), number.scale()) : 0; int scale = value instanceof BigDecimal number ? Math.max(0, number.scale()) : Set.of("time", "datetime2", "datetimeoffset").contains(type) ? 7 : 0; if (precision > 28) throw new SQLException("Native decimal precision above 28 requires a wider server representation", "22003");
                while (parameters.size() < index) parameters.add(null); parameters.set(index - 1, new Parameter(type, value, value instanceof String || value instanceof byte[] ? -1 : 0, precision, scale)); return null;
            }
            return switch (name) {
                case "execute" -> execute(sql == null ? (String)args[0] : sql); case "executeQuery" -> { if (!execute(sql == null ? (String)args[0] : sql)) throw new SQLException("No result set", "02000"); yield cursor.proxy(); } case "executeUpdate" -> { if (execute(sql == null ? (String)args[0] : sql)) throw new SQLException("Expected update count", "07000"); yield Math.toIntExact(cursor == null ? -1 : cursor.affected); } case "executeLargeUpdate" -> { if (execute(sql == null ? (String)args[0] : sql)) throw new SQLException("Expected update count", "07000"); yield cursor == null ? -1L : cursor.affected; }
                case "getResultSet" -> cursor == null || cursor.columns.length == 0 ? null : cursor.proxy(); case "getUpdateCount" -> cursor == null || cursor.columns.length != 0 ? -1 : Math.toIntExact(cursor.affected); case "getLargeUpdateCount" -> cursor == null || cursor.columns.length != 0 ? -1L : cursor.affected;
                case "getMoreResults" -> {
                    if (args != null && args.length != 0 && (int)args[0] != Statement.CLOSE_CURRENT_RESULT && (int)args[0] != Statement.CLOSE_ALL_RESULTS) throw unsupported("keeping earlier results open");
                    if (cursor == null) yield false;
                    if (cursor != null) {
                        if (connection.link.current != cursor) throw new SQLException("Result belongs to a superseded request", "24000");
                        cursor.finish(); cursor.closed = true;
                    }
                    cursor = connection.link.result(); yield cursor != null && cursor.columns.length != 0;
                }
                case "close" -> { if (!closed) { try { if (cursor != null && connection.link.current == cursor && !connection.link.socket.isClosed()) connection.link.drain(); } finally { closeLocally(); connection.statements.remove(this); } } yield null; } case "isClosed" -> closed || connection.closed; case "cancel" -> throw unsupported("request cancellation (stage2)"); case "getConnection" -> connection.proxy();
                case "setMaxRows" -> { if ((int)args[0] < 0) throw new SQLException("Invalid row limit", "HY092"); maxRows = (int)args[0]; yield null; } case "getMaxRows" -> maxRows; case "setQueryTimeout" -> { if ((int)args[0] < 0) throw new SQLException("Invalid timeout", "HY092"); timeout = (int)args[0]; yield null; } case "getQueryTimeout" -> timeout;
                case "getResultSetType" -> ResultSet.TYPE_FORWARD_ONLY; case "getResultSetConcurrency" -> ResultSet.CONCUR_READ_ONLY; case "getResultSetHoldability" -> ResultSet.CLOSE_CURSORS_AT_COMMIT; case "getFetchDirection" -> ResultSet.FETCH_FORWARD; case "getFetchSize" -> 256; case "getMaxFieldSize" -> 0; case "isPoolable", "isCloseOnCompletion" -> false;
                case "setFetchSize", "setFetchDirection", "setMaxFieldSize", "setEscapeProcessing", "setPoolable", "clearWarnings" -> null; case "getWarnings" -> null;
                case "clearParameters" -> { if (template == null) throw unsupported(name); parameters.clear(); yield null; }
                case "addBatch", "clearBatch", "executeBatch", "executeLargeBatch", "closeOnCompletion" -> throw unsupported(name);
                default -> common(instance, method, args);
            };
        }
    }
    static int targetType(Object target) throws SQLException {
        if (target instanceof Integer code) return code;
        if (target instanceof SQLType type && type.getVendorTypeNumber() != null) return type.getVendorTypeNumber();
        throw new SQLException("Missing JDBC target type", "HY004");
    }
    static Object convert(Object value, String type) throws SQLException {
        if (value == null) return null;
        try {
            return switch (type) {
                case "int" -> new BigDecimal(value.toString()).intValueExact();
                case "bigint" -> new BigDecimal(value.toString()).longValueExact();
                case "smallint" -> new BigDecimal(value.toString()).shortValueExact();
                case "tinyint" -> { int number = new BigDecimal(value.toString()).intValueExact(); if (number < 0 || number > 255) throw new ArithmeticException("tinyint range"); yield number; }
                case "decimal" -> value instanceof BigDecimal ? value : new BigDecimal(value.toString());
                case "real" -> { float number = Float.parseFloat(value.toString()); if (!Float.isFinite(number)) throw new ArithmeticException("real range"); yield number; }
                case "float" -> { double number = Double.parseDouble(value.toString()); if (!Double.isFinite(number)) throw new ArithmeticException("float range"); yield number; }
                case "bit" -> value instanceof Boolean ? value : new BigDecimal(value.toString()).signum() != 0;
                case "nvarchar" -> { if (!(value instanceof String)) throw unsupported("non-string to character conversion"); yield value; }
                case "varbinary" -> { if (!(value instanceof byte[])) throw unsupported("non-binary to binary conversion"); yield value; }
                case "date" -> { if (!(value instanceof java.sql.Date || value instanceof LocalDate)) throw unsupported("date conversion"); yield value; }
                case "time" -> { if (!(value instanceof java.sql.Time || value instanceof LocalTime)) throw unsupported("time conversion"); yield value; }
                case "datetime2" -> { if (!(value instanceof Timestamp || value instanceof LocalDateTime)) throw unsupported("timestamp conversion"); yield value; }
                case "datetimeoffset" -> { if (!(value instanceof OffsetDateTime)) throw unsupported("timestamp with offset conversion"); yield value; }
                default -> throw unsupported("target type " + type);
            };
        } catch (NumberFormatException error) { throw new SQLException("Invalid numeric parameter", "22018", error); }
        catch (ArithmeticException error) { throw new SQLException("Numeric parameter outside target range", "22003", error); }
    }
    static String infer(Object value) throws SQLException { if (value == null || value instanceof String) return "nvarchar"; if (value instanceof Integer) return "int"; if (value instanceof Long) return "bigint"; if (value instanceof Short) return "smallint"; if (value instanceof Byte) return "tinyint"; if (value instanceof Boolean) return "bit"; if (value instanceof BigDecimal) return "decimal"; if (value instanceof Float) return "real"; if (value instanceof Double) return "float"; if (value instanceof byte[]) return "varbinary"; if (value instanceof java.sql.Date || value instanceof LocalDate) return "date"; if (value instanceof java.sql.Time || value instanceof LocalTime) return "time"; if (value instanceof Timestamp || value instanceof LocalDateTime) return "datetime2"; if (value instanceof OffsetDateTime) return "datetimeoffset"; if (value instanceof UUID) return "uniqueidentifier"; throw unsupported("parameter " + value.getClass()); }
    static String sqlName(int type) throws SQLException { return switch (type) { case Types.BOOLEAN, Types.BIT -> "bit"; case Types.TINYINT -> "tinyint"; case Types.SMALLINT -> "smallint"; case Types.INTEGER -> "int"; case Types.BIGINT -> "bigint"; case Types.REAL -> "real"; case Types.FLOAT, Types.DOUBLE -> "float"; case Types.DECIMAL, Types.NUMERIC -> "decimal"; case Types.BINARY, Types.VARBINARY, Types.LONGVARBINARY -> "varbinary"; case Types.DATE -> "date"; case Types.TIME -> "time"; case Types.TIMESTAMP -> "datetime2"; case Types.TIMESTAMP_WITH_TIMEZONE -> "datetimeoffset"; case Types.CHAR, Types.VARCHAR, Types.LONGVARCHAR, Types.NCHAR, Types.NVARCHAR, Types.LONGNVARCHAR, Types.NULL -> "nvarchar"; default -> throw unsupported("JDBC parameter type " + type); }; }
    static int jdbcType(String name) { return switch (name.toLowerCase(Locale.ROOT)) { case "bit" -> Types.BIT; case "tinyint" -> Types.TINYINT; case "smallint" -> Types.SMALLINT; case "int" -> Types.INTEGER; case "bigint" -> Types.BIGINT; case "real" -> Types.REAL; case "float" -> Types.DOUBLE; case "decimal", "numeric", "money", "smallmoney" -> Types.DECIMAL; case "char" -> Types.CHAR; case "nchar" -> Types.NCHAR; case "varchar" -> Types.VARCHAR; case "nvarchar", "sysname" -> Types.NVARCHAR; case "text" -> Types.LONGVARCHAR; case "ntext" -> Types.LONGNVARCHAR; case "binary", "timestamp", "rowversion" -> Types.BINARY; case "varbinary" -> Types.VARBINARY; case "image" -> Types.LONGVARBINARY; case "date" -> Types.DATE; case "time" -> Types.TIME; case "datetime", "smalldatetime", "datetime2" -> Types.TIMESTAMP; case "datetimeoffset" -> Types.TIMESTAMP_WITH_TIMEZONE; case "xml" -> Types.SQLXML; default -> Types.OTHER; }; }
    static String className(String type) { return switch (jdbcType(type)) { case Types.BIT -> Boolean.class.getName(); case Types.INTEGER, Types.TINYINT, Types.SMALLINT -> Integer.class.getName(); case Types.BIGINT -> Long.class.getName(); case Types.REAL -> Float.class.getName(); case Types.DOUBLE -> Double.class.getName(); case Types.DECIMAL -> BigDecimal.class.getName(); case Types.BINARY, Types.VARBINARY, Types.LONGVARBINARY -> byte[].class.getName(); case Types.DATE -> java.sql.Date.class.getName(); case Types.TIME -> LocalTime.class.getName(); case Types.TIMESTAMP -> Timestamp.class.getName(); case Types.TIMESTAMP_WITH_TIMEZONE -> OffsetDateTime.class.getName(); case Types.OTHER -> UUID.class.getName(); default -> String.class.getName(); }; }
    record SqlTemplate(String text, int count) {}
    static SqlTemplate parse(String sql) throws SQLException {
        StringBuilder text = new StringBuilder(); int parameter = 0; char quote = 0; int block = 0; boolean line = false;
        for (int i = 0; i < sql.length(); i++) { char ch = sql.charAt(i); char next = i + 1 < sql.length() ? sql.charAt(i + 1) : 0;
            if (line) { text.append(ch); if (ch == '\n' || ch == '\r') line = false; continue; }
            if (block > 0) { text.append(ch); if (ch == '/' && next == '*') { text.append(next); i++; block++; } else if (ch == '*' && next == '/') { text.append(next); i++; block--; } continue; }
            if (quote != 0) { text.append(ch); if (ch == quote) { if (next == quote) { text.append(next); i++; } else quote = 0; } continue; }
            if (ch == '-' && next == '-') { line = true; text.append(ch).append(next); i++; } else if (ch == '/' && next == '*') { block = 1; text.append(ch).append(next); i++; } else if (ch == '\'' || ch == '"' || ch == '[') { quote = ch == '[' ? ']' : ch; text.append(ch); } else if (ch == '?') text.append("@p").append(parameter++); else text.append(ch);
        }
        if (quote != 0 || block != 0) throw new SQLException("Unterminated SQL token", "42000");
        if (parameter > 2100) throw new SQLException("Too many parameters", "07009");
        return new SqlTemplate(text.toString(), parameter);
    }
}
