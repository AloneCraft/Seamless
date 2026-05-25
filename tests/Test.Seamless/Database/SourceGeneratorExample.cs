using System.ComponentModel;
using System.Reflection;
using Seamless;
using Seamless.Linq;

using System.Linq.Expressions;

namespace Test.Seamless.Database.Database0 {
    //置き換え元LINQコードからSourceGeneratorにインターセプトを出力させる見本である。
    //置き換え先メソッドは置き換え前メソッドの実装から取得し最適化する
    //AIに実装して欲しい
    //AIが実装を解析するのは難しい場合実装コードにヒントを入れて欲しければ教えて
    //例えばループの起点以外で戻り値となるコレクションをベースから取得するからインスタンス化しなくてもいいもの。例えばWhere,Selectならコレクションをnewして代入するローカル変数に[Stream]属性を付けるなど
    public sealed class SourceGeneratorExample {
        private struct StructSelect {
            public static int selector0(int p) => p+1;
        }
        [Fact, Trait("Category","Unit")]
        public void Select0() {
            var s = new int[3];
            var インターセプト前 = s.Select(static p => p+1);//インライン成功している
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    var selectorItem0 = StructSelect.selector0(item0);
                    result0.Add(selectorItem0);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        [Fact, Trait("Category","Unit")]
        public void Select() {
            var インターセプト前 = new int[3].Select(static p => p+1);//インライン成功している
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    var selectorItem0 = StructSelect.selector0(item0);
                    result0.Add(selectorItem0);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private readonly struct StructInstanceSelect0(int a) {
            public int selector0(int p) => p*p;
        }
        [Fact, Trait("Category","Unit")]
        public void Select非staticデリゲートだが実質staticデリゲート() {
            var a       = 3;
            var インターセプト前= new int[3].Select(p => p*p);//非staticだが実質staticに出来るのでインライン成功している
            var インターセプト後= 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0              = new List<int>();
                var StructInstanceSelect1= new StructInstanceSelect0(3);
                foreach(var item0 in new int[3]) {
                    var selectorItem2 = StructInstanceSelect1.selector0(item0);
                    result0.Add(selectorItem2);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private readonly struct StructInstanceSelect1(int a) {
            public int selector0(int p) => p*a;
        }
        [Fact, Trait("Category","Unit")]
        public void InstanceSelect非staticデリゲートInstanceStruct呼び出しになるべき() {
            var a       = 3;
            var インターセプト前= new int[3].Select(p => p*a);//キャプチャ変数は成功しない。インスタンスstructメソッドにならない
            var インターセプト後= 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0              = new List<int>();
                var StructInstanceSelect1= new StructInstanceSelect1(3);
                foreach(var item0 in new int[3]) {
                    var selectorItem2 = StructInstanceSelect1.selector0(item0);
                    result0.Add(selectorItem2);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructSelectSelect {
            public static int selector0(int p) => p+1;
            public static int selector1(int p) => p*p;
        }
        [Fact, Trait("Category","Unit")]
        public void SelectSelect() {
            var インターセプト前 = new int[3].Select(p => p+1).Select(p => p*p);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    var selectorItem0 = StructSelectSelect.selector0(item0);
                    var selectorItem1 = StructSelectSelect.selector1(selectorItem0);
                    result0.Add(selectorItem1);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructSelectSelectSelect {
            public static int selector0(int p) => p+1;
            public static int selector1(int p) => p*p;
            public static int selector2(int q) => q-1;
        }
        [Fact, Trait("Category","Unit")]
        public void SelectSelectSelect() {
            var インターセプト前 = new int[3].Select(p => p+1).Select(p => p*p).Select(q => q-1);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    var selectorItem0 = StructSelectSelectSelect.selector0(item0);
                    var selectorItem1 = StructSelectSelectSelect.selector1(selectorItem0);
                    var selectorItem2 = StructSelectSelectSelect.selector2(selectorItem1);
                    result0.Add(selectorItem2);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructWhereWhere {
            public static bool predicate0(int p) => p%2==0;
            public static bool predicate1(int p) => p>0;
        }
        [Fact, Trait("Category","Unit")]
        public void WhereWhere() {
            var インターセプト前 = new int[3].Where(p => p%2==0).Where(p => p>0);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    if(!StructWhereWhere.predicate0(item0)) continue;
                    if(!StructWhereWhere.predicate1(item0)) continue;
                    result0.Add(item0);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructWhere {
            public static bool predicate0(int p) => p%2==0;
        }
        [Fact, Trait("Category","Unit")]
        public void Where() {
            var インターセプト前 = new int[3].Where(p => p%2==0);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    if(!StructWhere.predicate0(item0)) continue;
                    result0.Add(item0);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructWhereSelect {
            public static bool predicate0(int p) => p%2==0;
            public static int selector1(int p) => p*p;
        }
        [Fact, Trait("Category","Unit")]
        public void WhereSelect() {
            var インターセプト前 = new int[3].Where(p => p%2==0).Select(p => p*p);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    if(!StructWhereSelect.predicate0(item0)) continue;
                    var selectorItem2 = StructWhereSelect.selector1(item0);
                    result0.Add(selectorItem2);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private readonly struct StructInstanceWhereInstanceSelect(int a) {
            public bool predicate0(int p) => p%2==a;
            public int selector1(int p) => p*a;
        }
        [Fact, Trait("Category","Unit")]
        public void InstanceWhereInstanceSelect() {
            var a = 3;
            var インターセプト前 = new int[3].Where(p => p%2==a).Select(p => p*a);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var result0 = new List<int>();
                var StructInstanceWhereInstanceSelect1 = new StructInstanceWhereInstanceSelect(3);
                foreach(var item0 in new int[3]) {
                    if(!StructInstanceWhereInstanceSelect1.predicate0(item0)) continue;
                    var selectorItem2 = StructInstanceWhereInstanceSelect1.selector1(item0);
                    result0.Add(selectorItem2);
                }
                return result0;
            }
            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructJoin {
            public static int outerKeySelector0(int o) => o*2;
            public static int innerKeySelector1(int i) => i*3;
            public static int resultSelector2(int o,int i) => o+i;
        }
        [Fact, Trait("Category","Unit")]
        public void Join() {
            var インターセプト前 = new int[3].Join(new int[2],o => o*2,i => i*3,(o,i) => o+i);
            var インターセプト後 = 置き換え先メソッド(new int[3],new int[2]);
            static List<int> 置き換え先メソッド(int[] outer,int[] inner) {
                var result0 = new List<int>();
                var Lookup = new Enumerable.LookupImpl<int,int>(EqualityComparer<int>.Default);
                foreach(var item in inner)
                    Lookup.Add(StructJoin.innerKeySelector1(item),item);
                foreach(var outerItem in outer) {
                    foreach(var innerItem in Lookup[StructJoin.outerKeySelector0(outerItem)]) {
                        var resultSelectorItem = StructJoin.resultSelector2(outerItem,innerItem);
                        result0.Add(resultSelectorItem);
                    }
                }
                return result0;
            }

            Assert.Equal(インターセプト後,インターセプト前);
        }
        private struct StructWhereJoin {
            public static bool predicate0(int p) => p%2==0;
            public static int outerKeySelector1(int o) => o*2;
            public static int innerKeySelector2(int i) => i*3;
            public static int resultSelector3(int o,int i) => o+i;
        }
        [Fact, Trait("Category","Unit")]
        public void WhereJoin() {
            var インターセプト前 = new int[3].Where(p => p%2==0).Join(new int[2],o => o*2,i => i*3,(o,i) => o+i);
            var インターセプト後 = 置き換え先メソッド(new int[3],new int[2]);
            static List<int> 置き換え先メソッド(int[] outer,int[] inner) {
                var Lookup = new Enumerable.LookupImpl<int,int>(EqualityComparer<int>.Default);
                foreach(var item0 in inner)
                    Lookup.Add(StructWhereJoin.innerKeySelector2(item0),item0);
                var result0 = new List<int>();
                foreach(var item0 in new int[3]) {
                    if(!StructWhereJoin.predicate0(item0)) continue;
                    foreach(var innerItem in Lookup[StructWhereJoin.outerKeySelector1(item0)]) {
                        var resultSelectorItem = StructWhereJoin.resultSelector3(item0,innerItem);
                        result0.Add(resultSelectorItem);
                    }
                }
                return result0;
            }

            Assert.Equal(インターセプト後,インターセプト前);
        }
        [Fact, Trait("Category","Unit")]
        public void SelectMany() {
            var インターセプト前 = new int[3].SelectMany(p => new int[2]);
            var インターセプト後 = 置き換え先メソッド();
            static List<int> 置き換え先メソッド() {
                var outer = new List<int>();
                var result = new List<int>();
                foreach(var item in new int[3]) {
                    foreach(var projectedItem in new int[2]) {
                        result.Add(projectedItem);
                    }
                }
                return result;
            }

            Assert.Equal(インターセプト後,インターセプト前);
        }

        [Fact, Trait("Category","Unit")]
        public void 手書き置換は具体シグネチャのInterceptorを生成する() {
            AssertConcreteInterceptor(nameof(Select),nameof(Enumerable.Select));
            AssertConcreteInterceptor(nameof(SelectSelect),nameof(Enumerable.Select));
            AssertConcreteInterceptor(nameof(WhereSelect),nameof(Enumerable.Select));
            AssertConcreteInterceptor(nameof(WhereJoin),nameof(Enumerable.Join));
            AssertConcreteInterceptor(nameof(SelectMany),nameof(Enumerable.SelectMany));
        }

        private static void AssertConcreteInterceptor(string containingMethodName,string linqMethodName) {
            var interceptors = typeof(SourceGeneratorExample).Assembly
                .GetTypes()
                .Where(type => type.Namespace?.StartsWith("Intercept.",StringComparison.Ordinal)==true
                    || type.Namespace=="Intercept")
                .SelectMany(type => type.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                .Where(method => method.Name.StartsWith("OptimizedLinq_",StringComparison.Ordinal))
                .Where(method => !method.IsGenericMethodDefinition)
                .ToArray();

            Assert.NotEmpty(interceptors);
            Assert.All(interceptors,interceptor => Assert.False(interceptor.IsGenericMethodDefinition));
        }

    }
}
