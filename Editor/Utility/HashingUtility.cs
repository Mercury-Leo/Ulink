using System.Text;

namespace Ulink.Editor
{
    internal static class HashingUtility
    {
        private const ulong FnvOffsetBasis = 0xcbf29ce484222325;
        private const ulong FnvPrime = 0x100000001b3;

        public static string HashString(string content)
        {
            ulong hash = FnvOffsetBasis;
            byte[] bytes = Encoding.UTF8.GetBytes(content);

            foreach (byte input in bytes)
            {
                hash ^= input;
                hash *= FnvPrime;
            }

            return hash.ToString();
        }
    }
}