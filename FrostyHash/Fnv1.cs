using HashifyNet;
using HashifyNet.Algorithms.FNV;

namespace Frosty.Hash;

public class Fnv1
{
    private const int FNV1_32_OFFSET_BASIS = 5381;
    private const int FNV1_32_PRIME        = 33;

    private static readonly IFNV1 s_fnv132 = HashFactory<IFNV1>.Create(new FNVConfigProfile32Bits() { Offset = FNV1_32_OFFSET_BASIS, Prime = FNV1_32_PRIME });

    /* Frostbite uses the default prime and offset basis for 64-bit Fnv1 (the default values provided by HashifyNET), so we don't need to be explicit here. */
    private static readonly IFNV1 s_fnv164 = HashFactory<IFNV1>.Create(new FNVConfigProfile64Bits());

    public static int Hash(byte[] data)
    {
        if (data.Length is 0)
            return FNV1_32_OFFSET_BASIS;
        else
            return s_fnv132.ComputeHash(data).AsInt32();
    }

    public static int HashString(string data)
    {
        if (data.Length is 0)
            return FNV1_32_OFFSET_BASIS;
        else
            return s_fnv132.ComputeHash(data).AsInt32();
    }

    /* Compared to the 32-bit implementations, `FrostyHash` doesn't return the 64-bit Fnv1 offset basis if the data is empty. We'll maintain this behavior for
     * compatibility.
     */
    public static ulong Hash64(byte[] data)
    {
        if (data.Length is 0)
            return 0;
        else
            return (ulong)(s_fnv164.ComputeHash(data).AsInt64());
    }

    public static ulong HashString64(string data)
    { 
        if (data.Length is 0)
            return 0;
        else
            return (ulong)(s_fnv164.ComputeHash(data).AsInt64());
    }
}
