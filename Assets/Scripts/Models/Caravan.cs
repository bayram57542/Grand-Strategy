using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Faza 8.2 - Qlobal Xəritədə Əyalətlər arası hərəkət edən Karvan (Caravan) modeli.
    /// Ticarət müqaviləsi olan dövlətlər və ya fərqli resursları olan əyalətlər arasında resurs daşıyır.
    /// Düşmən orduları tərəfindən yağmalana (Raiding) bilər.
    /// </summary>
    [Table("Caravans")]
    public class Caravan
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Karvanın sahibi olan dövlətin ID-si.
        /// </summary>
        [Indexed]
        public int OwnerFactionId { get; set; }

        /// <summary>
        /// Karvanın yola çıxdığı mənbə əyalətin ID-si.
        /// </summary>
        [Indexed]
        public int SourceProvinceId { get; set; }

        /// <summary>
        /// Karvanın çatacağı hədəf əyalətin ID-si.
        /// </summary>
        [Indexed]
        public int TargetProvinceId { get; set; }

        /// <summary>
        /// Daşınan resursun növü: "Iron", "Silk", "Horses", "Gold", "Food".
        /// </summary>
        public string ResourceType { get; set; } = "Silk";

        /// <summary>
        /// Daşınan resursun miqdarı (məsələn: 50 İpək).
        /// </summary>
        public float ResourceAmount { get; set; } = 50f;

        /// <summary>
        /// Karvanın yoldakı irəliləyişi (0.0f - 100.0f arası).
        /// 100-ə çatdıqda hədəf dövlətin xəzinəsinə təhvil verilir.
        /// </summary>
        public float TravelProgress { get; set; } = 0f;

        /// <summary>
        /// Karvanın düşmən tərəfindən yağmalanıb-yağmalanmadığı.
        /// </summary>
        public bool IsRaided { get; set; } = false;

        /// <summary>
        /// Karvanın hədəfə çatıb tamamlandığı.
        /// </summary>
        public bool IsCompleted { get; set; } = false;

        [Ignore]
        public bool IsActive => !IsRaided && !IsCompleted && TravelProgress < 100f;

        public override string ToString()
        {
            return $"[Caravan #{Id}] Sahibi: Fraksiya #{OwnerFactionId} | Mənbə: #{SourceProvinceId} -> Hədəf: #{TargetProvinceId} | " +
                   $"Resurs: {ResourceAmount:F0} {ResourceType} | İrəliləyiş: {TravelProgress:F1}% | Yağmalanıb: {IsRaided} | Tamamlanıb: {IsCompleted}";
        }
    }
}
