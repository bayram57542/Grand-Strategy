using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Dövlət (Nation) cədvəli üçün SQLite ORM modeli (Faza 5).
    /// Qızıl (Gold), Ərzaq (Food) ehtiyatlarını və ümumi demoqrafik təbəqələri
    /// (Babies, Youths, Adults, Elders, Wounded) bazada saxlayır.
    /// </summary>
    [Table("Nations")]
    public class Nation
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Faza 6D - Dövlətin hakim sülalə/rəsmi adı (məsələn: "Səlcuqlu Sultanlığı", "Əfşarlar İmperiyası").
        /// Taxt çevrilişi baş verdikdə yeni sülalənin adı ilə əvəzlənir.
        /// </summary>
        public string RulingDynastyName { get; set; } = "Səlcuqlu Sultanlığı";

        /// <summary>
        /// Dövlətin nəzarətində olan ümumi əyalətlərin sayı.
        /// </summary>
        public int ControlledProvinceCount { get; set; } = 1;

        /// <summary>
        /// Qubernatorsuz, mərkəzdən birbaşa idarə olunan əyalətlərin sayı.
        /// </summary>
        public int DirectControlledProvinceCount { get; set; } = 1;

        /// <summary>
        /// Dövlətin cəzasız birbaşa idarə edə biləcəyi maksimum əyalət sayı (Default: 3).
        /// </summary>
        public int MaxDirectControl { get; set; } = 3;

        /// <summary>
        /// Faza 7.2 - Diplomatik Nüfuz / Reputasiya (0-100 arası, başlanğıc 100).
        /// Əgər dövlət qüvvədə olan müqaviləni (məs: Atəşkəs, Hücum Etməmək Paktı) pozarsa, -30 bal düşür.
        /// Nüfuzu 50-dən aşağı olan dövlətlərlə AI-lər müqavilə bağlamaqdan imtina edir.
        /// </summary>
        public float DiplomaticReputation { get; set; } = 100f;

        /// <summary>
        /// Faza 7.2 - Dövlətin çatdığı ən yüksək hərbi güc (əsgər sayı).
        /// AI-nin fürsətçi hücumunu (ordunun 70%-ni itirdikdə) hesablamaq üçün istifadə olunur.
        /// </summary>
        public int PeakMilitaryPower { get; set; } = 0;

        public float Gold { get; set; }

        public float Food { get; set; }

        public int Babies { get; set; }

        public int Youths { get; set; }

        public int Adults { get; set; }

        public int Elders { get; set; }

        public int Wounded { get; set; }

        [Ignore]
        public int TotalPopulation => Babies + Youths + Adults + Elders + Wounded;

        /// <summary>
        /// Genişlənmə həddi aşımı (Overextension): Birbaşa idarə sayı MaxDirectControl-dən nə qədər çoxdur.
        /// </summary>
        [Ignore]
        public int OverextensionCount => UnityEngine.Mathf.Max(0, DirectControlledProvinceCount - MaxDirectControl);

        /// <summary>
        /// Overextension cəzası (hər artıq əyalət başına 5% = 0.05f vergi və sədaqət cəzası).
        /// </summary>
        [Ignore]
        public float OverextensionPenaltyRate => OverextensionCount * 0.05f;

        public override string ToString()
        {
            string overextStr = OverextensionCount > 0
                ? $" (Overextension: +{OverextensionCount}, -{OverextensionPenaltyRate * 100f:F0}% Vergi/Sədaqət)"
                : "";

            return $"[Nation #{Id}] Sülalə/Dövlət: {RulingDynastyName} ({Name}) | " +
                   $"İdarəetmə: {DirectControlledProvinceCount}/{MaxDirectControl} Birbaşa (Ümumi: {ControlledProvinceCount}){overextStr} | " +
                   $"Diplomatik Nüfuz: {DiplomaticReputation:F0}/100 | " +
                   $"Gold: {Gold:F1} | Food: {Food:F1} | Ümumi Əhali: {TotalPopulation}";
        }
    }
}
