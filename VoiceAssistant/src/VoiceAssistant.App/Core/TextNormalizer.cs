using System.Text.RegularExpressions;

namespace VoiceAssistant.App.Core
{
    public class TextNormalizer
    {
        public string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            // 1. Lowercase
            var text = input.ToLowerInvariant();

            // 2. Cyrillic replacements
            text = text.Replace('ё', 'е');

            // 3. Remove punctuation (keep digits, letters, and spaces)
            // Regex: Replace any character that is NOT a word char or whitespace with empty string.
            // \w includes digits and letters (including cyrillic if supported by regex engine, but safer to specify ranges if unsure, 
            // though .NET \w handles Unicode categories usually).
            // Requirement: "без пунктуации".
            // Let's use specific removal of common punctuation to be safe or keep only specific chars.
            // Keeping it simple: remove anything that is Punctuation or Symbol.
            
            // Better approach for "phrase matching": keep only letters and digits.
            // Using \p{P} for punctuation and \p{S} for symbols.
            text = Regex.Replace(text, @"[\p{P}\p{S}]", "");

            // 4. Collapse spaces
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
    }
}
