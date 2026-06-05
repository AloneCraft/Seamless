namespace Seamless.Linq.Carrier.Internal;

internal static class RandomShared
{
    public static void Shuffle<T>(Span<T> span)
    {
        Shared.Shuffle(span);
    }

    public static void PartialShuffle<T>(Span<T> span, int count)
    {
        if (count >= span.Length)
        {
            Shared.Shuffle(span);
        }
        else
        {
            Shared.PartialShuffle(span, count);
        }
    }

    internal sealed class Xoshiro256StarStar
    {
        private ulong _s0, _s1, _s2, _s3;

        public Xoshiro256StarStar()
        {
#if NETSTANDARD2_1_OR_GREATER
            var span = (stackalloc ulong[4]);
            do
            {
                RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(span));
                _s0 = span[0];
                _s1 = span[1];
                _s2 = span[2];
                _s3 = span[3];
            } while (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0);
#else
            var array = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            do
            {
                rng.GetBytes(array);
                var ulongSpan = MemoryMarshal.Cast<byte, ulong>(array);
                _s3 = ulongSpan[3];
                _s0 = ulongSpan[0];
                _s1 = ulongSpan[1];
                _s2 = ulongSpan[2];
            } while (_s0 == 0 && _s1 == 0 && _s2 == 0 && _s3 == 0);
#endif
        }

        public ulong NextUInt64()
        {
            ulong s0 = _s0;
            ulong s1 = _s1;
            ulong s2 = _s2;
            ulong s3 = _s3;

#if NETCOREAPP3_0_OR_GREATER
            ulong result = BitOperations.RotateLeft(s1 * 5, 7) * 9;
#else
            ulong result = s1 * 5;
            result = s1 << 7 ^ s1 >> -7;
            result *= 9;
#endif
            ulong t = s1 << 17;

            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;

            s2 ^= t;

#if NETCOREAPP3_0_OR_GREATER
            s3 = BitOperations.RotateLeft(s3, 45);
#else
            s3 = s3 << 45 ^ s3 >> -45;
#endif

            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;

            return result;
        }

        private static ulong BigMul(ulong a, ulong b, out ulong lo)
        {
#if NET5_0_OR_GREATER
            return Math.BigMul(a, b, out lo);
#else
            uint alo = (uint)a;
            uint ahi = (uint)(a >> 32);
            uint blo = (uint)b;
            uint bhi = (uint)(b >> 32);

            ulong lolo = (ulong)alo * blo;
            ulong lohi = (ulong)alo * bhi;
            ulong hilo = (ulong)ahi * blo;
            ulong hihi = (ulong)ahi * bhi;

            ulong middle = lohi + hilo;
            ulong carry = ((middle < lohi) ? 1ul : 0ul) << 32;

            lo = lolo + (middle << 32);
            carry += (lo < lolo) ? 1ul : 0ul;

            ulong hi = hihi + (middle >> 32) + carry;
            return hi;
#endif
        }

        public ulong NextUInt64(ulong maxExclusive)
        {
            ulong r = NextUInt64();
            ulong rhi = BigMul(r, maxExclusive, out ulong rlo);

            if (rlo < maxExclusive)
            {
                ulong mod = (0 - maxExclusive) % maxExclusive;
                while (rlo < mod)
                {
                    r = NextUInt64();
                    rhi = BigMul(r, maxExclusive, out rlo);
                }
            }

            return rhi;
        }

