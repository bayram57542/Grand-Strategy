using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Əyalətlər (Provinces) cədvəli üçün SQLite ORM modeli (Faza 1-6A).
    /// İqlim/inkişaf göstəriciləri, Gold/Food ehtiyatları, demoqrafik siniflər
    /// (Babies, Youths, Adults, Elders, Wounded) və Faza 6A İnfrastruktur sahələrini
    /// (ProductivityMultiplier, ActiveConstruction, ConstructionMonthsLeft) saxlayır.
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
        /// Əyalətin qızıl (xəzinə) ehtiyatı.
        /// </summary>
        public float Gold { get; set; }

        /// <summary>
        /// Əyalətin ərzaq anbarı ehtiyatı.
        /// </summary>
        public float Food { get; set; }

        /// <summary>
        /// Körpələr (Babies) sinfi.
        /// </summary>
        public int Babies { get; set; }

        /// <summary>
        /// Gənclər (Youths) sinfi.
        /// </summary>
        public int Youths { get; set; }

        /// <summary>
        /// Yetişkinlər (Adults) sinfi.
        /// </summary>
        public int Adults { get; set; }

        /// <summary>
        /// Yaşlılar (Elders) sinfi.
        /// </summary>
        public int Elders { get; set; }

        /// <summary>
        /// Yaralılar (Wounded) sinfi.
        /// </summary>
        public int Wounded { get; set; }

        /// <summary>
        /// Əyalətin məhsuldarlıq çarpanı (Başlanğıc dəyər: 1.0f = 100%).
        /// </summary>
        public float ProductivityMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Hazırda tikilən layihənin adı (boşdursa null/empty).
        /// </summary>
        public string ActiveConstruction { get; set; } = string.Empty;

        /// <summary>
        /// Tikintinin bitməsinə qalan Ay/Tick sayı.
        /// </summary>
        public int ConstructionMonthsLeft { get; set; }

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
        /// Xalqın Moralı (PublicOrder): 0-100 intervalında (Default: 100).
        /// Əgər 30-dan aşağı düşərsə, məhsuldarlıq yarıya (0.5x) düşür (itaətsizlik).
        /// </summary>
        public float PublicOrder { get; set; } = 100f;

        /// <summary>
        /// Mühasirə vəziyyəti (Default: false).
        /// Mühasirədə olan əyalətin resursları qlobal (Nation) xəzinəyə axmır.
        /// </summary>
        public bool IsBesieged { get; set; } = false;

        /// <summary>
        /// Doğum dərəcəsinə təsir edən çarpan (Default: 1.0f = 100%).
        /// Fövqəladə vəziyyətdə qadınlar orduya çağırıldıqda 0.2f-ə (20%) düşür.
        /// </summary>
        public float BirthRateModifier { get; set; } = 1.0f;

        /// <summary>
        /// Mühasirə istismar taktikası (Default: false).
        /// Hər ay ərzaq istehsalı mühasirə edən orduya/xəzinəyə yönləndirilir, PublicOrder -= 5 düşür.
        /// </summary>
        public bool IsExploited { get; set; } = false;

        /// <summary>
        /// Əyalətin inkişaf səviyyəsi.
        /// </summary>
        public int DevelopmentLevel { get; set; }

        [Ignore]
        public int TotalPopulation => Babies + Youths + Adults + Elders + Wounded;

        [Ignore]
        public bool HasActiveConstruction => !string.IsNullOrWhiteSpace(ActiveConstruction) && ConstructionMonthsLeft > 0;

        /// <summary>
        /// Xalqın moralı 30-dan aşağı düşdükdə faktiki məhsuldarlıq yarıbayarı (0.5x) azalır.
        /// </summary>
        [Ignore]
        public float EffectiveProductivityMultiplier => PublicOrder < 30f ? (ProductivityMultiplier * 0.5f) : ProductivityMultiplier;

        public override string ToString()
        {
            string constructionInfo = HasActiveConstruction
                ? $"{ActiveConstruction} ({ConstructionMonthsLeft} ay qaldı)"
                : "Yoxdur";

            string siegeInfo = IsBesieged
                ? (IsExploited ? "Mühasirədə (İstismar Edilir)" : "Mühasirədə")
                : "Sərbəst";

            return $"[Province #{Id}] Ad: {Name} | Gold: {Gold:F1} | Food: {Food:F1} | " +
                   $"Moral (PublicOrder): {PublicOrder:F0}/100 | Vəziyyət: {siegeInfo} | " +
                   $"Doğum Çarpanı: {BirthRateModifier * 100f:F0}% | " +
                   $"Məhsuldarlıq: {ProductivityMultiplier * 100f:F0}% (Faktiki: {EffectiveProductivityMultiplier * 100f:F0}%) | " +
                   $"Tikinti: {constructionInfo} | Əhali: {TotalPopulation} " +
                   $"(Babies: {Babies}, Youths: {Youths}, Adults: {Adults}, Elders: {Elders}, Wounded: {Wounded}) | " +
                   $"İqlim: {Climate} | Sahib Dövlət ID: {OwnerFactionId} | İnkişaf: {DevelopmentLevel}";
        }
    }
}
