#nullable enable
using System.Linq;

namespace Intercept.Labo.Shared.Linq.Enumerable {
    using global::Labo.Shared.Linq.Enumerable;
    using global::Labo.Shared.Linq;
    using global::Labo.Shared;
    using global::Labo;

    internal static partial class Methods1 {
        private struct OptimizedLinq_Aggregate_func_Impl_Methods1_Aggregate_func_44_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Aggregate_func_Impl_Methods1_Aggregate_func_44_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "srcCount")]
        private static extern global::System.Collections.Generic.IEnumerable<int> __Method_Base_srcCount(global::Benchmark.Shared.Base @this, int elementCount);
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "srcCache")]
        private static extern ref global::System.Collections.Generic.Dictionary<int, int[]> __Field_Base_srcCache(global::Benchmark.Shared.Base @this);
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "GetOrCreateByLength")]
        private static extern int[] __Method_Base_GetOrCreateByLength(global::Benchmark.Shared.Base @this, int length, global::System.Collections.Generic.Dictionary<int, int[]> cache, global::System.Func<int, int[]> factory);
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "Result")]
        private static extern ref object? __Field_Base_Result(global::Benchmark.Shared.Base @this);
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "consumer")]
        private static extern ref global::Benchmark.Shared.Consumer __Field_Base_consumer(global::Benchmark.Shared.Base @this);
        /// <summary>Intercepts <c>Aggregate_func_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdMHAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Aggregate_func_Impl_Methods1_Aggregate_func_44_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq0;

            using(var __s0=new OptimizedLinq_Aggregate_func_Impl_Methods1_Aggregate_func_44_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                int __r0;
            if(__s0.TryGetSpan(out var __span)){
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                __r0=default!;
                var __first=true;
                foreach(var __item in __span){
                    if(__first){
                        __r0=__item;
                        __first=false;
                    }else{
                        
            __r0=__r0+__item;
                    }
                }
            }else{
                if(!__s0.TryGetNext(out var __item)) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                __r0=__item;
                while(__s0.TryGetNext(out __item)){
                    
            __r0=__r0+__item;
                }
            }
                __linq0=__r0;
            }
            var result=__linq0;
            __Field_Base_Result(@this)=result;
            __Field_Base_consumer(@this).Consume(result);
        }

        private struct OptimizedLinq_Aggregate_seed_func_Impl_Methods1_Aggregate_seed_func_54_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Aggregate_seed_func_Impl_Methods1_Aggregate_seed_func_54_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Aggregate_seed_func_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUsJAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Aggregate_seed_func_Impl_Methods1_Aggregate_seed_func_54_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            long __linq1;

            using(var __s0=new OptimizedLinq_Aggregate_seed_func_Impl_Methods1_Aggregate_seed_func_54_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                var __r0 = 0L;
                if(__s0.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    
            __r0=__r0+__item;
                }
            }else{
                while(__s0.TryGetNext(out var __item)){
                    
            __r0=__r0+__item;
                }
            }
                __linq1=__r0;
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Aggregate_seed_func_resultSelector_Impl_Methods1_Aggregate_seed_func_resultSelector_60_57_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Aggregate_seed_func_resultSelector_Impl_Methods1_Aggregate_seed_func_resultSelector_60_57_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Aggregate_seed_func_resultSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2maoKAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Aggregate_seed_func_resultSelector_Impl_Methods1_Aggregate_seed_func_resultSelector_60_57(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            double __linq1;


            using(var __s0=new OptimizedLinq_Aggregate_seed_func_resultSelector_Impl_Methods1_Aggregate_seed_func_resultSelector_60_57_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                var __r0 = 0L;
                if(__s0.TryGetSpan(out var __span)){
                foreach(var __item in __span){
                    
            __r0=__r0+__item;
                }
            }else{
                while(__s0.TryGetNext(out var __item)){
                    
            __r0=__r0+__item;
                }
            }
                
            __linq1=(double)__r0/1000000;
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.KeyValuePair<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.KeyValuePair<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::System.Collections.Generic.Dictionary<int, int>? __lk0;
            private global::System.Collections.Generic.Dictionary<int, int>.Enumerator __ie0;
            public OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public global::System.Collections.Generic.KeyValuePair<int, int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.KeyValuePair<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.KeyValuePair<int, int> current) {
                if(this.__lk0 is null) {
                    if(__e0.TryGetSpan(out var __span)) {
                        this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>(__span.Length);
                        for(var __i=0;__i<__span.Length;__i++) {
                            var __it0=__span[__i];
                            int __k0=default!;

                __k0=__it0%1000;
                            ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.__lk0,__k0,out var __exists);
                            if(!__exists) {
                                __agg=0;
                            }

                __agg=__agg+__it0;
                        }
                    }else{
                        if(__e0.TryGetNonEnumeratedCount(out var __count)) {
                            this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>(__count);
                        }else{
                            this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>();
                        }
                        while(__e0.MoveNext()) {
                            var __it0=__e0.Current;
                            int __k0=default!;

                __k0=__it0%1000;
                            ref var __agg=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.__lk0,__k0,out var __exists);
                            if(!__exists) {
                                __agg=0;
                            }

                __agg=__agg+__it0;
                        }
                    }
                    this.__ie0=this.__lk0.GetEnumerator();
                }
                if(this.__ie0.MoveNext()) {
                    var __kv=this.__ie0.Current;
                    current=this.Current=new global::System.Collections.Generic.KeyValuePair<int, int>(__kv.Key,__kv.Value);
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>AggregateBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSoMAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct0<OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct1>(new OptimizedLinq_AggregateBy_keySelector_Impl_Methods1_AggregateBy_keySelector_66_46_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out global::System.Collections.Generic.KeyValuePair<int, int> __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<bool>, global::System.Collections.Generic.IEnumerator<bool>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public bool Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static bool Compute(TEnum source) {
                if(source.TryGetSpan(out var __span)){
                    foreach(var __it0 in __span){
                        if(!(__it0>0)) return false;
                    }
                }else{
                    while(source.MoveNext()) {
                        var __it0=source.Current;
                        if(!(__it0>0)) return false;
                    }
                }
                return true;
            }
            public OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<bool> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<bool> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out bool current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>All_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWkNAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            bool __linq1=default!;
            using var e___linq1=new OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct0<OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct1>(new OptimizedLinq_All_predicate_Impl_Methods1_All_predicate_72_36_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<bool>, global::System.Collections.Generic.IEnumerator<bool>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private bool __yd1 = false;
            public OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct1(TEnum __e1) {
                this.__e1 = __e1;
            }
            public bool Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<bool> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<bool> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out bool current) {
                if(this.__yd1) {
                    current = default;
                    return false;
                }
                this.__yd1 = true;
                if(__e1.TryGetNonEnumeratedCount(out var __count)) {
                    this.Current = __count != 0;
                }else{
                    this.Current = __e1.MoveNext();
                }
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>Any_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mXIOAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Any_Impl_Methods1_Any_78_26(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var a=0; a<800000; a++)
            {
            bool __linq1=default!;
            using var e___linq1=new OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct1<OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct0>(new OptimizedLinq_Any_Impl_Methods1_Any_78_26_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<bool>, global::System.Collections.Generic.IEnumerator<bool>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            public bool Current { get; private set; }
            private bool __yd1 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static bool Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                foreach(var __it1 in __span){
                    bool __tmp1=default!;

            __tmp1=__it1>999_999;
                    if(__tmp1) return true;
                }
                return false;
            }
            public OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct1(TEnum __e1) {
                this.__e1 = __e1;
                    this.Current = Compute(__e1);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<bool> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<bool> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out bool current) {
                if(this.__yd1) {
                    current = default!;
                    return false;
                }
                this.__yd1 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>Any_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mbMPAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            bool __linq1=default!;
            using var e___linq1=new OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct1<OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct0>(new OptimizedLinq_Any_predicate_Impl_Methods1_Any_predicate_88_36_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int element0;
            private byte __st0;
            public OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct0(TEnum __e0, int element0) {
                this.__e0 = __e0;
                this.element0 = element0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__st0==0) {
                    if(__e0.MoveNext()) {
                        current=this.Current=__e0.Current;
                        return true;
                    }
                    this.__st0=1;
                }
                if(this.__st0==1) {
                    this.__st0=2;
                    current=this.Current=element0;
                    return true;
                }
                current=default!;
                return false;

            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Append_element_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQERAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct0<OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct1>(new OptimizedLinq_Append_element_Impl_Methods1_Append_element_95_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 0);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static double Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                return (double)global::Tsavorite.Carrier.ValueEnumerableExtensions.AverageIntSimd(__span);
            }
        }
        /// <summary>Intercepts <c>Average_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQcSAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Average_Impl_Methods1_Average_101_30(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            double __linq1;
            using(var __s0=new OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct0<OptimizedLinq_Average_Impl_Methods1_Average_101_30_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static double Compute(TEnum source) {
                double sum=0;
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                foreach(var __it0 in __span){
                    double __tmp0=default!;

            __tmp0=(double)__it0;
                    sum+=__tmp0;
                }
                return (double)(sum/__span.Length);
            }
        }
        /// <summary>Intercepts <c>Average_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mRcTAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            double __linq1;
            using(var __s0=new OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct0<OptimizedLinq_Average_selector_Impl_Methods1_Average_selector_107_39_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.MoveNext()) {
                    current = this.Current = (int)__e0.Current!;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Cast_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVYUAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Cast_Impl_Methods1_Cast_114_27(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct0<OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct1>(new OptimizedLinq_Cast_Impl_Methods1_Cast_114_27_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int[]>, global::System.Collections.Generic.IEnumerator<int[]>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int size;
            private int __a0;
            private int[]? __g0;
            public OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct0(TEnum __e0, int size) {
                this.__e0 = __e0;
                this.size = size;
            }
            public int[] Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int[]> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int[]> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int[] current) {
                if(size<=0) throw new global::System.ArgumentOutOfRangeException(nameof(size));
                if(__e0.TryGetSpan(out var __span)) {
                    if((uint)this.__a0<(uint)__span.Length) {
                        var len=global::System.Math.Min(size,__span.Length-this.__a0);
                        current=this.Current=__span.Slice(this.__a0,len).ToArray();
                        this.__a0+=len;
                        return true;
                    }
                    current=default!;
                    return false;
                }
                var __i=0;
                if(this.__g0 is null) this.__g0=new int[size];
                while(__e0.MoveNext()) {
                    this.__g0[__i++]= __e0.Current;
                    if(__i==size) {
                        current=this.Current=this.__g0;
                        this.__g0=new int[size];
                        return true;
                    }
                }
                if(__i>0) {
                    var __last=new int[__i];
                    global::System.Array.Copy(this.__g0,__last,__i);
                    current=this.Current=__last;
                    this.__g0=null;
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Chunk_size_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVMVAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct0<OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct1>(new OptimizedLinq_Chunk_size_Impl_Methods1_Chunk_size_120_33_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 1000);
            while(__ce0.TryGetNext(out int[] __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct0<TEnum1, TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<int>
            where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private bool __hv0;
            public OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct0(TEnum1 source, TEnum2 second) {
                this.source = source;
                this.second = second;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.__hv0) {
                    if(source.MoveNext()) {
                        current=this.Current=source.Current;
                        return true;
                    }
                    this.__hv0=true;
                }
                if(second.MoveNext()) {
                    current=this.Current=(int)(object)second.Current!;
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "src2Cache")]
        private static extern ref global::System.Collections.Generic.Dictionary<int, int[]> __Field_Base_src2Cache(global::Benchmark.Shared.Base @this);
        /// <summary>Intercepts <c>Concat_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYwWAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct0<OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct1, OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct2>(new OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), new OptimizedLinq_Concat_second_Impl_Methods1_Concat_second_127_36_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static bool Compute(TEnum source, int value) {
                var comparer=global::System.Collections.Generic.EqualityComparer<int>.Default;
                source.TryGetSpan(out var __span);
                foreach(var item in __span){
                    if(comparer.Equals(item,value)) return true;
                }
                return false;
            }
        }
        /// <summary>Intercepts <c>Contains_value_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mb0XAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int value=500_000;
            for(var i=0; i<3; i++)
            {
            bool __linq1;
            using(var __s0=new OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct0<OptimizedLinq_Contains_value_Impl_Methods1_Contains_value_133_37_補助0_Struct1>.Compute(__s0, value);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return (int)__span.Length;
            }
        }
        /// <summary>Intercepts <c>Count_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUcZAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Count_Impl_Methods1_Count_145_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct0<OptimizedLinq_Count_Impl_Methods1_Count_145_28_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public int Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                int count = 0;
                checked{
                    for(var __i = 0; __i < __span.Length; __i++) {
                        var __it0 = __span[__i];
                        bool __tmp0 = default!;

            __tmp0=(__it0&1)==0;
                        if(__tmp0) count++;
                    }
                }
                return count;
            }
            public OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Count_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mU8aAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1=default!;
            using var e___linq1=new OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct0<OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct1>(new OptimizedLinq_Count_predicate_Impl_Methods1_Count_predicate_151_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.KeyValuePair<int, global::System.Int32>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.KeyValuePair<int, global::System.Int32>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::System.Collections.Generic.Dictionary<int, int>? __lk0;
            private global::System.Collections.Generic.Dictionary<int, int>.Enumerator __ie0;
            public OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public global::System.Collections.Generic.KeyValuePair<int, global::System.Int32> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.KeyValuePair<int, global::System.Int32>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.KeyValuePair<int, global::System.Int32>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.KeyValuePair<int, global::System.Int32> current) {
                if(this.__lk0 is null) {
                    if(__e0.TryGetSpan(out var __span)) {
                        this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>(__span.Length);
                        for(var __i=0;__i<__span.Length;__i++) {
                            var __it0=__span[__i];
                            int __k0=default!;

                __k0=__it0%1000;
                            ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.__lk0,__k0,out var __exists);
                            __cnt++;
                        }
                    }else{
                        if(__e0.TryGetNonEnumeratedCount(out var __count)) {
                            this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>(__count);
                        }else{
                            this.__lk0=new global::System.Collections.Generic.Dictionary<int, int>();
                        }
                        while(__e0.MoveNext()) {
                            var __it0=__e0.Current;
                            int __k0=default!;

                __k0=__it0%1000;
                            ref var __cnt=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(this.__lk0,__k0,out var __exists);
                            __cnt++;
                        }
                    }
                    this.__ie0=this.__lk0.GetEnumerator();
                }
                if(this.__ie0.MoveNext()) {
                    var __kv=this.__ie0.Current;
                    current=this.Current=new global::System.Collections.Generic.KeyValuePair<int, global::System.Int32>(__kv.Key,__kv.Value);
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>CountBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYMbAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct0<OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct1>(new OptimizedLinq_CountBy_keySelector_Impl_Methods1_CountBy_keySelector_157_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out global::System.Collections.Generic.KeyValuePair<int, global::System.Int32> __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int defaultValue;
            private bool __hv0;
            private bool __yd0;
            public OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct0(TEnum __e0, int defaultValue) {
                this.__e0 = __e0;
                this.defaultValue = defaultValue;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(__e0.TryGetNonEnumeratedCount(out var __c)) {
                    if(__c==0) {
                        count=1;
                    }else{
                        count=__c;
                    }
                    return true;
                }
                count=0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.MoveNext()) {
                    this.__hv0=true;
                    current=this.Current=__e0.Current;
                    return true;
                }
                if(!this.__hv0&&!this.__yd0) {
                    this.__yd0=true;
                    current=this.Current=defaultValue;
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>DefaultIfEmpty_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdAcAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct0<OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct1>(new OptimizedLinq_DefaultIfEmpty_Impl_Methods1_DefaultIfEmpty_164_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 0);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.HashSet<int> __lk0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                // Distinct は列挙途中で set を更新し続けるため、ここでは root の型に応じた span 判定を入れない。
                // 生成時点で root が span 直走査できる場合は上位の consumer 専用 lowering が選ばれ、それ以外はこの enumerator 経路に落ちる。
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31_補助0_Struct0(TEnum __e0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(__e0,comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(__e0.TryGetNext(out var __it0)){
                    if(this.__lk0.Add(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Distinct_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQkeAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_Distinct_Impl_Methods1_Distinct_171_31_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __lk0;
            if(__src0.TryGetNonEnumeratedCount(out var __count___lk0)){
                __lk0=new global::System.Collections.Generic.HashSet<int>(__count___lk0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __lk0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(__lk0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(__lk0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.HashSet<int> __lk0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                // DistinctBy も同じで、span 走査かどうかの判定はここではしない。
                // コンパイル時に root 形状が分かる場合は上位で専用生成されるため、ここは汎用経路として最小状態だけを保持する。
                return new global::System.Collections.Generic.HashSet<int>(1999,comparer);
            }
            public OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45_補助0_Struct0(TEnum __e0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(__e0,comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(__e0.TryGetNext(out var __it0)){
                    int __k0=default!;

                __k0=__it0%1000;
                    if(this.__lk0.Add(__k0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>DistinctBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUMfAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_DistinctBy_keySelector_Impl_Methods1_DistinctBy_keySelector_178_45_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            var __seen0=new bool[1999];
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    int __key0=default!;

                    __key0=value%1000;
                    var __seenIndex0=__key0-(-999);
                    if(!__seen0[__seenIndex0]){
                        __seen0[__seenIndex0]=true;
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    int __key0=default!;

                    __key0=value%1000;
                    var __seenIndex0=__key0-(-999);
                    if(!__seen0[__seenIndex0]){
                        __seen0[__seenIndex0]=true;
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source, int index) {
                if(index<0) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                source.TryGetSpan(out var __span);
                if((uint)index>=(uint)__span.Length) throw new global::System.ArgumentOutOfRangeException(nameof(index));
                return __span[index];
            }
        }
        /// <summary>Intercepts <c>ElementAt_index_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mW0gAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<3; i++)
            {
            int __linq1;
            using(var __s0=new OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct0<OptimizedLinq_ElementAt_index_Impl_Methods1_ElementAt_index_184_38_補助0_Struct1>.Compute(__s0, 500000);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source, int index) {
                if(index<0) return default!;
                source.TryGetSpan(out var __span);
                if((uint)index>=(uint)__span.Length) return default!;
                return __span[index];
            }
        }
        /// <summary>Intercepts <c>ElementAtOrDefault_index_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQsiAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct0<OptimizedLinq_ElementAtOrDefault_index_Impl_Methods1_ElementAtOrDefault_index_194_47_補助0_Struct1>.Compute(__s0, 500_000);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Empty_Impl_Methods1_Empty_200_28_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            public int Current => default!;
            object? global::System.Collections.IEnumerator.Current => default;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.ReadOnlySpan<int>.Empty;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => true;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Empty_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mT0jAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Empty_Impl_Methods1_Empty_200_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<25000; i++)
            {
            var consumer___linq0=__Field_Base_consumer(@this);

            }
        }

        private struct OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private bool __yd0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(this.second,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.__yd0){
                    while(second.TryGetNext(out var __it0)){
                        this.__lk0.Add(__it0);
                    }
                    this.__yd0=true;
                }
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Add(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>Except_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2maskAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_Except_second_Impl_Methods1_Except_second_212_36_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__sec0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    __set0.Add(value);
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    __set0.Add(value);
                }
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private byte __st0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(this.second,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__st0==0){
                    while(second.TryGetNext(out var __tmp0)) this.__lk0.Add(__tmp0);
                    this.__st0=1;
                }
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Add(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>ExceptBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQEmAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_ExceptBy_keySelector_Impl_Methods1_ExceptBy_keySelector_219_43_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__sec0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    __set0.Add(value);
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    __set0.Add(value);
                }
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(!__set0.Add(value)){
                        continue;
                    }
                    consumer___linq0.Consume(value);
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(!__set0.Add(value)){
                        continue;
                    }
                    consumer___linq0.Consume(value);
                }
            }
        }

        private struct OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                if(source.MoveNext()) return source.Current;
                throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
            }
        }
        /// <summary>Intercepts <c>First_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSUnAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_First_Impl_Methods1_First_225_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<5000; i++)
            {
            int __linq1;
            using(var __s0=new OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct0<OptimizedLinq_First_Impl_Methods1_First_225_28_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                return source.MoveNext() ? source.Current : default!;
            }
        }
        /// <summary>Intercepts <c>FirstOrDefault_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mX0oAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<4000; i++)
            {
            int __linq1;
            using(var __s0=new OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct0<OptimizedLinq_FirstOrDefault_Impl_Methods1_FirstOrDefault_236_37_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public int Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                while(source.MoveNext()) {
                    var __it0 = source.Current;
                    bool __tmp0 = default!;

            __tmp0=__it0>500_000;
                    if(__tmp0) return __it0;
                }
                return default!;
            }
            public OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>FirstOrDefault_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQ0qAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1=default!;
            using var e___linq1=new OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct0<OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct1>(new OptimizedLinq_FirstOrDefault_predicate_Impl_Methods1_FirstOrDefault_predicate_247_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Linq.IGrouping<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __g0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __lk0;
            private bool __hv0;
            public OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public global::System.Linq.IGrouping<int, int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Linq.IGrouping<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Linq.IGrouping<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Linq.IGrouping<int, int> current) {
                if(!this.__hv0){
                    this.__hv0=true;
                    var __builder=new global::Tsavorite.Carrier.Linq.LookupBuilder<int, int>(global::System.Collections.Generic.EqualityComparer<int>.Default);
                                if(__e0.TryGetSpan(out var __span)){
                                    for(var __i=0;__i<__span.Length;__i++){
                                    var __it0=__span[__i];
                                    int __k0=default!;

                                                __k0=__it0%1000;
                                                    int __a0=default!;

                                                __a0=__it0*2;
                                    __builder.Add(__k0,__a0);
                                }
                            }else{
                                    while(__e0.MoveNext()){
                                    var __it0=__e0.Current;
                                    int __k0=default!;

                                                __k0=__it0%1000;
                                                    int __a0=default!;

                                                __a0=__it0*2;
                                    __builder.Add(__k0,__a0);
                                }
                            }
                    this.__g0=this.__lk0=__builder.GetRootGroupAndClear();
                }
                if(this.__g0 is null){
                    current=default!;
                    return false;
                }
                var __cur=this.__g0;
                var __next=__cur.NextGroupInAddOrder;
                if(__next==this.__lk0){
                    this.__g0=null;
                }else{
                    this.__g0=__next;
                }current=this.Current=__cur;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>GroupBy_elementSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWUrAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct0<OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct1>(new OptimizedLinq_GroupBy_elementSelector_Impl_Methods1_GroupBy_elementSelector_253_46_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out global::System.Linq.IGrouping<int, int> __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __g0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __lk0;
            private bool __hv0;
            public OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.__hv0){
                    this.__hv0=true;
                    var __builder=new global::Tsavorite.Carrier.Linq.LookupBuilder<int, int>(global::System.Collections.Generic.EqualityComparer<int>.Default);
                                if(__e0.TryGetSpan(out var __span)){
                                    for(var __i=0;__i<__span.Length;__i++){
                                    var __it0=__span[__i];
                                    int __k0=default!;

                                                __k0=__it0%1000;
                                                    int __a0=default!;

                                                __a0=__it0*2;
                                    __builder.Add(__k0,__a0);
                                }
                            }else{
                                    while(__e0.MoveNext()){
                                    var __it0=__e0.Current;
                                    int __k0=default!;

                                                __k0=__it0%1000;
                                                    int __a0=default!;

                                                __a0=__it0*2;
                                    __builder.Add(__k0,__a0);
                                }
                            }
                    this.__g0=this.__lk0=__builder.GetRootGroupAndClear();
                }
                if(this.__g0 is null){
                    current=default!;
                    return false;
                }
                var __cur=this.__g0;
                var __next=__cur.NextGroupInAddOrder;
                if(__next==this.__lk0){
                    this.__g0=null;
                }else{
                    this.__g0=__next;
                }current = Current = __cur.Key;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>GroupBy_elementSelector_resultSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mcgsAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct0<OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct1>(new OptimizedLinq_GroupBy_elementSelector_resultSelector_Impl_Methods1_GroupBy_elementSelector_resultSelector_259_61_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Linq.IGrouping<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __g0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __lk0;
            private bool __hv0;
            public OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public global::System.Linq.IGrouping<int, int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Linq.IGrouping<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Linq.IGrouping<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Linq.IGrouping<int, int> current) {
                if(!this.__hv0){
                    this.__hv0=true;
                    var __builder=new global::Tsavorite.Carrier.Linq.LookupBuilder<int, int>(global::System.Collections.Generic.EqualityComparer<int>.Default);
                                if(__e0.TryGetSpan(out var __span)){
                                    for(var __i=0;__i<__span.Length;__i++){
                                    var __it0=__span[__i];
                                    int __k0=default!;

                                                __k0=__it0%1000;

                                    __builder.Add(__k0,__it0);
                                }
                            }else{
                                    while(__e0.MoveNext()){
                                    var __it0=__e0.Current;
                                    int __k0=default!;

                                                __k0=__it0%1000;

                                    __builder.Add(__k0,__it0);
                                }
                            }
                    this.__g0=this.__lk0=__builder.GetRootGroupAndClear();
                }
                if(this.__g0 is null){
                    current=default!;
                    return false;
                }
                var __cur=this.__g0;
                var __next=__cur.NextGroupInAddOrder;
                if(__next==this.__lk0){
                    this.__g0=null;
                }else{
                    this.__g0=__next;
                }current=this.Current=__cur;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>GroupBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mTAuAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct0<OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct1>(new OptimizedLinq_GroupBy_keySelector_Impl_Methods1_GroupBy_keySelector_265_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out global::System.Linq.IGrouping<int, int> __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __g0;
            private global::Tsavorite.Carrier.Linq.Grouping<int, int>? __lk0;
            private bool __hv0;
            public OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.__hv0){
                    this.__hv0=true;
                    var __builder=new global::Tsavorite.Carrier.Linq.LookupBuilder<int, int>(global::System.Collections.Generic.EqualityComparer<int>.Default);
                                if(__e0.TryGetSpan(out var __span)){
                                    for(var __i=0;__i<__span.Length;__i++){
                                    var __it0=__span[__i];
                                    int __k0=default!;

                                                __k0=__it0%1000;

                                    __builder.Add(__k0,__it0);
                                }
                            }else{
                                    while(__e0.MoveNext()){
                                    var __it0=__e0.Current;
                                    int __k0=default!;

                                                __k0=__it0%1000;

                                    __builder.Add(__k0,__it0);
                                }
                            }
                    this.__g0=this.__lk0=__builder.GetRootGroupAndClear();
                }
                if(this.__g0 is null){
                    current=default!;
                    return false;
                }
                var __cur=this.__g0;
                var __next=__cur.NextGroupInAddOrder;
                if(__next==this.__lk0){
                    this.__g0=null;
                }else{
                    this.__g0=__next;
                }current = Current = __cur.Key;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>GroupBy_resultSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWQvAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct0<OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct1>(new OptimizedLinq_GroupBy_resultSelector_Impl_Methods1_GroupBy_resultSelector_271_45_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 28000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct0<TOuterEnum,TInnerEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<int>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<int> {
            private TOuterEnum __e0;
            private TInnerEnum __ie0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> __lk0;
            private int __st0;
            private static global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                var __lk0=new global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>>(comparer ?? global::System.Collections.Generic.EqualityComparer<int>.Default);
                using var __ie0=inner;
                while(__ie0.MoveNext()){

                    var __it0=__ie0.Current;
                    int __k0=default!;

            __k0=__it0;

                    if(!__lk0.TryGetValue(__k0,out var __g0)){
                        __g0=new global::System.Collections.Generic.List<int>();
                        __lk0.Add(__k0,__g0);
                    }

                    __g0.Add(__it0);
                }
                return __lk0;
            }
            public OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct0(TOuterEnum __e0,TInnerEnum __ie0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.__ie0 = __ie0;
                this.comparer = comparer;
                    this.__lk0=BuildLookup(this.__ie0,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__st0==1){
                    current=default!;
                    return false;
                }
                if(__e0.MoveNext()){

                    var __ov0=__e0.Current;
                    int __k0=default!;

                __k0=__ov0;

                    if(__lk0.TryGetValue(__k0,out var __g0)){
                        current = this.Current = __ov0;
                    }else{
                        current = this.Current = __ov0;
                    }
                    current=this.Current;
                    return true;
                }
                __st0=1;
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>GroupJoin_inner_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mZ0wAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct0<OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct1,OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct2>(new OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })),new OptimizedLinq_GroupJoin_inner_Impl_Methods1_GroupJoin_inner_277_38_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })),null);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<(int Index, int Item)>, global::System.Collections.Generic.IEnumerator<(int Index, int Item)>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int __a0=0;
            public OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public (int Index, int Item) Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<(int Index, int Item)> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<(int Index, int Item)> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out (int Index, int Item) current) {
                if(__e0.MoveNext()) {
                    current=this.Current=(this.__a0++,__e0.Current);
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Index_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdQxAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Index_Impl_Methods1_Index_283_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct0<OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct1>(new OptimizedLinq_Index_Impl_Methods1_Index_283_28_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out (int Index, int Item) __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private bool __yd0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(this.second,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.__yd0){
                    while(second.TryGetNext(out var __it0)){
                        this.__lk0.Add(__it0);
                    }
                    this.__yd0=true;
                }
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Remove(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>Intersect_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQQzAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_Intersect_second_Impl_Methods1_Intersect_second_290_39_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__sec0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    __set0.Add(value);
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    __set0.Add(value);
                }
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(__set0.Remove(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(__set0.Remove(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private byte __st0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(this.second,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__st0==0){
                    while(second.TryGetNext(out var __tmp0)) this.__lk0.Add(__tmp0);
                    this.__st0=1;
                }
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Remove(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>IntersectBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWk0AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_IntersectBy_keySelector_Impl_Methods1_IntersectBy_keySelector_297_46_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__sec0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    __set0.Add(value);
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    __set0.Add(value);
                }
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(!__set0.Remove(value)){
                        continue;
                    }
                    consumer___linq0.Consume(value);
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(!__set0.Remove(value)){
                        continue;
                    }
                    consumer___linq0.Consume(value);
                }
            }
        }

        private struct OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.TryGetNext(out var __it0)) {

                    current = this.Current = __it0+global::Benchmark.Shared.HoistInvariant.Value();
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>InvariantHoisting_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2ma41AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct0<OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct1>(new OptimizedLinq_InvariantHoisting_Impl_Methods1_InvariantHoisting_303_40_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct0<TOuterEnum,TInnerEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<int>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<int> {
            private TOuterEnum __e0;
            private TInnerEnum __ie0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> __lk0;
            private global::System.Collections.Generic.List<int>? __g0;
            private int __gi0;
            private int __st0;
            private int __ov0;
            private static bool IsNullKey<T>(T key){
                return key is null;
            }
            private static global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                var __lk0=new global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>>(comparer ?? global::System.Collections.Generic.EqualityComparer<int>.Default);
                using var __ie0=inner;
                while(__ie0.MoveNext()){

                    var __it0=__ie0.Current;
                    int __k0=default!;

            __k0=__it0;
                    if(IsNullKey(__k0)){
                        continue;
                    }

                    if(!__lk0.TryGetValue(__k0,out var __g0)){
                        __g0=new global::System.Collections.Generic.List<int>();
                        __lk0.Add(__k0,__g0);
                    }

                    __g0.Add(__it0);
                }
                return __lk0;
            }
            public OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct0(TOuterEnum __e0,TInnerEnum __ie0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.__ie0 = __ie0;
                this.comparer = comparer;
                    this.__lk0=BuildLookup(this.__ie0,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(__st0==1&&__g0 is not null){
                    if(__gi0<__g0.Count){
                        var __it0=__g0[__gi0++];
                        current = this.Current = __ov0;
                        return true;
                    }
                    __g0=null;
                    __st0=0;
                }
                if(!__e0.MoveNext()){
                    current=default!;
                    return false;
                }

                __ov0=__e0.Current;
                int __k0=default!;

                __k0=__ov0;
                if(IsNullKey(__k0)){
                    goto Start0;
                }

                if(!__lk0.TryGetValue(__k0,out __g0)){
                    goto Start0;
                }
                __gi0=0;
                __st0=1;
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Join_inner_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2md02AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct0<OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct1,OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct2>(new OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })),new OptimizedLinq_Join_inner_Impl_Methods1_Join_inner_309_33_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })),null);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct0<TOuterEnum,TInnerEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<int>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<int> {
            private TOuterEnum __e0;
            private TInnerEnum __ie0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> __lk0;
            private global::System.Collections.Generic.List<int>? __g0;
            private int __gi0;
            private int __st0;
            private int __ov0;
            private static bool IsNullKey<T>(T key){
                return key is null;
            }
            private static global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                var __lk0=new global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>>(comparer ?? global::System.Collections.Generic.EqualityComparer<int>.Default);
                using var __ie0=inner;
                while(__ie0.MoveNext()){

                    var __it0=__ie0.Current;
                    int __k0=default!;

            __k0=(__it0+global::Benchmark.Shared.HoistInvariant.Value())%1000;
                    if(IsNullKey(__k0)){
                        continue;
                    }

                    if(!__lk0.TryGetValue(__k0,out var __g0)){
                        __g0=new global::System.Collections.Generic.List<int>();
                        __lk0.Add(__k0,__g0);
                    }

                    __g0.Add(__it0);
                }
                return __lk0;
            }
            public OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct0(TOuterEnum __e0,TInnerEnum __ie0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.__ie0 = __ie0;
                this.comparer = comparer;
                    this.__lk0=BuildLookup(this.__ie0,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(__st0==1&&__g0 is not null){
                    if(__gi0<__g0.Count){
                        var __it0=__g0[__gi0++];
                        current = this.Current = __ov0+__it0;
                        return true;
                    }
                    __g0=null;
                    __st0=0;
                }
                if(!__e0.MoveNext()){
                    current=default!;
                    return false;
                }

                __ov0=__e0.Current;
                int __k0=default!;

                __k0=__ov0%1000;
                if(IsNullKey(__k0)){
                    goto Start0;
                }

                if(!__lk0.TryGetValue(__k0,out __g0)){
                    goto Start0;
                }
                __gi0=0;
                __st0=1;
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Join_innerKey_Hoisting_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mRs4AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct0<OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct1,OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct2>(new OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })),new OptimizedLinq_Join_innerKey_Hoisting_Impl_Methods1_Join_innerKey_Hoisting_315_45_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })),null);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct0<TOuterEnum,TInnerEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<int>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<int> {
            private TOuterEnum __e0;
            private TInnerEnum __ie0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> __lk0;
            private global::System.Collections.Generic.List<int>? __g0;
            private int __gi0;
            private int __st0;
            private int __ov0;
            private static bool IsNullKey<T>(T key){
                return key is null;
            }
            private static global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                var __lk0=new global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>>(comparer ?? global::System.Collections.Generic.EqualityComparer<int>.Default);
                using var __ie0=inner;
                while(__ie0.MoveNext()){

                    var __it0=__ie0.Current;
                    int __k0=default!;

            __k0=__it0%1000;
                    if(IsNullKey(__k0)){
                        continue;
                    }

                    if(!__lk0.TryGetValue(__k0,out var __g0)){
                        __g0=new global::System.Collections.Generic.List<int>();
                        __lk0.Add(__k0,__g0);
                    }

                    __g0.Add(__it0);
                }
                return __lk0;
            }
            public OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct0(TOuterEnum __e0,TInnerEnum __ie0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.__ie0 = __ie0;
                this.comparer = comparer;
                    this.__lk0=BuildLookup(this.__ie0,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(__st0==1&&__g0 is not null){
                    if(__gi0<__g0.Count){
                        var __it0=__g0[__gi0++];
                        current = this.Current = __ov0+__it0;
                        return true;
                    }
                    __g0=null;
                    __st0=0;
                }
                if(!__e0.MoveNext()){
                    current=default!;
                    return false;
                }

                __ov0=__e0.Current;
                int __k0=default!;

                __k0=(__ov0+global::Benchmark.Shared.HoistInvariant.Value())%1000;
                if(IsNullKey(__k0)){
                    goto Start0;
                }

                if(!__lk0.TryGetValue(__k0,out __g0)){
                    goto Start0;
                }
                __gi0=0;
                __st0=1;
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Join_outerKey_Hoisting_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mZY5AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct0<OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct1,OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct2>(new OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })),new OptimizedLinq_Join_outerKey_Hoisting_Impl_Methods1_Join_outerKey_Hoisting_321_45_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })),null);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct0<TOuterEnum,TInnerEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TOuterEnum:struct,global::System.Collections.Generic.IEnumerator<int>
            where TInnerEnum:struct,global::System.Collections.Generic.IEnumerator<int> {
            private TOuterEnum __e0;
            private TInnerEnum __ie0;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> __lk0;
            private global::System.Collections.Generic.List<int>? __g0;
            private int __gi0;
            private int __st0;
            private int __ov0;
            private static bool IsNullKey<T>(T key){
                return key is null;
            }
            private static global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> BuildLookup(TInnerEnum inner,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                var __lk0=new global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>>(comparer ?? global::System.Collections.Generic.EqualityComparer<int>.Default);
                using var __ie0=inner;
                while(__ie0.MoveNext()){

                    var __it0=__ie0.Current;
                    int __k0=default!;

            __k0=__it0%1000;
                    if(IsNullKey(__k0)){
                        continue;
                    }

                    if(!__lk0.TryGetValue(__k0,out var __g0)){
                        __g0=new global::System.Collections.Generic.List<int>();
                        __lk0.Add(__k0,__g0);
                    }

                    __g0.Add(__it0);
                }
                return __lk0;
            }
            public OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct0(TOuterEnum __e0,TInnerEnum __ie0,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.__e0 = __e0;
                this.__ie0 = __ie0;
                this.comparer = comparer;
                    this.__lk0=BuildLookup(this.__ie0,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(__st0==1&&__g0 is not null){
                    if(__gi0<__g0.Count){
                        var __it0=__g0[__gi0++];
                        current = this.Current = __ov0+__it0+global::Benchmark.Shared.HoistInvariant.Value();
                        return true;
                    }
                    __g0=null;
                    __st0=0;
                }
                if(!__e0.MoveNext()){
                    current=default!;
                    return false;
                }

                __ov0=__e0.Current;
                int __k0=default!;

                __k0=__ov0%1000;
                if(IsNullKey(__k0)){
                    goto Start0;
                }

                if(!__lk0.TryGetValue(__k0,out __g0)){
                    goto Start0;
                }
                __gi0=0;
                __st0=1;
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Join_resultSelector_Hoisting_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mR07AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct0<OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct1,OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct2>(new OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })),new OptimizedLinq_Join_resultSelector_Hoisting_Impl_Methods1_Join_resultSelector_Hoisting_327_51_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 330000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })),null);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                return __span[__span.Length-1];
            }
        }
        /// <summary>Intercepts <c>Last_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYI8AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Last_Impl_Methods1_Last_333_27(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct0<OptimizedLinq_Last_Impl_Methods1_Last_333_27_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return __span.Length>0?__span[__span.Length-1]:default!;
            }
        }
        /// <summary>Intercepts <c>LastOrDefault_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYM9AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct0<OptimizedLinq_LastOrDefault_Impl_Methods1_LastOrDefault_339_36_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public int Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                int result = default!;
                source.TryGetSpan(out var __span);
                for(var __i=__span.Length-1;__i>=0;__i--){
                    var __it0=__span[__i];
                    bool __tmp0=default!;

            __tmp0=__it0>500000;
                    if(__tmp0){
                        return __it0;
                    }
                }
                return result;
            }
            public OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>LastOrDefault_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mbM+AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1=default!;
            using var e___linq1=new OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct0<OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct1>(new OptimizedLinq_LastOrDefault_predicate_Impl_Methods1_LastOrDefault_predicate_345_46_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static long Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                return (long)__span.Length;
            }
        }
        /// <summary>Intercepts <c>LongCount_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mes/AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            long __linq1;
            using(var __s0=new OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct0<OptimizedLinq_LongCount_Impl_Methods1_LongCount_351_32_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<long>, global::System.Collections.Generic.IEnumerator<long>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public long Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static long Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                long count = 0;
                checked{
                    for(var __i = 0; __i < __span.Length; __i++) {
                        var __it0 = __span[__i];
                        bool __tmp0 = default!;

            __tmp0=(__it0&1)==0;
                        if(__tmp0) count++;
                    }
                }
                return count;
            }
            public OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<long> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<long> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out long current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>LongCount_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQdBAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            long __linq1=default!;
            using var e___linq1=new OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct0<OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct1>(new OptimizedLinq_LongCount_predicate_Impl_Methods1_LongCount_predicate_357_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Max_Impl_Methods1_Max_363_26_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Max_Impl_Methods1_Max_363_26_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Max_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSdCAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Max_Impl_Methods1_Max_363_26(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Max_Impl_Methods1_Max_363_26_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=global::Tsavorite.Carrier.ValueEnumerableExtensions.Max(new global::Tsavorite.Carrier.ValueEnumerable<OptimizedLinq_Max_Impl_Methods1_Max_363_26_補助0_Struct1, int>(__s0));
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                int result=default!;
                if(source.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    var __first=true;
                    foreach(var __it0 in __span){
                        int value=default!;

            value=__it0*2;
                        if(__first||comparer.Compare(value,result)>0){
                            __first=false;
                            result=value;
                        }
                    }
                }else{
                    int __it0;
                    if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    __it0=source.Current;

            result=__it0*2;
                    while(source.MoveNext()){
                        __it0=source.Current;
                        int value=default!;

            value=__it0*2;
                        if(comparer.Compare(value,result)>0) result=value;
                    }
                }
                return result;
            }
        }
        /// <summary>Intercepts <c>Max_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSNDAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct0<OptimizedLinq_Max_selector_Impl_Methods1_Max_selector_369_35_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source){
                if(source.TryGetSpan(out var __span)){
                    var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                    if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    int bestElement=default!;
                    int bestKey=default!;
                    var __first=true;
                    foreach(var __it0 in __span){
                        int __k0=default!;

            __k0=__it0%1000;
                        if(__first||comparer.Compare(__k0,bestKey)>0){
                            __first=false;
                            bestElement=__it0;
                            bestKey=__k0;
                        }
                    }
                    return bestElement;
                }
                return ComputeEnumerator(source);
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int ComputeEnumerator(TEnum source){
                var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                int bestElement=source.Current;
                int bestKey=default!;

            bestKey=bestElement%1000;
                while(source.MoveNext()){
                    var __it0=source.Current;
                    int __k0=default!;

            __k0=__it0%1000;
                    if(comparer.Compare(__k0,bestKey)>0){
                        bestElement=__it0;
                        bestKey=__k0;
                    }
                }
                return bestElement;
            }
        }
        /// <summary>Intercepts <c>MaxBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUFEAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct0<OptimizedLinq_MaxBy_keySelector_Impl_Methods1_MaxBy_keySelector_375_40_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Min_Impl_Methods1_Min_381_26_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Min_Impl_Methods1_Min_381_26_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Min_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVJFAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Min_Impl_Methods1_Min_381_26(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Min_Impl_Methods1_Min_381_26_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=global::Tsavorite.Carrier.ValueEnumerableExtensions.Min(new global::Tsavorite.Carrier.ValueEnumerable<OptimizedLinq_Min_Impl_Methods1_Min_381_26_補助0_Struct1, int>(__s0));
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                int result=default!;
                if(source.TryGetSpan(out var __span)){
                    if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    var __first=true;
                    foreach(var __it0 in __span){
                        int value=default!;

            value=__it0*2;
                        if(__first||comparer.Compare(value,result)<0){
                            __first=false;
                            result=value;
                        }
                    }
                }else{
                    int __it0;
                    if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    __it0=source.Current;

            result=__it0*2;
                    while(source.MoveNext()){
                        __it0=source.Current;
                        int value=default!;

            value=__it0*2;
                        if(comparer.Compare(value,result)<0) result=value;
                    }
                }
                return result;
            }
        }
        /// <summary>Intercepts <c>Min_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mU5GAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct0<OptimizedLinq_Min_selector_Impl_Methods1_Min_selector_387_35_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source){
                if(source.TryGetSpan(out var __span)){
                    var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                    if(__span.Length==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                    int bestElement=default!;
                    int bestKey=default!;
                    var __first=true;
                    foreach(var __it0 in __span){
                        int __k0=default!;

            __k0=__it0%1000;
                        if(__first||comparer.Compare(__k0,bestKey)<0){
                            __first=false;
                            bestElement=__it0;
                            bestKey=__k0;
                        }
                    }
                    return bestElement;
                }
                return ComputeEnumerator(source);
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int ComputeEnumerator(TEnum source){
                var comparer=global::System.Collections.Generic.Comparer<int>.Default;
                if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                int bestElement=source.Current;
                int bestKey=default!;

            bestKey=bestElement%1000;
                while(source.MoveNext()){
                    var __it0=source.Current;
                    int __k0=default!;

            __k0=__it0%1000;
                    if(comparer.Compare(__k0,bestKey)<0){
                        bestElement=__it0;
                        bestKey=__k0;
                    }
                }
                return bestElement;
            }
        }
        /// <summary>Intercepts <c>MinBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWxHAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            using(var __s0=new OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct0<OptimizedLinq_MinBy_keySelector_Impl_Methods1_MinBy_keySelector_393_40_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_OfType_Impl_Methods1_OfType_399_29_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_OfType_Impl_Methods1_OfType_399_29_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_OfType_Impl_Methods1_OfType_399_29_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_OfType_Impl_Methods1_OfType_399_29_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(__e0.MoveNext()) {
                    if(__e0.Current is int __it0) {
                        current = this.Current = __it0;
                        return true;
                    }
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>OfType_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYNIAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_OfType_Impl_Methods1_OfType_399_29(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_OfType_Impl_Methods1_OfType_399_29_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            if(__ce1.TryGetSpan(out var __sp___ce1)){
                for(var __i___ce1=0;__i___ce1<__sp___ce1.Length;__i___ce1++){
                    var __cv1=__sp___ce1[__i___ce1];
                    consumer___linq0.Consume(__cv1);
                }
            }else{
                while(__ce1.TryGetNext(out int __cv1)){
                    consumer___linq0.Consume(__cv1);
                }
            }
        }

        private struct OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;
                        __k1_0=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return compare;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_1=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_1;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>Order_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2maVJAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Order_Impl_Methods1_Order_406_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct1<OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct0>(new OptimizedLinq_Order_Impl_Methods1_Order_406_28_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;

                        __k1_0=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return compare;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_1=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_1;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>OrderBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdFKAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct1<OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct0>(new OptimizedLinq_OrderBy_keySelector_Impl_Methods1_OrderBy_keySelector_413_42_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;

                        __k1_0=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return -compare;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_1=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_1;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>OrderByDescending_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mTNMAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct1<OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct0>(new OptimizedLinq_OrderByDescending_keySelector_Impl_Methods1_OrderByDescending_keySelector_420_52_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;
                        __k1_0=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return -compare;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_1=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_1;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>OrderDescending_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mZdNAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct1<OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct0>(new OptimizedLinq_OrderDescending_Impl_Methods1_OrderDescending_427_38_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int element0;
            private byte __st0;
            public OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct0(TEnum __e0, int element0) {
                this.__e0 = __e0;
                this.element0 = element0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__st0==0) {
                    this.__st0=1;
                    current=this.Current=element0;
                    return true;
                }
                if(__e0.MoveNext()) {
                    current=this.Current=__e0.Current;
                    return true;
                }
                current=default!;
                return false;

            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Prepend_element_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdlOAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct0<OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct1>(new OptimizedLinq_Prepend_element_Impl_Methods1_Prepend_element_434_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 0);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Range_Impl_Methods1_Range_440_28_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int start;
            private int count;
            private int __e0 = -1;
            public OptimizedLinq_Range_Impl_Methods1_Range_440_28_補助0_Struct0(int start,int count) { this.start = start; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < count) {
                    current = this.Current = start + __e0;
                    return true;
                }
                current = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Range_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2md5PAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Range_Impl_Methods1_Range_440_28(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            {
                var __start0=1;
                var __count0=110000000;
                for(var __i0=0;__i0<__count0;__i0++) {
                    consumer___linq0.Consume(__start0 + __i0);
                }
            }
        }

        private struct OptimizedLinq_Repeat_Impl_Methods1_Repeat_446_29_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int value;
            private int count;
            private int __e0 = -1;
            public OptimizedLinq_Repeat_Impl_Methods1_Repeat_446_29_補助0_Struct0(int value,int count) { this.value = value; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < count) {
                    current = this.Current = value;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        /// <summary>Intercepts <c>Repeat_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdJQAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Repeat_Impl_Methods1_Repeat_446_29(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            {
                var __value0=7;
                var __count0=110000000;
                for(var __i0=0;__i0<__count0;__i0++) {
                    consumer___linq0.Consume(__value0);
                }
            }
        }

        private struct OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int __a0=int.MinValue;
            private global::System.Collections.Generic.List<int>? __g0;
            public OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                // __a0 は int.MinValue=未初期化 / -1=列挙完了 を区別する。
                // -1 を未初期化扱いにすると、TryGetNext を完了後も呼ぶ消費側（TraversalSupport の while ループ等）で
                // 末尾到達ごとに Length-1 へ再初期化して無限ループになる。
                if(this.__a0==int.MinValue) {
                    if(__e0.TryGetSpan(out var __span0)) {
                        this.__a0=__span0.Length-1;
                    }else{
                        if(__e0.TryGetNonEnumeratedCount(out var __count)) {
                            this.__g0=new global::System.Collections.Generic.List<int>(__count);
                        }else{
                            this.__g0=new global::System.Collections.Generic.List<int>();
                        }
                        while(__e0.MoveNext()) this.__g0.Add(__e0.Current);
                        this.__a0=this.__g0.Count-1;
                    }
                }
                if(this.__a0>=0) {
                    if(__e0.TryGetSpan(out var __span)) {
                        current=this.Current=__span[this.__a0--];
                    }else{
                        current=this.Current=this.__g0![this.__a0--];
                    }
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Reverse_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mfRRAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct0<OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct1>(new OptimizedLinq_Reverse_Impl_Methods1_Reverse_453_30_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int __a0 = -1;
            public OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.TryGetNext(out var __it0)) {

                    this.__a0++;
                    current = this.Current = __it0+this.__a0;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Select_indexSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mS9TAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct0<OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct1>(new OptimizedLinq_Select_indexSelector_Impl_Methods1_Select_indexSelector_460_43_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.TryGetNext(out var __it0)) {

                    current = this.Current = __it0+__it0;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Select_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYZUAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct0<OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct1>(new OptimizedLinq_Select_selector_Impl_Methods1_Select_selector_467_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::Labo.Shared.Linq.Enumerable.Methods1 @this;
            private int[] __innerArr__it1_0 = global::System.Array.Empty<int>();
            private int __innerIdx__it1_0 = 0;
            private bool __innerHas__it1_0 = false;
            private int __ov1_0 = default!;
            public OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct1(TEnum __e1, global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
                this.__e1 = __e1;
                this.@this = @this;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(true) {
                    if(this.__innerHas__it1_0) {
                        if(this.__innerIdx__it1_0 < this.__innerArr__it1_0.Length) {
                            var __it1_1=this.__innerArr__it1_0[this.__innerIdx__it1_0];
                            this.__innerIdx__it1_0++;
                    if(!(__it1_1>120&&global::Benchmark.Shared.HoistInvariant.Value()==0)) continue;
                            current = this.Current = __it1_1;
                            return true;
                        }
                        this.__innerHas__it1_0=false;
                    }
                    while(__e1.TryGetNext(out var __it1_0)) {
                        this.__ov1_0=__it1_0;
                        this.__innerArr__it1_0=__Field_Methods1_inner(@this);
                        this.__innerIdx__it1_0=0;
                        this.__innerHas__it1_0=true;
                        break;
                    }
                    if(!this.__innerHas__it1_0) {
                        current = default!;
                        return false;
                    }
                }
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "outer")]
        private static extern ref int[] __Field_Methods1_outer(global::Labo.Shared.Linq.Enumerable.Methods1 @this);
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "inner")]
        private static extern ref int[] __Field_Methods1_inner(global::Labo.Shared.Linq.Enumerable.Methods1 @this);
        /// <summary>Intercepts <c>SelectMany_Hoisting_PredicatePushdown_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mdJVAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct1<OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct0>(new OptimizedLinq_SelectMany_Hoisting_PredicatePushdown_Impl_Methods1_SelectMany_Hoisting_PredicatePushdown_473_60_補助0_Struct0(__Field_Methods1_outer(@this)), @this);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>
        {
            private global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source;
            private global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>? __e1;
            private int __index = -1;
            public OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct1(global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source) { this.source = source; }
            public global::System.Collections.Generic.IEnumerable<int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.IEnumerable<int>> span) {
                var start = __index + 1;
                if(source is global::System.Collections.Generic.IEnumerable<int>[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<global::System.Collections.Generic.IEnumerable<int>> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.IEnumerable<int>> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.IEnumerable<int> current) {
                this.__e1 ??= source.GetEnumerator();
                if(this.__e1.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e1.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e1?.Dispose();
            }
        }
        private struct OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>> {
            private TEnum __e0;
            private global::System.Collections.Generic.IEnumerator<int>? innerEnumerator = null;
            private global::System.Collections.Generic.IEnumerable<int> __ov0 = default!;
            private int __a0 = -1;
            public OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(this.innerEnumerator is not null) {
                    if(this.innerEnumerator.MoveNext()) {

                        var __it0 = this.innerEnumerator.Current;
                        current = this.Current = __it0;
                        return true;
                    }
                    this.innerEnumerator.Dispose();
                    this.innerEnumerator = null;
                }
                if(!__e0.MoveNext()) {
                    current = default!;
                    return false;
                }
                this.__a0++;

                this.__ov0 = __e0.Current;
                this.innerEnumerator = ((global::System.Collections.Generic.IEnumerable<int>)__ov0).GetEnumerator();
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                if(this.innerEnumerator is not null) {
                    this.innerEnumerator.Dispose();
                }
                __e0.Dispose();
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "srcNestedCount")]
        private static extern global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> __Method_Base_srcNestedCount(global::Benchmark.Shared.Base @this, int elementCount);
        /// <summary>Intercepts <c>SelectMany_indexSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVRXAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct0<OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct1>(new OptimizedLinq_SelectMany_indexSelector_Impl_Methods1_SelectMany_indexSelector_479_47_補助0_Struct1(((global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>>)(__Method_Base_srcNestedCount(@this, 24000000)))));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>
        {
            private global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source;
            private global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>? __e1;
            private int __index = -1;
            public OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct1(global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source) { this.source = source; }
            public global::System.Collections.Generic.IEnumerable<int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.IEnumerable<int>> span) {
                var start = __index + 1;
                if(source is global::System.Collections.Generic.IEnumerable<int>[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<global::System.Collections.Generic.IEnumerable<int>> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.IEnumerable<int>> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.IEnumerable<int> current) {
                this.__e1 ??= source.GetEnumerator();
                if(this.__e1.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e1.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e1?.Dispose();
            }
        }
        private struct OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>> {
            private TEnum __e0;
            private global::System.Collections.Generic.IEnumerator<int>? innerEnumerator = null;
            private global::System.Collections.Generic.IEnumerable<int> __ov0 = default!;
            private int __a0 = -1;
            public OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(this.innerEnumerator is not null) {
                    if(this.innerEnumerator.MoveNext()) {

                        var __it0 = this.innerEnumerator.Current;
                        current = this.Current = __it0+1;
                        return true;
                    }
                    this.innerEnumerator.Dispose();
                    this.innerEnumerator = null;
                }
                if(!__e0.MoveNext()) {
                    current = default!;
                    return false;
                }
                this.__a0++;

                this.__ov0 = __e0.Current;
                this.innerEnumerator = ((global::System.Collections.Generic.IEnumerable<int>)__ov0).GetEnumerator();
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                if(this.innerEnumerator is not null) {
                    this.innerEnumerator.Dispose();
                }
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SelectMany_indexSelector_resultSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mcdYAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct0<OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct1>(new OptimizedLinq_SelectMany_indexSelector_resultSelector_Impl_Methods1_SelectMany_indexSelector_resultSelector_485_62_補助0_Struct1(((global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>>)(__Method_Base_srcNestedCount(@this, 24000000)))));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>
        {
            private global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source;
            private global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>? __e1;
            private int __index = -1;
            public OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct1(global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source) { this.source = source; }
            public global::System.Collections.Generic.IEnumerable<int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.IEnumerable<int>> span) {
                var start = __index + 1;
                if(source is global::System.Collections.Generic.IEnumerable<int>[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<global::System.Collections.Generic.IEnumerable<int>> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.IEnumerable<int>> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.IEnumerable<int> current) {
                this.__e1 ??= source.GetEnumerator();
                if(this.__e1.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e1.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e1?.Dispose();
            }
        }
        private struct OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>> {
            private TEnum __e0;
            private global::System.Collections.Generic.IEnumerator<int>? innerEnumerator = null;
            private global::System.Collections.Generic.IEnumerable<int> __ov0 = default!;
            public OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(this.innerEnumerator is not null) {
                    if(this.innerEnumerator.MoveNext()) {

                        var __it0 = this.innerEnumerator.Current;
                        current = this.Current = __it0+1;
                        return true;
                    }
                    this.innerEnumerator.Dispose();
                    this.innerEnumerator = null;
                }
                if(!__e0.MoveNext()) {
                    current = default!;
                    return false;
                }


                this.__ov0 = __e0.Current;
                this.innerEnumerator = ((global::System.Collections.Generic.IEnumerable<int>)__ov0).GetEnumerator();
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                if(this.innerEnumerator is not null) {
                    this.innerEnumerator.Dispose();
                }
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SelectMany_resultSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVNaAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct0<OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct1>(new OptimizedLinq_SelectMany_resultSelector_Impl_Methods1_SelectMany_resultSelector_491_48_補助0_Struct1(((global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>>)(__Method_Base_srcNestedCount(@this, 24000000)))));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>
        {
            private global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source;
            private global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>? __e1;
            private int __index = -1;
            public OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct1(global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>> source) { this.source = source; }
            public global::System.Collections.Generic.IEnumerable<int> Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.IEnumerable<int>> span) {
                var start = __index + 1;
                if(source is global::System.Collections.Generic.IEnumerable<int>[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<global::System.Collections.Generic.IEnumerable<int>> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.IEnumerable<int>> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.IEnumerable<int> current) {
                this.__e1 ??= source.GetEnumerator();
                if(this.__e1.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e1.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e1?.Dispose();
            }
        }
        private struct OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private global::System.Collections.Generic.IEnumerable<int> source;
            private global::System.Collections.Generic.IEnumerator<int>? __e2;
            private int __index = -1;
            public OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct2(global::System.Collections.Generic.IEnumerable<int> source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                var start = __index + 1;
                if(source is int[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<int> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                this.__e2 ??= source.GetEnumerator();
                if(this.__e2.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e2.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e2?.Dispose();
            }
        }
        private struct OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.IEnumerable<int>>, global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.IEnumerable<int>> {
            private TEnum __e0;
            private OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct2 innerEnumerator = default!;
            private bool __hv0 = false;
            private global::System.Collections.Generic.IEnumerable<int> __ov0 = default!;
            public OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(this.__hv0) {
                    if(this.innerEnumerator.TryGetNext(out var __it0)) {

                        current = this.Current = __it0;
                        return true;
                    }
                    this.innerEnumerator.Dispose();
                    this.__hv0 = false;
                }
                if(!__e0.MoveNext()) {
                    current = default!;
                    return false;
                }

                this.__ov0 = __e0.Current;
                this.innerEnumerator = new OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct2(((global::System.Collections.Generic.IEnumerable<int>)(__ov0)));
                this.__hv0 = true;
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                if(this.__hv0) {
                    this.innerEnumerator.Dispose();
                }
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SelectMany_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mbNbAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct0<OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct1>(new OptimizedLinq_SelectMany_selector_Impl_Methods1_SelectMany_selector_497_42_補助0_Struct1(((global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.IEnumerable<int>>)(__Method_Base_srcNestedCount(@this, 24000000)))));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::Labo.Shared.Linq.Enumerable.Methods1 @this;
            private int[] __innerArr__it1_0 = global::System.Array.Empty<int>();
            private int __innerIdx__it1_0 = 0;
            private bool __innerHas__it1_0 = false;
            private int __ov1_0 = default!;
            public OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct1(TEnum __e1, global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
                this.__e1 = __e1;
                this.@this = @this;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(true) {
                    if(this.__innerHas__it1_0) {
                        if(this.__innerIdx__it1_0 < this.__innerArr__it1_0.Length) {
                            var __it1_1=this.__innerArr__it1_0[this.__innerIdx__it1_0];
                            this.__innerIdx__it1_0++;
                    if(!(__it1_1>120)) continue;
                            current = this.Current = __it1_1;
                            return true;
                        }
                        this.__innerHas__it1_0=false;
                    }
                    while(__e1.TryGetNext(out var __it1_0)) {
                        this.__ov1_0=__it1_0;
                        this.__innerArr__it1_0=__Field_Methods1_inner(@this);
                        this.__innerIdx__it1_0=0;
                        this.__innerHas__it1_0=true;
                        break;
                    }
                    if(!this.__innerHas__it1_0) {
                        current = default!;
                        return false;
                    }
                }
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>SelectManyPredicatePushdown_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mQBdAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct1<OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct0>(new OptimizedLinq_SelectManyPredicatePushdown_Impl_Methods1_SelectManyPredicatePushdown_503_50_補助0_Struct0(__Field_Methods1_outer(@this)), @this);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct0<TEnum1, TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<bool>, global::System.Collections.Generic.IEnumerator<bool>
            where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            public bool Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static bool Compute(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                comparer??=global::System.Collections.Generic.EqualityComparer<int>.Default;
                source.TryGetSpan(out var __spanL);
                second.TryGetSpan(out var __spanR);
                if(__spanL.Length!=__spanR.Length) return false;
                for(int __i=0;(uint)__i<(uint)__spanL.Length;__i++){
                    if(!comparer.Equals(__spanL[__i],(int)(object)__spanR[__i]!)) return false;
                }
                return true;
            }
            public OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct0(TEnum1 source, TEnum2 second, global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.Current = Compute(source, second, comparer);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<bool> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<bool> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out bool current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>SequenceEqual_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUteAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            bool __linq1=default!;
            using var e___linq1=new OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct0<OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct1, OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct2>(new OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), new OptimizedLinq_SequenceEqual_second_Impl_Methods1_SequenceEqual_second_509_43_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null);
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Single_Impl_Methods1_Single_515_29_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int value;
            private int count;
            private int __e1 = -1;
            public OptimizedLinq_Single_Impl_Methods1_Single_515_29_補助0_Struct1(int value,int count) { this.value = value; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < count) {
                    current = this.Current = value;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Single_Impl_Methods1_Single_515_29_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                if(!source.MoveNext()) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                int value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "srcOne")]
        private static extern global::System.Collections.Generic.IEnumerable<int> __Method_Base_srcOne(global::Benchmark.Shared.Base @this, int scale);
        /// <summary>Intercepts <c>Single_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mYBfAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Single_Impl_Methods1_Single_515_29(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<6000; i++)
            {
            int __linq1;
            {
                var __value0=0;
                var __count0=1;
                if(__count0==0) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                if(__count0>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                __linq1=__value0;
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        [global::System.Runtime.CompilerServices.InlineArray(16)]
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct0_Buf {
            private int _element0;
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int value;
            private int count;
            private int __e1 = -1;
            public OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct1(int value,int count) { this.value = value; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < count) {
                    current = this.Current = value;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int[] Compute(TEnum source) {
                using var enumerator = source;
                // 要素数が InlineCapacity 以内ならスタック上の InlineArray だけで受け、ArrayPool/List の
                // ヒープ確保もスレッドセーフな Rent/Return も一切回避する。溢れたときだけ ArrayPool へスピルし、
                // 最後に正確なサイズの配列へ1回だけコピーする。MoveNext/Current のみで TEnum 制約に依存しない。
                OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct0_Buf __inline = default;
                global::System.Span<int> __stack = __inline;
                int[]? __heap = null;
                var n = 0;
                try {
                    while(enumerator.MoveNext()) {
                        if(__heap is null) {
                            if(n < 16) {
                                __stack[n++] = enumerator.Current;
                                continue;
                            }
                            // スタックバッファが満杯になったので ArrayPool へ移行する。
                            __heap = global::System.Buffers.ArrayPool<int>.Shared.Rent(16 * 2);
                            __stack.CopyTo(__heap);
                        } else if(n == __heap.Length) {
                            var __next = global::System.Buffers.ArrayPool<int>.Shared.Rent(__heap.Length * 2);
                            global::System.Array.Copy(__heap, __next, n);
                            global::System.Buffers.ArrayPool<int>.Shared.Return(__heap, clearArray: true);
                            __heap = __next;
                        }
                        __heap[n++] = enumerator.Current;
                    }
                    if(n == 0) {
                        return global::System.Array.Empty<int>();
                    }
                    var result = new int[n];
                    if(__heap is null) {
                        __stack.Slice(0, n).CopyTo(result);
                    } else {
                        global::System.Array.Copy(__heap, result, n);
                    }
                    return result;
                } finally {
                    if(__heap is not null) {
                        global::System.Buffers.ArrayPool<int>.Shared.Return(__heap, clearArray: true);
                    }
                }
            }
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct3 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e3 = -1;
            public OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct3(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e3 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e3 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e3 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e3 < source.Length) {
                    current = this.Current = source[__e3];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct2<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e2;
            public int Current { get; private set; }
            private bool __yd2 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                int value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var __it2 in __span){
                    bool __tmp2=default!;

            __tmp2=__it2==0;
                    if(__tmp2) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value=__it2;
                        found=true;
                    }
                }
                if(!found) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                return value;
            }
            public OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct2(TEnum __e2) {
                this.__e2 = __e2;
                    this.Current = Compute(__e2);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd2) {
                    current = default!;
                    return false;
                }
                this.__yd2 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e2.Dispose();
            }
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct5 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e5 = -1;
            public OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct5(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e5 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e5 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e5 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e5 < source.Length) {
                    current = this.Current = source[__e5];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct4<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e4;
            public int Current { get; private set; }
            private bool __yd4 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                int value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var __it4 in __span){
                    bool __tmp4=default!;

            __tmp4=__it4==1;
                    if(__tmp4) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value=__it4;
                        found=true;
                    }
                }
                if(!found) throw global::Tsavorite.GeneratedCodeHelpers.NoElements();
                return value;
            }
            public OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct4(TEnum __e4) {
                this.__e4 = __e4;
                    this.Current = Compute(__e4);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd4) {
                    current = default!;
                    return false;
                }
                this.__yd4 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e4.Dispose();
            }
        }
        /// <summary>Intercepts <c>Single_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mbpgAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int[] __linq0;
            {
                var __src__r0=global::System.Linq.Enumerable.Repeat<int>(0, 1);
                __linq0=global::System.Linq.Enumerable.ToArray(__src__r0);
            }
            var y=__linq0;
            int __linq1=default!;
            using var e___linq1=new OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct2<OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct3>(new OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct3(y));
            e___linq1.TryGetNext(out __linq1);
            var x=__linq1;
            int __linq3=default!;
            using var e___linq3=new OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct4<OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct5>(new OptimizedLinq_Single_predicate_Impl_Methods1_Single_predicate_526_39_補助0_Struct5(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq3.TryGetNext(out __linq3);
            __Field_Base_consumer(@this).Consume(__linq3);
        }

        private struct OptimizedLinq_SingleOrDefault_Impl_Methods1_SingleOrDefault_537_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int value;
            private int count;
            private int __e1 = -1;
            public OptimizedLinq_SingleOrDefault_Impl_Methods1_SingleOrDefault_537_38_補助0_Struct1(int value,int count) { this.value = value; this.count = count; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = this.count - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < count) {
                    current = this.Current = value;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SingleOrDefault_Impl_Methods1_SingleOrDefault_537_38_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                if(!source.MoveNext()) return default!;
                int value = source.Current;
                if(source.MoveNext()) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                return value;
            }
        }
        /// <summary>Intercepts <c>SingleOrDefault_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mT1iAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SingleOrDefault_Impl_Methods1_SingleOrDefault_537_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            for(var i=0; i<4500; i++)
            {
            int __linq1;
            {
                var __value0=0;
                var __count0=1;
                if(__count0>1) throw new global::System.InvalidOperationException("Sequence contains more than one element");
                __linq1=__count0==0 ? default! : __value0;
            }
            __Field_Base_consumer(@this).Consume(__linq1);
            }
        }

        private struct OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public int Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static int Compute(TEnum source) {
                int value = default!;
                bool found = false;
                source.TryGetSpan(out var __span);
                foreach(var __it0 in __span){
                    bool __tmp0=default!;

            __tmp0=__it0==1;
                    if(__tmp0) {
                        if(found) throw new global::System.InvalidOperationException("Sequence contains more than one matching element");
                        value=__it0;
                        found=true;
                    }
                }
                return value;
            }
            public OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SingleOrDefault_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2ma1jAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1=default!;
            using var e___linq1=new OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct0<OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct1>(new OptimizedLinq_SingleOrDefault_predicate_Impl_Methods1_SingleOrDefault_predicate_548_48_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int count;
            private bool skipped = false;
            public OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct0(TEnum __e0, int count) {
                this.__e0 = __e0;
                this.count = count;
                    if(this.count < 0) { this.count = 0; }
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(!this.skipped) {
                    this.skipped = true;
                    var remaining = this.count;
                    while(remaining > 0 && __e0.MoveNext()) {
                        remaining--;
                    }
                }
                if(__e0.MoveNext()) {
                    current = this.Current = __e0.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Skip_count_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mRBlAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct0<OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct1>(new OptimizedLinq_Skip_count_Impl_Methods1_Skip_count_555_33_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 36000000/4);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int count;
            private int __a0;
            private int __tmp0=-1;
            private global::System.Collections.Generic.Queue<int>? __lk0;
            private bool __yd0;
            public OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct0(TEnum __e0, int count) {
                this.__e0 = __e0;
                this.count = count;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.TryGetSpan(out var __span)) {
                    if(this.__tmp0<0) {
                        this.__tmp0=__span.Length-global::System.Math.Min(count<0?0:count,__span.Length);
                    }
                    if((uint)this.__a0<(uint)this.__tmp0) {
                        current=this.Current=__span[this.__a0++];
                        return true;
                    }
                    current=default!;
                    return false;
                }
                var __c=count<0?0:count;
                if(__c==0) {
                    if(__e0.MoveNext()) {
                        current=this.Current=__e0.Current;
                        return true;
                    }
                    current=default!;
                    return false;
                }
                if(!this.__yd0) {
                    this.__lk0=new global::System.Collections.Generic.Queue<int>(__c+1);
                    while(this.__lk0.Count<__c&&__e0.MoveNext()) this.__lk0.Enqueue(__e0.Current);
                    this.__yd0=true;
                }
                if(this.__lk0 is not null&&__e0.MoveNext()) {
                    this.__lk0.Enqueue(__e0.Current);
                    current=this.Current=this.__lk0.Dequeue();
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SkipLast_count_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVxmAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct0<OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct1>(new OptimizedLinq_SkipLast_count_Impl_Methods1_SkipLast_count_562_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 36000000/4);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private bool skipping = true;
            private int __a0 = -1;
            public OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(__e0.MoveNext()) {
                    var __it0 = __e0.Current;
                    this.__a0++;
                    if(this.skipping && (__a0<36000000/4)) {
                        continue;
                    }
                    this.skipping = false;
                    current = this.Current = __it0;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SkipWhile_indexPredicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mchnAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct0<OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct1>(new OptimizedLinq_SkipWhile_indexPredicate_Impl_Methods1_SkipWhile_indexPredicate_569_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private bool skipping = true;
            public OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(__e0.MoveNext()) {
                    var __it0 = __e0.Current;
                    if(this.skipping && (__it0<36000000/4+1)) {
                        continue;
                    }
                    this.skipping = false;
                    current = this.Current = __it0;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>SkipWhile_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUJpAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct0<OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct1>(new OptimizedLinq_SkipWhile_predicate_Impl_Methods1_SkipWhile_predicate_576_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Sum_Impl_Methods1_Sum_582_26_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private global::System.Collections.Generic.IEnumerable<int> source;
            private global::System.Collections.Generic.IEnumerator<int>? __e1;
            private int __index = -1;
            public OptimizedLinq_Sum_Impl_Methods1_Sum_582_26_補助0_Struct1(global::System.Collections.Generic.IEnumerable<int> source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                if(global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out var sourceCount)) {
                    count = sourceCount - (__index + 1);
                    return true;
                }
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                var start = __index + 1;
                if(source is int[] array) {
                    span = global::System.MemoryExtensions.AsSpan(array, start);
                    return true;
                }
                if(source is global::System.Collections.Generic.List<int> list) {
                    span = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
                    return true;
                }
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                if(!TryGetSpan(out var span)) {
                    return false;
                }
                var start = offset.GetOffset(span.Length);
                span.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                this.__e1 ??= source.GetEnumerator();
                if(this.__e1.MoveNext()) {
                    __index++;
                    current = this.Current = this.__e1.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                this.__e1?.Dispose();
            }
        }
        private struct OptimizedLinq_Sum_Impl_Methods1_Sum_582_26_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int Compute(TEnum source) {
                int sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        foreach(var item in __span) sum+=item;
                    }else{
                        while(source.MoveNext()) sum+=source.Current;
                    }
                }
                return sum;
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "srcMinusPlusCount")]
        private static extern global::System.Collections.Generic.IEnumerable<int> __Method_Base_srcMinusPlusCount(global::Benchmark.Shared.Base @this, int elementCount);
        /// <summary>Intercepts <c>Sum_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWdqAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Sum_Impl_Methods1_Sum_582_26(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int __linq1;
            {
                var __src__r0=((global::System.Collections.Generic.IEnumerable<int>)(__Method_Base_srcMinusPlusCount(@this, 96000000)));
                __linq1=global::System.Linq.Enumerable.Sum(__src__r0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static long Compute(TEnum source) {
                long sum = default!;
                checked{
                    if(source.TryGetSpan(out var __span)){
                        foreach(var __it0 in __span){
                            long __tmp0=default!;

            __tmp0=(long)__it0;
                            sum+=__tmp0;
                        }
                    }else{
                        while(source.MoveNext()){
                            var __it0=source.Current;
                            long __tmp0=default!;

            __tmp0=(long)__it0;
                            sum+=__tmp0;
                        }
                    }
                }
                return sum;
            }
        }
        /// <summary>Intercepts <c>Sum_selector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWxrAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            long __linq1;
            using(var __s0=new OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 96000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct0<OptimizedLinq_Sum_selector_Impl_Methods1_Sum_selector_588_35_補助0_Struct1>.Compute(__s0);
            }
            __Field_Base_consumer(@this).Consume(__linq1);
        }

        private struct OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int count;
            private int __a0 = 0;
            public OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct0(TEnum __e0, int count) {
                this.__e0 = __e0;
                this.count = count;
                    if(this.count < 0) { this.count = 0; }
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__a0 >= this.count) {
                    current = default!;
                    return false;
                }
                if(__e0.MoveNext()) {
                    this.__a0++;
                    current = this.Current = __e0.Current;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Take_count_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2malsAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct0<OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct1>(new OptimizedLinq_Take_count_Impl_Methods1_Take_count_595_33_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 36000000*3/4);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int count;
            private int __a0=-1;
            private int __tmp0;
            private global::System.Collections.Generic.Queue<int>? __lk0;
            public OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct0(TEnum __e0, int count) {
                this.__e0 = __e0;
                this.count = count;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(__e0.TryGetSpan(out var __span)) {
                    if(this.__a0<0) {
                        var __spanCount=count<0?0:count;
                        this.__a0=__span.Length-global::System.Math.Min(__spanCount,__span.Length);
                        this.__tmp0=__span.Length;
                    }
                    if((uint)this.__a0<(uint)this.__tmp0) {
                        current=this.Current=__span[this.__a0++];
                        return true;
                    }
                    current=default!;
                    return false;
                }
                var __takeCount=count<0?0:count;
                if(this.__lk0 is null) {
                    this.__lk0=new global::System.Collections.Generic.Queue<int>(__takeCount);
                    if(__takeCount==0) {
                        current=default!;
                        return false;
                    }
                    while(__e0.MoveNext()) {
                        if(this.__lk0.Count==__takeCount) this.__lk0.Dequeue();
                        this.__lk0.Enqueue(__e0.Current);
                    }
                }
                if(this.__lk0.Count>0) {
                    current=this.Current=this.__lk0.Dequeue();
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>TakeLast_count_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mfdtAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct0<OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct1>(new OptimizedLinq_TakeLast_count_Impl_Methods1_TakeLast_count_602_37_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), 36000000/4);
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private bool done = false;
            private int __a0 = -1;
            public OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.done || !__e0.MoveNext()) {
                    current = default!;
                    return false;
                }
                var __it0 = __e0.Current;
                this.__a0++;
                if(!(__a0<36000000*3/4)) {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.Current = __it0;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>TakeWhile_indexPredicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWNvAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct0<OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct1>(new OptimizedLinq_TakeWhile_indexPredicate_Impl_Methods1_TakeWhile_indexPredicate_609_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private bool done = false;
            public OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.done || !__e0.MoveNext()) {
                    current = default!;
                    return false;
                }
                var __it0 = __e0.Current;
                if(!(__it0<36000000*3/4+1)) {
                    this.done = true;
                    current = default!;
                    return false;
                }
                current = this.Current = __it0;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>TakeWhile_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2md9wAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct0<OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct1>(new OptimizedLinq_TakeWhile_predicate_Impl_Methods1_TakeWhile_predicate_616_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_1;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int secondKey,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.SecondKey=secondKey;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int SecondKey { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_1) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
                this.__ordcmp__it1_1 = __ordcmp__it1_1;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;

                        __k1_0=__it1_0%1000;
                        int __k1_1=default!;

                        __k1_1=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__k1_1,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return compare;
                        var compare2=left.SecondKey.CompareTo(right.SecondKey);
                        if(compare2!=0) return compare2;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_2=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_2;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>ThenBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSRyAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct1<OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct0>(new OptimizedLinq_ThenBy_keySelector_Impl_Methods1_ThenBy_keySelector_622_41_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null, null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        private struct OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct0 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e0 = -1;
            public OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct0(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e0 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e0 < source.Length) {
                    current = this.Current = source[__e0];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }

        private struct OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct1<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e1;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0;
            private global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_1;
            private readonly struct __sortitem__it1_0 {
                public __sortitem__it1_0(int value,int key,int secondKey,int index) {
                    this.Value=value;
                    this.Key=key;
                    this.SecondKey=secondKey;
                    this.Index=index;
                }
                public int Value { get; }
                public int Key { get; }
                public int SecondKey { get; }
                public int Index { get; }
            }
            private global::System.Collections.Generic.List<__sortitem__it1_0>? __sorted__it1_0 = null;
            private int __st1_0 = 0;
            public OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct1(TEnum __e1, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_0, global::System.Collections.Generic.IComparer<int>? __ordcmp__it1_1) {
                this.__e1 = __e1;
                this.__ordcmp__it1_0 = __ordcmp__it1_0;
                this.__ordcmp__it1_1 = __ordcmp__it1_1;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(this.__sorted__it1_0 is null) {
                    global::System.Collections.Generic.List<__sortitem__it1_0> __sorted__it1_0;
                    if(__e1.TryGetNonEnumeratedCount(out var __sortCount0)) {
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>(__sortCount0);
                    }else{
                        __sorted__it1_0=new global::System.Collections.Generic.List<__sortitem__it1_0>();
                    }
                    var __sortIndex=0;
                    while(__e1.TryGetNext(out var __it1_0)) {
                        int __k1_0=default!;

                        __k1_0=__it1_0%1000;
                        int __k1_1=default!;

                        __k1_1=__it1_0;
                        __sorted__it1_0.Add(new __sortitem__it1_0(__it1_0,__k1_0,__k1_1,__sortIndex++));
                    }
                    __sorted__it1_0.Sort((left,right) => {
                        var compare=left.Key.CompareTo(right.Key);
                        if(compare!=0) return compare;
                        var compare2=left.SecondKey.CompareTo(right.SecondKey);
                        if(compare2!=0) return -compare2;
                        return left.Index.CompareTo(right.Index);
                    });
                    this.__sorted__it1_0=__sorted__it1_0;
                    this.__st1_0=0;
                }
                while(this.__st1_0<this.__sorted__it1_0.Count) {
                    var __it1_2=this.__sorted__it1_0[this.__st1_0++].Value;
                    current = this.Current = __it1_2;
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e1.Dispose();
            }
        }
        /// <summary>Intercepts <c>ThenByDescending_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mWtzAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce1=new OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct1<OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct0>(new OptimizedLinq_ThenByDescending_keySelector_Impl_Methods1_ThenByDescending_keySelector_628_51_補助0_Struct0(__Method_Base_GetOrCreateByLength(@this, 1700000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), null, null);
            while(__ce1.TryGetNext(out int __cv1)){
                consumer___linq0.Consume(__cv1);
            }
        }

        [global::System.Runtime.CompilerServices.InlineArray(16)]
        private struct OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct0_Buf {
            private int _element0;
        }
        private struct OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int[] Compute(TEnum source) {
                using var enumerator = source;
                // 要素数が InlineCapacity 以内ならスタック上の InlineArray だけで受け、ArrayPool/List の
                // ヒープ確保もスレッドセーフな Rent/Return も一切回避する。溢れたときだけ ArrayPool へスピルし、
                // 最後に正確なサイズの配列へ1回だけコピーする。MoveNext/Current のみで TEnum 制約に依存しない。
                OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct0_Buf __inline = default;
                global::System.Span<int> __stack = __inline;
                int[]? __heap = null;
                var n = 0;
                try {
                    while(enumerator.MoveNext()) {
                        if(__heap is null) {
                            if(n < 16) {
                                __stack[n++] = enumerator.Current;
                                continue;
                            }
                            // スタックバッファが満杯になったので ArrayPool へ移行する。
                            __heap = global::System.Buffers.ArrayPool<int>.Shared.Rent(16 * 2);
                            __stack.CopyTo(__heap);
                        } else if(n == __heap.Length) {
                            var __next = global::System.Buffers.ArrayPool<int>.Shared.Rent(__heap.Length * 2);
                            global::System.Array.Copy(__heap, __next, n);
                            global::System.Buffers.ArrayPool<int>.Shared.Return(__heap, clearArray: true);
                            __heap = __next;
                        }
                        __heap[n++] = enumerator.Current;
                    }
                    if(n == 0) {
                        return global::System.Array.Empty<int>();
                    }
                    var result = new int[n];
                    if(__heap is null) {
                        __stack.Slice(0, n).CopyTo(result);
                    } else {
                        global::System.Array.Copy(__heap, result, n);
                    }
                    return result;
                } finally {
                    if(__heap is not null) {
                        global::System.Buffers.ArrayPool<int>.Shared.Return(__heap, clearArray: true);
                    }
                }
            }
        }
        /// <summary>Intercepts <c>ToArray_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2maZ0AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            int[] __linq1;
            using(var __s0=new OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 48000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct0<OptimizedLinq_ToArray_Impl_Methods1_ToArray_634_30_補助0_Struct1>.Compute(__s0);
            }
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.Dictionary<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.Dictionary<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public global::System.Collections.Generic.Dictionary<int, int> Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static global::System.Collections.Generic.Dictionary<int, int> Compute(TEnum source){
                global::System.Collections.Generic.Dictionary<int, int> result;
                if(source.TryGetNonEnumeratedCount(out var __count)){
                    result=new(__count);
                }else{
                    result=new();
                }
                while(source.MoveNext()){
                    var __it0=source.Current;
                    int __k0=default!;

            __k0=__it0;
                    int __tmp0=default!;

            __tmp0=__it0*2;
                    result.Add(__k0,__tmp0);
                }
                return result;
            }
            public OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.Dictionary<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.Dictionary<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.Dictionary<int, int> current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>ToDictionary_elementSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mc51AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Collections.Generic.Dictionary<int, int> __linq1=default!;
            using var e___linq1=new OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct0<OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct1>(new OptimizedLinq_ToDictionary_elementSelector_Impl_Methods1_ToDictionary_elementSelector_640_51_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 24000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Collections.Generic.Dictionary<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.Dictionary<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public global::System.Collections.Generic.Dictionary<int, int> Current { get; private set; }
            private bool __yd0 = false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static global::System.Collections.Generic.Dictionary<int, int> Compute(TEnum source){
                global::System.Collections.Generic.Dictionary<int, int> result;
                if(source.TryGetNonEnumeratedCount(out var __count)){
                    result=new(__count);
                }else{
                    result=new();
                }
                while(source.MoveNext()){
                    var __it0=source.Current;
                    int __k0=default!;

            __k0=__it0;
                    result.Add(__k0,__it0);
                }
                return result;
            }
            public OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Collections.Generic.Dictionary<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Collections.Generic.Dictionary<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Collections.Generic.Dictionary<int, int> current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>ToDictionary_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mSZ3AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Collections.Generic.Dictionary<int, int> __linq1=default!;
            using var e___linq1=new OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct0<OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct1>(new OptimizedLinq_ToDictionary_keySelector_Impl_Methods1_ToDictionary_keySelector_646_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 24000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static global::System.Collections.Generic.HashSet<int> Compute(TEnum source){
                global::System.Collections.Generic.HashSet<int> result;
                if(source.TryGetNonEnumeratedCount(out var __count)){
                    result=new(__count);
                }else{
                    result=new();
                }
                if(source.TryGetSpan(out var __span)){
                    foreach(var item in __span) result.Add(item);
                }else{
                    while(source.MoveNext()) result.Add(source.Current);
                }
                return result;
            }
        }
        /// <summary>Intercepts <c>ToHashSet_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVF4AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Collections.Generic.HashSet<int> __linq1;
            using(var __s0=new OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 24000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct0<OptimizedLinq_ToHashSet_Impl_Methods1_ToHashSet_652_32_補助0_Struct1>.Compute(__s0);
            }
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct0<TEnum>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static global::System.Collections.Generic.List<int> Compute(TEnum source) {
                global::System.Collections.Generic.List<int> result;
                if(source.TryGetNonEnumeratedCount(out var __count)) {
                    result=new(__count);
                }else{
                    result=new();
                }
                using var enumerator = source;
                while(enumerator.MoveNext()) result.Add(enumerator.Current);
                return result;
            }
        }
        /// <summary>Intercepts <c>ToList_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mVF5AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToList_Impl_Methods1_ToList_658_29(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Collections.Generic.List<int> __linq1;
            using(var __s0=new OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 48000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }))){
                __linq1=OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct0<OptimizedLinq_ToList_Impl_Methods1_ToList_658_29_補助0_Struct1>.Compute(__s0);
            }
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Linq.ILookup<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Linq.ILookup<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public global::System.Linq.ILookup<int, int> Current { get; private set; }
            private bool __yd0 = false;
            private sealed class TsavoriteGrouping:global::System.Linq.IGrouping<int,int>{
                private readonly int key;
                private readonly global::System.Collections.Generic.List<int> elements;
                public TsavoriteGrouping(int key,global::System.Collections.Generic.List<int> elements){this.key=key;this.elements=elements;}
                public int Key=>this.key;
                public global::System.Collections.Generic.IEnumerator<int> GetEnumerator()=>this.elements.GetEnumerator();
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            }
            private sealed class TsavoriteLookup:global::System.Linq.ILookup<int,int>{
                private readonly global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> map;
                private readonly global::System.Collections.Generic.List<int> order;
                public TsavoriteLookup(global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> map,global::System.Collections.Generic.List<int> order){this.map=map;this.order=order;}
                public int Count=>this.map.Count;
                public global::System.Collections.Generic.IEnumerable<int> this[int key]=>this.map.TryGetValue(key,out var g)?g:global::System.Linq.Enumerable.Empty<int>();
                public bool Contains(int key)=>this.map.ContainsKey(key);
                public global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<int,int>> GetEnumerator(){
                    foreach(var k in this.order) yield return new TsavoriteGrouping(k,this.map[k]);
                }
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static global::System.Linq.ILookup<int, int> Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                var map = new global::System.Collections.Generic.Dictionary<int, global::System.Collections.Generic.List<int>>(__span.Length);
                var order = new global::System.Collections.Generic.List<int>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var __it0 = __span[__i];
                    int __k0 = default!;

            __k0=__it0%1000;
                    int __tmp0 = default!;

            __tmp0=__it0*2;
                    ref var __g0=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,__k0,out var __exists);
                    if(!__exists) {
                        __g0 = new global::System.Collections.Generic.List<int>();
                        order.Add(__k0);
                    }
                    __g0.Add(__tmp0);
                }
                return new TsavoriteLookup(map, order);
            }
            public OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Linq.ILookup<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Linq.ILookup<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Linq.ILookup<int, int> current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {

            }
        }
        /// <summary>Intercepts <c>ToLookup_elementSelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mW56AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Linq.ILookup<int, int> __linq1=default!;
            using var e___linq1=new OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct0<OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct1>(new OptimizedLinq_ToLookup_elementSelector_Impl_Methods1_ToLookup_elementSelector_664_47_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 32000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<global::System.Linq.ILookup<int, int>>, global::System.Collections.Generic.IEnumerator<global::System.Linq.ILookup<int, int>>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            public global::System.Linq.ILookup<int, int> Current { get; private set; }
            private bool __yd0 = false;
            private sealed class TsavoriteGrouping:global::System.Linq.IGrouping<int,int>{
                private readonly int key;
                private readonly global::System.Collections.Generic.List<int> elements;
                public TsavoriteGrouping(int key,global::System.Collections.Generic.List<int> elements){this.key=key;this.elements=elements;}
                public int Key=>this.key;
                public global::System.Collections.Generic.IEnumerator<int> GetEnumerator()=>this.elements.GetEnumerator();
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            }
            private sealed class TsavoriteLookup:global::System.Linq.ILookup<int,int>{
                private readonly global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> map;
                private readonly global::System.Collections.Generic.List<int> order;
                public TsavoriteLookup(global::System.Collections.Generic.Dictionary<int,global::System.Collections.Generic.List<int>> map,global::System.Collections.Generic.List<int> order){this.map=map;this.order=order;}
                public int Count=>this.map.Count;
                public global::System.Collections.Generic.IEnumerable<int> this[int key]=>this.map.TryGetValue(key,out var g)?g:global::System.Linq.Enumerable.Empty<int>();
                public bool Contains(int key)=>this.map.ContainsKey(key);
                public global::System.Collections.Generic.IEnumerator<global::System.Linq.IGrouping<int,int>> GetEnumerator(){
                    foreach(var k in this.order) yield return new TsavoriteGrouping(k,this.map[k]);
                }
                global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static global::System.Linq.ILookup<int, int> Compute(TEnum source) {
                source.TryGetSpan(out var __span);
                var map = new global::System.Collections.Generic.Dictionary<int, global::System.Collections.Generic.List<int>>(__span.Length);
                var order = new global::System.Collections.Generic.List<int>(__span.Length);
                for(var __i=0;__i<__span.Length;__i++) {
                    var __it0 = __span[__i];
                    int __k0 = default!;

            __k0=__it0%1000;
                    ref var __g0=ref global::System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(map,__k0,out var __exists);
                    if(!__exists) {
                        __g0 = new global::System.Collections.Generic.List<int>();
                        order.Add(__k0);
                    }
                    __g0.Add(__it0);
                }
                return new TsavoriteLookup(map, order);
            }
            public OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
                    this.Current = Compute(__e0);
            }
            
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<global::System.Linq.ILookup<int, int>> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<global::System.Linq.ILookup<int, int>> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out global::System.Linq.ILookup<int, int> current) {
                if(this.__yd0) {
                    current = default!;
                    return false;
                }
                this.__yd0 = true;
                current = this.Current;
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {

            }
        }
        /// <summary>Intercepts <c>ToLookup_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mbl7AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            global::System.Linq.ILookup<int, int> __linq1=default!;
            using var e___linq1=new OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct0<OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct1>(new OptimizedLinq_ToLookup_keySelector_Impl_Methods1_ToLookup_keySelector_670_43_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 32000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            e___linq1.TryGetNext(out __linq1);
            __linq1.Consume(__Field_Base_consumer(@this));
        }

        private struct OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private bool __yd0;
            public OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=new(this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Add(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                if(!this.__yd0){
                    this.__yd0=true;
                    while(second.TryGetNext(out var __it0)){
                        var value=(int)(object?)__it0!;
                        if(this.__lk0.Add(value)){
                            current=this.Current=value;
                            return true;
                        }
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>Union_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2me9+AABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_Union_second_Impl_Methods1_Union_second_688_35_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__src0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct0<TEnum1,TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int>
            where TEnum2:struct,global::System.Collections.Generic.IEnumerator<int>,global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;
            private global::System.Collections.Generic.IEqualityComparer<int>? comparer;
            private readonly global::System.Collections.Generic.HashSet<int> __lk0;
            private byte __st0;
            private static global::System.Collections.Generic.HashSet<int> CreateLookup(TEnum2 source,global::System.Collections.Generic.IEqualityComparer<int>? comparer){
                if(source.TryGetNonEnumeratedCount(out var __cnt0)){
                    return new global::System.Collections.Generic.HashSet<int>(__cnt0,comparer);
                }else{
                    return new global::System.Collections.Generic.HashSet<int>(comparer);
                }
            }
            public OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct0(TEnum1 source,TEnum2 second,global::System.Collections.Generic.IEqualityComparer<int>? comparer) {
                this.source = source;
                this.second = second;
                this.comparer = comparer;
                    this.__lk0=CreateLookup(this.second,this.comparer);
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                while(source.TryGetNext(out var __it0)){
                    if(this.__lk0.Add(__it0)){
                        current=this.Current=__it0;
                        return true;
                    }
                }
                if(this.__st0==0){
                    this.__st0=1;
                    while(second.TryGetNext(out var __tmp0)){
                        if(this.__lk0.Add(__tmp0)){
                            current=this.Current=__tmp0;
                            return true;
                        }
                    }
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>UnionBy_keySelector_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUCAAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __src0=new OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     }));
            using var __sec0=new OptimizedLinq_UnionBy_keySelector_Impl_Methods1_UnionBy_keySelector_695_42_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 19000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     }));
            global::System.Collections.Generic.HashSet<int> __set0;
            if(__sec0.TryGetNonEnumeratedCount(out var __count___set0)){
                __set0=new global::System.Collections.Generic.HashSet<int>(__count___set0,(global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }else{
                __set0=new global::System.Collections.Generic.HashSet<int>((global::System.Collections.Generic.IEqualityComparer<int>?)null);
            }
            if(__src0.TryGetSpan(out var __sp___src0)){
                for(var __i___src0=0;__i___src0<__sp___src0.Length;__i___src0++){
                    var value=__sp___src0[__i___src0];
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__src0.TryGetNext(out int value)){
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
            if(__sec0.TryGetSpan(out var __sp___sec0)){
                for(var __i___sec0=0;__i___sec0<__sp___sec0.Length;__i___sec0++){
                    var value=__sp___sec0[__i___sec0];
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }else{
                while(__sec0.TryGetNext(out int value)){
                    if(__set0.Add(value)){
                        consumer___linq0.Consume(value);
                    }
                }
            }
        }

        private struct OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;
            private int __a0 = -1;
            public OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(!__e0.TryGetNext(out var __it0)) {
                    current = default!;
                    return false;
                }

                this.__a0++;
                if(!(__a0%2==0))
                    goto Skip0;
                current = this.Current = __it0;
                return true;
                Skip0:
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Where_indexPredicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2maiBAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct0<OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct1>(new OptimizedLinq_Where_indexPredicate_Impl_Methods1_Where_indexPredicate_702_43_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(!__e0.TryGetNext(out var __it0)) {
                    current = default!;
                    return false;
                }

                if(!((__it0&1)==0))
                    goto Skip0;
                current = this.Current = __it0;
                return true;
                Skip0:
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        /// <summary>Intercepts <c>Where_predicate_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mf+CAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct0<OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct1>(new OptimizedLinq_Where_predicate_Impl_Methods1_Where_predicate_709_38_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct0<TEnum> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum : struct, global::System.Collections.Generic.IEnumerator<int>, global::Tsavorite.Carrier.IValueEnumerator<int> {
            private TEnum __e0;

            public OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct0(TEnum __e0) {
                this.__e0 = __e0;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                Start0:
                if(!__e0.TryGetNext(out var __it0)) {
                    current = default!;
                    return false;
                }

                if(!((__it0&1)==0))
                    goto Skip0;
                current = this.Current = __it0;
                return true;
                Skip0:
                goto Start0;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                __e0.Dispose();
            }
        }
        [global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "array")]
        private static extern ref int[] __Field_Methods1_array(global::Labo.Shared.Linq.Enumerable.Methods1 @this);
        /// <summary>Intercepts <c>Where_SourceShape_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mUaEAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct0<OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct1>(new OptimizedLinq_Where_SourceShape_Impl_Methods1_Where_SourceShape_716_40_補助0_Struct1(__Field_Methods1_array(@this)));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct0<TEnum1, TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
            where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<int>
            where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;

            public OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct0(TEnum1 source, TEnum2 second) {
                this.source = source;
                this.second = second;
            }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(source.MoveNext()&&second.MoveNext()) {
                    var __it0=source.Current;
                    var __tmp0=second.Current;
                    current = Current = __it0+__tmp0;
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>Zip_second_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mXGFAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct0<OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct1, OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct2>(new OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), new OptimizedLinq_Zip_second_Impl_Methods1_Zip_second_723_33_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })));
            while(__ce0.TryGetNext(out int __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

        private struct OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct1 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e1 = -1;
            public OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct1(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e1 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e1 < source.Length) {
                    current = this.Current = source[__e1];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct2 : global::Tsavorite.Carrier.IValueEnumerator<int>, global::System.Collections.Generic.IEnumerator<int>
        {
            private int[] source;
            private int __e2 = -1;
            public OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct2(int[] source) { this.source = source; }
            public int Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = source.Length - (__e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<int> span) {
                span = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<int> destination, global::System.Index offset) {
                var remaining = global::System.MemoryExtensions.AsSpan(source, __e2 + 1);
                var start = offset.GetOffset(remaining.Length);
                remaining.Slice(start).CopyTo(destination);
                return true;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out int current) {
                if(++__e2 < source.Length) {
                    current = this.Current = source[__e2];
                    return true;
                }
                current = default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() { }
        }
        private struct OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct0<TEnum1, TEnum2> : global::Tsavorite.Carrier.IValueEnumerator<(int First, int Second)>, global::System.Collections.Generic.IEnumerator<(int First, int Second)>
            where TEnum1 : struct, global::System.Collections.Generic.IEnumerator<int>
            where TEnum2 : struct, global::System.Collections.Generic.IEnumerator<int> {
            private TEnum1 source;
            private TEnum2 second;

            public OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct0(TEnum1 source, TEnum2 second) {
                this.source = source;
                this.second = second;
            }
            public (int First, int Second) Current { get; private set; } = default!;
            object? global::System.Collections.IEnumerator.Current => Current;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void global::System.Collections.IEnumerator.Reset() => throw new global::System.NotSupportedException();
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => TryGetNext(out _);
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNonEnumeratedCount(out int count) {
                count = 0;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetSpan(out global::System.ReadOnlySpan<(int First, int Second)> span) {
                span = default;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryCopyTo(scoped global::System.Span<(int First, int Second)> destination, global::System.Index offset) => false;
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool TryGetNext(out (int First, int Second) current) {
                if(source.MoveNext()&&second.MoveNext()) {
                    current=this.Current=(source.Current,second.Current);
                    return true;
                }
                current=default!;
                return false;
            }
            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                source.Dispose();
                second.Dispose();
            }
        }
        /// <summary>Intercepts <c>Zip_tuple_Impl()</c> in <see cref="Labo.Shared.Linq.Enumerable.Methods1"/>.</summary>
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "EHSQtNNw07sT8vdhVsv2mZqGAABNZXRob2RzMS5jcw==")]
        public static void OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32(this global::Labo.Shared.Linq.Enumerable.Methods1 @this) {
            var consumer___linq0=__Field_Base_consumer(@this);
            using var __ce0=new OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct0<OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct1, OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct2>(new OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct1(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_srcCache(@this), static length => {         var values = new int[length];         for(var i = 0;i<values.Length;i++) {             values[i]=i+1;         }         return values;     })), new OptimizedLinq_Zip_tuple_Impl_Methods1_Zip_tuple_729_32_補助0_Struct2(__Method_Base_GetOrCreateByLength(@this, 36000000, __Field_Base_src2Cache(@this), static length => {         var values = new int[length/2];         var start = length/2+1;         for(var i = 0;i<values.Length;i++) {             values[i]=start+i;         }         return values;     })));
            while(__ce0.TryGetNext(out (int First, int Second) __cv0)){
                consumer___linq0.Consume(__cv0);
            }
        }

    }

}
