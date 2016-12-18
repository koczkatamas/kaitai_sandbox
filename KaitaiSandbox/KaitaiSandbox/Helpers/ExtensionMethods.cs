using System;

namespace KaitaiSandbox
{
    public static partial class ExtensionMethods
    {
        public static string[] Split(this string str, string separator) => str.Split(new[] { separator }, StringSplitOptions.None);
    }
}