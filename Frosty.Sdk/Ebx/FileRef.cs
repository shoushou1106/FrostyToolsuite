using System.Diagnostics.CodeAnalysis;

namespace Frosty.Sdk.Ebx;

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

    public static FileRef FromString(string? value)
    {
        return new FileRef(value);
    }

    public override string ToString()
    {
        return $"FileRef '{_fileName ?? "null"}'";
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FileRef b && Equals(b);
    }

    public bool Equals(FileRef other)
    {
        return _fileName == other._fileName;
    }

    public static bool operator ==(FileRef a, object? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(FileRef a, object? b)
    {
        return !a.Equals(b);
    }

    public override int GetHashCode()
    {
        return _fileName?.GetHashCode(StringComparison.Ordinal) ?? 0;
    }
}