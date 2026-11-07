using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Diplomatik Münasibətlər (Diplomacy) cədvəli üçün SQLite ORM modeli.
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

        /// <summary>
        /// Münasibət balı (-100 ilə +100 arası).
        /// </summary>
        public int RelationshipScore { get; set; }

        /// <summary>
        /// Müqavilə növü: "Sülh", "Müharibə", "Vassal", "Ticarət".
        /// </summary>
        public string TreatyType { get; set; }

        /// <summary>
        /// Müqavilənin neçə 'Tick' (Ay/İl) davam edəcəyi.
        /// </summary>
        public int TreatyDuration { get; set; }

        public override string ToString()
        {
            return $"[Diplomacy #{Id}] Dövlət {FactionA_Id} <-> Dövlət {FactionB_Id} | Münasibət: {RelationshipScore} | Müqavilə: {TreatyType} ({TreatyDuration} Tick)";
        }
    }
}
