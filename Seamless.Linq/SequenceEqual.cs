namespace Seamless.Linq {
    partial class Enumerable {
        public static Boolean SequenceEqual<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second)
    where TEnumerator : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,Throws.IfNull(second).AsValueEnumerable(),null);
        }

        public static Boolean SequenceEqual<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,IEnumerable<TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,Throws.IfNull(second).AsValueEnumerable(),comparer);
        }

        public static Boolean SequenceEqual<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource> {
            return SequenceEqual(source,second,null);
        }

        public static Boolean SequenceEqual<TEnumerator, TEnumerator2, TSource>(this ValueEnumerable<TEnumerator,TSource> source,ValueEnumerable<TEnumerator2,TSource> second,IEqualityComparer<TSource>? comparer)
            where TEnumerator : struct, IValueEnumerator<TSource>
            where TEnumerator2 : struct, IValueEnumerator<TSource> {
            using var e1 = source.Enumerator;
            using var e2 = second.Enumerator;

            if(e1.TryGetNonEnumeratedCount(out var count1)&&e2.TryGetNonEnumeratedCount(out var count2)&&count1!=count2) {
                return false;
            }

#if NET8_0_OR_GREATER
            if(e1.TryGetSpan(out var sourceSpan)&&e2.TryGetSpan(out var secondSpan)) {
                // comparer 引数を渡す MemoryExtensions.SequenceEqual オーバーロードは、
                // comparer が null でも要素ごとの EqualityComparer<T>.Default ループへ落ち、
                // ベクトル化 memcmp を使う 2 引数版には乗らない。comparer が無いときは
                // SequenceEqualSpan(typeof(T) 実行時分岐で 2 引数版へ振る)を使う。
                return comparer is null
                    ? SequenceEqualSpan<TSource>(sourceSpan,secondSpan)
                    : sourceSpan.SequenceEqual(secondSpan,comparer);
            }
#endif

            comparer??=EqualityComparer<TSource>.Default;
            while(e1.TryGetNext(out var value1)) {
                if(!(e2.TryGetNext(out var value2)&&comparer.Equals(value1,value2))) {
                    return false;
                }
            }

            return !e2.TryGetNext(out _);
        }

        // FLinq の生成コードは T がジェネリック型引数（ベンチマーク基底クラス等）の場合、
        // 構文時点では要素型名が "T" としてしか見えず、生成器側の型名ホワイトリスト判定が
        // 発火しない。int 等の具象型で呼ばれる場面でも要素ごとの EqualityComparer<T>.Default
        // ループへ落ちてしまい、MemoryExtensions.SequenceEqual のベクトル化 memcmp を
        // 使えていなかった。Min/Max/Sum と同じ「呼び出し側は常にこの汎用メソッドを呼び、
        // 型分岐は実行時（呼び出し側で T が具象化された後）の typeof(T) 比較に任せる」
        // 方式に揃える。
        [EditorBrowsable(EditorBrowsableState.Never)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEqualSpan<TSource>(ReadOnlySpan<TSource> spanL,ReadOnlySpan<TSource> spanR) {
            if(spanL.Length!=spanR.Length) return false;
            if(spanL.Length==0) return true;
            if(global::System.Runtime.CompilerServices.Unsafe.AreSame(
                ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(spanL),
                ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(spanR))) {
                return true;
            }
            if(typeof(TSource)==typeof(byte)) return UnsafeSpanBitCast<TSource,byte>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,byte>(spanR));
            if(typeof(TSource)==typeof(sbyte)) return UnsafeSpanBitCast<TSource,sbyte>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,sbyte>(spanR));
            if(typeof(TSource)==typeof(short)) return UnsafeSpanBitCast<TSource,short>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,short>(spanR));
            if(typeof(TSource)==typeof(ushort)) return UnsafeSpanBitCast<TSource,ushort>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,ushort>(spanR));
            if(typeof(TSource)==typeof(int)) return UnsafeSpanBitCast<TSource,int>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,int>(spanR));
            if(typeof(TSource)==typeof(uint)) return UnsafeSpanBitCast<TSource,uint>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,uint>(spanR));
            if(typeof(TSource)==typeof(long)) return UnsafeSpanBitCast<TSource,long>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,long>(spanR));
            if(typeof(TSource)==typeof(ulong)) return UnsafeSpanBitCast<TSource,ulong>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,ulong>(spanR));
            if(typeof(TSource)==typeof(float)) return UnsafeSpanBitCast<TSource,float>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,float>(spanR));
            if(typeof(TSource)==typeof(double)) return UnsafeSpanBitCast<TSource,double>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,double>(spanR));
            if(typeof(TSource)==typeof(decimal)) return UnsafeSpanBitCast<TSource,decimal>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,decimal>(spanR));
            if(typeof(TSource)==typeof(char)) return UnsafeSpanBitCast<TSource,char>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,char>(spanR));
            if(typeof(TSource)==typeof(bool)) return UnsafeSpanBitCast<TSource,bool>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,bool>(spanR));
            if(typeof(TSource)==typeof(string)) return UnsafeSpanBitCast<TSource,string>(spanL).SequenceEqual(UnsafeSpanBitCast<TSource,string>(spanR));

            var eq = EqualityComparer<TSource>.Default;
            for(int i = 0;(uint)i<(uint)spanL.Length;i++) {
                if(!eq.Equals(spanL[i],spanR[i])) return false;
            }
            return true;
        }

        // SequenceEqualSpan と同じ理由(FLinq生成コードの呼び出し元では要素型が構文上 "T" としてしか
        // 見えず、生成器側の型名ホワイトリストが発火しない)で、comparer 省略時の Contains も
        // 呼び出し側は常にこの関数を呼び、型分岐は実行時 typeof(T) 判定に任せる。既知のプリミティブ値型
        // なら MemoryExtensions.Contains の 2 引数版(ベクトル化探索)へ振る。それ以外は
        // EqualityComparer<T>.Default による従来ループへ落とす。
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool ContainsSpan<TSource>(ReadOnlySpan<TSource> span,TSource value) {
            if(typeof(TSource)==typeof(byte)) return UnsafeSpanBitCast<TSource,byte>(span).Contains(Unsafe.As<TSource,byte>(ref value));
            if(typeof(TSource)==typeof(sbyte)) return UnsafeSpanBitCast<TSource,sbyte>(span).Contains(Unsafe.As<TSource,sbyte>(ref value));
            if(typeof(TSource)==typeof(short)) return UnsafeSpanBitCast<TSource,short>(span).Contains(Unsafe.As<TSource,short>(ref value));
            if(typeof(TSource)==typeof(ushort)) return UnsafeSpanBitCast<TSource,ushort>(span).Contains(Unsafe.As<TSource,ushort>(ref value));
            if(typeof(TSource)==typeof(int)) return UnsafeSpanBitCast<TSource,int>(span).Contains(Unsafe.As<TSource,int>(ref value));
            if(typeof(TSource)==typeof(uint)) return UnsafeSpanBitCast<TSource,uint>(span).Contains(Unsafe.As<TSource,uint>(ref value));
            if(typeof(TSource)==typeof(long)) return UnsafeSpanBitCast<TSource,long>(span).Contains(Unsafe.As<TSource,long>(ref value));
            if(typeof(TSource)==typeof(ulong)) return UnsafeSpanBitCast<TSource,ulong>(span).Contains(Unsafe.As<TSource,ulong>(ref value));
            if(typeof(TSource)==typeof(float)) return UnsafeSpanBitCast<TSource,float>(span).Contains(Unsafe.As<TSource,float>(ref value));
            if(typeof(TSource)==typeof(double)) return UnsafeSpanBitCast<TSource,double>(span).Contains(Unsafe.As<TSource,double>(ref value));
            if(typeof(TSource)==typeof(char)) return UnsafeSpanBitCast<TSource,char>(span).Contains(Unsafe.As<TSource,char>(ref value));
            if(typeof(TSource)==typeof(bool)) return UnsafeSpanBitCast<TSource,bool>(span).Contains(Unsafe.As<TSource,bool>(ref value));

            var eq = EqualityComparer<TSource>.Default;
            foreach(var item in span) {
                if(eq.Equals(item,value)) return true;
            }
            return false;
        }
    }
}
