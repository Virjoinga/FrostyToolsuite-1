using HashifyNet;
using HashifyNet.Algorithms.MurmurHash;

namespace Frosty.Hash;

public class Murmur2
{
    public static ulong Hash64(byte[] data, ulong seed)
    {
        if (data.Length is 0)
            return 0;
        IMurmurHash2 murmurHash264 = HashFactory<IMurmurHash2>.Create(new MurmurHash2Config() { HashSizeInBits = 64, Seed = (long)(seed) });
        return (ulong)(murmurHash264.ComputeHash(data).AsInt64());
    }

    public static ulong HashString64(string data, ulong seed)
    {
        if (data.Length is 0)
            return 0;
        IMurmurHash2 murmurHash264 = HashFactory<IMurmurHash2>.Create(new MurmurHash2Config() { HashSizeInBits = 64, Seed = (long)(seed) });
        return (ulong)(murmurHash264.ComputeHash(data).AsInt64());
    }
}
