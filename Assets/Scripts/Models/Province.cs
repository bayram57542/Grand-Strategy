using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Əyalətlər (Provinces) cədvəli üçün SQLite ORM modeli.
    /// </summary>
    [Table("Provinces")]
    public class Province
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Əyalətin adı (məs: "Anadolu", "Şirvan").
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Əyalətin iqlim tipi (məs: "Mülayim", "Quraq", "Dağlıq").
        /// </summary>
        public string Climate { get; set; }

        /// <summary>
        /// Əyalətin aid olduğu dövlətin (Fraksiyanın) ID-si.
        /// </summary>
        [Indexed]
        public int OwnerFactionId { get; set; }

        /// <summary>
        /// Əyalətin inkişaf səviyyəsi.
        /// </summary>
        public int DevelopmentLevel { get; set; }

        public override string ToString()
        {
            return $"[Province #{Id}] Ad: {Name} | İqlim: {Climate} | Sahib Dövlət ID: {OwnerFactionId} | İnkişaf: {DevelopmentLevel}";
        }
    }
}
