using System;
using SQLite;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Faza 6B-7.1 - Liderlər, Sədaqət, Üsyan və Gizli Vassal Əlaqələri üçün SQLite ORM modeli (Leaders cədvəli).
    /// Liderlərin RPG xüsusiyyətləri (Traits) yoxdur, lakin Sədaqət (Loyalty: 0-100) göstəricisi var.
    /// Sədaqət 30-dan aşağı düşdükdə qubernator üsyan qaldırır (IsRebel = true).
    /// Faza 7.1: Sədaqət 50-dən aşağı düşdükdə güclü düşmən dövlətlə gizli ittifaq (SecretPactFactionId) bağlayır.
    /// </summary>
    [Table("Leaders")]
    public class Leader
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// Liderin adı (məs: "Kral", "Qubernator A").
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Liderin sədaqət göstəricisi (0-100 arası, başlanğıc 100).
        /// </summary>
        public float Loyalty { get; set; } = 100f;

        /// <summary>
        /// Liderin müstəqilliyini elan edib-etmədiyini (üsyan edib-etmədiyini) göstərir (default: false).
        /// </summary>
        public bool IsIndependent { get; set; } = false;

        /// <summary>
        /// Geri-uyğunluq üçün IsIndependent xassəsinin ləqəbi (alias).
        /// </summary>
        [Ignore]
        public bool IsRebel
        {
            get => IsIndependent;
            set => IsIndependent = value;
        }

        /// <summary>
        /// Liderin qubernator təyin edildiyi əyalətin ID-si (null ola bilər).
        /// </summary>
        [Indexed]
        public string AssignedProvinceId { get; set; }

        /// <summary>
        /// Liderin sərkərdə (komandir) təyin edildiyi ordunun ID-si (null ola bilər).
        /// </summary>
        [Indexed]
        public string AssignedArmyId { get; set; }

        /// <summary>
        /// Bu liderin dövlətin taxtında oturan Hökmdar (Kral / Xan / Sultan) olub-olmadığını bildirir.
        /// </summary>
        public bool IsRuler { get; set; } = false;

        /// <summary>
        /// Faza 7.1 - Vassalın gizli ittifaq qurduğu xarici düşmən dövlətin (AI) ID-si (default: null).
        /// </summary>
        [Indexed]
        public int? SecretPactFactionId { get; set; } = null;

        /// <summary>
        /// Bu liderin "Kral" olub-olmadığını müəyyən edir.
        /// </summary>
        [Ignore]
        public bool IsKing
        {
            get => IsRuler || (!string.IsNullOrWhiteSpace(Name) && Name.IndexOf("Kral", StringComparison.OrdinalIgnoreCase) >= 0);
            set => IsRuler = value;
        }

        /// <summary>
        /// AssignedProvinceId dəyərini rəqəmsal Province.Id-yə çevirir (əgər təyin edilibsə).
        /// </summary>
        [Ignore]
        public int? ParsedProvinceId => ParseNullableId(AssignedProvinceId);

        /// <summary>
        /// AssignedArmyId dəyərini rəqəmsal Army.Id-yə çevirir (əgər təyin edilibsə).
        /// </summary>
        [Ignore]
        public int? ParsedArmyId => ParseNullableId(AssignedArmyId);

        public static int? ParseNullableId(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return null;
            }

            string trimmed = rawId.Trim();
            if (int.TryParse(trimmed, out int directId) && directId > 0)
            {
                return directId;
            }

            int underscoreIndex = trimmed.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex + 1 < trimmed.Length)
            {
                string suffix = trimmed.Substring(underscoreIndex + 1);
                if (int.TryParse(suffix, out int parsedSuffix) && parsedSuffix > 0)
                {
                    return parsedSuffix;
                }
            }

            return null;
        }

        public override string ToString()
        {
            string provStr = string.IsNullOrWhiteSpace(AssignedProvinceId) ? "null" : AssignedProvinceId;
            string armyStr = string.IsNullOrWhiteSpace(AssignedArmyId) ? "null" : AssignedArmyId;
            string pactStr = SecretPactFactionId.HasValue ? $" | Gizli Müqavilə: Fraksiya #{SecretPactFactionId.Value}" : "";
            return $"[Leader #{Id} | {Name}] Sədaqət (Loyalty): {Loyalty:F1}/100 | Üsyançı (IsRebel): {IsRebel} | " +
                   $"Əyalət (AssignedProvinceId): {provStr} | Ordu (AssignedArmyId): {armyStr}{pactStr}";
        }
    }
}
