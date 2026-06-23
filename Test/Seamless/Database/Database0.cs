using System.Linq;
using System.Linq.Expressions;
using Seamless.Tsavorite.Collections;
using Seamless.Linq;

namespace Test.Seamless.Database.Database0 {
    static partial class DatabaseGenerator;
    /// <summary>
    ///  <see cref="Test.Seamless.Database.Database0.Container"/>
    ///  <see cref="Test.Seamless.Database.Database0.Keys.Key0"/>
    ///  <see cref="Test.Seamless.Diagnostics.InterceptorSmokeTests.ListWhere_Interceptor_Smoke"/>
    /// </summary>
    public class Container {
        public global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> Optimize無virtual無(global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> outer) {
            new global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int>(global::System.Linq.Enumerable.Select(outer,static p => p+p));
            var x = new global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int>(global::System.Linq.Enumerable.Select(outer,static p => p+p));
            return new(global::System.Linq.Enumerable.Select(outer,static p => p+p));
        }
        public virtual global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> Optimize無virtual有(global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> outer) {
            return new(global::System.Linq.Enumerable.Select(outer,static p => p+p));
        }
        //[Optimize]public global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> Optimize有virtual無(global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> outer) {
        //    return new(global::System.Linq.Enumerable.Select(outer,static p => p+p));
        //}
        public virtual global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> Optimize有virtual有(global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> outer) {
            return new(global::System.Linq.Enumerable.Select(outer,static p => p+p));
        }
        [Fact]
        public virtual void Call() {
            var x = this.Select_Method(new int[3]);
        }
        public virtual global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> Select_Method(global::Seamless.Tsavorite.Collections.Generic.IEnumerable<int> outer) {
            return new(global::System.Linq.Enumerable.Select(outer,static p => p+p));
        }
        public virtual IEnumerable<int> SelectSelect_Method(IEnumerable<int> outer) {
            return outer.Select(p => p+p).Select(p => p*p);
        }
        public virtual IEnumerable<(T0 o,T1 i)> SelectMany_Query<T0, T1>(IEnumerable<T0> outer,IEnumerable<T1> inner) {
            return from o in outer
                   from i in inner
                   select (o,i);
        }

        public virtual IEnumerable<(T0 o,T1 i)> SelectMany_Method<T0, T1>(IEnumerable<T0> outer,IEnumerable<T1> inner) {
            return outer.SelectMany(_ => inner,(o,i) => (o,i));
        }

        public virtual Expression<Func<IEnumerable<int>>> SelectMany_QueryTree(IEnumerable<int> outer,IEnumerable<int> inner) => () =>
            from o in outer
            from i in inner
            select o+i;

        public virtual Expression<Func<IEnumerable<int>>> SelectMany_MethodTree(IEnumerable<int> outer,IEnumerable<int> inner) => () =>
            outer.SelectMany(_ => inner,(o,i) => o+i);

        public virtual IEnumerable<(T o,T p)> SelectQuery<T>(IEnumerable<T> outer,T p) {
            return from o in outer
                   select (o,p);
        }

        public virtual IEnumerable<(T o,T p)> WhereSelectQuery<T>(IEnumerable<T> outer,T p) {
            return from o in outer
                   where EqualityComparer<T>.Default.Equals(o,p)
                   select (o,p);
        }

        public virtual IEnumerable<(T o,T p)> WhereSelectMethod<T>(IEnumerable<T> outer,T p) {
            return outer.Where(o => EqualityComparer<T>.Default.Equals(o,p)).Select(o => (o,p));
        }

        public virtual IEnumerable<(T o,T p)> SelectWhereQuery<T>(IEnumerable<T> outer,T p) {
            return from o in outer
                   select (o,p) into op
                   where EqualityComparer<T>.Default.Equals(op.o,p)
                   select op;
        }

        public virtual IEnumerable<(T o,T p)> SelectWhereMethod<T>(IEnumerable<T> outer,T p) {
            return outer.Select(o => (o,p)).Where(op => EqualityComparer<T>.Default.Equals(op.o,p));
        }

        public virtual IEnumerable<int> LetQuery(IEnumerable<int> outer) {
            return from value in outer
                   let doubled = value*2
                   where doubled>2
                   select doubled;
        }

        public virtual IEnumerable<(int outerKey,int innerValue)> JoinQuery(int minimumColumn1) {
            return from outerRow in this.Type0Table
                   join innerRow in this.Type0Table
                       on outerRow.Column0 equals innerRow.Column0
                   select (outerRow.Column0,innerRow.Column1);
        }

        public virtual IEnumerable<(int outerKey,int innerValue)> JoinIntoQuery(int minimumColumn1) {
            return from outerRow in this.Type0Table
                   join innerRow in this.Type0Table
                       on outerRow.Column0 equals innerRow.Column0 into matches
                   from match in matches
                   select (outerRow.Column0,match.Column1);
        }

        public virtual IEnumerable<int> OrderByQuery() {
            return from row in this.Type0Table
                   orderby row.Column1 descending, row.Column0
                   select row.Column0;
        }