        // for details on the algorithm, see https://github.com/dotnet/runtime/pull/111015
        public void Shuffle<T>(Span<T> values)
        {
            // The upper limit of the first random number generated.
            // 2432902008176640000 == 20! (Largest factorial smaller than 2^64)
            ulong bound = 2432902008176640000;

            int nextIndex = Math.Min(20, values.Length);

            for (int i = 1; i < values.Length;)
            {
                ulong r = NextUInt64();

                // Correct r to be unbiased.
                // Ensure that the result of `BigMul(r, bound, out _)` is
                // uniformly distributed between 0 <= result < bound without bias.
                ulong rbound = r * bound;

                // Look at the lower 64 bits of r * bound,
                // and if there is a carryover possibility...
                // (The maximum value added in subsequent processing is bound - 1,
                //  so if rbound <= (2^64) - bound, no carryover occurs.)
                if (rbound > 0 - bound)
                {
                    ulong sum, carry;
                    do
                    {
                        // Generate an additional random number t and check if it carries over
                        //   [rhi] . [rlo]        -> r * bound; upper rhi, lower rlo
                        // +     0 . [thi] [tlo]  -> t * bound; upper thi, lower tlo
                        // ---------------------
                        //   [carry.  sum] [tlo]  -> rhi + carry is the result
                        ulong t = NextUInt64();
                        ulong thi = BigMul(t, bound, out ulong tlo);
                        sum = rbound + thi;
                        carry = sum < rbound ? 1ul : 0ul;
                        rbound = tlo;

                        // If sum == 0xff...ff, there is a possibility of a carry
                        // in the future, so calculate it again.
                        // If not, there will be no more carry,
                        // so add the carry and finish.
                    } while (sum == ~0ul);
                    r += carry;
                }

                // Do the Fisher-Yates shuffle based on r.
                // For example, the result of `BigMul(r, 20!, out _)` is expressed as
                //    (0..2) * 20!/2! + (0..3) * 20!/3! + ... + (0..20) * 20!/20!
                // Imagine extracting the numbers inside the parentheses.
                for (int m = i; m < nextIndex; m++)
                {
                    int index = (int)BigMul(r, (ulong)(m + 1), out r);

                    T temp = values[m];
                    values[m] = values[index];
                    values[index] = temp;
                }

                i = nextIndex;

                // Calculates next bound.
                // bound is (i + 1) * (i + 2) * ... * (nextIndex) < 2^64
                bound = (ulong)(i + 1);
                for (nextIndex = i + 1; nextIndex < values.Length; nextIndex++)
                {
                    if (BigMul(bound, (ulong)(nextIndex + 1), out var newbound) == 0)
                    {
                        bound = newbound;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public void PartialShuffle<T>(Span<T> values, int count)
        {
            count = Math.Min(count, values.Length);

            for (int i = 0; i < count;)
            {
                // Calculates the bound first because, unlike Shuffle(),
                // it calculates it in the smaller direction:
                // (Length * (Length - 1) * (Length - 2) * ...).
                ulong bound = (ulong)(values.Length - i);
                int nextIndex;
                if (bound <= 20)
                {
                    bound = 2432902008176640000;
                    nextIndex = count;
                }
                else
                {
                    for (nextIndex = i + 1; nextIndex < count; nextIndex++)
                    {
                        if (BigMul(bound, (ulong)(values.Length - nextIndex), out var newbound) == 0)
                        {
                            bound = newbound;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // Correct r to be unbiased.
                ulong r = NextUInt64();
                ulong rbound = r * bound;
                if (rbound > 0 - bound)
                {
                    ulong sum, carry;
                    do
                    {
                        ulong t = NextUInt64();
                        ulong thi = BigMul(t, bound, out ulong tlo);
                        sum = rbound + thi;
                        carry = sum < rbound ? 1ul : 0ul;
                        rbound = tlo;
                    } while (sum == ~0ul);
                    r += carry;
                }

                // Do the Fisher-Yates shuffle based on r.
                for (int m = i; m < nextIndex; m++)
                {
                    int index = m + (int)BigMul(r, (ulong)(values.Length - m), out r);

                    T temp = values[m];
                    values[m] = values[index];
                    values[index] = temp;
                }

                i = nextIndex;
            }
        }
    }

    [ThreadStatic]
    private static Xoshiro256StarStar? s_Shared;
    private static Xoshiro256StarStar Shared => s_Shared ??= new();
}
