using System;

namespace Frosty.Modding.Attributes;

public class HandlerAttribute : Attribute
{
    public int Hash { get; }

    public HandlerAttribute(int inHash)
    {
        Hash = inHash;
    }

    public HandlerAttribute(string inName)
    {
        Hash = Sdk.Utils.Utils.HashString(inName, true);
    }
}