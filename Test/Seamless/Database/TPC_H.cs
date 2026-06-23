using Seamless.Tsavorite.Stores;
using System.Globalization;
using System.Linq;
using Tsavorite.core;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
#pragma warning disable CS8981

namespace Test.Seamless.Database.TPC_H {
    // DatabaseGenerator marker ? Keys / Records 繝ｻ・ｽ繝ｻ・ｽ schema struct 繝ｻ・ｽ繝ｻ・ｽ namespace Keys / Records 繝ｻ・ｽ・画慣・ｿ・ｽ繝ｻ・ｽ・壼ｮ｣險繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
    // source generator 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ{繝ｻ・ｽC繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ[繝ｻ・ｽR繝ｻ・ｽ[繝ｻ・ｽh (繝ｻ・ｽR繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽX繝ｻ・ｽg繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽN繝ｻ・ｽ^繝ｻ・ｽ[繝ｻ・ｽE_u/_m 繝ｻ・ｽt繝ｻ・ｽB繝ｻ・ｽ[繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽh繝ｻ・ｽEMemoryPack 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ) 繝ｻ・ｽ﨟擾ｽｶ謦ｰ・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
    static partial class DatabaseGenerator;

    [global::Seamless.FLinq.LinqIntercept(false)]
    public sealed class Container:IDisposable {
        private readonly List<IDisposable> disposables = [];

        public Container() {
            regions=CreateTable<Records.region>();
            nations=CreateTable<Records.nation>();
            suppliers=CreateTable<Records.supplier>();
            customers=CreateTable<Records.customer>();
            parts=CreateTable<Records.part>();
            partsupps=CreateTable<Records.partsupp>();
            orders=CreateTable<Records.orders>();
            lineitems=CreateTable<Records.lineitem>();
        }

