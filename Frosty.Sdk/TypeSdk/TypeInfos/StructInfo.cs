using Frosty.Sdk.IO;
using Frosty.Sdk.TypeSdk.TypeInfoDatas;

namespace Frosty.Sdk.TypeSdk.TypeInfos;

internal class StructInfo : TypeInfo
{
    public StructInfo(StructInfoData data)
        : base(data)
    {
    }

    public void ReadDefaultValues(MemoryReader reader)
    {
        (m_data as StructInfoData)?.ReadDefaultValues(reader);
    }

    public override string ReadDefaultValue(MemoryReader reader)
    {
        return (m_data as StructInfoData)?.ReadDefaultValue(reader) ?? string.Empty;
    }
}

