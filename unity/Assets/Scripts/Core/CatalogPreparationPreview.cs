using System;

namespace Expedition.Core
{
    // Preparation shows eligible positions, not the recipients a future cast will choose.
    // These radii are also used by the combat skills so the overlay cannot drift from combat.
    public sealed class CatalogSupportPreview
    {
        public readonly float Radius;
        public readonly string Rule;
        public bool Global { get { return float.IsPositiveInfinity(Radius); } }

        internal CatalogSupportPreview(float radius, string rule)
        { Radius = radius; Rule = rule; }

        public bool Reaches(float sourceX, float sourceY, float targetX, float targetY)
        {
            float dx = sourceX - targetX, dy = sourceY - targetY;
            return Global || dx * dx + dy * dy <= Radius * Radius;
        }
    }

    public static class CatalogPreparationPreview
    {
        static readonly CatalogSupportPreview Priest = new CatalogSupportPreview(4.5f,
            CatalogText.Get("text.0029"));
        static readonly CatalogSupportPreview Banner = new CatalogSupportPreview(2.5f,
            CatalogText.Get("text.0030"));
        static readonly CatalogSupportPreview Guardian = new CatalogSupportPreview(2f,
            CatalogText.Get("text.0031"));
        static readonly CatalogSupportPreview ShieldPriest = new CatalogSupportPreview(4.8f,
            CatalogText.Get("text.0032"));
        static readonly CatalogSupportPreview Healer = new CatalogSupportPreview(4.8f,
            CatalogText.Get("text.0033"));
        static readonly CatalogSupportPreview GuardBanner = new CatalogSupportPreview(2.8f,
            CatalogText.Get("text.0034"));
        static readonly CatalogSupportPreview AssaultBanner = new CatalogSupportPreview(2.8f,
            CatalogText.Get("text.0035"));
        static readonly CatalogSupportPreview FinalKnight = new CatalogSupportPreview(2.5f,
            CatalogText.Get("text.0036"));
        static readonly CatalogSupportPreview FinalPriest = new CatalogSupportPreview(float.PositiveInfinity,
            CatalogText.Get("text.0037"));
        static readonly CatalogSupportPreview FinalBanner = new CatalogSupportPreview(float.PositiveInfinity,
            CatalogText.Get("text.0038"));
        static readonly CatalogSupportPreview Warmth = new CatalogSupportPreview(2.5f,
            CatalogText.Get("text.0039"));
        static readonly CatalogSupportPreview Song = new CatalogSupportPreview(4.5f,
            CatalogText.Get("text.0040"));
        static readonly CatalogSupportPreview Spoils = new CatalogSupportPreview(3f,
            CatalogText.Get("text.0041"));

        public static CatalogSupportPreview ForSkill(string id)
        {
            switch (id)
            {
                case "B-PR": return Priest;
                case "B-BA": return Banner;
                case "E-KN-G": return Guardian;
                case "E-PR-G": return ShieldPriest;
                case "E-PR-H": return Healer;
                case "E-BA-G": return GuardBanner;
                case "E-BA-A": return AssaultBanner;
                case "F-KN": return FinalKnight;
                case "F-PR": return FinalPriest;
                case "F-BA": return FinalBanner;
                case "H-02": return Warmth;
                case "H-05": return Song;
                case "H-06": return Spoils;
                default: return null;
            }
        }

        public static float SupportRadius(string skill)
        {
            CatalogSupportPreview preview = ForSkill(skill);
            if (preview == null) throw new ArgumentException("Not an allied support skill: " + skill);
            return preview.Radius;
        }

        public static string PrimarySkill(CatalogMember member)
        {
            return member.FormId.StartsWith("F-", StringComparison.Ordinal) ? member.BranchId : member.FormId;
        }
    }
}
