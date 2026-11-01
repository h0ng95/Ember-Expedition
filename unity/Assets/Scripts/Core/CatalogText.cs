using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Expedition.Core
{
    // Locale affects presentation only. No save, RNG, unit ID or balance data is touched.
    public static partial class CatalogText
    {
        sealed class Entry { public string Korean, English; }
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> sourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> englishKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        public static string Language { get; private set; }
        public static int Revision { get; private set; }
        public static bool English { get { return Language == "en"; } }
        public static int Count { get { return entries.Count; } }
        static CatalogText() { Language = "ko"; Populate(); }
        static void Add(string key, string ko, string en)
        {
            entries.Add(key, new Entry { Korean = ko, English = en });
            if (!sourceKeys.ContainsKey(ko)) sourceKeys.Add(ko, key);
            if (!englishKeys.ContainsKey(en)) englishKeys.Add(en, key);
        }
        public static string ResolveLanguage(string choice, string systemLanguage)
        {
            if (choice == "ko" || choice == "en") return choice;
            return !string.IsNullOrEmpty(systemLanguage) &&
                (systemLanguage.Equals("Korean", StringComparison.OrdinalIgnoreCase) ||
                 systemLanguage.Equals("ko", StringComparison.OrdinalIgnoreCase) ||
                 systemLanguage.StartsWith("ko-", StringComparison.OrdinalIgnoreCase)) ? "ko" : "en";
        }
        public static void SetLanguage(string language)
        {
            if (language != "ko" && language != "en") throw new ArgumentException("Unsupported locale.");
            if (Language == language) return;
            Language = language; Revision++;
        }
        public static string Get(string key)
        {
            Entry entry;
            if (!entries.TryGetValue(key, out entry)) throw new ArgumentException("Missing text key: " + key);
            return English ? entry.English : entry.Korean;
        }
        public static string Format(string key, params object[] args)
        { return string.Format(CultureInfo.InvariantCulture, Get(key), args); }
        public static string KeyFor(string value)
        {
            if (value == null || entries.ContainsKey(value)) return value;
            string key;
            return sourceKeys.TryGetValue(value, out key) || englishKeys.TryGetValue(value, out key) ? key : value;
        }
        // Catalog content and legacy error messages retain their original source values.
        // Exact lookup only; never translates pieces of IDs or modifies arbitrary strings.
        public static string Resolve(string value)
        {
            if (value == null) return null;
            if (entries.ContainsKey(value)) return Get(value);
            string key;
            return sourceKeys.TryGetValue(value, out key) ? Get(key) : value;
        }
    }

    public sealed class CatalogLocalizedStrings : IReadOnlyList<string>
    {
        readonly string[] keys;
        public CatalogLocalizedStrings(params string[] values) { keys = values; }
        public int Count { get { return keys.Length; } }
        public int Length { get { return keys.Length; } }
        public string this[int index] { get { return CatalogText.Get(keys[index]); } }
        public IEnumerator<string> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
