namespace Benchmark.TpcC.Core;
/// <summary>一貫性の条件 1 つの検査結果。</summary>
public sealed record ConsistencyResult(int Condition,string Name,long Checked,long Violations,string Detail){
    public bool Passed=>this.Violations==0;
}
/// <summary>
/// TPC-C 5.11 の 3.3.2 の一貫性の条件の全件検査。
/// 条件 1〜4 は必須(<c>deep</c> でなくても調べる)。条件 5〜10・12 は <c>deep</c> のとき、条件 11 は初期状態(<c>initial</c>)のときだけ調べる
/// (条件 11 は配送のたびに崩れるので、初期データの直後にだけ成り立つ)。
/// 地区に新規注文が 1 件も無いとき、条件 2 は D_NEXT_O_ID-1 = max(O_ID) だけを調べ、条件 3 は調べない(max/min が定義されない)。
/// </summary>
public static class TpcCConsistency{
    private const int DetailLimit=5;
    public static IReadOnlyList<ConsistencyResult> Check<TPartition>(TpcCDatabase<TPartition> db,bool deep,bool initial) where TPartition:TpcCPartitionBase{
        var state=new Totals();
        foreach(var partition in db.Partitions){
            foreach(var row in partition.Warehouse.EnumerateRows()){
                state.WarehouseYtd[row.Id]=row.Ytd;
            }
            foreach(var row in partition.District.EnumerateRows()){
                var key=District(row.WarehouseId,row.Id);
                state.DistrictYtd[key]=row.Ytd;
                state.NextOrderId[key]=row.NextOrderId;
            }
            foreach(var row in partition.Order.EnumerateRows()){
                var key=District(row.WarehouseId,row.DistrictId);
                state.OrderCount[key]=state.OrderCount.GetValueOrDefault(key)+1;
                state.OrderLineCountSum[key]=state.OrderLineCountSum.GetValueOrDefault(key)+row.LineCount;
                if(row.Id>state.MaxOrderId.GetValueOrDefault(key)){
                    state.MaxOrderId[key]=row.Id;
                }
                if(deep){
                    state.Orders[Order(row.WarehouseId,row.DistrictId,row.Id)]=new OrderInfo(row.CustomerId,row.CarrierId==0,row.LineCount);
                }
            }
            foreach(var row in partition.NewOrder.EnumerateRows()){
                var key=District(row.WarehouseId,row.DistrictId);
                state.NewOrderCount[key]=state.NewOrderCount.GetValueOrDefault(key)+1;
                if(row.OrderId>state.MaxNewOrderId.GetValueOrDefault(key)){
                    state.MaxNewOrderId[key]=row.OrderId;
                }
                if(!state.MinNewOrderId.TryGetValue(key,out var min)||row.OrderId<min){
                    state.MinNewOrderId[key]=row.OrderId;
                }
                if(deep){
                    state.NewOrders.Add(Order(row.WarehouseId,row.DistrictId,row.OrderId));
                }
            }
            foreach(var row in partition.OrderLine.EnumerateRows()){
                var key=District(row.WarehouseId,row.DistrictId);
                state.OrderLineCount[key]=state.OrderLineCount.GetValueOrDefault(key)+1;
                if(deep){
                    var order=Order(row.WarehouseId,row.DistrictId,row.OrderId);
                    state.LinesOfOrder[order]=state.LinesOfOrder.GetValueOrDefault(order)+1;
                    if((row.DeliveryDate==0)!=(state.Orders.TryGetValue(order,out var info)&&info.CarrierIsNull)){
                        state.DeliveryDateMismatches++;
                        state.DeliveryDateMismatchExamples.Add(order);
                    }
                    if(state.Orders.TryGetValue(order,out var owner)&&row.DeliveryDate!=0){
                        var customer=Customer(row.WarehouseId,row.DistrictId,owner.CustomerId);
                        state.DeliveredAmountOfCustomer[customer]=state.DeliveredAmountOfCustomer.GetValueOrDefault(customer)+row.Amount;
                    }
                }
            }
            foreach(var row in partition.History.EnumerateRows()){
                state.HistoryByWarehouse[row.WarehouseId]=state.HistoryByWarehouse.GetValueOrDefault(row.WarehouseId)+row.Amount;
                var district=District(row.WarehouseId,row.DistrictId);
                state.HistoryByDistrict[district]=state.HistoryByDistrict.GetValueOrDefault(district)+row.Amount;
                if(deep){
                    var customer=Customer(row.CustomerWarehouseId,row.CustomerDistrictId,row.CustomerId);
                    state.HistoryByCustomer[customer]=state.HistoryByCustomer.GetValueOrDefault(customer)+row.Amount;
                }
            }
            if(deep){
                foreach(var row in partition.Customer.EnumerateRows()){
                    state.Customers[Customer(row.WarehouseId,row.DistrictId,row.Id)]=(row.Balance,row.YtdPayment);
                }
            }
        }
        var results=new List<ConsistencyResult>();
        results.Add(Condition1(state));
        results.Add(Condition2(state));
        results.Add(Condition3(state));
        results.Add(Condition4(state));
        if(deep){
            results.Add(Condition5(state));
            results.Add(Condition6(state));
            results.Add(Condition7(state));
            results.Add(Condition8(state));
            results.Add(Condition9(state));
            results.Add(Condition10(state));
        }
        if(initial){
            results.Add(Condition11(state,db.Scale));
        }
        if(deep){
            results.Add(Condition12(state));
        }
        return results;
    }
    private static ConsistencyResult Condition1(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (warehouse,ytd) in state.WarehouseYtd){
            count++;
            var sum=0m;
            foreach(var (key,districtYtd) in state.DistrictYtd){
                if(key>>8==warehouse){
                    sum+=districtYtd;
                }
            }
            if(ytd!=sum){
                violations.Add($"w={warehouse} W_YTD={ytd} sum(D_YTD)={sum}");
            }
        }
        return violations.Result(1,"W_YTD = sum(D_YTD)",count);
    }
    private static ConsistencyResult Condition2(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (district,next) in state.NextOrderId){
            count++;
            var maxOrder=state.MaxOrderId.GetValueOrDefault(district);
            if(next-1!=maxOrder){
                violations.Add($"{Text(district)} D_NEXT_O_ID-1={next-1} max(O_ID)={maxOrder}");
                continue;
            }
            if(state.NewOrderCount.GetValueOrDefault(district)>0&&state.MaxNewOrderId[district]!=maxOrder){
                violations.Add($"{Text(district)} max(O_ID)={maxOrder} max(NO_O_ID)={state.MaxNewOrderId[district]}");
            }
        }
        return violations.Result(2,"D_NEXT_O_ID-1 = max(O_ID) = max(NO_O_ID)",count);
    }
    private static ConsistencyResult Condition3(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (district,rows) in state.NewOrderCount){
            count++;
            var span=state.MaxNewOrderId[district]-state.MinNewOrderId[district]+1;
            if(span!=rows){
                violations.Add($"{Text(district)} max-min+1={span} rows={rows}");
            }
        }
        return violations.Result(3,"max(NO_O_ID)-min(NO_O_ID)+1 = rows of NEW-ORDER",count);
    }
    private static ConsistencyResult Condition4(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (district,sum) in state.OrderLineCountSum){
            count++;
            var lines=state.OrderLineCount.GetValueOrDefault(district);
            if(sum!=lines){
                violations.Add($"{Text(district)} sum(O_OL_CNT)={sum} order-lines={lines}");
            }
        }
        return violations.Result(4,"sum(O_OL_CNT) = rows of ORDER-LINE",count);
    }
    private static ConsistencyResult Condition5(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (order,info) in state.Orders){
            count++;
            if(info.CarrierIsNull!=state.NewOrders.Contains(order)){
                violations.Add($"{OrderText(order)} carrier null={info.CarrierIsNull} in NEW-ORDER={state.NewOrders.Contains(order)}");
            }
        }
        return violations.Result(5,"O_CARRIER_ID is null iff the order is in NEW-ORDER",count);
    }
    private static ConsistencyResult Condition6(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (order,info) in state.Orders){
            count++;
            var lines=state.LinesOfOrder.GetValueOrDefault(order);
            if(info.LineCount!=lines){
                violations.Add($"{OrderText(order)} O_OL_CNT={info.LineCount} order-lines={lines}");
            }
        }
        return violations.Result(6,"O_OL_CNT = rows of ORDER-LINE per order",count);
    }
    private static ConsistencyResult Condition7(Totals state){
        var violations=Collector.Create();
        foreach(var order in state.DeliveryDateMismatchExamples){
            violations.Add($"{OrderText(order)} OL_DELIVERY_D and O_CARRIER_ID disagree");
        }
        return violations.Result(7,"OL_DELIVERY_D is null iff O_CARRIER_ID is null",state.Orders.Count,state.DeliveryDateMismatches);
    }
    private static ConsistencyResult Condition8(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (warehouse,ytd) in state.WarehouseYtd){
            count++;
            var sum=state.HistoryByWarehouse.GetValueOrDefault(warehouse);
            if(ytd!=sum){
                violations.Add($"w={warehouse} W_YTD={ytd} sum(H_AMOUNT)={sum}");
            }
        }
        return violations.Result(8,"W_YTD = sum(H_AMOUNT)",count);
    }
    private static ConsistencyResult Condition9(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (district,ytd) in state.DistrictYtd){
            count++;
            var sum=state.HistoryByDistrict.GetValueOrDefault(district);
            if(ytd!=sum){
                violations.Add($"{Text(district)} D_YTD={ytd} sum(H_AMOUNT)={sum}");
            }
        }
        return violations.Result(9,"D_YTD = sum(H_AMOUNT)",count);
    }
    private static ConsistencyResult Condition10(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (customer,(balance,_)) in state.Customers){
            count++;
            var expected=state.DeliveredAmountOfCustomer.GetValueOrDefault(customer)-state.HistoryByCustomer.GetValueOrDefault(customer);
            if(balance!=expected){
                violations.Add($"{CustomerText(customer)} C_BALANCE={balance} sum(OL_AMOUNT)-sum(H_AMOUNT)={expected}");
            }
        }
        return violations.Result(10,"C_BALANCE = sum(delivered OL_AMOUNT) - sum(H_AMOUNT)",count);
    }
    private static ConsistencyResult Condition11(Totals state,TpcCScale scale){
        var violations=Collector.Create();
        long count=0;
        var expected=scale.FirstInitialNewOrder-1;
        foreach(var (district,orders) in state.OrderCount){
            count++;
            var difference=orders-state.NewOrderCount.GetValueOrDefault(district);
            if(difference!=expected){
                violations.Add($"{Text(district)} rows(ORDER)-rows(NEW-ORDER)={difference} expected={expected}");
            }
        }
        return violations.Result(11,"rows(ORDER) - rows(NEW-ORDER) = delivered orders of the initial population (initial state only)",count);
    }
    private static ConsistencyResult Condition12(Totals state){
        var violations=Collector.Create();
        long count=0;
        foreach(var (customer,(balance,ytdPayment)) in state.Customers){
            count++;
            var delivered=state.DeliveredAmountOfCustomer.GetValueOrDefault(customer);
            if(balance+ytdPayment!=delivered){
                violations.Add($"{CustomerText(customer)} C_BALANCE+C_YTD_PAYMENT={balance+ytdPayment} sum(delivered OL_AMOUNT)={delivered}");
            }
        }
        return violations.Result(12,"C_BALANCE + C_YTD_PAYMENT = sum(delivered OL_AMOUNT)",count);
    }
    // (倉庫, 地区)を 1 つの long に詰める。
    private static long District(int warehouse,int district)=>((long)warehouse<<8)|(uint)district;
    private static long Order(int warehouse,int district,int order)=>((long)warehouse<<36)|((long)district<<32)|(uint)order;
    private static long Customer(int warehouse,int district,int customer)=>Order(warehouse,district,customer);
    private static string Text(long district)=>$"w={district>>8} d={district&0xFF}";
    private static string OrderText(long order)=>$"w={order>>36} d={(order>>32)&0xF} o={(uint)order}";
    private static string CustomerText(long customer)=>$"w={customer>>36} d={(customer>>32)&0xF} c={(uint)customer}";
    private readonly record struct OrderInfo(int CustomerId,bool CarrierIsNull,int LineCount);
    private sealed class Totals{
        public readonly Dictionary<int,decimal> WarehouseYtd=[];
        public readonly Dictionary<long,decimal> DistrictYtd=[];
        public readonly Dictionary<long,int> NextOrderId=[];
        public readonly Dictionary<long,int> OrderCount=[];
        public readonly Dictionary<long,int> OrderLineCountSum=[];
        public readonly Dictionary<long,int> MaxOrderId=[];
        public readonly Dictionary<long,int> NewOrderCount=[];
        public readonly Dictionary<long,int> MaxNewOrderId=[];
        public readonly Dictionary<long,int> MinNewOrderId=[];
        public readonly Dictionary<long,int> OrderLineCount=[];
        public readonly Dictionary<int,decimal> HistoryByWarehouse=[];
        public readonly Dictionary<long,decimal> HistoryByDistrict=[];
        public readonly Dictionary<long,OrderInfo> Orders=[];
        public readonly HashSet<long> NewOrders=[];
        public readonly Dictionary<long,int> LinesOfOrder=[];
        public readonly Dictionary<long,decimal> DeliveredAmountOfCustomer=[];
        public readonly Dictionary<long,decimal> HistoryByCustomer=[];
        public readonly Dictionary<long,(decimal Balance,decimal YtdPayment)> Customers=[];
        public long DeliveryDateMismatches;
        public readonly List<long> DeliveryDateMismatchExamples=[];
    }
    private sealed class Collector{
        private readonly List<string> examples=[];
        private long total;
        public static Collector Create()=>new();
        public void Add(string detail){
            this.total++;
            if(this.examples.Count<DetailLimit){
                this.examples.Add(detail);
            }
        }
        public ConsistencyResult Result(int condition,string name,long checkedCount){
            return this.Result(condition,name,checkedCount,this.total);
        }
        public ConsistencyResult Result(int condition,string name,long checkedCount,long violations){
            return new ConsistencyResult(condition,name,checkedCount,violations,string.Join("; ",this.examples));
        }
    }
}
