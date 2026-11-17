using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database.System;

public enum DatabaseObjectKind {
    Table,
    View,
    TableFunction,
    ScalarFunction,
    StoredProcedure,
    SystemTable,
}
