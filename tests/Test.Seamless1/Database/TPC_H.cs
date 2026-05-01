using System.Globalization;
using System.Linq;
using Seamless;
using Seamless.Collections;
using Seamless.Stores;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
#pragma warning disable CS8981

namespace Test.Seamless.Database.TPC_H1 {
    // DatabaseGenerator marker — Keys / Records の schema struct は namespace Keys / Records に直接宣言する。
    // source generator がボイラーコード (コンストラクター・_u/_m フィールド・MemoryPack 実装) を生成する。
    static class DatabaseGenerator;

    [Optimize]
    public partial class Container:IDisposable {
        private readonly global::System.Collections.Generic.List<IDisposable> disposables = [];

        public Container() {
            this.regions=this.CreateTable<Records.region>();
            this.nations=this.CreateTable<Records.nation>();
            this.suppliers=this.CreateTable<Records.supplier>();
            this.customers=this.CreateTable<Records.customer>();
            this.parts=this.CreateTable<Records.part>();
            this.partsupps=this.CreateTable<Records.partsupp>();
            this.orders=this.CreateTable<Records.orders>();
            this.lineitems=this.CreateTable<Records.lineitem>();
        }

        public ManagedHashSet<Records.region> regions { get; }
        public ManagedHashSet<Records.nation> nations { get; }
        public ManagedHashSet<Records.supplier> suppliers { get; }
        public ManagedHashSet<Records.customer> customers { get; }
        public ManagedHashSet<Records.part> parts { get; }
        public ManagedHashSet<Records.partsupp> partsupps { get; }
        public ManagedHashSet<Records.orders> orders { get; }
        public ManagedHashSet<Records.lineitem> lineitems { get; }

        private ManagedHashSet<T> CreateTable<T>() {
            var store = HashStore.Create(IndexSize: 1L<<12).CreateManagedHashSet<T>();
            var table = store.Create();
            this.disposables.Add(store);
            this.disposables.Add(table);
            return table;
        }

        public void Dispose() {
            for(var index = this.disposables.Count-1; index>=0; index--) {
                this.disposables[index].Dispose();
            }
            this.disposables.Clear();
        }

        public static string Row(int query,params object?[] values) =>
            $"Q{query:D2}|"+string.Join("|",values.Select(FormatValue));

        public static string FormatValue(object? value) => value switch {
            null => "",
            decimal decimalValue => decimalValue.ToString("0.00",CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null,CultureInfo.InvariantCulture)??string.Empty,
            _ => value.ToString()??string.Empty,
        };

        public virtual IEnumerable<string> Q01Method() =>
            this.lineitems
                .Where(line => line.l_shipdate<=19981201)
                .GroupBy(line => new { line.l_returnflag,line.l_linestatus })
                .OrderBy(group => group.Key.l_returnflag)
                .ThenBy(group => group.Key.l_linestatus)
                .Select(group => Row(1,group.Key.l_returnflag,group.Key.l_linestatus,group.Sum(line => line.l_quantity),group.Sum(line => line.l_extendedprice)));

        public virtual IEnumerable<string> Q01Query() =>
            from line in this.lineitems
            where line.l_shipdate<=19981201
            group line by new { line.l_returnflag,line.l_linestatus } into grouped
            orderby grouped.Key.l_returnflag, grouped.Key.l_linestatus
            select Row(1,grouped.Key.l_returnflag,grouped.Key.l_linestatus,grouped.Sum(line => line.l_quantity),grouped.Sum(line => line.l_extendedprice));

