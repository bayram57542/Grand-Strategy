using System;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Faza 10.1 - Əyalətin SQLite bazasından gələn real iqtisadi və demoqrafik məlumat konteyneri.
    /// UI kartları (Population, Gold, Food, Iron) və iqtisadi simulyasiya arasında sinxronizasiyanı təmin edir.
    /// </summary>
    [Serializable]
    public class ProvinceEconomyData
    {
        public int ProvinceId;
        public string ProvinceName;
        public int TotalPopulation;
        public float GoldIncome;        // Aylıq Qızıl gəliri (LocalTaxIncome)
        public float FoodProduction;     // Aylıq Ərzaq istehsalı (FoodProduction)
        public float IronProduction;     // Dəmir ehtiyatı / istehsalı (Iron / RawMaterials)
        public float TreasuryGold;       // Xəzinədəki qızıl
        public float FoodStockpile;      // Anbardakı ərzaq
        public string StrategicResource; // "Iron", "Horses", "Silk" və s.
        public int DevelopmentLevel;     // İnkişaf səviyyəsi
        public string Climate;           // İqlim
        public bool IsFromDatabase;      // Məlumatın SQLite bazasından gəldiyini təsdiqləyir

        public ProvinceEconomyData() { }

        public ProvinceEconomyData(
            int id,
            string name,
            int population,
            float goldIncome,
            float foodProduction,
            float ironProduction,
            float treasuryGold = 0f,
            float foodStockpile = 0f,
            string strategicResource = "None",
            int developmentLevel = 5,
            string climate = "Temperate",
            bool isFromDatabase = true)
        {
            ProvinceId = id;
            ProvinceName = name;
            TotalPopulation = population;
            GoldIncome = goldIncome;
            FoodProduction = foodProduction;
            IronProduction = ironProduction;
            TreasuryGold = treasuryGold;
            FoodStockpile = foodStockpile;
            StrategicResource = strategicResource;
            DevelopmentLevel = developmentLevel;
            Climate = climate;
            IsFromDatabase = isFromDatabase;
        }

        public override string ToString()
        {
            return $"[{ProvinceName} #{ProvinceId}] Əhali: {TotalPopulation:N0} | Qızıl: +{GoldIncome:F0}/ay | Ərzaq: +{FoodProduction:F0}/ay | Dəmir: +{IronProduction:F0}/ay | Baza: {IsFromDatabase}";
        }
    }
}
