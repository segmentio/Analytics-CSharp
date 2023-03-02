using global::System.Text;

namespace Segment.Analytics.Utilities
{
    public static partial class ExtensionMethods
    {
        public static byte[] GetBytes(this string str) => Encoding.UTF8.GetBytes(str);
    }
}
