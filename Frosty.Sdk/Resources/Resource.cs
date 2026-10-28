using System;
using Frosty.Sdk.IO;

namespace Frosty.Sdk.Resources;

public abstract class Resource
{
    public abstract void Deserialize(DataAccessor inAccessor, ReadOnlySpan<byte> inResMeta);

    public abstract void Serialize(DataAccessor inAccessor, Span<byte> resMeta);
}