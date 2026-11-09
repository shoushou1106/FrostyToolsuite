using System.Diagnostics.CodeAnalysis;

namespace Frosty.Sdk.Utils;

public readonly struct FourCc(uint value) : IEquatable<FourCc>
{
    private readonly uint _value = value;

    public static implicit operator uint(FourCc value)
    {
        return value._value;
    }

    public static implicit operator FourCc(uint value)
    {
        return new FourCc(value);
    }

    public static implicit operator string(FourCc value)
    {
        return FromFourCc(value);
    }

    public static implicit operator FourCc(string value)
    {
        return ToFourCc(value);
    }

    public static FourCc FromUInt32(uint value)
    {
        return new FourCc(value);
    }

    public uint ToUInt32()
    {
        return _value;
    }

    public static bool operator ==(FourCc a, FourCc b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(FourCc a, FourCc b)
    {
        return !a.Equals(b);
    }

    public static bool operator ==(FourCc a, object? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(FourCc a, object? b)
    {
        return !a.Equals(b);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj switch
        {
            uint b => b == _value,
            string s => Equals(ToFourCc(s)),
            FourCc other => Equals(other),
            _ => false
        };
    }

    public bool Equals(FourCc other)
    {
        return _value == other._value;
    }

    public override int GetHashCode()
    {
        return (int)_value;
    }

    /// <remarks><b>Encoding:</b> Latin-1 (one byte per char)</remarks>
    public static string FromFourCc(FourCc value)
    {
        ReadOnlySpan<char> result =
        [
            (char)(value._value & 0xFF),
            (char)((value._value >> 8) & 0xFF),
            (char)((value._value >> 16) & 0xFF),
            (char)((value._value >> 24) & 0xFF)
        ];
        return new string(result);
    }

    /// <remarks><b>Encoding:</b> Latin-1 (one byte per char)</remarks>
    /// <exception cref="ArgumentException"><paramref name="value" /> is not exactly 4 characters.</exception>
    public static FourCc ToFourCc(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length != 4 || value.AsSpan().ContainsAnyExceptInRange('\0', '\u00FF'))
        {
            throw new ArgumentException("FourCC string must be exactly 4 characters long.", nameof(value));
        }

        uint result = (uint)(((byte)value[3] << 24)
                             | ((byte)value[2] << 16)
                             | ((byte)value[1] << 8)
                             | (byte)value[0]);

        return new FourCc(result);
    }
}