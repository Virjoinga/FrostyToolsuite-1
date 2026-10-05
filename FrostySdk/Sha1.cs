using System;

namespace FrostySdk
{
    public struct Sha1 : IEquatable<Sha1>
    {
        public static readonly Sha1 Zero = new Sha1();
        private uint m_a, m_b, m_c, m_d, m_e;

        public Sha1(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 20)
                throw new ArgumentException("Input buffer is too small");

            m_a = (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16 | bytes[3] << 24);
            m_b = (uint)(bytes[4] | bytes[5] << 8 | bytes[6] << 16 | bytes[7] << 24);
            m_c = (uint)(bytes[8] | bytes[9] << 8 | bytes[10] << 16 | bytes[11] << 24);
            m_d = (uint)(bytes[12] | bytes[13] << 8 | bytes[14] << 16 | bytes[15] << 24);
            m_e = (uint)(bytes[16] | bytes[17] << 8 | bytes[18] << 16 | bytes[19] << 24);
        }

        public Sha1(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 20)
            {
                throw new ArgumentException("Input buffer is too small");
            }

            m_a = (uint)(bytes[0 * 4 + 0] | bytes[0 * 4 + 1] << 8 | bytes[0 * 4 + 2] << 16 | bytes[0 * 4 + 3] << 24);
            m_b = (uint)(bytes[1 * 4 + 0] | bytes[1 * 4 + 1] << 8 | bytes[1 * 4 + 2] << 16 | bytes[1 * 4 + 3] << 24);
            m_c = (uint)(bytes[2 * 4 + 0] | bytes[2 * 4 + 1] << 8 | bytes[2 * 4 + 2] << 16 | bytes[2 * 4 + 3] << 24);
            m_d = (uint)(bytes[3 * 4 + 0] | bytes[3 * 4 + 1] << 8 | bytes[3 * 4 + 2] << 16 | bytes[3 * 4 + 3] << 24);
            m_e = (uint)(bytes[4 * 4 + 0] | bytes[4 * 4 + 1] << 8 | bytes[4 * 4 + 2] << 16 | bytes[4 * 4 + 3] << 24);
        }

        public Sha1(string text)
        {
            byte[] bytes = new byte[text.Length / 2];
            for (int i = 0; i < text.Length; i += 2)
                bytes[i / 2] = byte.Parse(text.Substring(i, 2), System.Globalization.NumberStyles.AllowHexSpecifier);

            if (bytes.Length < 20)
            {
                throw new ArgumentException("Input buffer is too small");
            }

            m_a = (uint)(bytes[0 * 4 + 0] | bytes[0 * 4 + 1] << 8 | bytes[0 * 4 + 2] << 16 | bytes[0 * 4 + 3] << 24);
            m_b = (uint)(bytes[1 * 4 + 0] | bytes[1 * 4 + 1] << 8 | bytes[1 * 4 + 2] << 16 | bytes[1 * 4 + 3] << 24);
            m_c = (uint)(bytes[2 * 4 + 0] | bytes[2 * 4 + 1] << 8 | bytes[2 * 4 + 2] << 16 | bytes[2 * 4 + 3] << 24);
            m_d = (uint)(bytes[3 * 4 + 0] | bytes[3 * 4 + 1] << 8 | bytes[3 * 4 + 2] << 16 | bytes[3 * 4 + 3] << 24);
            m_e = (uint)(bytes[4 * 4 + 0] | bytes[4 * 4 + 1] << 8 | bytes[4 * 4 + 2] << 16 | bytes[4 * 4 + 3] << 24);
        }

        public static bool operator ==(Sha1 A, Sha1 B) => A.Equals(B);
        public static bool operator !=(Sha1 A, Sha1 B) => !A.Equals(B);

        public bool Equals(Sha1 other) => m_a == other.m_a && m_b == other.m_b && m_c == other.m_c && m_d == other.m_d && m_e == other.m_e;

        public override bool Equals(object obj)
        {
            if (obj is Sha1 otherSha1)
                return Equals(otherSha1);

            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = (hash * 16777619) ^ m_a.GetHashCode();
                hash = (hash * 16777619) ^ m_b.GetHashCode();
                hash = (hash * 16777619) ^ m_c.GetHashCode();
                hash = (hash * 16777619) ^ m_d.GetHashCode();
                hash = (hash * 16777619) ^ m_e.GetHashCode();
                return hash;
            }
        }

        // Used by NativeWriter to push bytes into a buffer allocation free
        public void WriteTo(byte[] buffer, int offset)
        {
            buffer[offset + 0] = (byte)(m_a & 0xFF); buffer[offset + 1] = (byte)((m_a >> 8) & 0xFF); buffer[offset + 2] = (byte)((m_a >> 16) & 0xFF); buffer[offset + 3] = (byte)((m_a >> 24) & 0xFF);
            buffer[offset + 4] = (byte)(m_b & 0xFF); buffer[offset + 5] = (byte)((m_b >> 8) & 0xFF); buffer[offset + 6] = (byte)((m_b >> 16) & 0xFF); buffer[offset + 7] = (byte)((m_b >> 24) & 0xFF);
            buffer[offset + 8] = (byte)(m_c & 0xFF); buffer[offset + 9] = (byte)((m_c >> 8) & 0xFF); buffer[offset + 10] = (byte)((m_c >> 16) & 0xFF); buffer[offset + 11] = (byte)((m_c >> 24) & 0xFF);
            buffer[offset + 12] = (byte)(m_d & 0xFF); buffer[offset + 13] = (byte)((m_d >> 8) & 0xFF); buffer[offset + 14] = (byte)((m_d >> 16) & 0xFF); buffer[offset + 15] = (byte)((m_d >> 24) & 0xFF);
            buffer[offset + 16] = (byte)(m_e & 0xFF); buffer[offset + 17] = (byte)((m_e >> 8) & 0xFF); buffer[offset + 18] = (byte)((m_e >> 16) & 0xFF); buffer[offset + 19] = (byte)((m_e >> 24) & 0xFF);
        }

        public byte[] ToByteArray()
        {
            byte[] bytes =
            [
                (byte)(m_a & 0xFF),
                (byte)((m_a >> 8) & 0xFF),
                (byte)((m_a >> 16) & 0xFF),
                (byte)((m_a >> 24) & 0xFF),
                (byte)(m_b & 0xFF),
                (byte)((m_b >> 8) & 0xFF),
                (byte)((m_b >> 16) & 0xFF),
                (byte)((m_b >> 24) & 0xFF),
                (byte)(m_c & 0xFF),
                (byte)((m_c >> 8) & 0xFF),
                (byte)((m_c >> 16) & 0xFF),
                (byte)((m_c >> 24) & 0xFF),
                (byte)(m_d & 0xFF),
                (byte)((m_d >> 8) & 0xFF),
                (byte)((m_d >> 16) & 0xFF),
                (byte)((m_d >> 24) & 0xFF),
                (byte)(m_e & 0xFF),
                (byte)((m_e >> 8) & 0xFF),
                (byte)((m_e >> 16) & 0xFF),
                (byte)((m_e >> 24) & 0xFF),
            ];
            return bytes;
        }

        public bool TryWriteBytes(Span<byte> destination)
        {
            if (destination.Length < 20)
            {
                return false;
            }

            destination[0 * 4 + 0] = (byte)(m_a & 0xFF); destination[0 * 4 + 1] = (byte)((m_a >> 8) & 0xFF); destination[0 * 4 + 2] = (byte)((m_a >> 16) & 0xFF); destination[0 * 4 + 3] = (byte)((m_a >> 24) & 0xFF);
            destination[1 * 4 + 0] = (byte)(m_b & 0xFF); destination[1 * 4 + 1] = (byte)((m_b >> 8) & 0xFF); destination[1 * 4 + 2] = (byte)((m_b >> 16) & 0xFF); destination[1 * 4 + 3] = (byte)((m_b >> 24) & 0xFF);
            destination[2 * 4 + 0] = (byte)(m_c & 0xFF); destination[2 * 4 + 1] = (byte)((m_c >> 8) & 0xFF); destination[2 * 4 + 2] = (byte)((m_c >> 16) & 0xFF); destination[2 * 4 + 3] = (byte)((m_c >> 24) & 0xFF);
            destination[3 * 4 + 0] = (byte)(m_d & 0xFF); destination[3 * 4 + 1] = (byte)((m_d >> 8) & 0xFF); destination[3 * 4 + 2] = (byte)((m_d >> 16) & 0xFF); destination[3 * 4 + 3] = (byte)((m_d >> 24) & 0xFF);
            destination[4 * 4 + 0] = (byte)(m_e & 0xFF); destination[4 * 4 + 1] = (byte)((m_e >> 8) & 0xFF); destination[4 * 4 + 2] = (byte)((m_e >> 16) & 0xFF); destination[4 * 4 + 3] = (byte)((m_e >> 24) & 0xFF);

            return true;
        }

        public override string ToString()
        {
            return
                $"{ToBigEndianHex(m_a)}{ToBigEndianHex(m_b)}{ToBigEndianHex(m_c)}{ToBigEndianHex(m_d)}{ToBigEndianHex(m_e)}";
        }

        private string ToBigEndianHex(uint inValue)
        {
            return ((byte)inValue).ToString("x2")
                   + ((byte)(inValue >> 8)).ToString("x2")
                   + ((byte)(inValue >> 16)).ToString("x2")
                   + ((byte)(inValue >> 24)).ToString("x2");
        }
    }
}