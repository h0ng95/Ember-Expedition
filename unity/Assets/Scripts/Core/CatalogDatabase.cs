using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Expedition.Core
{
    public sealed class CatalogForm
    {
        public readonly string Id, Name, Tier, Family, InnateTrait, Description;
        public readonly float Hp, Power, Defense, AttackInterval, Range, MoveSpeed;

        internal CatalogForm(Dictionary<string, string> fields)
        {
            Id = Text(fields, "id"); Name = Text(fields, "name"); Tier = Text(fields, "tier");
            Family = Text(fields, "family"); InnateTrait = Text(fields, "innateTrait");
            string description;
            Description = fields.TryGetValue("description", out description) ? description : null;
            Hp = Number(fields, "hp"); Power = Number(fields, "power");
            Defense = Number(fields, "defense"); AttackInterval = Number(fields, "attackInterval");
            Range = Number(fields, "range"); MoveSpeed = Number(fields, "moveSpeed");
            if (Hp <= 0 || Power <= 0 || Defense < 0 || AttackInterval <= .25f || Range <= 0 || MoveSpeed <= 0)
                throw new ArgumentException("Invalid combat stats for " + Id);
        }

        static string Text(Dictionary<string, string> fields, string name)
        {
            string value;
            if (!fields.TryGetValue(name, out value) || string.IsNullOrEmpty(value))
                throw new ArgumentException("Catalog field missing: " + name);
            return value;
        }
        static float Number(Dictionary<string, string> fields, string name)
        {
            return float.Parse(Text(fields, name), CultureInfo.InvariantCulture);
        }
    }

    // Parses only the flat unit records in the shipped content file. Unity and headless tests use the same text.
    public sealed class CatalogDatabase
    {
        readonly List<CatalogForm> all = new List<CatalogForm>();
        readonly Dictionary<string, CatalogForm> byId = new Dictionary<string, CatalogForm>(StringComparer.Ordinal);
        public IReadOnlyList<CatalogForm> All { get { return all.AsReadOnly(); } }

        public CatalogForm Get(string id)
        {
            CatalogForm form;
            if (!byId.TryGetValue(id, out form)) throw new ArgumentException("Unknown form: " + id);
            return form;
        }

        public static CatalogDatabase FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new ArgumentException("Catalog JSON is empty.");
            var result = new CatalogDatabase();
            Match array = Regex.Match(json, "\\\"units\\\"\\s*:\\s*\\[(?<rows>[\\s\\S]*?)\\]", RegexOptions.CultureInvariant);
            if (!array.Success) throw new ArgumentException("Catalog has no units array.");
            foreach (Match row in Regex.Matches(array.Groups["rows"].Value, "\\{[^{}]*\\}"))
            {
                var fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match pair in Regex.Matches(row.Value,
                    "\\\"(?<key>[^\\\"]+)\\\"\\s*:\\s*(?:\\\"(?<text>[^\\\"]*)\\\"|(?<number>-?[0-9]+(?:\\.[0-9]+)?))"))
                    fields.Add(pair.Groups["key"].Value, pair.Groups["text"].Success ?
                        pair.Groups["text"].Value : pair.Groups["number"].Value);
                var form = new CatalogForm(fields);
                if (result.byId.ContainsKey(form.Id)) throw new ArgumentException("Duplicate form: " + form.Id);
                result.byId.Add(form.Id, form);
                result.all.Add(form);
            }
            if (result.all.Count != 30) throw new ArgumentException("Expected all 30 playable forms, found " + result.all.Count);
            foreach (string family in new[] { "KN", "AR", "FI", "PR", "IC", "BA" })
            {
                result.Get("B-" + family);
                result.Get("F-" + family);
            }
            for (int hybrid = 1; hybrid <= 6; hybrid++) result.Get("H-" + hybrid.ToString("00"));
            return result;
        }
    }
}
