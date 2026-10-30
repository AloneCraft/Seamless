namespace Shared.TpcE.Transactions;
/// <summary>Data-Maintenance(3.3.11)のフレーム 1。手順は既存の <see cref="DataMaintenanceFrames"/> を使う。出力は無い。</summary>
internal sealed class DataMaintenanceHarness{
    private readonly DataMaintenanceFrames frames=new();
    public void Frame1(TpcEDatabase db,DataMaintenanceTxnInput input,long now){
        var taxId=input.TxId;
        var taxrate=default(Code8);
        if(taxId.Length>0){
            taxrate=Code8.From(taxId);
        }
        var symbol=default(Code16);
        if(input.Symbol.Length>0){
            symbol=Code16.From(input.Symbol);
        }
        this.frames.Run(db,new DataMaintenanceInput{
            Now=now,
            TableName=input.TableName,
            AccountId=input.AcctId,
            CustomerId=input.CId,
            CompanyId=input.CoId,
            DayOfMonth=input.DayOfMonth,
            Symbol=symbol,
            TaxId=taxrate,
            VolumeIncrement=input.VolIncr,
        });
    }
}