        public global::Seamless.Tsavorite.Collections.HashSet<Records.region> regions { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.nation> nations { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.supplier> suppliers { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.customer> customers { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.part> parts { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.partsupp> partsupps { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.orders> orders { get; }
        public global::Seamless.Tsavorite.Collections.HashSet<Records.lineitem> lineitems { get; }

        private global::Seamless.Tsavorite.Collections.HashSet<T> CreateTable<T>() {
            var loadCheckpointDir = Path.Combine(Environment.CurrentDirectory,GetType().FullName!);
            Directory.CreateDirectory(loadCheckpointDir);

            var logPath = Path.Combine(loadCheckpointDir,"log");
            var objPath = Path.Combine(loadCheckpointDir,"obj");
            var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(logPath);
            var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(objPath);
            var store = Store.Create(IndexSize: 1L<<12,MemorySize: 1L<<24).Create<T>(logDevice,objDevice);
            var table = store.Create();
            disposables.Add(logDevice);
            disposables.Add(objDevice);
            disposables.Add(store);
            disposables.Add(table);
            return table;
        }

        public void Dispose() {
            for(var index = disposables.Count-1; index>=0; index--) {
                disposables[index].Dispose();
            }
            disposables.Clear();
        }

        public static string Row(int query,params object?[] values) =>
            $"Q{query:D2}|"+string.Join("|",values.Select(FormatValue));

        public static string FormatValue(object? value) => value switch {
            null => "",
            decimal decimalValue => decimalValue.ToString("0.00",CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null,CultureInfo.InvariantCulture)??string.Empty,
            _ => value.ToString()??string.Empty,
        };

        public IEnumerable<string> Q01Method() =>
            lineitems
                .Where(line => line.l_shipdate<=19981201)
                .GroupBy(line => new { line.l_returnflag,line.l_linestatus })
                .OrderBy(group => group.Key.l_returnflag)
                .ThenBy(group => group.Key.l_linestatus)
                .Select(group => Row(1,group.Key.l_returnflag,group.Key.l_linestatus,group.Sum(line => line.l_quantity),group.Sum(line => line.l_extendedprice)));

        public IEnumerable<string> Q01Query() =>
            from line in lineitems
            where line.l_shipdate<=19981201
            group line by new { line.l_returnflag,line.l_linestatus } into grouped
            orderby grouped.Key.l_returnflag, grouped.Key.l_linestatus
            select Row(1,grouped.Key.l_returnflag,grouped.Key.l_linestatus,grouped.Sum(line => line.l_quantity),grouped.Sum(line => line.l_extendedprice));

        public IEnumerable<string> Q02Method() =>
            parts
                .Where(part => part.p_size==15&&part.p_type.Contains("BRASS"))
                .Join(partsupps,part => part.p_partkey,partsupp => partsupp.ps_partkey,(part,partsupp) => new { part,partsupp })
                .Join(suppliers,row => row.partsupp.ps_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.partsupp,supplier })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.part,row.partsupp,row.supplier,nation })
                .Join(regions,row => row.nation.n_regionkey,region => region.r_regionkey,(row,region) => new { row.part,row.partsupp,row.supplier,row.nation,region })
                .Where(row => row.region.r_name=="EUROPE")
                .OrderBy(row => row.partsupp.ps_supplycost)
                .ThenBy(row => row.supplier.s_name)
                .Take(3)
                .Select(row => Row(2,row.supplier.s_name,row.nation.n_name,row.part.p_partkey,row.partsupp.ps_supplycost));

        public IEnumerable<string> Q02Query() =>
            (from part in parts
             join partsupp in partsupps on part.p_partkey equals partsupp.ps_partkey
             join supplier in suppliers on partsupp.ps_suppkey equals supplier.s_suppkey
             join nation in nations on supplier.s_nationkey equals nation.n_nationkey
             join region in regions on nation.n_regionkey equals region.r_regionkey
             where part.p_size==15&&part.p_type.Contains("BRASS")&&region.r_name=="EUROPE"
             orderby partsupp.ps_supplycost, supplier.s_name
             select new { supplier,nation,part,partsupp })
            .Take(3)
            .Select(row => Row(2,row.supplier.s_name,row.nation.n_name,row.part.p_partkey,row.partsupp.ps_supplycost));

        public IEnumerable<string> Q03Method() =>
            customers
                .Where(customer => customer.c_mktsegment=="BUILDING")
                .Join(orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.order,line })
                .Where(row => row.order.o_orderdate<19950315&&row.line.l_shipdate>19950315)
                .GroupBy(row => row.order.o_orderkey)
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(3,group.Key,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q03Query() =>
            from customer in customers
            join order in orders on customer.c_custkey equals order.o_custkey
            join line in lineitems on order.o_orderkey equals line.l_orderkey
            where customer.c_mktsegment=="BUILDING"&&order.o_orderdate<19950315&&line.l_shipdate>19950315
            group new { order,line } by order.o_orderkey into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(3,grouped.Key,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q04Method() =>
            orders
                .Where(order => order.o_orderdate>=19930701&&order.o_orderdate<19931001)
                .Where(order => lineitems.Any(line => line.l_orderkey==order.o_orderkey&&line.l_commitdate<line.l_receiptdate))
                .GroupBy(order => order.o_orderpriority)
                .OrderBy(group => group.Key)
                .Select(group => Row(4,group.Key,group.Count()));

        public IEnumerable<string> Q04Query() =>
            from order in orders
            where order.o_orderdate>=19930701&&order.o_orderdate<19931001
            where lineitems.Any(line => line.l_orderkey==order.o_orderkey&&line.l_commitdate<line.l_receiptdate)
            group order by order.o_orderpriority into grouped
            orderby grouped.Key
            select Row(4,grouped.Key,grouped.Count());

        public IEnumerable<string> Q05Method() =>
            customers
                .Join(orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.customer,row.order,line })
                .Join(suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.customer,row.order,row.line,supplier })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.customer,row.order,row.line,row.supplier,nation })
                .Where(row => row.customer.c_nationkey==row.supplier.s_nationkey&&row.order.o_orderdate>=19940101&&row.order.o_orderdate<19950101)
                .GroupBy(row => row.nation.n_name)
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(5,group.Key,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q05Query() =>
            from customer in customers
            join order in orders on customer.c_custkey equals order.o_custkey
            join line in lineitems on order.o_orderkey equals line.l_orderkey
            join supplier in suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where customer.c_nationkey==supplier.s_nationkey&&order.o_orderdate>=19940101&&order.o_orderdate<19950101
            group new { line } by nation.n_name into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(5,grouped.Key,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q06Method() =>
            lineitems
                .Where(line => line.l_shipdate>=19940101&&line.l_shipdate<19950101&&line.l_discount>=0.05m&&line.l_discount<=0.07m&&line.l_quantity<24m)
                .Select(line => Row(6,line.l_extendedprice*line.l_discount));

        public IEnumerable<string> Q06Query() =>
            from line in lineitems
            where line.l_shipdate>=19940101&&line.l_shipdate<19950101&&line.l_discount>=0.05m&&line.l_discount<=0.07m&&line.l_quantity<24m
            select Row(6,line.l_extendedprice*line.l_discount);

        public IEnumerable<string> Q07Method() =>
            suppliers
                .Join(nations,supplier => supplier.s_nationkey,nation => nation.n_nationkey,(supplier,supplierNation) => new { supplier,supplierNation })
                .Join(lineitems,row => row.supplier.s_suppkey,line => line.l_suppkey,(row,line) => new { row.supplier,row.supplierNation,line })
                .Join(orders,row => row.line.l_orderkey,order => order.o_orderkey,(row,order) => new { row.supplierNation,row.line,order })
                .Join(customers,row => row.order.o_custkey,customer => customer.c_custkey,(row,customer) => new { row.supplierNation,row.line,customer })
                .Join(nations,row => row.customer.c_nationkey,nation => nation.n_nationkey,(row,customerNation) => new { row.supplierNation,customerNation,row.line })
                .Where(row => row.line.l_shipdate>=19950101&&row.line.l_shipdate<=19961231)
                .GroupBy(row => new { Supplier = row.supplierNation.n_name,Customer = row.customerNation.n_name,Year = row.line.l_shipdate/10000 })
                .OrderBy(group => group.Key.Supplier)
                .ThenBy(group => group.Key.Customer)
                .Select(group => Row(7,group.Key.Supplier,group.Key.Customer,group.Key.Year,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q07Query() =>
            from supplier in suppliers
            join supplierNation in nations on supplier.s_nationkey equals supplierNation.n_nationkey
            join line in lineitems on supplier.s_suppkey equals line.l_suppkey
            join order in orders on line.l_orderkey equals order.o_orderkey
            join customer in customers on order.o_custkey equals customer.c_custkey
            join customerNation in nations on customer.c_nationkey equals customerNation.n_nationkey
            where line.l_shipdate>=19950101&&line.l_shipdate<=19961231
            group new { line } by new { Supplier = supplierNation.n_name,Customer = customerNation.n_name,Year = line.l_shipdate/10000 } into grouped
            orderby grouped.Key.Supplier, grouped.Key.Customer
            select Row(7,grouped.Key.Supplier,grouped.Key.Customer,grouped.Key.Year,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q08Method() =>
            parts
                .Where(part => part.p_type.Contains("BRASS"))
                .Join(lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Join(suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.line,supplier })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.part,row.line,row.supplier,nation })
                .GroupBy(row => row.line.l_shipdate/10000)
                .OrderBy(group => group.Key)
                .Select(group => Row(8,group.Key,group.Sum(row => row.nation.n_name=="BRAZIL" ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q08Query() =>
            from part in parts
            join line in lineitems on part.p_partkey equals line.l_partkey
            join supplier in suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where part.p_type.Contains("BRASS")
            group new { line,nation } by line.l_shipdate/10000 into grouped
            orderby grouped.Key
            select Row(8,grouped.Key,grouped.Sum(row => row.nation.n_name=="BRAZIL" ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q09Method() =>
            parts
                .Where(part => part.p_name.Contains("forest"))
                .Join(lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Join(partsupps,row => new { row.line.l_partkey,row.line.l_suppkey },partsupp => new { l_partkey = partsupp.ps_partkey,l_suppkey = partsupp.ps_suppkey },(row,partsupp) => new { row.part,row.line,partsupp })
                .Join(suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.line,row.partsupp,supplier })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.line,row.partsupp,nation })
                .GroupBy(row => new { row.nation.n_name,Year = row.line.l_shipdate/10000 })
                .OrderBy(group => group.Key.n_name)
                .ThenByDescending(group => group.Key.Year)
                .Select(group => Row(9,group.Key.n_name,group.Key.Year,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)-row.partsupp.ps_supplycost*row.line.l_quantity)));

        public IEnumerable<string> Q09Query() =>
            from part in parts
            join line in lineitems on part.p_partkey equals line.l_partkey
            join partsupp in partsupps on new { line.l_partkey,line.l_suppkey } equals new { l_partkey = partsupp.ps_partkey,l_suppkey = partsupp.ps_suppkey }
            join supplier in suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where part.p_name.Contains("forest")
            group new { line,partsupp } by new { nation.n_name,Year = line.l_shipdate/10000 } into grouped
            orderby grouped.Key.n_name, grouped.Key.Year descending
            select Row(9,grouped.Key.n_name,grouped.Key.Year,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)-row.partsupp.ps_supplycost*row.line.l_quantity));

        public IEnumerable<string> Q10Method() =>
            customers
                .Join(orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.customer,row.order,line })
                .Where(row => row.order.o_orderdate>=19931001&&row.order.o_orderdate<19940101&&row.line.l_returnflag=="R")
                .GroupBy(row => new { row.customer.c_custkey,row.customer.c_name })
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(10,group.Key.c_custkey,group.Key.c_name,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q10Query() =>
            from customer in customers
            join order in orders on customer.c_custkey equals order.o_custkey
            join line in lineitems on order.o_orderkey equals line.l_orderkey
            where order.o_orderdate>=19931001&&order.o_orderdate<19940101&&line.l_returnflag=="R"
            group new { line } by new { customer.c_custkey,customer.c_name } into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(10,grouped.Key.c_custkey,grouped.Key.c_name,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q11Method() =>
            partsupps
                .Join(suppliers,partsupp => partsupp.ps_suppkey,supplier => supplier.s_suppkey,(partsupp,supplier) => new { partsupp,supplier })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.partsupp,nation })
                .Where(row => row.nation.n_name=="GERMANY")
                .GroupBy(row => row.partsupp.ps_partkey)
                .OrderByDescending(group => group.Sum(row => row.partsupp.ps_supplycost*row.partsupp.ps_availqty))
                .Select(group => Row(11,group.Key,group.Sum(row => row.partsupp.ps_supplycost*row.partsupp.ps_availqty)));

        public IEnumerable<string> Q11Query() =>
            from partsupp in partsupps
            join supplier in suppliers on partsupp.ps_suppkey equals supplier.s_suppkey
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where nation.n_name=="GERMANY"
            group partsupp by partsupp.ps_partkey into grouped
            orderby grouped.Sum(partsupp => partsupp.ps_supplycost*partsupp.ps_availqty) descending
            select Row(11,grouped.Key,grouped.Sum(partsupp => partsupp.ps_supplycost*partsupp.ps_availqty));

        public IEnumerable<string> Q12Method() =>
            orders
                .Join(lineitems,order => order.o_orderkey,line => line.l_orderkey,(order,line) => new { order,line })
                .Where(row => (row.line.l_shipmode=="MAIL"||row.line.l_shipmode=="SHIP")&&row.line.l_receiptdate>=19940101&&row.line.l_receiptdate<19950101)
                .GroupBy(row => row.line.l_shipmode)
                .OrderBy(group => group.Key)
                .Select(group => Row(12,group.Key,group.Count(row => row.order.o_orderpriority=="1-URGENT"||row.order.o_orderpriority=="2-HIGH"),group.Count(row => row.order.o_orderpriority!="1-URGENT"&&row.order.o_orderpriority!="2-HIGH")));

        public IEnumerable<string> Q12Query() =>
            from order in orders
            join line in lineitems on order.o_orderkey equals line.l_orderkey
            where (line.l_shipmode=="MAIL"||line.l_shipmode=="SHIP")&&line.l_receiptdate>=19940101&&line.l_receiptdate<19950101
            group new { order,line } by line.l_shipmode into grouped
            orderby grouped.Key
            select Row(12,grouped.Key,grouped.Count(row => row.order.o_orderpriority=="1-URGENT"||row.order.o_orderpriority=="2-HIGH"),grouped.Count(row => row.order.o_orderpriority!="1-URGENT"&&row.order.o_orderpriority!="2-HIGH"));

        public IEnumerable<string> Q13Method() =>
            customers
                .GroupJoin(this.orders.Where(order => !order.o_comment.Contains("special requests")),customer => customer.c_custkey,order => order.o_custkey,(customer,orders) => new { customer,OrderCount = orders.Count() })
                .GroupBy(row => row.OrderCount)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key)
                .Select(group => Row(13,group.Key,group.Count()));

