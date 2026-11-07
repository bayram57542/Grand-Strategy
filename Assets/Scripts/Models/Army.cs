using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Ordu (Army) birlikləri cədvəli üçün SQLite ORM modeli.
    /// </summary>
    [Table("Armies")]
    public class Army
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Ordunun hansı dövlətə (Fraksiyaya) məxsus olduğu.
        /// </summary>
        [Indexed]
        public int OwnerFactionId { get; set; }

        /// <summary>
        /// Ordunun xəritədə hazırda hansı əyalətdə dayandığı (Province.Id).
        /// </summary>
        [Indexed]
        public int LocationProvinceId { get; set; }

        /// <summary>
        /// Ordudakı Elit Süvarilərin (Gənclərin - Youths) sayı.
        /// </summary>
        public int YouthSoldiers { get; set; }

        /// <summary>
        /// Ordudakı Ağır Piyadaların (Yetişkinlərin - Adults) sayı.
        /// </summary>
        public int AdultSoldiers { get; set; }

        /// <summary>
        /// Ordunun mənəviyyatı (normalda 1.0, aclıq olan əyalətdə 0.7).
        /// </summary>
        public float Morale { get; set; } = 1.0f;

        /// <summary>
        /// Ordudakı ümumi əsgər sayı (bazada ayrıca sütun tutmur).
        /// </summary>
        [Ignore]
        public int TotalSoldiers => YouthSoldiers + AdultSoldiers;

        public override string ToString()
        {
            return $"[Army #{Id} | Dövlət: {OwnerFactionId} | Əyalət ID: {LocationProvinceId}] " +
                   $"Ümumi Əsgər: {TotalSoldiers} (Elit Süvari/Gənc: {YouthSoldiers}, Ağır Piyada/Yetişkin: {AdultSoldiers}) | Mənəviyyat (Morale): {Morale:F2}";
        }
    }
}
