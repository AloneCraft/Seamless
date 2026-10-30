namespace Shared.TpcC.Transactions;
// LINQ は *Queries にだけ書き、*Frames から呼ぶ(FLinq は「LINQ を含むメソッドの呼び出し箇所」を置き換えるので、同じアセンブリに置く)。
/// <summary>姓で顧客を引くときの選び方(Payment と Order-Status)。</summary>
internal sealed class CustomerQueries{
    /// <summary>名の昇順に並んだ同じ姓の顧客 n 人のうち、ceil(n/2) 番目(1 から数える)の顧客 ID(仕様 2.5.2.2・2.6.2.2、HammerDB は Q-019 でこの位置)。</summary>
    public int PickByName(CustomerNameEntry[] entries){
        return entries.Select(entry=>(int)entry.CustomerId).ElementAt((entries.Length-1)/2);
    }
}
internal sealed class NewOrderQueries{
    /// <summary>注文の合計金額 = 明細の金額の和 × (1 − 顧客の割引) × (1 + 倉庫の税 + 地区の税)(仕様 2.4.2.2。HammerDB は計算しない: Q-032)。</summary>
    public decimal Total(decimal[] amounts,decimal discount,decimal warehouseTax,decimal districtTax){
        return amounts.Sum()*(1m-discount)*(1m+warehouseTax+districtTax);
    }
}
internal sealed class OrderStatusQueries{
    public OrderStatusLine[] ToLines(OrderLineRow[] rows){
        return rows.Select(row=>new OrderStatusLine(row.ItemId,row.SupplyWarehouseId,row.Quantity,row.Amount,row.DeliveryDate)).ToArray();
    }
}
internal sealed class DeliveryQueries{
    /// <summary>配達した注文の明細の金額の合計。HammerDB は合計を整数型の変数に足し込むので、明細ごとに整数へ丸めて足したものになる(Q-040)。</summary>
    public decimal RoundedTotal(OrderLineRow[] lines){
        return lines.Sum(line=>Math.Round(line.Amount,0,MidpointRounding.AwayFromZero));
    }
    /// <summary>仕様 2.7.4.2: 明細の金額の正確な合計。</summary>
    public decimal ExactTotal(OrderLineRow[] lines){
        return lines.Sum(line=>line.Amount);
    }
}
internal sealed class StockLevelQueries{
    /// <summary>直近の注文の明細に出てくる品目(重複なし)。</summary>
    public int[] DistinctItems(OrderLineRow[] lines){
        return lines.Select(line=>line.ItemId).Distinct().ToArray();
    }
    /// <summary>在庫数がしきい値より少ない品目の数。</summary>
    public int LowStock(StockRow[] stocks,int threshold){
        return stocks.Count(stock=>stock.Quantity<threshold);
    }
}
internal sealed class AdminQueries{
    /// <summary>区画の全地区の D_NEXT_O_ID の合計(NOPM の測り方)。</summary>
    public long SumNextOrderIds(TpcCPartition partition){
        return partition.District.Rows.Sum(row=>(long)row.NextOrderId);
    }
}