        public IEnumerable<string> Q13Query() =>
            from customer in customers
            join order in orders.Where(order => !order.o_comment.Contains("special requests")) on customer.c_custkey equals order.o_custkey into customerOrders
            let orderCount = customerOrders.Count()
            group customer by orderCount into grouped
            orderby grouped.Count() descending, grouped.Key descending
            select Row(13,grouped.Key,grouped.Count());

        public IEnumerable<string> Q14Method() =>
            parts
                .Join(lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.line.l_shipdate>=19950901&&row.line.l_shipdate<19951001)
                .GroupBy(_ => 1)
                .Select(group => Row(14,group.Sum(row => row.part.p_type.StartsWith("PROMO") ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q14Query() =>
            from part in parts
            join line in lineitems on part.p_partkey equals line.l_partkey
            where line.l_shipdate>=19950901&&line.l_shipdate<19951001
            group new { part,line } by 1 into grouped
            select Row(14,grouped.Sum(row => row.part.p_type.StartsWith("PROMO") ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q15Method() =>
            suppliers
                .Join(lineitems.Where(line => line.l_shipdate>=19960101&&line.l_shipdate<19960401),supplier => supplier.s_suppkey,line => line.l_suppkey,(supplier,line) => new { supplier,line })
                .GroupBy(row => new { row.supplier.s_suppkey,row.supplier.s_name })
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Take(1)
                .Select(group => Row(15,group.Key.s_suppkey,group.Key.s_name,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public IEnumerable<string> Q15Query() =>
            (from supplier in suppliers
             join line in lineitems.Where(line => line.l_shipdate>=19960101&&line.l_shipdate<19960401) on supplier.s_suppkey equals line.l_suppkey
             group new { line } by new { supplier.s_suppkey,supplier.s_name } into grouped
             orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
             select new { grouped.Key.s_suppkey,grouped.Key.s_name,Revenue = grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) })
            .Take(1)
            .Select(row => Row(15,row.s_suppkey,row.s_name,row.Revenue));

        public IEnumerable<string> Q16Method() =>
            parts
                .Where(part => part.p_brand!="Brand#45"&&!part.p_type.StartsWith("MEDIUM POLISHED")&&part.p_size>=1)
                .Join(partsupps,part => part.p_partkey,partsupp => partsupp.ps_partkey,(part,partsupp) => new { part,partsupp })
                .GroupBy(row => new { row.part.p_brand,row.part.p_type,row.part.p_size })
                .OrderByDescending(group => group.Select(row => row.partsupp.ps_suppkey).Distinct().Count())
                .Select(group => Row(16,group.Key.p_brand,group.Key.p_type,group.Key.p_size,group.Select(row => row.partsupp.ps_suppkey).Distinct().Count()));

        public IEnumerable<string> Q16Query() =>
            from part in parts
            join partsupp in partsupps on part.p_partkey equals partsupp.ps_partkey
            where part.p_brand!="Brand#45"&&!part.p_type.StartsWith("MEDIUM POLISHED")&&part.p_size>=1
            group new { partsupp } by new { part.p_brand,part.p_type,part.p_size } into grouped
            orderby grouped.Select(row => row.partsupp.ps_suppkey).Distinct().Count() descending
            select Row(16,grouped.Key.p_brand,grouped.Key.p_type,grouped.Key.p_size,grouped.Select(row => row.partsupp.ps_suppkey).Distinct().Count());

        public IEnumerable<string> Q17Method() =>
            parts
                .Where(part => part.p_brand=="Brand#23"&&part.p_container=="MED BOX")
                .Join(lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.line.l_quantity<lineitems.Where(line => line.l_partkey==row.part.p_partkey).Average(line => line.l_quantity)*0.2m)
                .Select(row => Row(17,row.part.p_partkey,row.line.l_extendedprice/7m));

        public IEnumerable<string> Q17Query() =>
            from part in parts
            join line in lineitems on part.p_partkey equals line.l_partkey
            where part.p_brand=="Brand#23"&&part.p_container=="MED BOX"
            where line.l_quantity<lineitems.Where(inner => inner.l_partkey==part.p_partkey).Average(inner => inner.l_quantity)*0.2m
            select Row(17,part.p_partkey,line.l_extendedprice/7m);

        public IEnumerable<string> Q18Method() =>
            orders
                .Join(customers,order => order.o_custkey,customer => customer.c_custkey,(order,customer) => new { order,customer })
                .Where(row => lineitems.Where(line => line.l_orderkey==row.order.o_orderkey).Sum(line => line.l_quantity)>20m)
                .OrderByDescending(row => row.order.o_totalprice)
                .Select(row => Row(18,row.customer.c_name,row.order.o_orderkey,row.order.o_totalprice,lineitems.Where(line => line.l_orderkey==row.order.o_orderkey).Sum(line => line.l_quantity)));

        public IEnumerable<string> Q18Query() =>
            from order in orders
            join customer in customers on order.o_custkey equals customer.c_custkey
            let quantity = lineitems.Where(line => line.l_orderkey==order.o_orderkey).Sum(line => line.l_quantity)
            where quantity>20m
            orderby order.o_totalprice descending
            select Row(18,customer.c_name,order.o_orderkey,order.o_totalprice,quantity);

        public IEnumerable<string> Q19Method() =>
            parts
                .Join(lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.part.p_brand=="Brand#12"&&row.part.p_container.Contains("BOX")&&row.line.l_quantity>=1m&&row.line.l_quantity<=11m&&row.line.l_shipmode=="AIR")
                .Select(row => Row(19,row.part.p_brand,row.line.l_extendedprice*(1-row.line.l_discount)));

        public IEnumerable<string> Q19Query() =>
            from part in parts
            join line in lineitems on part.p_partkey equals line.l_partkey
            where part.p_brand=="Brand#12"&&part.p_container.Contains("BOX")&&line.l_quantity>=1m&&line.l_quantity<=11m&&line.l_shipmode=="AIR"
            select Row(19,part.p_brand,line.l_extendedprice*(1-line.l_discount));

        public IEnumerable<string> Q20Method() =>
            suppliers
                .Join(nations,supplier => supplier.s_nationkey,nation => nation.n_nationkey,(supplier,nation) => new { supplier,nation })
                .Where(row => row.nation.n_name=="CANADA")
                .Where(row => partsupps.Any(partsupp => partsupp.ps_suppkey==row.supplier.s_suppkey&&parts.Any(part => part.p_partkey==partsupp.ps_partkey&&part.p_name.StartsWith("forest"))))
                .OrderBy(row => row.supplier.s_name)
                .Select(row => Row(20,row.supplier.s_name,row.nation.n_name));

        public IEnumerable<string> Q20Query() =>
            from supplier in suppliers
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where nation.n_name=="CANADA"
            where partsupps.Any(partsupp => partsupp.ps_suppkey==supplier.s_suppkey&&parts.Any(part => part.p_partkey==partsupp.ps_partkey&&part.p_name.StartsWith("forest")))
            orderby supplier.s_name
            select Row(20,supplier.s_name,nation.n_name);

        public IEnumerable<string> Q21Method() =>
            suppliers
                .Join(lineitems,supplier => supplier.s_suppkey,line => line.l_suppkey,(supplier,line) => new { supplier,line })
                .Join(orders,row => row.line.l_orderkey,order => order.o_orderkey,(row,order) => new { row.supplier,row.line,order })
                .Join(nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.supplier,row.line,row.order,nation })
                .Where(row => row.order.o_orderstatus=="F"&&row.line.l_receiptdate>row.line.l_commitdate&&row.nation.n_name=="SAUDI ARABIA")
                .GroupBy(row => row.supplier.s_name)
                .OrderByDescending(group => group.Count())
                .Select(group => Row(21,group.Key,group.Count()));

        public IEnumerable<string> Q21Query() =>
            from supplier in suppliers
            join line in lineitems on supplier.s_suppkey equals line.l_suppkey
            join order in orders on line.l_orderkey equals order.o_orderkey
            join nation in nations on supplier.s_nationkey equals nation.n_nationkey
            where order.o_orderstatus=="F"&&line.l_receiptdate>line.l_commitdate&&nation.n_name=="SAUDI ARABIA"
            group supplier by supplier.s_name into grouped
            orderby grouped.Count() descending
            select Row(21,grouped.Key,grouped.Count());

        public IEnumerable<string> Q22Method() =>
            customers
                .Where(customer => customer.c_acctbal>0m&&(customer.c_phone.StartsWith("13")||customer.c_phone.StartsWith("31")))
                .GroupBy(customer => customer.c_phone[..2])
                .OrderBy(group => group.Key)
                .Select(group => Row(22,group.Key,group.Count(),group.Sum(customer => customer.c_acctbal)));

        public IEnumerable<string> Q22Query() =>
            from customer in customers
            where customer.c_acctbal>0m&&(customer.c_phone.StartsWith("13")||customer.c_phone.StartsWith("31"))
            group customer by customer.c_phone[..2] into grouped
            orderby grouped.Key
            select Row(22,grouped.Key,grouped.Count(),grouped.Sum(customer => customer.c_acctbal));
    }

    public sealed class ContainerFixture:IDisposable {
        public Container Container { get; } = TpchTests.Load();
        public void Dispose() => Container.Dispose();
    }

    [Collection("TpchCollection")]
    public sealed class TpchTests(ContainerFixture fixture):IClassFixture<ContainerFixture> {
        private const long SF = 1;
        private Container Container => fixture.Container;
        internal static Container Load() {
            var container = new Container();

            // TPC-H v3.0.1 繝ｻ・ｽ・梧ｧ九・・ｽ繝ｻ・ｽ繝ｻ・ｽE繝ｻ・ｽ闌・ｽｦ繝ｻ・ｽ・画誓・ｿ・ｽ繝ｻ・ｽ蠅ｲ・ｹ繝ｻ・ｽ繝ｻ・ｽB繝ｻ・ｽe繝ｻ・ｽX繝ｻ・ｽg繝ｻ・ｽ・崎ｶ｣・ｿ・ｽ繝ｻ・ｽs繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・斐・繧托ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ 1/100 繝ｻ・ｽX繝ｻ・ｽP繝ｻ・ｽ[繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・・腸・ｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
            const int unitScaleDivisor = 1000;
            var supplierCount = (int)Math.Max(5,SF*10000/unitScaleDivisor);
            var customerCount = (int)Math.Max(10,SF*150000/unitScaleDivisor);
            var partCount = (int)Math.Max(20,SF*200000/unitScaleDivisor);
            var orderCount = (int)Math.Max(50,SF*1500000/unitScaleDivisor);

            var random = new Random(20260502);
            var regions = new[] {
                (0,"AFRICA"),
                (1,"AMERICA"),
                (2,"ASIA"),
                (3,"EUROPE"),
                (4,"MIDDLE EAST"),
            };
            foreach(var region in regions) {
                container.regions.Add(new Records.region(region.Item1,region.Item2,"tpch region"));
            }

            var nations = Nations;
            foreach(var nation in nations) {
                container.nations.Add(new Records.nation(nation.Key,nation.Name,nation.RegionKey,"tpch nation"));
            }

            for(var supplierKey = 1; supplierKey<=supplierCount; supplierKey++) {
                var nationKey = nations[random.Next(nations.Length)].Key;
                var item = new Records.supplier(
                    supplierKey,
                    $"Supplier#{supplierKey:D9}",
                    BuildAddress(random),
                    nationKey,
                    BuildPhone(random,nationKey),
                    BuildAccountBalance(random),
                    supplierKey%37==0 ? "Customer Complaints" : "tpch supplier");
                container.suppliers.Add(item);
            }

            for(var customerKey = 1; customerKey<=customerCount; customerKey++) {
                var nationKey = nations[random.Next(nations.Length)].Key;
                container.customers.Add(new Records.customer(
                    customerKey,
                    $"Customer#{customerKey:D9}",
                    BuildAddress(random),
                    nationKey,
                    BuildPhone(random,nationKey),
                    BuildAccountBalance(random),
                    Segments[random.Next(Segments.Length)],
                    customerKey%29==0 ? "special requests" : "tpch customer"));
            }

            var partSuppliers = new Dictionary<int,int[]>(partCount);
            for(var partKey = 1; partKey<=partCount; partKey++) {
                var partName = partKey%7==0 ? $"forest part {partKey}" : $"part {partKey}";
                var brand = $"Brand#{(partKey%55)+1:D2}";
                var containerName = Containers[random.Next(Containers.Length)];
                var type = PartTypes[random.Next(PartTypes.Length)];
                container.parts.Add(new Records.part(
                    partKey,
                    partName,
                    $"Manufacturer#{(partKey%5)+1}",
                    brand,
                    type,
                    (partKey%50)+1,
                    containerName,
                    RetailPrice(partKey),
                    "tpch part"));

                var suppKeys = new int[4];
                for(var slot = 0; slot<suppKeys.Length; slot++) {
                    var suppKey = ((partKey*37)+(slot*53))%supplierCount+1;
                    while(Array.IndexOf(suppKeys,suppKey)>=0) {
                        suppKey=suppKey==supplierCount ? 1 : suppKey+1;
                    }
                    suppKeys[slot]=suppKey;
                    container.partsupps.Add(new Records.partsupp(
                        partKey,
                        suppKey,
                        random.Next(1,10000),
                        BuildSupplyCost(random),
                        "tpch partsupp"));
                }
                partSuppliers[partKey]=suppKeys;
            }

            for(var orderKey = 1; orderKey<=orderCount; orderKey++) {
                var customerKey = random.Next(1,customerCount+1);
                var orderDate = RandomDateInt(random,new DateTime(1992,1,1),new DateTime(1998,8,2));
                var lineCount = random.Next(1,8);
                decimal total = 0m;
                for(var lineNumber = 1; lineNumber<=lineCount; lineNumber++) {
                    var partKey = random.Next(1,partCount+1);
                    var suppChoices = partSuppliers[partKey];
                    var suppKey = suppChoices[random.Next(suppChoices.Length)];
                    var quantity = random.Next(1,51);
                    var extendedPrice = Math.Round(RetailPrice(partKey)*quantity,2,MidpointRounding.AwayFromZero);
                    var discount = Math.Round(random.Next(0,11)/100m,2,MidpointRounding.AwayFromZero);
                    var tax = Math.Round(random.Next(0,9)/100m,2,MidpointRounding.AwayFromZero);
                    var shipDate = AddDays(orderDate,random.Next(1,121));
                    var commitDate = AddDays(orderDate,random.Next(1,90));
                    var receiptDate = AddDays(shipDate,random.Next(1,31));
                    if(commitDate>=receiptDate) {
                        commitDate=AddDays(receiptDate,-1);
                    }

                    total+=extendedPrice*(1m-discount)*(1m+tax);
                    container.lineitems.Add(new Records.lineitem(
                        orderKey,
                        partKey,
                        suppKey,
                        lineNumber,
                        quantity,
                        extendedPrice,
                        discount,
                        tax,
                        ReturnFlags[random.Next(ReturnFlags.Length)],
                        shipDate<=19980902 ? "F" : "O",
                        shipDate,
                        commitDate,
                        receiptDate,
                        ShipInstructs[random.Next(ShipInstructs.Length)],
                        ShipModes[random.Next(ShipModes.Length)],
                        "tpch lineitem"));
                }

                var orderStatus = orderKey%3==0 ? "F" : (orderKey%3==1 ? "O" : "P");
                container.orders.Add(new Records.orders(
                    orderKey,
                    customerKey,
                    orderStatus,
                    Math.Round(total,2,MidpointRounding.AwayFromZero),
                    orderDate,
                    Priorities[random.Next(Priorities.Length)],
                    $"Clerk#{(orderKey%1000):D3}",
                    0,
                    orderKey%17==0 ? "special requests" : "tpch order"));
            }
            return container;
        }

        private static readonly (int Key,string Name,int RegionKey)[] Nations = [
            (1,"ALGERIA",0),(2,"ARGENTINA",1),(3,"BRAZIL",1),(4,"CANADA",1),(5,"EGYPT",4),
            (6,"ETHIOPIA",0),(7,"FRANCE",3),(8,"GERMANY",3),(9,"INDIA",2),(10,"INDONESIA",2),
            (11,"IRAN",4),(12,"IRAQ",4),(13,"JAPAN",2),(14,"JORDAN",4),(15,"KENYA",0),
            (16,"MOROCCO",0),(17,"MOZAMBIQUE",0),(18,"PERU",1),(19,"CHINA",2),(20,"ROMANIA",3),
            (21,"SAUDI ARABIA",4),(22,"VIETNAM",2),(23,"RUSSIA",3),(24,"UNITED KINGDOM",3),(25,"UNITED STATES",1)
        ];

        private static readonly string[] Segments =["AUTOMOBILE","BUILDING","FURNITURE","HOUSEHOLD","MACHINERY"];
        private static readonly string[] Priorities =["1-URGENT","2-HIGH","3-MEDIUM","4-NOT SPECIFIED","5-LOW"];
        private static readonly string[] ReturnFlags =["R","A","N"];
        private static readonly string[] ShipModes =["REG AIR","AIR","RAIL","SHIP","TRUCK","MAIL","FOB"];
        private static readonly string[] ShipInstructs =["DELIVER IN PERSON","COLLECT COD","NONE","TAKE BACK RETURN"];
        private static readonly string[] Containers =["SM BOX","SM PACK","MED BOX","MED BAG","LG BOX","JUMBO PACK"];
        private static readonly string[] PartTypes = [
            "SMALL BRASS","MEDIUM POLISHED","LARGE ANODIZED","PROMO BURNISHED","ECONOMY BRASS","STANDARD POLISHED"
        ];

        private static string BuildAddress(Random random) {
            return $"Addr-{random.Next(100000,999999):D6}";
        }

        private static string BuildPhone(Random random,int nationKey) {
            var country = 10+(nationKey%90);
            return $"{country:D2}-{random.Next(100,1000):D3}-{random.Next(1000,10000):D4}";
        }

        private static decimal BuildAccountBalance(Random random) {
            return Math.Round((decimal)random.NextDouble()*11000m-1000m,2,MidpointRounding.AwayFromZero);
        }

        private static decimal BuildSupplyCost(Random random) {
            return Math.Round((decimal)random.NextDouble()*999m+1m,2,MidpointRounding.AwayFromZero);
        }

        private static decimal RetailPrice(int partKey) {
            return Math.Round(900m+(partKey*0.37m),2,MidpointRounding.AwayFromZero);
        }

        private static int RandomDateInt(Random random,DateTime start,DateTime endInclusive) {
            var days = (endInclusive-start).Days;
            var offset = random.Next(0,days+1);
            var date = start.AddDays(offset);
            return (date.Year*10000)+(date.Month*100)+date.Day;
        }

        private static int AddDays(int yyyymmdd,int days) {
            var year = yyyymmdd/10000;
            var month = (yyyymmdd/100)%100;
            var day = yyyymmdd%100;
            var date = new DateTime(year,month,day).AddDays(days);
            return (date.Year*10000)+(date.Month*100)+date.Day;
        }

        private static string[] QueryRows(Container container,int query,bool querySyntax) {
            var prefix = query<10 ? "Q0" : "Q";
            var suffix = querySyntax ? "Query" : "Method";
            var methodName = $"{prefix}{query}{suffix}";
            var method = typeof(Container).GetMethod(methodName,global::System.Reflection.BindingFlags.Instance|global::System.Reflection.BindingFlags.Public)
                ?? throw new ArgumentOutOfRangeException(nameof(query));
            var rows = (IEnumerable<string>)(method.Invoke(container,[]) ?? Array.Empty<string>());
            var result = rows.ToArray();
            Array.Sort(result,StringComparer.Ordinal);
            return result;
        }

        private void AssertMethodAndQuery(int query) {
            var method = QueryRows(Container,query,false);
            var queryResult = QueryRows(Container,query,true);
            Assert.Equal(method,queryResult);
        }

        private void AssertStorageListRoundTrip(int query) {
            var expected = QueryRows(Container,query,false);
            var folder = TestPath.Directory<TpchTests>($"Q{query:D2}");
            using(var list = global::Seamless.Persisted.Collections.Store.Create().Create<string>(Path.Combine(folder,"result.data"),initialCapacity: 8,initialDataCapacity: 4096)) {
                foreach(var row in expected) {
                    list.Add(row);
                }
            }
            using(var list = global::Seamless.Persisted.Collections.Store.Create().Create<string>(Path.Combine(folder,"result.data"),initialCapacity: 8,initialDataCapacity: 4096)) {
                var actual = list.OrderBy(row => row,StringComparer.Ordinal).ToArray();
                Assert.Equal(expected,actual);
            }
        }

        private void AssertInterceptedMatchesReflection(int query,bool querySyntax) {
            var expected = QueryRows(Container,query,querySyntax);
            var actual = (querySyntax ? DirectQuery(Container,query) : DirectMethod(Container,query)).ToArray();
            Array.Sort(actual,StringComparer.Ordinal);
            Assert.Equal(expected,actual);
        }

        private static IEnumerable<string> DirectMethod(Container c,int query) => query switch {
            1 => c.Q01Method(),
            2 => c.Q02Method(),
            3 => c.Q03Method(),
            4 => c.Q04Method(),
            5 => c.Q05Method(),
            6 => c.Q06Method(),
            7 => c.Q07Method(),
            8 => c.Q08Method(),
            9 => c.Q09Method(),
            10 => c.Q10Method(),
            11 => c.Q11Method(),
            12 => c.Q12Method(),
            13 => c.Q13Method(),
            14 => c.Q14Method(),
            15 => c.Q15Method(),
            16 => c.Q16Method(),
            17 => c.Q17Method(),
            18 => c.Q18Method(),
            19 => c.Q19Method(),
            20 => c.Q20Method(),
            21 => c.Q21Method(),
            22 => c.Q22Method(),
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        private static IEnumerable<string> DirectQuery(Container c,int query) => query switch {
            1 => c.Q01Query(),
            2 => c.Q02Query(),
            3 => c.Q03Query(),
            4 => c.Q04Query(),
            5 => c.Q05Query(),
            6 => c.Q06Query(),
            7 => c.Q07Query(),
            8 => c.Q08Query(),
            9 => c.Q09Query(),
            10 => c.Q10Query(),
            11 => c.Q11Query(),
            12 => c.Q12Query(),
            13 => c.Q13Query(),
            14 => c.Q14Query(),
            15 => c.Q15Query(),
            16 => c.Q16Query(),
            17 => c.Q17Query(),
            18 => c.Q18Query(),
            19 => c.Q19Query(),
            20 => c.Q20Query(),
            21 => c.Q21Query(),
            22 => c.Q22Query(),
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        [Fact] public void Q01_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(1,false);
        [Fact] public void Q02_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(2,false);
        [Fact] public void Q03_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(3,false);
        [Fact] public void Q04_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(4,false);
        [Fact] public void Q05_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(5,false);
        [Fact] public void Q06_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(6,false);
        [Fact] public void Q07_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(7,false);
        [Fact] public void Q08_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(8,false);
        [Fact] public void Q09_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(9,false);
        [Fact] public void Q10_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(10,false);
        [Fact] public void Q11_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(11,false);
        [Fact] public void Q12_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(12,false);
        [Fact] public void Q13_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(13,false);
        [Fact] public void Q14_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(14,false);
        [Fact] public void Q15_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(15,false);
        [Fact] public void Q16_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(16,false);
        [Fact] public void Q17_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(17,false);
        [Fact] public void Q18_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(18,false);
        [Fact] public void Q19_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(19,false);
        [Fact] public void Q20_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(20,false);
        [Fact] public void Q21_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(21,false);
        [Fact] public void Q22_DirectMethod_MatchesReflection() => AssertInterceptedMatchesReflection(22,false);

        [Fact] public void Q01_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(1,true);
        [Fact] public void Q02_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(2,true);
        [Fact] public void Q03_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(3,true);
        [Fact] public void Q04_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(4,true);
        [Fact] public void Q05_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(5,true);
        [Fact] public void Q06_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(6,true);
        [Fact] public void Q07_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(7,true);
        [Fact] public void Q08_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(8,true);
        [Fact] public void Q09_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(9,true);
        [Fact] public void Q10_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(10,true);
        [Fact] public void Q11_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(11,true);
        [Fact] public void Q12_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(12,true);
        [Fact] public void Q13_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(13,true);
        [Fact] public void Q14_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(14,true);
        [Fact] public void Q15_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(15,true);
        [Fact] public void Q16_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(16,true);
        [Fact] public void Q17_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(17,true);
        [Fact] public void Q18_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(18,true);
        [Fact] public void Q19_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(19,true);
        [Fact] public void Q20_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(20,true);
        [Fact] public void Q21_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(21,true);
        [Fact] public void Q22_DirectQuery_MatchesReflection() => AssertInterceptedMatchesReflection(22,true);
    }
    namespace Keys {
        public partial struct region {
            public partial int r_regionkey { get; }
        }
        public partial struct nation {
            public partial int n_nationkey { get; }
        }
        public partial struct supplier {
            public partial int s_suppkey { get; }
        }
        public partial struct customer {
            public partial int c_custkey { get; }
        }
        public partial struct part {
            public partial int p_partkey { get; }
        }
        public partial struct partsupp {
            public partial int ps_partkey { get; }
            public partial int ps_suppkey { get; }
        }
        public partial struct orders {
            public partial int o_orderkey { get; }
        }
        public partial struct lineitem {
            public partial int l_orderkey { get; }
            public partial int l_linenumber { get; }
        }
    }

    // TPC-H 繝ｻ・ｽ繝ｻ・ｽ 8 繝ｻ・ｽe繝ｻ・ｽ[繝ｻ・ｽu繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ・牙ｯｾ莨夲ｽｿ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ鬩幃ｯ会ｽｿ・ｽR繝ｻ・ｽ[繝ｻ・ｽh struct繝ｻ・ｽB
    // source generator 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽR繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽX繝ｻ・ｽg繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽN繝ｻ・ｽ^繝ｻ・ｽ[繝ｻ・ｽE_u/_m 繝ｻ・ｽt繝ｻ・ｽB繝ｻ・ｽ[繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽh繝ｻ・ｽEMemoryPack 繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ遶難ｽｮ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽ繝ｻ・ｽB
    namespace Records {
        public partial struct region {
            public partial int r_regionkey { get; }
            public partial string r_name { get; }
            public partial string r_comment { get; }
        }
        public partial struct nation {
            public partial int n_nationkey { get; }
            public partial string n_name { get; }
            public partial int n_regionkey { get; }
            public partial string n_comment { get; }
        }
        public partial struct supplier {
            public partial int s_suppkey { get; }
            public partial string s_name { get; }
            public partial string s_address { get; }
            public partial int s_nationkey { get; }
            public partial string s_phone { get; }
            public partial decimal s_acctbal { get; }
            public partial string s_comment { get; }
        }
        public partial struct customer {
            public partial int c_custkey { get; }
            public partial string c_name { get; }
            public partial string c_address { get; }
            public partial int c_nationkey { get; }
            public partial string c_phone { get; }
            public partial decimal c_acctbal { get; }
            public partial string c_mktsegment { get; }
            public partial string c_comment { get; }
        }
        public partial struct part {
            public partial int p_partkey { get; }
            public partial string p_name { get; }
            public partial string p_mfgr { get; }
            public partial string p_brand { get; }
            public partial string p_type { get; }
            public partial int p_size { get; }
            public partial string p_container { get; }
            public partial decimal p_retailprice { get; }
            public partial string p_comment { get; }
        }
        public partial struct partsupp {
            public partial int ps_partkey { get; }
            public partial int ps_suppkey { get; }
            public partial int ps_availqty { get; }
            public partial decimal ps_supplycost { get; }
            public partial string ps_comment { get; }
        }
        public partial struct orders {
            public partial int o_orderkey { get; }
            public partial int o_custkey { get; }
            public partial string o_orderstatus { get; }
            public partial decimal o_totalprice { get; }
            public partial int o_orderdate { get; }
            public partial string o_orderpriority { get; }
            public partial string o_clerk { get; }
            public partial int o_shippriority { get; }
            public partial string o_comment { get; }
        }
        public partial struct lineitem {
            public partial int l_orderkey { get; }
            public partial int l_partkey { get; }
            public partial int l_suppkey { get; }
            public partial int l_linenumber { get; }
            public partial decimal l_quantity { get; }
            public partial decimal l_extendedprice { get; }
            public partial decimal l_discount { get; }
            public partial decimal l_tax { get; }
            public partial string l_returnflag { get; }
            public partial string l_linestatus { get; }
            public partial int l_shipdate { get; }
            public partial int l_commitdate { get; }
            public partial int l_receiptdate { get; }
            public partial string l_shipinstruct { get; }
            public partial string l_shipmode { get; }
            public partial string l_comment { get; }
        }
    }
}