        public virtual IEnumerable<string> Q02Method() =>
            this.parts
                .Where(part => part.p_size==15&&part.p_type.Contains("BRASS"))
                .Join(this.partsupps,part => part.p_partkey,partsupp => partsupp.ps_partkey,(part,partsupp) => new { part,partsupp })
                .Join(this.suppliers,row => row.partsupp.ps_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.partsupp,supplier })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.part,row.partsupp,row.supplier,nation })
                .Join(this.regions,row => row.nation.n_regionkey,region => region.r_regionkey,(row,region) => new { row.part,row.partsupp,row.supplier,row.nation,region })
                .Where(row => row.region.r_name=="EUROPE")
                .OrderBy(row => row.partsupp.ps_supplycost)
                .ThenBy(row => row.supplier.s_name)
                .Take(3)
                .Select(row => Row(2,row.supplier.s_name,row.nation.n_name,row.part.p_partkey,row.partsupp.ps_supplycost));

        public virtual IEnumerable<string> Q02Query() =>
            (from part in this.parts
             join partsupp in this.partsupps on part.p_partkey equals partsupp.ps_partkey
             join supplier in this.suppliers on partsupp.ps_suppkey equals supplier.s_suppkey
             join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
             join region in this.regions on nation.n_regionkey equals region.r_regionkey
             where part.p_size==15&&part.p_type.Contains("BRASS")&&region.r_name=="EUROPE"
             orderby partsupp.ps_supplycost, supplier.s_name
             select new { supplier,nation,part,partsupp })
            .Take(3)
            .Select(row => Row(2,row.supplier.s_name,row.nation.n_name,row.part.p_partkey,row.partsupp.ps_supplycost));

        public virtual IEnumerable<string> Q03Method() =>
            this.customers
                .Where(customer => customer.c_mktsegment=="BUILDING")
                .Join(this.orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(this.lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.order,line })
                .Where(row => row.order.o_orderdate<19950315&&row.line.l_shipdate>19950315)
                .GroupBy(row => row.order.o_orderkey)
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(3,group.Key,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q03Query() =>
            from customer in this.customers
            join order in this.orders on customer.c_custkey equals order.o_custkey
            join line in this.lineitems on order.o_orderkey equals line.l_orderkey
            where customer.c_mktsegment=="BUILDING"&&order.o_orderdate<19950315&&line.l_shipdate>19950315
            group new { order,line } by order.o_orderkey into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(3,grouped.Key,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q04Method() =>
            this.orders
                .Where(order => order.o_orderdate>=19930701&&order.o_orderdate<19931001)
                .Where(order => this.lineitems.Any(line => line.l_orderkey==order.o_orderkey&&line.l_commitdate<line.l_receiptdate))
                .GroupBy(order => order.o_orderpriority)
                .OrderBy(group => group.Key)
                .Select(group => Row(4,group.Key,group.Count()));

        public virtual IEnumerable<string> Q04Query() =>
            from order in this.orders
            where order.o_orderdate>=19930701&&order.o_orderdate<19931001
            where this.lineitems.Any(line => line.l_orderkey==order.o_orderkey&&line.l_commitdate<line.l_receiptdate)
            group order by order.o_orderpriority into grouped
            orderby grouped.Key
            select Row(4,grouped.Key,grouped.Count());

        public virtual IEnumerable<string> Q05Method() =>
            this.customers
                .Join(this.orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(this.lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.customer,row.order,line })
                .Join(this.suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.customer,row.order,row.line,supplier })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.customer,row.order,row.line,row.supplier,nation })
                .Where(row => row.customer.c_nationkey==row.supplier.s_nationkey&&row.order.o_orderdate>=19940101&&row.order.o_orderdate<19950101)
                .GroupBy(row => row.nation.n_name)
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(5,group.Key,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q05Query() =>
            from customer in this.customers
            join order in this.orders on customer.c_custkey equals order.o_custkey
            join line in this.lineitems on order.o_orderkey equals line.l_orderkey
            join supplier in this.suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where customer.c_nationkey==supplier.s_nationkey&&order.o_orderdate>=19940101&&order.o_orderdate<19950101
            group new { line } by nation.n_name into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(5,grouped.Key,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q06Method() =>
            this.lineitems
                .Where(line => line.l_shipdate>=19940101&&line.l_shipdate<19950101&&line.l_discount>=0.05m&&line.l_discount<=0.07m&&line.l_quantity<24m)
                .Select(line => Row(6,line.l_extendedprice*line.l_discount));

        public virtual IEnumerable<string> Q06Query() =>
            from line in this.lineitems
            where line.l_shipdate>=19940101&&line.l_shipdate<19950101&&line.l_discount>=0.05m&&line.l_discount<=0.07m&&line.l_quantity<24m
            select Row(6,line.l_extendedprice*line.l_discount);

        public virtual IEnumerable<string> Q07Method() =>
            this.suppliers
                .Join(this.nations,supplier => supplier.s_nationkey,nation => nation.n_nationkey,(supplier,supplierNation) => new { supplier,supplierNation })
                .Join(this.lineitems,row => row.supplier.s_suppkey,line => line.l_suppkey,(row,line) => new { row.supplier,row.supplierNation,line })
                .Join(this.orders,row => row.line.l_orderkey,order => order.o_orderkey,(row,order) => new { row.supplierNation,row.line,order })
                .Join(this.customers,row => row.order.o_custkey,customer => customer.c_custkey,(row,customer) => new { row.supplierNation,row.line,customer })
                .Join(this.nations,row => row.customer.c_nationkey,nation => nation.n_nationkey,(row,customerNation) => new { row.supplierNation,customerNation,row.line })
                .Where(row => row.line.l_shipdate>=19950101&&row.line.l_shipdate<=19961231)
                .GroupBy(row => new { Supplier = row.supplierNation.n_name,Customer = row.customerNation.n_name,Year = row.line.l_shipdate/10000 })
                .OrderBy(group => group.Key.Supplier)
                .ThenBy(group => group.Key.Customer)
                .Select(group => Row(7,group.Key.Supplier,group.Key.Customer,group.Key.Year,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q07Query() =>
            from supplier in this.suppliers
            join supplierNation in this.nations on supplier.s_nationkey equals supplierNation.n_nationkey
            join line in this.lineitems on supplier.s_suppkey equals line.l_suppkey
            join order in this.orders on line.l_orderkey equals order.o_orderkey
            join customer in this.customers on order.o_custkey equals customer.c_custkey
            join customerNation in this.nations on customer.c_nationkey equals customerNation.n_nationkey
            where line.l_shipdate>=19950101&&line.l_shipdate<=19961231
            group new { line } by new { Supplier = supplierNation.n_name,Customer = customerNation.n_name,Year = line.l_shipdate/10000 } into grouped
            orderby grouped.Key.Supplier, grouped.Key.Customer
            select Row(7,grouped.Key.Supplier,grouped.Key.Customer,grouped.Key.Year,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q08Method() =>
            this.parts
                .Where(part => part.p_type.Contains("BRASS"))
                .Join(this.lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Join(this.suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.line,supplier })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.part,row.line,row.supplier,nation })
                .GroupBy(row => row.line.l_shipdate/10000)
                .OrderBy(group => group.Key)
                .Select(group => Row(8,group.Key,group.Sum(row => row.nation.n_name=="BRAZIL" ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q08Query() =>
            from part in this.parts
            join line in this.lineitems on part.p_partkey equals line.l_partkey
            join supplier in this.suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where part.p_type.Contains("BRASS")
            group new { line,nation } by line.l_shipdate/10000 into grouped
            orderby grouped.Key
            select Row(8,grouped.Key,grouped.Sum(row => row.nation.n_name=="BRAZIL" ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q09Method() =>
            this.parts
                .Where(part => part.p_name.Contains("forest"))
                .Join(this.lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Join(this.partsupps,row => new { row.line.l_partkey,row.line.l_suppkey },partsupp => new { l_partkey = partsupp.ps_partkey,l_suppkey = partsupp.ps_suppkey },(row,partsupp) => new { row.part,row.line,partsupp })
                .Join(this.suppliers,row => row.line.l_suppkey,supplier => supplier.s_suppkey,(row,supplier) => new { row.part,row.line,row.partsupp,supplier })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.line,row.partsupp,nation })
                .GroupBy(row => new { row.nation.n_name,Year = row.line.l_shipdate/10000 })
                .OrderBy(group => group.Key.n_name)
                .ThenByDescending(group => group.Key.Year)
                .Select(group => Row(9,group.Key.n_name,group.Key.Year,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)-row.partsupp.ps_supplycost*row.line.l_quantity)));

        public virtual IEnumerable<string> Q09Query() =>
            from part in this.parts
            join line in this.lineitems on part.p_partkey equals line.l_partkey
            join partsupp in this.partsupps on new { line.l_partkey,line.l_suppkey } equals new { l_partkey = partsupp.ps_partkey,l_suppkey = partsupp.ps_suppkey }
            join supplier in this.suppliers on line.l_suppkey equals supplier.s_suppkey
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where part.p_name.Contains("forest")
            group new { line,partsupp } by new { nation.n_name,Year = line.l_shipdate/10000 } into grouped
            orderby grouped.Key.n_name, grouped.Key.Year descending
            select Row(9,grouped.Key.n_name,grouped.Key.Year,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)-row.partsupp.ps_supplycost*row.line.l_quantity));

        public virtual IEnumerable<string> Q10Method() =>
            this.customers
                .Join(this.orders,customer => customer.c_custkey,order => order.o_custkey,(customer,order) => new { customer,order })
                .Join(this.lineitems,row => row.order.o_orderkey,line => line.l_orderkey,(row,line) => new { row.customer,row.order,line })
                .Where(row => row.order.o_orderdate>=19931001&&row.order.o_orderdate<19940101&&row.line.l_returnflag=="R")
                .GroupBy(row => new { row.customer.c_custkey,row.customer.c_name })
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Select(group => Row(10,group.Key.c_custkey,group.Key.c_name,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q10Query() =>
            from customer in this.customers
            join order in this.orders on customer.c_custkey equals order.o_custkey
            join line in this.lineitems on order.o_orderkey equals line.l_orderkey
            where order.o_orderdate>=19931001&&order.o_orderdate<19940101&&line.l_returnflag=="R"
            group new { line } by new { customer.c_custkey,customer.c_name } into grouped
            orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
            select Row(10,grouped.Key.c_custkey,grouped.Key.c_name,grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q11Method() =>
            this.partsupps
                .Join(this.suppliers,partsupp => partsupp.ps_suppkey,supplier => supplier.s_suppkey,(partsupp,supplier) => new { partsupp,supplier })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.partsupp,nation })
                .Where(row => row.nation.n_name=="GERMANY")
                .GroupBy(row => row.partsupp.ps_partkey)
                .OrderByDescending(group => group.Sum(row => row.partsupp.ps_supplycost*row.partsupp.ps_availqty))
                .Select(group => Row(11,group.Key,group.Sum(row => row.partsupp.ps_supplycost*row.partsupp.ps_availqty)));

        public virtual IEnumerable<string> Q11Query() =>
            from partsupp in this.partsupps
            join supplier in this.suppliers on partsupp.ps_suppkey equals supplier.s_suppkey
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where nation.n_name=="GERMANY"
            group partsupp by partsupp.ps_partkey into grouped
            orderby grouped.Sum(partsupp => partsupp.ps_supplycost*partsupp.ps_availqty) descending
            select Row(11,grouped.Key,grouped.Sum(partsupp => partsupp.ps_supplycost*partsupp.ps_availqty));

        public virtual IEnumerable<string> Q12Method() =>
            this.orders
                .Join(this.lineitems,order => order.o_orderkey,line => line.l_orderkey,(order,line) => new { order,line })
                .Where(row => (row.line.l_shipmode=="MAIL"||row.line.l_shipmode=="SHIP")&&row.line.l_receiptdate>=19940101&&row.line.l_receiptdate<19950101)
                .GroupBy(row => row.line.l_shipmode)
                .OrderBy(group => group.Key)
                .Select(group => Row(12,group.Key,group.Count(row => row.order.o_orderpriority=="1-URGENT"||row.order.o_orderpriority=="2-HIGH"),group.Count(row => row.order.o_orderpriority!="1-URGENT"&&row.order.o_orderpriority!="2-HIGH")));

        public virtual IEnumerable<string> Q12Query() =>
            from order in this.orders
            join line in this.lineitems on order.o_orderkey equals line.l_orderkey
            where (line.l_shipmode=="MAIL"||line.l_shipmode=="SHIP")&&line.l_receiptdate>=19940101&&line.l_receiptdate<19950101
            group new { order,line } by line.l_shipmode into grouped
            orderby grouped.Key
            select Row(12,grouped.Key,grouped.Count(row => row.order.o_orderpriority=="1-URGENT"||row.order.o_orderpriority=="2-HIGH"),grouped.Count(row => row.order.o_orderpriority!="1-URGENT"&&row.order.o_orderpriority!="2-HIGH"));

        public virtual IEnumerable<string> Q13Method() =>
            this.customers
                .GroupJoin(this.orders.Where(order => !order.o_comment.Contains("special requests")),customer => customer.c_custkey,order => order.o_custkey,(customer,orders) => new { customer,OrderCount = orders.Count() })
                .GroupBy(row => row.OrderCount)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key)
                .Select(group => Row(13,group.Key,group.Count()));

        public virtual IEnumerable<string> Q13Query() =>
            from customer in this.customers
            join order in this.orders.Where(order => !order.o_comment.Contains("special requests")) on customer.c_custkey equals order.o_custkey into customerOrders
            let orderCount = customerOrders.Count()
            group customer by orderCount into grouped
            orderby grouped.Count() descending, grouped.Key descending
            select Row(13,grouped.Key,grouped.Count());

        public virtual IEnumerable<string> Q14Method() =>
            this.parts
                .Join(this.lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.line.l_shipdate>=19950901&&row.line.l_shipdate<19951001)
                .GroupBy(_ => 1)
                .Select(group => Row(14,group.Sum(row => row.part.p_type.StartsWith("PROMO") ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q14Query() =>
            from part in this.parts
            join line in this.lineitems on part.p_partkey equals line.l_partkey
            where line.l_shipdate>=19950901&&line.l_shipdate<19951001
            group new { part,line } by 1 into grouped
            select Row(14,grouped.Sum(row => row.part.p_type.StartsWith("PROMO") ? row.line.l_extendedprice*(1-row.line.l_discount) : 0m),grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q15Method() =>
            this.suppliers
                .Join(this.lineitems.Where(line => line.l_shipdate>=19960101&&line.l_shipdate<19960401),supplier => supplier.s_suppkey,line => line.l_suppkey,(supplier,line) => new { supplier,line })
                .GroupBy(row => new { row.supplier.s_suppkey,row.supplier.s_name })
                .OrderByDescending(group => group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)))
                .Take(1)
                .Select(group => Row(15,group.Key.s_suppkey,group.Key.s_name,group.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount))));

        public virtual IEnumerable<string> Q15Query() =>
            (from supplier in this.suppliers
             join line in this.lineitems.Where(line => line.l_shipdate>=19960101&&line.l_shipdate<19960401) on supplier.s_suppkey equals line.l_suppkey
             group new { line } by new { supplier.s_suppkey,supplier.s_name } into grouped
             orderby grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) descending
             select new { grouped.Key.s_suppkey,grouped.Key.s_name,Revenue = grouped.Sum(row => row.line.l_extendedprice*(1-row.line.l_discount)) })
            .Take(1)
            .Select(row => Row(15,row.s_suppkey,row.s_name,row.Revenue));

        public virtual IEnumerable<string> Q16Method() =>
            this.parts
                .Where(part => part.p_brand!="Brand#45"&&!part.p_type.StartsWith("MEDIUM POLISHED")&&part.p_size>=1)
                .Join(this.partsupps,part => part.p_partkey,partsupp => partsupp.ps_partkey,(part,partsupp) => new { part,partsupp })
                .GroupBy(row => new { row.part.p_brand,row.part.p_type,row.part.p_size })
                .OrderByDescending(group => group.Select(row => row.partsupp.ps_suppkey).Distinct().Count())
                .Select(group => Row(16,group.Key.p_brand,group.Key.p_type,group.Key.p_size,group.Select(row => row.partsupp.ps_suppkey).Distinct().Count()));

        public virtual IEnumerable<string> Q16Query() =>
            from part in this.parts
            join partsupp in this.partsupps on part.p_partkey equals partsupp.ps_partkey
            where part.p_brand!="Brand#45"&&!part.p_type.StartsWith("MEDIUM POLISHED")&&part.p_size>=1
            group new { partsupp } by new { part.p_brand,part.p_type,part.p_size } into grouped
            orderby grouped.Select(row => row.partsupp.ps_suppkey).Distinct().Count() descending
            select Row(16,grouped.Key.p_brand,grouped.Key.p_type,grouped.Key.p_size,grouped.Select(row => row.partsupp.ps_suppkey).Distinct().Count());

        public virtual IEnumerable<string> Q17Method() =>
            this.parts
                .Where(part => part.p_brand=="Brand#23"&&part.p_container=="MED BOX")
                .Join(this.lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.line.l_quantity<this.lineitems.Where(line => line.l_partkey==row.part.p_partkey).Average(line => line.l_quantity)*0.2m)
                .Select(row => Row(17,row.part.p_partkey,row.line.l_extendedprice/7m));

        public virtual IEnumerable<string> Q17Query() =>
            from part in this.parts
            join line in this.lineitems on part.p_partkey equals line.l_partkey
            where part.p_brand=="Brand#23"&&part.p_container=="MED BOX"
            where line.l_quantity<this.lineitems.Where(inner => inner.l_partkey==part.p_partkey).Average(inner => inner.l_quantity)*0.2m
            select Row(17,part.p_partkey,line.l_extendedprice/7m);

        public virtual IEnumerable<string> Q18Method() =>
            this.orders
                .Join(this.customers,order => order.o_custkey,customer => customer.c_custkey,(order,customer) => new { order,customer })
                .Where(row => this.lineitems.Where(line => line.l_orderkey==row.order.o_orderkey).Sum(line => line.l_quantity)>20m)
                .OrderByDescending(row => row.order.o_totalprice)
                .Select(row => Row(18,row.customer.c_name,row.order.o_orderkey,row.order.o_totalprice,this.lineitems.Where(line => line.l_orderkey==row.order.o_orderkey).Sum(line => line.l_quantity)));

        public virtual IEnumerable<string> Q18Query() =>
            from order in this.orders
            join customer in this.customers on order.o_custkey equals customer.c_custkey
            let quantity = this.lineitems.Where(line => line.l_orderkey==order.o_orderkey).Sum(line => line.l_quantity)
            where quantity>20m
            orderby order.o_totalprice descending
            select Row(18,customer.c_name,order.o_orderkey,order.o_totalprice,quantity);

        public virtual IEnumerable<string> Q19Method() =>
            this.parts
                .Join(this.lineitems,part => part.p_partkey,line => line.l_partkey,(part,line) => new { part,line })
                .Where(row => row.part.p_brand=="Brand#12"&&row.part.p_container.Contains("BOX")&&row.line.l_quantity>=1m&&row.line.l_quantity<=11m&&row.line.l_shipmode=="AIR")
                .Select(row => Row(19,row.part.p_brand,row.line.l_extendedprice*(1-row.line.l_discount)));

        public virtual IEnumerable<string> Q19Query() =>
            from part in this.parts
            join line in this.lineitems on part.p_partkey equals line.l_partkey
            where part.p_brand=="Brand#12"&&part.p_container.Contains("BOX")&&line.l_quantity>=1m&&line.l_quantity<=11m&&line.l_shipmode=="AIR"
            select Row(19,part.p_brand,line.l_extendedprice*(1-line.l_discount));

        public virtual IEnumerable<string> Q20Method() =>
            this.suppliers
                .Join(this.nations,supplier => supplier.s_nationkey,nation => nation.n_nationkey,(supplier,nation) => new { supplier,nation })
                .Where(row => row.nation.n_name=="CANADA")
                .Where(row => this.partsupps.Any(partsupp => partsupp.ps_suppkey==row.supplier.s_suppkey&&this.parts.Any(part => part.p_partkey==partsupp.ps_partkey&&part.p_name.StartsWith("forest"))))
                .OrderBy(row => row.supplier.s_name)
                .Select(row => Row(20,row.supplier.s_name,row.nation.n_name));

        public virtual IEnumerable<string> Q20Query() =>
            from supplier in this.suppliers
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where nation.n_name=="CANADA"
            where this.partsupps.Any(partsupp => partsupp.ps_suppkey==supplier.s_suppkey&&this.parts.Any(part => part.p_partkey==partsupp.ps_partkey&&part.p_name.StartsWith("forest")))
            orderby supplier.s_name
            select Row(20,supplier.s_name,nation.n_name);

        public virtual IEnumerable<string> Q21Method() =>
            this.suppliers
                .Join(this.lineitems,supplier => supplier.s_suppkey,line => line.l_suppkey,(supplier,line) => new { supplier,line })
                .Join(this.orders,row => row.line.l_orderkey,order => order.o_orderkey,(row,order) => new { row.supplier,row.line,order })
                .Join(this.nations,row => row.supplier.s_nationkey,nation => nation.n_nationkey,(row,nation) => new { row.supplier,row.line,row.order,nation })
                .Where(row => row.order.o_orderstatus=="F"&&row.line.l_receiptdate>row.line.l_commitdate&&row.nation.n_name=="SAUDI ARABIA")
                .GroupBy(row => row.supplier.s_name)
                .OrderByDescending(group => group.Count())
                .Select(group => Row(21,group.Key,group.Count()));

        public virtual IEnumerable<string> Q21Query() =>
            from supplier in this.suppliers
            join line in this.lineitems on supplier.s_suppkey equals line.l_suppkey
            join order in this.orders on line.l_orderkey equals order.o_orderkey
            join nation in this.nations on supplier.s_nationkey equals nation.n_nationkey
            where order.o_orderstatus=="F"&&line.l_receiptdate>line.l_commitdate&&nation.n_name=="SAUDI ARABIA"
            group supplier by supplier.s_name into grouped
            orderby grouped.Count() descending
            select Row(21,grouped.Key,grouped.Count());

        public virtual IEnumerable<string> Q22Method() =>
            this.customers
                .Where(customer => customer.c_acctbal>0m&&(customer.c_phone.StartsWith("13")||customer.c_phone.StartsWith("31")))
                .GroupBy(customer => customer.c_phone[..2])
                .OrderBy(group => group.Key)
                .Select(group => Row(22,group.Key,group.Count(),group.Sum(customer => customer.c_acctbal)));

        public virtual IEnumerable<string> Q22Query() =>
            from customer in this.customers
            where customer.c_acctbal>0m&&(customer.c_phone.StartsWith("13")||customer.c_phone.StartsWith("31"))
            group customer by customer.c_phone[..2] into grouped
            orderby grouped.Key
            select Row(22,grouped.Key,grouped.Count(),grouped.Sum(customer => customer.c_acctbal));
    }

    public sealed class TpchTests {
        private static Container CreateMinimalContainer() {
            var container = new Container();
            container.regions.Add(new Records.region(0,"AFRICA","region"));
            container.regions.Add(new Records.region(1,"AMERICA","region"));
            container.regions.Add(new Records.region(2,"ASIA","region"));
            container.regions.Add(new Records.region(3,"EUROPE","region"));
            container.regions.Add(new Records.region(4,"MIDDLE EAST","region"));

            container.nations.Add(new Records.nation(1,"ALGERIA",0,"nation"));
            container.nations.Add(new Records.nation(2,"BRAZIL",1,"nation"));
            container.nations.Add(new Records.nation(3,"CANADA",1,"nation"));
            container.nations.Add(new Records.nation(4,"FRANCE",3,"nation"));
            container.nations.Add(new Records.nation(5,"GERMANY",3,"nation"));
            container.nations.Add(new Records.nation(6,"SAUDI ARABIA",4,"nation"));
            container.nations.Add(new Records.nation(7,"UNITED STATES",1,"nation"));

            container.suppliers.Add(new Records.supplier(1,"Supplier#FR","Paris",4,"31-111",100m,"supplier"));
            container.suppliers.Add(new Records.supplier(2,"Supplier#DE","Berlin",5,"32-222",200m,"supplier"));
            container.suppliers.Add(new Records.supplier(3,"Supplier#SA","Riyadh",6,"33-333",300m,"supplier"));
            container.suppliers.Add(new Records.supplier(4,"Supplier#CA","Ottawa",3,"13-444",400m,"supplier"));
            container.suppliers.Add(new Records.supplier(5,"Supplier#BR","Rio",2,"34-555",500m,"supplier"));

            container.customers.Add(new Records.customer(1,"Customer#US","Seattle",7,"13-111",10m,"BUILDING","customer"));
            container.customers.Add(new Records.customer(2,"Customer#FR","Lyon",4,"31-222",20m,"AUTOMOBILE","customer"));
            container.customers.Add(new Records.customer(3,"Customer#BR","Sao Paulo",2,"18-333",30m,"BUILDING","customer"));

            container.parts.Add(new Records.part(1,"forest green brass","MFGR#1","Brand#12","SMALL BRASS",15,"SM BOX",100m,"part"));
            container.parts.Add(new Records.part(2,"promo part","MFGR#2","Brand#23","PROMO BURNISHED",5,"MED BOX",200m,"part"));
            container.parts.Add(new Records.part(3,"forest widget","MFGR#3","Brand#45","LARGE ANODIZED",9,"LG BOX",300m,"part"));

            container.partsupps.Add(new Records.partsupp(1,1,50,20m,"partsupp"));
            container.partsupps.Add(new Records.partsupp(1,2,60,15m,"partsupp"));
            container.partsupps.Add(new Records.partsupp(1,5,70,12m,"partsupp"));
            container.partsupps.Add(new Records.partsupp(2,2,80,25m,"partsupp"));
            container.partsupps.Add(new Records.partsupp(3,3,90,30m,"partsupp"));
            container.partsupps.Add(new Records.partsupp(3,4,100,18m,"partsupp"));

            container.orders.Add(new Records.orders(100,1,"O",1500m,19950310,"1-URGENT","Clerk#1",0,"regular order"));
            container.orders.Add(new Records.orders(101,2,"F",2500m,19930715,"2-HIGH","Clerk#2",0,"regular order"));
            container.orders.Add(new Records.orders(102,3,"F",3500m,19940110,"3-MEDIUM","Clerk#3",0,"special requests"));
            container.orders.Add(new Records.orders(103,1,"F",4500m,19960110,"5-LOW","Clerk#4",0,"regular order"));

            container.lineitems.Add(new Records.lineitem(100,1,1,1,10m,1000m,0.05m,0.10m,"R","F",19950320,19950318,19950322,"DELIVER","AIR","line"));
            container.lineitems.Add(new Records.lineitem(100,2,2,2,5m,500m,0.06m,0.00m,"N","O",19950910,19950909,19950912,"DELIVER","SHIP","line"));
            container.lineitems.Add(new Records.lineitem(101,3,3,1,30m,900m,0.04m,0.00m,"R","F",19930720,19930718,19930725,"DELIVER","MAIL","line"));
            container.lineitems.Add(new Records.lineitem(102,1,5,1,3m,700m,0.07m,0.00m,"N","F",19940120,19940118,19940123,"DELIVER","AIR","line"));
            container.lineitems.Add(new Records.lineitem(103,3,4,1,35m,1200m,0.03m,0.00m,"N","F",19960120,19960115,19960125,"DELIVER","SHIP","line"));
            return container;
        }

        private static string[] QueryRows(int query,bool querySyntax) {
            using var container = CreateMinimalContainer();
            var rows = query switch {
                1 => querySyntax ? container.Q01Query() : container.Q01Method(),
                2 => querySyntax ? container.Q02Query() : container.Q02Method(),
                3 => querySyntax ? container.Q03Query() : container.Q03Method(),
                4 => querySyntax ? container.Q04Query() : container.Q04Method(),
                5 => querySyntax ? container.Q05Query() : container.Q05Method(),
                6 => querySyntax ? container.Q06Query() : container.Q06Method(),
                7 => querySyntax ? container.Q07Query() : container.Q07Method(),
                8 => querySyntax ? container.Q08Query() : container.Q08Method(),
                9 => querySyntax ? container.Q09Query() : container.Q09Method(),
                10 => querySyntax ? container.Q10Query() : container.Q10Method(),
                11 => querySyntax ? container.Q11Query() : container.Q11Method(),
                12 => querySyntax ? container.Q12Query() : container.Q12Method(),
                13 => querySyntax ? container.Q13Query() : container.Q13Method(),
                14 => querySyntax ? container.Q14Query() : container.Q14Method(),
                15 => querySyntax ? container.Q15Query() : container.Q15Method(),
                16 => querySyntax ? container.Q16Query() : container.Q16Method(),
                17 => querySyntax ? container.Q17Query() : container.Q17Method(),
                18 => querySyntax ? container.Q18Query() : container.Q18Method(),
                19 => querySyntax ? container.Q19Query() : container.Q19Method(),
                20 => querySyntax ? container.Q20Query() : container.Q20Method(),
                21 => querySyntax ? container.Q21Query() : container.Q21Method(),
                22 => querySyntax ? container.Q22Query() : container.Q22Method(),
                _ => throw new ArgumentOutOfRangeException(nameof(query)),
            };
            return rows.OrderBy(row => row,StringComparer.Ordinal).ToArray();
        }

        private static void AssertMethodAndQuery(int query) => Assert.Equal(QueryRows(query,false),QueryRows(query,true));

        private static void AssertStorageListRoundTrip(int query) {
            var expected = QueryRows(query,false);
            var folder = TestPath.Directory<TpchTests>($"Q{query:D2}");
            using(var list = new ManagedList<string>(Path.Combine(folder,"result.data"),initialCapacity: 8,initialDataCapacity: 4096)) {
                foreach(var row in expected) {
                    list.Add(row);
                }
            }
            using(var list = new ManagedList<string>(Path.Combine(folder,"result.data"),initialCapacity: 8,initialDataCapacity: 4096)) {
                var actual = list.OrderBy(row => row,StringComparer.Ordinal).ToArray();
                Assert.Equal(expected,actual);
            }
        }

        [Fact] public void Q01_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(1);
        [Fact] public void Q02_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(2);
        [Fact] public void Q03_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(3);
        [Fact] public void Q04_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(4);
        [Fact] public void Q05_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(5);
        [Fact] public void Q06_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(6);
        [Fact] public void Q07_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(7);
        [Fact] public void Q08_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(8);
        [Fact] public void Q09_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(9);
        [Fact] public void Q10_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(10);
        [Fact] public void Q11_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(11);
        [Fact] public void Q12_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(12);
        [Fact] public void Q13_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(13);
        [Fact] public void Q14_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(14);
        [Fact] public void Q15_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(15);
        [Fact] public void Q16_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(16);
        [Fact] public void Q17_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(17);
        [Fact] public void Q18_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(18);
        [Fact] public void Q19_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(19);
        [Fact] public void Q20_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(20);
        [Fact] public void Q21_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(21);
        [Fact] public void Q22_MethodAndQuery_ReturnSameRows() => AssertMethodAndQuery(22);

        [Fact] public void Q01_StorageList_RoundTrips() => AssertStorageListRoundTrip(1);
        [Fact] public void Q02_StorageList_RoundTrips() => AssertStorageListRoundTrip(2);
        [Fact] public void Q03_StorageList_RoundTrips() => AssertStorageListRoundTrip(3);
        [Fact] public void Q04_StorageList_RoundTrips() => AssertStorageListRoundTrip(4);
        [Fact] public void Q05_StorageList_RoundTrips() => AssertStorageListRoundTrip(5);
        [Fact] public void Q06_StorageList_RoundTrips() => AssertStorageListRoundTrip(6);
        [Fact] public void Q07_StorageList_RoundTrips() => AssertStorageListRoundTrip(7);
        [Fact] public void Q08_StorageList_RoundTrips() => AssertStorageListRoundTrip(8);
        [Fact] public void Q09_StorageList_RoundTrips() => AssertStorageListRoundTrip(9);
        [Fact] public void Q10_StorageList_RoundTrips() => AssertStorageListRoundTrip(10);
        [Fact] public void Q11_StorageList_RoundTrips() => AssertStorageListRoundTrip(11);
        [Fact] public void Q12_StorageList_RoundTrips() => AssertStorageListRoundTrip(12);
        [Fact] public void Q13_StorageList_RoundTrips() => AssertStorageListRoundTrip(13);
        [Fact] public void Q14_StorageList_RoundTrips() => AssertStorageListRoundTrip(14);
        [Fact] public void Q15_StorageList_RoundTrips() => AssertStorageListRoundTrip(15);
        [Fact] public void Q16_StorageList_RoundTrips() => AssertStorageListRoundTrip(16);
        [Fact] public void Q17_StorageList_RoundTrips() => AssertStorageListRoundTrip(17);
        [Fact] public void Q18_StorageList_RoundTrips() => AssertStorageListRoundTrip(18);
        [Fact] public void Q19_StorageList_RoundTrips() => AssertStorageListRoundTrip(19);
        [Fact] public void Q20_StorageList_RoundTrips() => AssertStorageListRoundTrip(20);
        [Fact] public void Q21_StorageList_RoundTrips() => AssertStorageListRoundTrip(21);
        [Fact] public void Q22_StorageList_RoundTrips() => AssertStorageListRoundTrip(22);
    }

    // TPC-H の 8 テーブルに対応するキー struct。
    // source generator がコンストラクター・_u フィールド・MemoryPack 実装を補完する。
    namespace Keys {
        public partial struct region {
            public partial int r_regionkey { get; }
            void mm(){
                new region(11);
            }
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

    // TPC-H の 8 テーブルに対応するレコード struct。
    // source generator がコンストラクター・_u/_m フィールド・MemoryPack 実装を補完する。
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
