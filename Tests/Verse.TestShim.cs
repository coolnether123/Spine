namespace Verse
{
    internal static class Prefs
    {
        internal static bool DevMode => false;
    }

    internal static class LanguageDatabase
    {
        internal static object activeLanguage => null;
    }

    internal static class TranslationExtensions
    {
        internal static bool CanTranslate(this string key)
        {
            return false;
        }
    }

    internal static class Log
    {
        internal static void Warning(string message)
        {
            throw new System.NotSupportedException(
                "Verse logging is outside this test executable.");
        }
    }
}
