using System;
using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Dövlətin (AI) əsas diplomatik və strateji marağı.
    /// </summary>
    public enum FactionMotive
    {
        Expansion,
        Trade,
        Consolidation
    }

    /// <summary>
    /// İki dövlət arasında bağlana bilən diplomatik müqavilə növləri.
    /// </summary>
    public enum AgreementType
    {
        Defensive,
        Military,
        Economic,
        Trade,
        NonAggression,
        MilitaryAccess,
        Vassalage,
        Tribute
    }

    /// <summary>
    /// Diplomatiya enum-ları və SQLite mətn təmsilçiləri arasında çevirmə köməkçiləri.
    /// </summary>
    public static class DiplomacyModelExtensions
    {
        public static string ToLabel(this FactionMotive motive)
        {
            switch (motive)
            {
                case FactionMotive.Expansion: return "Expansion";
                case FactionMotive.Trade: return "Trade";
                default: return "Consolidation";
            }
        }

        public static string ToAzerbaijaniDisplay(this FactionMotive motive)
        {
            switch (motive)
            {
                case FactionMotive.Expansion: return "Genişlənmə (Expansion)";
                case FactionMotive.Trade: return "Ticarət (Trade)";
                default: return "Güclənmə (Consolidation)";
            }
        }

        public static FactionMotive ParseMotive(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return FactionMotive.Consolidation;
            }

            if (raw.IndexOf("Expansion", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FactionMotive.Expansion;
            }

            if (raw.IndexOf("Trade", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FactionMotive.Trade;
            }

            return FactionMotive.Consolidation;
        }

        public static string ToDbType(this AgreementType type)
        {
            switch (type)
            {
                case AgreementType.Defensive: return "Müdafiə";
                case AgreementType.Military: return "Hərbi";
                case AgreementType.Economic: return "İqtisadi";
                case AgreementType.Trade: return "Ticarət";
                case AgreementType.NonAggression: return "Hücum Etməmək Paktı";
                case AgreementType.MilitaryAccess: return "Hərbi Keçid";
                case AgreementType.Vassalage: return "Vassallıq";
                case AgreementType.Tribute: return "Xərac";
                default: return "Ticarət";
            }
        }

        public static AgreementType ParseAgreementType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return AgreementType.Trade;
            }

            string normalized = raw.Trim();

            if (normalized.IndexOf("Müdafiə", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Defensive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.Defensive;
            }

            if (normalized.IndexOf("Keçid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("MilitaryAccess", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.MilitaryAccess;
            }

            if (normalized.IndexOf("Hərbi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("İttifaq", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Military", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.Military;
            }

            if (normalized.IndexOf("İqtisadi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Economic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.Economic;
            }

            if (normalized.IndexOf("Hücum Etməmək", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Pakt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("NonAggression", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.NonAggression;
            }

            if (normalized.IndexOf("Vassal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.Vassalage;
            }

            if (normalized.IndexOf("Xərac", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Tribute", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return AgreementType.Tribute;
            }

            return AgreementType.Trade;
        }
    }

    /// <summary>
    /// Factions SQLite cədvəlinə uyğun dövlət modeli.
    /// Xəzinə, diplomatik nüfuz və strateji maraqları (PrimaryMotive) saxlayır.
    /// </summary>
    [Table("Factions")]
    public class Faction
    {
        [PrimaryKey]
        public int ID { get; set; }

        [NotNull, Unique]
        public string Name { get; set; } = string.Empty;

        public string RulerName { get; set; } = string.Empty;

        public string GovernmentType { get; set; } = "Monarxiya";

        /// <summary>
        /// Dövlətin qızıl ehtiyatı (xəzinə).
        /// </summary>
        public float Treasury { get; set; } = 1000f;

        /// <summary>
        /// 0 - 100 arası diplomatik nüfuz göstəricisi.
        /// </summary>
        public int DiplomaticReputation { get; set; } = 50;

        /// <summary>
        /// Dövlətin siyasi marağı: "Expansion", "Trade", "Consolidation".
        /// </summary>
        public string PrimaryMotive { get; set; } = "Consolidation";

        public int MilitaryPower { get; set; } = 10000;

        public string ColorHex { get; set; } = "#FFFFFF";

        [Ignore]
        public FactionMotive MotiveEnum
        {
            get => DiplomacyModelExtensions.ParseMotive(PrimaryMotive);
            set => PrimaryMotive = value.ToLabel();
        }
    }

    /// <summary>
    /// FactionRelations SQLite cədvəli: iki dövlət arasındakı münasibət xalı və statusu.
    /// </summary>
    [Table("FactionRelations")]
    public class FactionRelation
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        [Indexed]
        public int Faction1_ID { get; set; }

        [Indexed]
        public int Faction2_ID { get; set; }

        /// <summary>
        /// -100 ilə +100 arası münasibət xalı.
        /// </summary>
        public int RelationScore { get; set; } = 0;

        /// <summary>
        /// Düşmənçilik, Gərgin, Neytral, Dostluq, Yaxın Müttəfiq.
        /// </summary>
        public string Status { get; set; } = "Neytral";

        public static void NormalizePair(int factionA, int factionB, out int minId, out int maxId)
        {
            if (factionA <= factionB)
            {
                minId = factionA;
                maxId = factionB;
            }
            else
            {
                minId = factionB;
                maxId = factionA;
            }
        }

        public static string GetStatusForScore(int score)
        {
            if (score <= -50) return "Düşmənçilik";
            if (score <= -15) return "Gərgin";
            if (score <= 25) return "Neytral";
            if (score <= 65) return "Dostluq";
            return "Yaxın Müttəfiq";
        }
    }

    /// <summary>
    /// DiplomaticAgreements SQLite cədvəli: qüvvədə olan ikitərəfli müqavilələr.
    /// </summary>
    [Table("DiplomaticAgreements")]
    public class DiplomaticAgreement
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        /// <summary>
        /// Müdafiə, Hərbi, İqtisadi, Ticarət, Hücum Etməmək Paktı, Hərbi Keçid, Vassallıq, Xərac.
        /// </summary>
        [NotNull]
        public string Type { get; set; } = "Ticarət";

        [Indexed]
        public int SourceFaction_ID { get; set; }

        [Indexed]
        public int TargetFaction_ID { get; set; }

        public int DurationMonths { get; set; } = 24;

        [Ignore]
        public AgreementType AgreementKind
        {
            get => DiplomacyModelExtensions.ParseAgreementType(Type);
            set => Type = value.ToDbType();
        }
    }
}
