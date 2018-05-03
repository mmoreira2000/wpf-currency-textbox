namespace CurrencyTextBoxControl
{
    internal static class ExtensionMethods
    {
        internal static string Right(this string str, int length)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Substring(str.Length - length);
        }
        internal static string Left(this string str, int length)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Substring(0, length);
        }
    }
}
