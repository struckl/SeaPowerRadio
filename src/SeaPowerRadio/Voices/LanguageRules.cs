using System;
using System.Collections.Generic;

namespace SeaPowerRadio.Voices;

/// <summary>
/// Number and callsign rules live in the base, keyed by a pack's <c>Language</c>. Only <c>en</c> exists today (the
/// current code). The first pack of a new language may need one base update for its rules; an unknown language
/// falls back to the English rules with a warning.
/// </summary>
internal static class LanguageRules
{
    public const string Fallback = "en";
    private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "en" };
    private static readonly HashSet<string> Warned = new HashSet<string>();

    /// <summary>The rule set to use for a language code (always a known one).</summary>
    public static string For(string language)
    {
        if (language != null && Known.Contains(language))
            return language.ToLowerInvariant();
        if (Warned.Add(language ?? ""))
            ModLog.Warn($"No number/callsign rules for language '{language}' in this version: using the English rules.");
        return Fallback;
    }
}
