using HashifyNet;
using HashifyNet.Algorithms.CityHash;

namespace Frosty.Hash;

public class CityHash
{
    private static readonly ICityHash s_cityHash64 = HashFactory<ICityHash>.Create(new CityHashConfigProfile64Bits());

    public static ulong Hash64(byte[] data)
    {
        if (data.Length is 0)
            return 0;
        else
            return (ulong)(s_cityHash64.ComputeHash(data).AsInt64());
    }
}
