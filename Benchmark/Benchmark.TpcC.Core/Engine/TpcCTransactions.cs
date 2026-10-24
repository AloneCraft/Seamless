namespace Benchmark.TpcC.Core;
public enum TpcCTxnKind{
    NewOrder=0,
    Payment=1,
    OrderStatus=2,
    Delivery=3,
    StockLevel=4,
}
/// <summary>New-Order の注文明細 1 行の入力(品目・供給倉庫・数量)。</summary>
public readonly record struct NewOrderLine(int ItemId,int SupplyWarehouseId,int Quantity);
/// <summary>
/// 取引の入力。入力生成器(<see cref="TpcCInputGenerator"/>)が作る展開済みの入力で、品目・供給倉庫・数量まで決まっている
/// (HammerDB は New-Order の品目などを手続きの中の乱数で決めるが、その分布は生成器が再現する)。
/// </summary>
public abstract record TpcCRequest(int WarehouseId,long Now){
    public abstract TpcCTxnKind Kind{get;}
    /// <summary>この取引が触る倉庫(家の倉庫を含み、昇順・重複なし)。区画をまたぐ取引の判定と、区画を昇順に押さえるのに使う。</summary>
    public abstract int[] TouchedWarehouses();
    protected static int[] Sorted(int home,IEnumerable<int> others){
        var set=new SortedSet<int>(others){home};
        return [..set];
    }
}
/// <summary>
/// New-Order。<see cref="AllLocal"/> は O_ALL_LOCAL の値(仕様は明細の供給倉庫から決まるが、HammerDB は W=1 でも「遠隔の抽選に当たった」だけで 0 にする: Q-029)。
/// 存在しない品目(最後の明細)を含むと、取引は巻き戻される。
/// </summary>
public sealed record NewOrderRequest(int WarehouseId,long Now,int DistrictId,int CustomerId,NewOrderLine[] Lines,int AllLocal):TpcCRequest(WarehouseId,Now){
    public override TpcCTxnKind Kind=>TpcCTxnKind.NewOrder;
    public override int[] TouchedWarehouses()=>Sorted(this.WarehouseId,this.Lines.Select(line=>line.SupplyWarehouseId));
}
/// <summary>Payment。<see cref="CustomerLast"/> が null でなければ姓で顧客を引く(<see cref="CustomerId"/> は姓で見つからなかったときに使う)。</summary>
public sealed record PaymentRequest(int WarehouseId,long Now,int DistrictId,int CustomerWarehouseId,int CustomerDistrictId,int CustomerId,string? CustomerLast,decimal Amount):TpcCRequest(WarehouseId,Now){
    public override TpcCTxnKind Kind=>TpcCTxnKind.Payment;
    public override int[] TouchedWarehouses()=>Sorted(this.WarehouseId,[this.CustomerWarehouseId]);
}
public sealed record OrderStatusRequest(int WarehouseId,long Now,int DistrictId,int CustomerId,string? CustomerLast):TpcCRequest(WarehouseId,Now){
    public override TpcCTxnKind Kind=>TpcCTxnKind.OrderStatus;
    public override int[] TouchedWarehouses()=>[this.WarehouseId];
}
public sealed record DeliveryRequest(int WarehouseId,long Now,int CarrierId):TpcCRequest(WarehouseId,Now){
    public override TpcCTxnKind Kind=>TpcCTxnKind.Delivery;
    public override int[] TouchedWarehouses()=>[this.WarehouseId];
}
public sealed record StockLevelRequest(int WarehouseId,long Now,int DistrictId,int Threshold):TpcCRequest(WarehouseId,Now){
    public override TpcCTxnKind Kind=>TpcCTxnKind.StockLevel;
    public override int[] TouchedWarehouses()=>[this.WarehouseId];
}
/// <summary>取引の結果。<see cref="RolledBack"/> は業務上の巻き戻し(New-Order の存在しない品目)。それ以外の失敗は例外になる。</summary>
public abstract record TpcCResult(bool RolledBack);
/// <summary>New-Order の結果。TotalAmount は仕様の形だけが計算する(HammerDB は計算しない: Q-032)。</summary>
public sealed record NewOrderResult(bool RolledBack,int OrderId,string CustomerLast,string CustomerCredit,decimal CustomerDiscount,decimal WarehouseTax,decimal DistrictTax,int LineCount,decimal TotalAmount):TpcCResult(RolledBack);
public sealed record PaymentResult(bool RolledBack,int CustomerId,string CustomerFirst,string CustomerLast,string CustomerCredit,decimal CustomerBalance,int HistoryId,string WarehouseName,string DistrictName):TpcCResult(RolledBack);
public readonly record struct OrderStatusLine(int ItemId,int SupplyWarehouseId,int Quantity,decimal Amount,long DeliveryDate);
/// <summary>Order-Status の結果。顧客に注文が無いときは OrderId が 0 で明細も空。</summary>
public sealed record OrderStatusResult(bool RolledBack,int CustomerId,string CustomerFirst,string CustomerLast,decimal CustomerBalance,int OrderId,long OrderEntryDate,int CarrierId,OrderStatusLine[] Lines):TpcCResult(RolledBack);
public readonly record struct DeliveredOrder(int DistrictId,int OrderId,int CustomerId,decimal Total);
/// <summary>Delivery の結果。配達した注文(地区の順)と、新規注文が無くてとばした地区の数。</summary>
public sealed record DeliveryResult(bool RolledBack,DeliveredOrder[] Delivered,int SkippedDistricts):TpcCResult(RolledBack);
public sealed record StockLevelResult(bool RolledBack,int LowStock):TpcCResult(RolledBack);
