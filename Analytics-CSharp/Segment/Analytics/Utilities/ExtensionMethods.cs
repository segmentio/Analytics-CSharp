namespace Segment.Analytics.Utilities
{
    using global::System.Text;

    public static partial class ExtensionMethods
    {
        public static byte[] GetBytes(this string str) => Encoding.UTF8.GetBytes(str);
    }
}
