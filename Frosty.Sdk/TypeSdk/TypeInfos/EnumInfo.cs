using Frosty.Sdk.IO;
using Frosty.Sdk.TypeSdk.TypeInfoDatas;

namespace Frosty.Sdk.TypeSdk.TypeInfos;

internal class EnumInfo : TypeInfo
{
    public EnumInfo(EnumInfoData data)
        : base(data)
    {
    }
    public override string ReadDefaultValue(MemoryReader reader)
    {
        return (m_data as EnumInfoData)?.ReadDefaultValue(reader) ?? string.Empty;
    }
}