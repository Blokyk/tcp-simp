public static class Utils
{
    public static string PadCenter(this string source, int length) {
        int spaces = length - source.Length;
        int padLeft = spaces / 2 + source.Length;
        return source.PadLeft(padLeft).PadRight(length);
    }
}