        public virtual IEnumerable<(int key,int value)> GroupQuery() {
            return from row in this.Type0Table
                   group row.Column1 by row.Column0%2 into grouped
                   orderby grouped.Key
                   from value in grouped
                   select (grouped.Key,value);
        }

        public virtual IEnumerable<(T0 o,T1 i)> Query2<T0, T1>(global::Seamless.Tsavorite.Collections.IHashSet<T0> outer,global::Seamless.Tsavorite.Collections.IHashSet<T1> inner)
            where T0 : IEquatable<T0>
            where T1 : IEquatable<T1> {
            return global::System.Linq.Enumerable.SelectMany(outer,_ => inner,(o,i) => (o,i));
        }

        public virtual T AggregateMethod<T>(IEnumerable<T> outer) {
            return outer.Aggregate((a,_) => a);
        }

        public virtual int FusedWhereSelectSum(IEnumerable<int> outer) {
            return outer.Where(value => value>1).Select(value => value*2).Sum();
        }

        public int NonVirtualFusedWhereSelectSum(IEnumerable<int> outer) {
            return outer.Where(value => value>1).Select(value => value*2).Sum();
        }

        public virtual double FusedSkipTakeAverage(IEnumerable<int> outer) {
            return outer.Skip(1).Take(3).Average();
        }

        public virtual int FusedSelectManyWhereSum(IEnumerable<int> outer,IEnumerable<int> inner) {
            return outer.SelectMany(outerValue => inner,(outerValue,innerValue) => outerValue+innerValue).Where(value => value>5).Sum();
        }

        public virtual int FusedDistinctSkipWhileTakeWhileSum(IEnumerable<int> outer) {
            return outer.SkipWhile(value => value<0).Distinct().TakeWhile(value => value<5).Sum();
        }

        public virtual int FusedCastSum(IEnumerable<object> outer) {
            return outer.Cast<int>().Where(value => value>0).Sum();
        }

        public virtual int FusedOfTypeSum(IEnumerable<object> outer) {
            return outer.OfType<int>().Sum();
        }

        //[Optimize]
        public virtual IEnumerable<int> FusedJoinSelectSequence() {
            return this.Type0Table
                .Join(this.Type0Table,outer => outer.Column0,inner => inner.Column0,(outer,inner) => (Outer: outer.Column1,Inner: inner.Column1))
                .Select(pair => pair.Outer+pair.Inner);
        }

        public virtual IEnumerable<(int outerValue,int innerValue)> FusedSelectManyHoistedSequence(IEnumerable<int> outer,IEnumerable<int> inner) {
            return outer.SelectMany(_ => inner,(outerValue,innerValue) => (outerValue,innerValue));
        }

        public virtual IEnumerable<int> FusedSelectManySplitWhereSequence(IEnumerable<int> outer,IEnumerable<int> inner,int parameter) {
            return outer.SelectMany(outerValue => inner
                .Where(innerValue => innerValue==parameter&&outerValue==parameter)
                .Select(innerValue => outerValue+innerValue));
        }

        public virtual IEnumerable<int> WhereAfterAnonymousSelect(int minimumColumn1) {
            return this.Type0Table
                .Select(row => new { row.Column0,row.Column1 })
                .Where(row => row.Column1>=minimumColumn1)
                .Select(row => row.Column0);
        }

        public virtual IEnumerable<int> WhereAfterTupleSelect(int minimumColumn1) {
            return this.Type0Table
                .Select(row => (row.Column0,row.Column1))
                .Where(row => row.Column1>=minimumColumn1)
                .Select(row => row.Column0);
        }

        public virtual IEnumerable<int> WhereAfterJoin(int minimumColumn1) {
            return this.Type0Table
                .Join(this.Type0Table,outer => outer.Column0,inner => inner.Column0,(outer,inner) => new { outer,inner })
                .Where(pair => pair.outer.Column1>=minimumColumn1)
                .Select(pair => pair.inner.Column1);
        }

        public virtual IEnumerable<int> WhereAfterExcept(int minimumColumn1) {
            return this.Type0Table
                .Select(row => row.Column1)
                .Except(this.Type0Table.Select(row => row.Column0))
                .Where(value => value>=minimumColumn1);
        }

        internal Records.Type0[] Type0Table => [new(1,10),new(2,20),new(3,30)];

        private IEnumerable<Records.Type0> FilterType0(int minimumColumn1) =>
            from row in this.Type0Table
            where row.Column1>=minimumColumn1
            select row;
    }

    namespace Keys {
        public partial struct Key0 {
            public partial int Column0 { get; }
        }

        public partial struct Key1 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
        }

        public partial struct Key2 {
            public partial string Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
        }
    }

    namespace Records {
        public partial struct Type0 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
        }

        public partial struct Type1 {
            public partial int Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
            public partial int Column3 { get; }
        }

        public partial struct Type2 {
            public partial string Column0 { get; }
            public partial int Column1 { get; }
            public partial int Column2 { get; }
            public partial int Column3 { get; }
        }
    }
}
