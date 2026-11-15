using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Diplomatik Münasibətlər (Diplomacy) cədvəli üçün SQLite ORM modeli.
    /// Faza 7.1: Zamana bağlı atəşkəs (CeasefireMonthsLeft) sistemi.
    /// Faza 7.2: RelationScore (-100 ilə +100 arası), Hücum Etməmək Paktı və Hərbi İttifaqlar.
    /// </summary>
    [Table("Diplomacies")]
    public class Diplomacy
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Birinci dövlətin (Fraksiyanın) ID-si.
        /// </summary>
        [Indexed]
        public int FactionA_Id { get; set; }

        /// <summary>
        /// İkinci dövlətin (Fraksiyanın) ID-si.
        /// </summary>
        [Indexed]
        public int FactionB_Id { get; set; }

        private int _relationScore = 0;

        /// <summary>
        /// Faza 7.2 - İki fraksiya arasındakı münasibət balı (-100 ilə +100 arası).
        /// -100-dən -60: Düşmən (Enemy)
        /// -19-dan +19: Neytral (Neutral)
        /// +60-dan +100: Müttəfiq (Ally)
        /// </summary>
        public int RelationScore
        {
            get => _relationScore;
            set
            {
                _relationScore = UnityEngine.Mathf.Clamp(value, -100, 100);
                _relationshipScore = _relationScore;
            }
        }

        private int _relationshipScore = 0;

        /// <summary>
        /// Geri-uyğunluq üçün mövcud sütun (RelationScore ilə sinxron saxlanılır).
        /// </summary>
        public int RelationshipScore
        {
            get => _relationScore;
            set
            {
                _relationScore = UnityEngine.Mathf.Clamp(value, -100, 100);
                _relationshipScore = _relationScore;
            }
        }

        /// <summary>
        /// Müqavilə növü: "Sülh", "Müharibə", "Vassal", "Ticarət", "Atəşkəs", "Neytral",
        /// "Hücum Etməmək Paktı", "Hərbi İttifaq", "Hərbi İttifaq (Koalisiya)".
        /// </summary>
        public string TreatyType { get; set; }

        /// <summary>
        /// Müqavilənin neçə 'Tick' (Ay/İl) davam edəcəyi.
        /// </summary>
        public int TreatyDuration { get; set; }

        /// <summary>
        /// Faza 7.1 - Zamana bağlı atəşkəs üçün qalan aylar (0 olduqda atəşkəs bitir).
        /// </summary>
        public int CeasefireMonthsLeft { get; set; } = 0;

        /// <summary>
        /// Faza 7.2 - Hücum Etməmək Paktı üçün qalan aylar (0 olduqda pakt bitir).
        /// </summary>
        public int NonAggressionMonthsLeft { get; set; } = 0;

        /// <summary>
        /// Münasibət balının diplomatik statusunu (Düşmən, Gərgin, Neytral, Dost, Müttəfiq) qaytarır.
        /// </summary>
        public static string GetRelationTier(int score)
        {
            if (score <= -60) return "Düşmən";
            if (score >= 60) return "Müttəfiq";
            if (score >= -19 && score <= 19) return "Neytral";
            if (score < -19) return "Gərgin";
            return "Dost";
        }

        [Ignore]
        public string RelationTier => GetRelationTier(RelationScore);

        [Ignore]
        public bool IsAlliance => !string.IsNullOrEmpty(TreatyType) && TreatyType.IndexOf("Hərbi İttifaq", System.StringComparison.OrdinalIgnoreCase) >= 0;

        [Ignore]
        public bool IsNonAggressionPactActive => NonAggressionMonthsLeft > 0 || (!string.IsNullOrEmpty(TreatyType) && TreatyType.IndexOf("Hücum Etməmək", System.StringComparison.OrdinalIgnoreCase) >= 0 && TreatyDuration > 0);

        [Ignore]
        public bool IsCeasefireActive => CeasefireMonthsLeft > 0 || (!string.IsNullOrEmpty(TreatyType) && TreatyType.IndexOf("Atəşkəs", System.StringComparison.OrdinalIgnoreCase) >= 0 && TreatyDuration > 0);

        public override string ToString()
        {
            string ceasefireStr = CeasefireMonthsLeft > 0 ? $" | Atəşkəs: {CeasefireMonthsLeft} ay" : "";
            string napStr = NonAggressionMonthsLeft > 0 ? $" | Hücum Etməmək Paktı: {NonAggressionMonthsLeft} ay" : "";
            return $"[Diplomacy #{Id}] Fraksiya {FactionA_Id} <-> Fraksiya {FactionB_Id} | Münasibət: {RelationScore} ({RelationTier}) | Müqavilə: {TreatyType} ({TreatyDuration} Tick){ceasefireStr}{napStr}";
        }
    }
}
