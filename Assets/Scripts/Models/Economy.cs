using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Bazar və Resurslar (Economy) cədvəli üçün SQLite ORM modeli.
    /// </summary>
    [Table("Economies")]
    public class Economy
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Aid olduğu əyalətin ID-si (Province.Id ilə əlaqəli Foreign Key).
        /// </summary>
        [Indexed]
        public int ProvinceId { get; set; }

        /// <summary>
        /// İstehsal edilən ərzaq (Taxıl, Ət).
        /// </summary>
        public float FoodProduction { get; set; }

        /// <summary>
        /// İstehsal edilən xammal (Dəmir, Ağac).
        /// </summary>
        public float RawMaterials { get; set; }

        /// <summary>
        /// Lüks mallar (İpək, Ədviyyat).
        /// </summary>
        public float LuxuryGoods { get; set; }

        /// <summary>
        /// Əyalətdən gələn vergi gəliri.
        /// </summary>
        public float LocalTaxIncome { get; set; }

        public override string ToString()
        {
            return $"[Economy #{Id} | ProvinceId: {ProvinceId}] Ərzaq: {FoodProduction:F1} | Xammal: {RawMaterials:F1} | Lüks: {LuxuryGoods:F1} | Vergi: {LocalTaxIncome:F1}";
        }
    }
}
