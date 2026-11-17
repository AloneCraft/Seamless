using Microsoft.Data.SqlClient;
using Seamless.Database.Rdbms;
namespace Seamless.Database.SqlServer;

public enum SqlServerSamplePackageKind {
    Backup,
    Script,
    SelfExtractingBackup,
}
