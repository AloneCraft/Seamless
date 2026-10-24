using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance;
/// <summary>SUT への取引の要求(0x01 + 取引 ID + EGen の TxnInput のバイト列)を作る。運転側(C++)も同じ形で送る。</summary>
internal static class HarnessRequest{
    public static byte[] Build(HarnessTransaction transaction,ReadOnlySpan<byte> input){
        var body=new byte[5+input.Length];
        body[0]=SutServer.TransactionMarker;
        BitConverter.TryWriteBytes(body.AsSpan(1,4),(int)transaction);
        input.CopyTo(body.AsSpan(5));
        return body;
    }
    /// <summary>Trade-Order の要求。成行の買い(TMB)を 100 株。</summary>
    public static byte[] TradeOrder(long accountId,string firstName,string lastName,string taxId,string companyName,string issue,string symbol,double price,string typeId,bool rollBack){
        var input=new byte[HarnessHost.SizeOf("TradeOrderTxnInput")];
        var wrapper=new TradeOrderTxnInput(input);
        wrapper.AcctId=accountId;
        wrapper.ExecFName=firstName;
        wrapper.ExecLName=lastName;
        wrapper.ExecTaxId=taxId;
        wrapper.CoName=companyName;
        wrapper.Issue=issue;
        wrapper.Symbol=symbol;
        wrapper.RequestedPrice=price;
        wrapper.TradeQty=100;
        wrapper.TradeTypeId=typeId;
        wrapper.TypeIsMargin=0;
        wrapper.IsLifo=1;
        wrapper.RollItBack=Convert.ToInt32(rollBack);
        wrapper.StPendingId="PNDG";
        wrapper.StSubmittedId="SBMT";
        return Build(HarnessTransaction.TradeOrder,input);
    }
}
