using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     A value of an EBX field that holds the path of a file.
/// </summary>
public readonly struct FileRef(string? value) : IEquatable<FileRef>
{
    private readonly string? _fileName = value;

    public static implicit operator string(FileRef value)
    {
        return value._fileName ?? string.Empty;
    }

    public static implicit operator FileRef(string? value)
    {
        return new FileRef(value);
    }

    public static bool operator ==(FileRef a, FileRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(FileRef a, FileRef b)
    {
        return !a.Equals(b);
    }

    public static FileRef FromString(string? value)
    {
        return new FileRef(value);
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"FileRef '{_fileName ?? "null"}'");
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FileRef other && Equals(other);
    }

    public bool Equals(FileRef other)
    {
        return string.Equals(_fileName, other._fileName, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return _fileName?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}