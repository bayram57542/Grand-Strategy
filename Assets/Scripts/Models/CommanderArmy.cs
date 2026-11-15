using System;
using UnityEngine;

namespace GrandStrategy.Models
{
    /// <summary>
    /// Faza 10.2 - Xəritədə hərəkət edə bilən tarixi və ya xüsusi Komandir və Ordu modeli.
    /// Əsgərlər sadəcə əyalətlərə deyil, xəritədə hərəkət edən Komandirlərə (Generals) mənimsədilir.
    /// Xəritədə hər ordunun vizual olaraq Komandir ikonu və əsgər sayı göstərilir.
    /// </summary>
    [Serializable]
    public class CommanderArmy
    {
        [SerializeField] private int id;
        [SerializeField] private string commanderName;
        [SerializeField] private string faction;
        [SerializeField] private int factionId;
        [SerializeField] private int currentProvinceId;
        [SerializeField] private string currentProvinceName;
        [SerializeField] private int troopCount;
        [SerializeField] private Color factionColor = Color.white;
        [SerializeField] private float morale = 1.0f;
        [SerializeField] private bool isSelected;
        [SerializeField] private bool isFactionRuler;
        [SerializeField] private float commanderSkill = 1.0f;

        public int Id { get => id; set => id = value; }
        public string CommanderName { get => commanderName; set => commanderName = value; }
        public string Faction { get => faction; set => faction = value; }
        public int FactionId { get => factionId; set => factionId = value; }
        public int CurrentProvinceId { get => currentProvinceId; set => currentProvinceId = value; }
        public string CurrentProvinceName { get => currentProvinceName; set => currentProvinceName = value; }
        public int TroopCount { get => troopCount; set => troopCount = value; }
        public Color FactionColor { get => factionColor; set => factionColor = value; }
        public float Morale { get => morale; set => morale = value; }
        public bool IsSelected { get => isSelected; set => isSelected = value; }
        public bool IsFactionRuler { get => isFactionRuler; set => isFactionRuler = value; }
        public float CommanderSkill { get => commanderSkill; set => commanderSkill = Mathf.Clamp(value, 0.5f, 3.0f); }

        public CommanderArmy() { }

        public CommanderArmy(
            int id,
            string commanderName,
            string faction,
            int factionId,
            int provinceId,
            string provinceName,
            int troopCount,
            Color factionColor,
            bool isFactionRuler = false,
            float commanderSkill = 1.0f)
        {
            this.id = id;
            this.commanderName = commanderName;
            this.faction = faction;
            this.factionId = factionId;
            this.currentProvinceId = provinceId;
            this.currentProvinceName = provinceName;
            this.troopCount = troopCount;
            this.factionColor = factionColor;
            this.morale = 1.0f;
            this.isFactionRuler = isFactionRuler;
            this.commanderSkill = Mathf.Clamp(commanderSkill, 0.5f, 3.0f);
        }

        /// <summary>
        /// Ordu sayını qısa və oxunaqlı formatda qaytarır (məs: 25000 -> "25k", 12500 -> "12.5k", 50000 -> "50k").
        /// </summary>
        public string FormattedTroopCount => FormatTroops(troopCount);

        public static string FormatTroops(int count)
        {
            if (count >= 1_000_000)
            {
                float millions = count / 1_000_000.0f;
                return millions.ToString(count % 1_000_000 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "M";
            }
            if (count >= 1_000)
            {
                float thousands = count / 1000.0f;
                return thousands.ToString(count % 1000 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
            }
            return count.ToString();
        }

        public override string ToString()
        {
            return $"[CommanderArmy #{id} | Komandir: {commanderName} | Fraksiya: {faction} | Əyalət: {currentProvinceName} (#{currentProvinceId}) | Ordu: {FormattedTroopCount} ({troopCount:N0})]";
        }
    }
}
