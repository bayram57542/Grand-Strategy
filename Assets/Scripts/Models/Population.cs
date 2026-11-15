using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Əhali (Population) demoqrafik təbəqələri cədvəli üçün SQLite ORM modeli.
    /// </summary>
    [Table("Populations")]
    public class Population
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Aid olduğu əyalətin ID-si (Province.Id ilə əlaqəli Foreign Key).
        /// </summary>
        [Indexed]
        public int ProvinceId { get; set; }

        /// <summary>
        /// Körpələrin sayı.
        /// </summary>
        public int Infants { get; set; }

        /// <summary>
        /// Körpələr (Babies) sinfi üçün Faza 5 uyğunluq xassəsi.
        /// </summary>
        [Ignore]
        public int Babies
        {
            get => Infants;
            set => Infants = value;
        }

        /// <summary>
        /// Gənclərin sayı (elit ordu potensialı).
        /// </summary>
        public int Youths { get; set; }

        /// <summary>
        /// Yetişkinlərin sayı (istehsal və əsas ordu).
        /// </summary>
        public int Adults { get; set; }

        /// <summary>
        /// Yaşlıların sayı.
        /// </summary>
        public int Elders { get; set; }

        /// <summary>
        /// Yaralıların sayı (istehlak edən və sağalan sinif).
        /// </summary>
        public int Wounded { get; set; }

        /// <summary>
        /// Əyalətdəki ümumi əhali sayı (hesablanan xassə, bazada ayrıca sütun tutmur).
        /// </summary>
        [Ignore]
        public int TotalPopulation => Infants + Youths + Adults + Elders + Wounded;

        public override string ToString()
        {
            return $"[Population #{Id} | ProvinceId: {ProvinceId}] Ümumi: {TotalPopulation} (Körpə: {Infants}, Gənc: {Youths}, Yetişkin: {Adults}, Yaşlı: {Elders}, Yaralı: {Wounded})";
        }
    }
}
