namespace Seamless.System.Carrier.Internal;

internal unsafe ref struct ValueStringBuilder(Span<char> initialBuffer) {
    private const int        StringMaxLength       = 0x3FFFFFDF;
    private const int        MinimumArrayPoolLength= 256;
    private       Span<char> chars                 =initialBuffer; // initial-buffer or array from pool
    private       int        currentPosition       =0;

    // use SegmentedArrayProvider is slow(I don't know why......) in small size, so use expandable pooled array in field.
    // SegmentedArrayProvider<char> arrayProvider;
    private char[]? arrayToReturnToPool=null;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char value) {
        var p = this.currentPosition;
        var c = this.chars;

        if(p>=c.Length) {
            this.ExpandAndAppend(value);
            return;
        }

        c[p]=value;
        this.currentPosition=p+1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ExpandAndAppend(char value) {
        this.Expand(1);
        this.chars[this.currentPosition]=value;
        this.currentPosition+=1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(string? value) {
        if(value==null) return;

        if(this.currentPosition>this.chars.Length-value.Length) {
            this.Expand(value.Length);
        }

        value.CopyTo(this.chars[this.currentPosition..]);
        this.currentPosition+=value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char separator,string? value) {
        if(value==null) return;

        var p = this.currentPosition;
        var c = this.chars;

        if(p>=c.Length) {
            this.ExpandAndAppend(separator);
        } else {
            c[p]=separator;
            this.currentPosition=p+1;
        }

        if(this.currentPosition>this.chars.Length-value.Length) {
            this.Expand(value.Length);
        }

        value.CopyTo(this.chars[this.currentPosition..]);
        this.currentPosition+=value.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(scoped ReadOnlySpan<char> value) {
    AGAIN:
        if(value.TryCopyTo(this.chars[this.currentPosition..])) {
            this.currentPosition+=value.Length;
        } else {
            this.Expand(value.Length);
            goto AGAIN;
        }
    }

    public unsafe void Append<T>(T value) {
        if(typeof(T)==typeof(string)) {
            this.Append(Unsafe.As<T,string>(ref value));
        }
#if NETSTANDARD2_1 || NET8_0_OR_GREATER
        else if(typeof(T)==typeof(byte)) {
            const int CharMaxLength = 3;
            int charsWritten;
            while(!Unsafe.As<T,byte>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(sbyte)) {
            const int CharMaxLength = 4;
            int charsWritten;
            while(!Unsafe.As<T,sbyte>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(short)) {
            const int CharMaxLength = 6;
            int charsWritten;
            while(!Unsafe.As<T,short>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(ushort)) {
            const int CharMaxLength = 5;
            int charsWritten;
            while(!Unsafe.As<T,ushort>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(int)) {
            const int CharMaxLength = 11;
            int charsWritten;
            while(!Unsafe.As<T,int>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(uint)) {
            const int CharMaxLength = 10;
            int charsWritten;
            while(!Unsafe.As<T,uint>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(long)) {
            const int CharMaxLength = 20;
            int charsWritten;
            while(!Unsafe.As<T,long>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(ulong)) {
            const int CharMaxLength = 20;
            int charsWritten;
            while(!Unsafe.As<T,ulong>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(float)) {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while(!Unsafe.As<T,float>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(double)) {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while(!Unsafe.As<T,double>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        } else if(typeof(T)==typeof(decimal)) {
            const int CharMaxLength = 128; // reserved space for unknown culture specified format
            int charsWritten;
            while(!Unsafe.As<T,decimal>(ref value).TryFormat(this.chars[this.currentPosition..],out charsWritten)) {
                this.Expand(CharMaxLength);
            }
            this.currentPosition+=charsWritten;
        }
#endif
          else {
            // NETATANDARD2_0 has no TryFormat so always boxed.
            // If Enum, we do not have Enum.TryFormatUnconstrained so always boxed in NETSTANDARD2_1.

#if NET8_0_OR_GREATER
            if(value is ISpanFormattable) {
                int charsWritten;
                while(!((ISpanFormattable)value).TryFormat(this.chars[this.currentPosition..],out charsWritten,default,null)) {
                    this.Expand(1);
                }

                this.currentPosition+=charsWritten;
                return;
            }
#endif

            string? s;
            if(value is IFormattable) {
                s=((IFormattable)value).ToString(format: null,formatProvider: null);
            } else {
                s=value?.ToString();
            }

            if(s is not null) {
                this.Append(s);
            }
        }
    }

    public string ToStringAndClear() {
        var str = this.chars[..this.currentPosition];
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

        char[]? toReturn = this.arrayToReturnToPool;
        this=default; // defensive clear
        if(toReturn is not null) {
            ArrayPool<char>.Shared.Return(toReturn,clearArray: false);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Expand(int appendSize) {
        uint requiredMinCapacity = (uint)this.currentPosition+(uint)appendSize;

        uint newCapacity = Math.Max(requiredMinCapacity,Math.Min((uint)this.chars.Length*2,StringMaxLength));
        var arraySize = (int)MathClamp(newCapacity,MinimumArrayPoolLength,int.MaxValue);

        var newChars = ArrayPool<char>.Shared.Rent(arraySize);
        this.chars[..this.currentPosition].CopyTo(newChars);

        if(this.arrayToReturnToPool is not null) {
            ArrayPool<char>.Shared.Return(this.arrayToReturnToPool,clearArray: false);
        }

        this.chars=this.arrayToReturnToPool=newChars;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint MathClamp(uint value,uint min,uint max) {
        if(min>max) {
            ThrowMinMaxException(min,max);
        }

        if(value<min) {
            return min;
        } else if(value>max) {
            return max;
        }

        return value;
    }
    private static void ThrowMinMaxException<T>(T min,T max) {
        throw new ArgumentException();
    }
}
