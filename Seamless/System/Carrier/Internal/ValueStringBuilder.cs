namespace Seamless.System.Carrier.Internal;

internal unsafe ref struct ValueStringBuilder
{
    const int StringMaxLength = 0x3FFFFFDF;
    const int MinimumArrayPoolLength = 256;

    Span<char> chars; // initial-buffer or array from pool
    int currentPosition;

    // use SegmentedArrayProvider is slow(I don't know why......) in small size, so use expandable pooled array in field.
    // SegmentedArrayProvider<char> arrayProvider;
    char[]? arrayToReturnToPool;

    public ValueStringBuilder(Span<char> initialBuffer)
    {
        arrayToReturnToPool = null;
        chars = initialBuffer;
        currentPosition = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char value)
    {
        var p = currentPosition;
        var c = chars;

        if (p >= c.Length)
        {
            ExpandAndAppend(value);
            return;
        }

        c[p] = value;
        currentPosition = p + 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void ExpandAndAppend(char value)
    {
        Expand(1);
        chars[currentPosition] = value;
        currentPosition += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? value)
    {
        if (value == null) return;

        if (currentPosition >chars.Length - value.Length)
        {
            Expand(value.Length);
        }

        value.CopyTo(chars.Slice(currentPosition));
        currentPosition += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char separator, string? value)
    {
        if (value == null) return;

        var p = currentPosition;
        var c = chars;

        if (p >= c.Length)
        {
            ExpandAndAppend(separator);
        }
        else
        {
            c[p] = separator;
            currentPosition = p + 1;
        }

        if (currentPosition >chars.Length - value.Length)
        {
            Expand(value.Length);
        }

        value.CopyTo(chars.Slice(currentPosition));
        currentPosition += value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<char> value)
    {
    AGAIN:
        if (value.TryCopyTo(chars.Slice(currentPosition)))
        {
            currentPosition += value.Length;
        }
        else
        {
            Expand(value.Length);
            goto AGAIN;
        }
    }

    public unsafe void Append<T>(T value)
    {
        if (typeof(T) == typeof(string))
        {
            Append(Unsafe.As<T, string>(ref value));
        }
#if NETSTANDARD2_1 || NET8_0_OR_GREATER
        else if (typeof(T) == typeof(byte))
        {
            const int CharMaxLength = 3;
            int charsWritten;
            while (!Unsafe.As<T, byte>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(sbyte))
        {
            const int CharMaxLength = 4;
            int charsWritten;
            while (!Unsafe.As<T, sbyte>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(short))
        {
            const int CharMaxLength = 6;
            int charsWritten;
            while (!Unsafe.As<T, short>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(ushort))
        {
            const int CharMaxLength = 5;
            int charsWritten;
            while (!Unsafe.As<T, ushort>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(int))
        {
            const int CharMaxLength = 11;
            int charsWritten;
            while (!Unsafe.As<T, int>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(uint))
        {
            const int CharMaxLength = 10;
            int charsWritten;
            while (!Unsafe.As<T, uint>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(long))
        {
            const int CharMaxLength = 20;
            int charsWritten;
            while (!Unsafe.As<T, long>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(ulong))
        {
            const int CharMaxLength = 20;
            int charsWritten;
            while (!Unsafe.As<T, ulong>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(float))
        {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while (!Unsafe.As<T, float>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(double))
        {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while (!Unsafe.As<T, double>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
        else if (typeof(T) == typeof(decimal))
        {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while (!Unsafe.As<T, decimal>(ref value).TryFormat(chars.Slice(currentPosition), out charsWritten))
            {
                Expand(CharMaxLength);
            }
            currentPosition += charsWritten;
        }
#endif
        else
        {
            // NETATANDARD2_0 has no TryFormat so always boxed.
            // If Enum, we do not have Enum.TryFormatUnconstrained so always boxed in NETSTANDARD2_1.

#if NET8_0_OR_GREATER
            if (value is ISpanFormattable)
            {
                int charsWritten;
                while (!((ISpanFormattable)value).TryFormat(chars.Slice(currentPosition), out charsWritten, default, null))
                {
                    Expand(1);
                }

                currentPosition += charsWritten;
                return;
            }
#endif

            string? s;
            if (value is IFormattable)
            {
                s = ((IFormattable)value).ToString(format: null, formatProvider: null);
            }
            else
            {
                s = value?.ToString();
            }

            if (s is not null)
            {
                Append(s);
            }
        }
    }

    public string ToStringAndClear()
    {
        var str = chars.Slice(0,currentPosition);
#if !NETSTANDARD2_0
        var result = new string(str);
#else
        string result;
        unsafe
        {
            fixed (char* p = str)
            {
                result = new string(p, 0, str.Length);
            }
        }
#endif

        char[]? toReturn = arrayToReturnToPool;
        this = default; // defensive clear
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn, clearArray: false);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Expand(int appendSize)
    {
        uint requiredMinCapacity = (uint)currentPosition + (uint)appendSize;

        uint newCapacity = Math.Max(requiredMinCapacity, Math.Min((uint)chars.Length * 2, StringMaxLength));
        var arraySize = (int)MathClamp(newCapacity, MinimumArrayPoolLength, int.MaxValue);

        var newChars = ArrayPool<char>.Shared.Rent(arraySize);
        chars.Slice(0,currentPosition).CopyTo(newChars);

        if (arrayToReturnToPool is not null)
        {
            ArrayPool<char>.Shared.Return(arrayToReturnToPool, clearArray: false);
        }

        chars =arrayToReturnToPool = newChars;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint MathClamp(uint value, uint min, uint max)
    {
        if (min > max)
        {
            ThrowMinMaxException(min, max);
        }

        if (value < min)
        {
            return min;
        }
        else if (value > max)
        {
            return max;
        }

        return value;
    }

    static void ThrowMinMaxException<T>(T min, T max)
    {
        throw new ArgumentException();
    }
}
