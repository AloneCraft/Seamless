namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    public AuditPage AuditEvents(UserAccount user,AuditQuery query){Require(user,"*",ManagementPermission.Manage);return this.store.AuditEvents(query,value=>Can(user,value.Database,ManagementPermission.Edit));}
    public AuditReadPage ReadSummaries(UserAccount user,AuditQuery query){Require(user,"*",ManagementPermission.Manage);return this.store.ReadSummaries(query);}
}
