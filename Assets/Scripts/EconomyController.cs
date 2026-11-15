using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Bir əyalətin aylıq iqtisadi və aclıq (Starvation) hesabatını saxlayan struktur.
    /// </summary>
    public struct MonthlyEconomyReport
    {
        public int Month;
        public int Year;
        public int ProvinceId;
        public string ProvinceName;
        public string Climate;
        public float FoodProduced;
        public float FoodConsumed;
        public float FoodBalance;
        public float RawMaterialsProduced;
        public float LocalTaxIncome;
        public bool IsStarving;
        public int StarvedInfants;
        public int StarvedElders;
        public int TotalStarved => StarvedInfants + StarvedElders;
    }

    /// <summary>
    /// Oyun dünyasındakı iqtisadi hesablamaları (ərzaq/xammal istehsalı, vergi gəliri,
    /// qida istehlakı və aclıqdan ölüm mexanikası) idarə edən alqoritm sinfi.
    /// </summary>
    public class EconomyController : MonoBehaviour
    {
        public static EconomyController Instance { get; private set; }

        [Header("Aylıq İstehsal Əmsalları (Production)")]
        [Tooltip("Ərzaq istehsalı düsturunda Yetişkin (Adults) böləni: (Adults / 100) * DevLevel * İqlimBonusu")]
        [SerializeField, Min(1f)] private float adultsFoodDivisor = 100f;

        [Tooltip("Xammal istehsalı düsturunda Yetişkin (Adults) böləni: (Adults / 200) * DevLevel")]
        [SerializeField, Min(1f)] private float adultsRawMaterialDivisor = 200f;

        [Header("İqlim Bonusları (Climate Multipliers)")]
        [Tooltip("'Mülayim' iqlim üçün ərzaq istehsalı çarpanı (1.2x)")]
        [SerializeField, Min(0.1f)] private float temperateClimateBonus = 1.2f;

        [Tooltip("'Quraq' iqlim üçün ərzaq istehsalı çarpanı (0.8x)")]
        [SerializeField, Min(0.1f)] private float aridClimateBonus = 0.8f;

        [Tooltip("Digər/standart iqlimlər üçün ərzaq istehsalı çarpanı (1.0x)")]
        [SerializeField, Min(0.1f)] private float defaultClimateBonus = 1.0f;

        [Header("Vergi Əmsalları (Taxation)")]
        [Tooltip("Hər yetişkin (Adults) başına düşən vergi əmsalı (0.5)")]
        [SerializeField, Min(0f)] private float taxPerAdult = 0.5f;

        [Tooltip("Hər vahid Lüks Mal (LuxuryGoods) başına düşən vergi əmsalı (2.0)")]
        [SerializeField, Min(0f)] private float taxPerLuxuryGood = 2.0f;

        [Header("Qida İstehlakı və Aclıq (Consumption & Starvation)")]
        [Tooltip("Adambaşına aylıq qida istehlakı (TotalPopulation * 0.1)")]
        [SerializeField, Min(0.01f)] private float foodConsumptionPerPerson = 0.1f;

        [Tooltip("Çatışmayan hər 1 vahid qida üçün neçə nəfərin aclıqdan öləcəyi (1.0)")]
        [SerializeField, Min(0.1f)] private float starvationDeathsPerMissingFood = 1.0f;

        [Tooltip("Aclıq ölümlərində Körpələrin (Infants) pay nisbəti (qalan hissə Yaşlılardan - Elders çıxılır)")]
        [SerializeField, Range(0f, 1f)] private float infantStarvationShare = 0.65f;

        /// <summary>
        /// Hər hansı əyalətdə aclıq baş verdikdə tətiklənən hadisə.
        /// </summary>
        public event Action<MonthlyEconomyReport> OnProvinceStarvationOccurred;

        /// <summary>
        /// Son aylıq iqtisadi dövrün bütün əyalətlər üzrə hesabat siyahısı.
        /// </summary>
        public List<MonthlyEconomyReport> LastMonthlyReports { get; } = new List<MonthlyEconomyReport>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Əyalətin iqlim adına əsasən ərzaq istehsalı bonusunu qaytarır.
        /// </summary>
        public float GetClimateFoodMultiplier(string climate)
        {
            if (string.IsNullOrWhiteSpace(climate))
            {
                return defaultClimateBonus;
            }

            if (climate.IndexOf("Quraq", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return aridClimateBonus;
            }

            if (climate.IndexOf("Mülayim", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return temperateClimateBonus;
            }

            return defaultClimateBonus;
        }

        /// <summary>
        /// Tək bir əyalət üçün aylıq İstehsal, Vergi, Qida İstehlakı və Aclıq riyaziyyatını icra edir.
        /// </summary>
        public MonthlyEconomyReport ApplyMonthlyEconomyAndConsumption(
            Province province,
            Population population,
            Economy economy,
            int month,
            int year,
            float taxModifier = 1.0f)
        {
            float climateBonus = GetClimateFoodMultiplier(province.Climate);
            float baseProductivity = province.ProductivityMultiplier > 0f ? province.ProductivityMultiplier : 1.0f;

            // Faza 6C: Əgər əyalətin PublicOrder dəyəri 30-dan aşağı düşərsə (itaətsizlik/üsyan),
            // həmin əyalətin faktiki məhsuldarlığı müvəqqəti olaraq yarıya (0.5x) düşür (xalq vergi vermir/işləmir).
            float productivity = province.PublicOrder < 30f ? (baseProductivity * 0.5f) : baseProductivity;

            int devLevel = Mathf.Max(1, province.DevelopmentLevel);
            int adults = Mathf.Max(0, population.Adults);
            int youths = Mathf.Max(0, population.Youths);

            // 1. Aylıq İstehsal və Vergi Hesablanması (Youths, Adults və ProductivityMultiplier nəzərə alınmaqla Gold və Food artır):
            // FoodProduction = ((Adults / 100) + (Youths / 250)) * DevelopmentLevel * İqlim Bonusu * ProductivityMultiplier
            economy.FoodProduction = ((adults / adultsFoodDivisor) + (youths / (adultsFoodDivisor * 2.5f))) * devLevel * climateBonus * productivity;

            // RawMaterials = (Adults / 200) * DevelopmentLevel * ProductivityMultiplier
            economy.RawMaterials = (adults / adultsRawMaterialDivisor) * devLevel * productivity;

            // LocalTaxIncome (Gold artımı) = ((Adults * 0.5) + (Youths * 0.25) + (LuxuryGoods * 2)) * ProductivityMultiplier * taxModifier (Overextension cəzası daxil)
            float clampedTaxMod = Mathf.Clamp01(taxModifier);
            economy.LocalTaxIncome = ((adults * taxPerAdult) + (youths * (taxPerAdult * 0.5f)) + (economy.LuxuryGoods * taxPerLuxuryGood)) * productivity * clampedTaxMod;

            // 2. Aylıq Qida İstehlakı (Babies, Elders, Wounded və aktiv əhali sayına görə Food azalır):
            float nonWorkingConsumption = (population.Infants + population.Elders + population.Wounded) * (foodConsumptionPerPerson * 1.2f);
            float workingConsumption = (population.Youths + population.Adults) * foodConsumptionPerPerson;
            float foodConsumed = nonWorkingConsumption + workingConsumption;
            float foodBalance = economy.FoodProduction - foodConsumed;

            // Əyalətin Qızıl (Gold) və Ərzaq (Food) ehtiyatlarını yeniləyirik
            province.Gold += economy.LocalTaxIncome;
            province.Food += foodBalance;

            // Faza 8.1: Strateji resursların (Iron, Horses, Silk) aylıq istehsalı:
            // Dağlıq əyalətlərdə Iron, Düzənlik/Çöl əyalətlərində Horses, Ticarət mərkəzində Silk
            if (string.Equals(province.StrategicResourceType, "Iron", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(province.Climate) && province.Climate.IndexOf("Dağlıq", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                province.Iron += Mathf.Max(0.5f, (adults / 200f) * devLevel * productivity * 0.1f);
            }
            if (string.Equals(province.StrategicResourceType, "Horses", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(province.Climate) && (province.Climate.IndexOf("Düzənlik", StringComparison.OrdinalIgnoreCase) >= 0 || province.Climate.IndexOf("Quraq", StringComparison.OrdinalIgnoreCase) >= 0)))
            {
                province.Horses += Mathf.Max(0.5f, (adults / 250f) * devLevel * productivity * 0.1f);
            }
            if (string.Equals(province.StrategicResourceType, "Silk", StringComparison.OrdinalIgnoreCase))
            {
                province.Silk += Mathf.Max(0.2f, (adults / 300f) * devLevel * productivity * 0.05f);
            }

            var report = new MonthlyEconomyReport
            {
                Month = month,
                Year = year,
                ProvinceId = province.Id,
                ProvinceName = province.Name,
                Climate = province.Climate,
                FoodProduced = economy.FoodProduction,
                FoodConsumed = foodConsumed,
                FoodBalance = foodBalance,
                RawMaterialsProduced = economy.RawMaterials,
                LocalTaxIncome = economy.LocalTaxIncome,
                IsStarving = false,
                StarvedInfants = 0,
                StarvedElders = 0
            };

            // Əgər aylıq balans və ya ümumi ərzaq mənfiyə düşərsə (Qida çatmırsa): Xəstəlik/Aclıq ölümü başlayır
            if (foodBalance < 0f || province.Food < 0f)
            {
                float missingFood = province.Food < 0f ? Mathf.Abs(province.Food) : Mathf.Abs(foodBalance);
                province.Food = Mathf.Max(0f, province.Food);
                int totalDeathsNeeded = Mathf.RoundToInt(missingFood * starvationDeathsPerMissingFood);

                if (totalDeathsNeeded > 0 && (population.Infants > 0 || population.Elders > 0))
                {
                    report.IsStarving = true;

                    int desiredInfantDeaths = Mathf.RoundToInt(totalDeathsNeeded * infantStarvationShare);
                    int desiredElderDeaths = totalDeathsNeeded - desiredInfantDeaths;

                    int actualInfantDeaths = Mathf.Min(population.Infants, desiredInfantDeaths);
                    int actualElderDeaths = Mathf.Min(population.Elders, desiredElderDeaths);

                    // Əgər bir sinifdə kifayət qədər insan yoxdursa, qalan aclıq ölümləri digər zəif sinifdən çıxılır
                    int remainingDeaths = totalDeathsNeeded - (actualInfantDeaths + actualElderDeaths);
                    if (remainingDeaths > 0)
                    {
                        if (population.Infants > actualInfantDeaths)
                        {
                            int extraInfants = Mathf.Min(population.Infants - actualInfantDeaths, remainingDeaths);
                            actualInfantDeaths += extraInfants;
                            remainingDeaths -= extraInfants;
                        }

                        if (remainingDeaths > 0 && population.Elders > actualElderDeaths)
                        {
                            int extraElders = Mathf.Min(population.Elders - actualElderDeaths, remainingDeaths);
                            actualElderDeaths += extraElders;
                        }
                    }

                    population.Infants -= actualInfantDeaths;
                    population.Elders -= actualElderDeaths;

                    report.StarvedInfants = actualInfantDeaths;
                    report.StarvedElders = actualElderDeaths;
                }
            }

            // Province daxilindəki demoqrafik sütunları sinxronlaşdırırıq
            province.Babies = population.Infants;
            province.Youths = population.Youths;
            province.Adults = population.Adults;
            province.Elders = population.Elders;
            province.Wounded = population.Wounded;

            return report;
        }

        /// <summary>
        /// Hər Aylıq Tick-də bütün əyalətlərin İstehsal, Vergi və Qida İstehlakı (Aclıq/Ölüm)
        /// əməliyyatlarını DatabaseManager.RunInTransaction daxilində toplu şəkildə yeniləyir (Auto-save).
        /// </summary>
        public void ProcessMonthlyEconomy(DatabaseManager dbManager, int month, int year, bool logStarvationToConsole = true)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogWarning("[EconomyController] Aylıq iqtisadi yeniləmə ötürüldü: DatabaseManager hazır deyil.");
                return;
            }

            LastMonthlyReports.Clear();

            dbManager.RunInTransaction(conn =>
            {
                List<Province> provinces = conn.Table<Province>().ToList();
                List<Population> populations = conn.Table<Population>().ToList();
                List<Economy> economies = conn.Table<Economy>().ToList();
                List<Nation> nations = conn.Table<Nation>().ToList();

                var popByProvince = new Dictionary<int, Population>(populations.Count);
                foreach (var pop in populations)
                {
                    popByProvince[pop.ProvinceId] = pop;
                }

                var ecoByProvince = new Dictionary<int, Economy>(economies.Count);
                foreach (var eco in economies)
                {
                    ecoByProvince[eco.ProvinceId] = eco;
                }

                for (int i = 0; i < provinces.Count; i++)
                {
                    Province prov = provinces[i];
                    if (!popByProvince.TryGetValue(prov.Id, out Population pop))
                    {
                        continue;
                    }

                    if (!ecoByProvince.TryGetValue(prov.Id, out Economy eco))
                    {
                        eco = new Economy { ProvinceId = prov.Id };
                        conn.Insert(eco);
                        economies.Add(eco);
                        ecoByProvince[prov.Id] = eco;
                    }

                    Nation matchingNation = nations.Find(n => n.Id == prov.OwnerFactionId);
                    float taxModifier = matchingNation != null ? Mathf.Clamp01(1.0f - matchingNation.OverextensionPenaltyRate) : 1.0f;
                    MonthlyEconomyReport report = ApplyMonthlyEconomyAndConsumption(prov, pop, eco, month, year, taxModifier);
                    LastMonthlyReports.Add(report);
                    if (matchingNation != null)
                    {
                        // Faza 6C & 8.1: Mühasirə başlayanda əyalətdəki resurslar (Qızıl, Ərzaq, Dəmir, At, İpək)
                        // qlobal (Nation) xəzinəyə axmağı dayandırır.
                        if (!prov.IsBesieged)
                        {
                            matchingNation.Gold = prov.Gold;
                            matchingNation.Food = prov.Food;
                            matchingNation.Iron = prov.Iron;
                            matchingNation.Horses = prov.Horses;
                            matchingNation.Silk = prov.Silk;
                        }

                        // Faza 8.1: İpək Bonusu: Aylıq döngüdə, əgər dövlətin xəzinəsində Silk varsa (Silk > 0),
                        // bütün əyalətlərdə PublicOrder (Xalqın Moralı) hər ay +2 bal artır.
                        if (matchingNation.Silk > 0f)
                        {
                            prov.PublicOrder = Mathf.Clamp(prov.PublicOrder + 2f, 0f, 100f);
                        }

                        matchingNation.Babies = prov.Babies;
                        matchingNation.Youths = prov.Youths;
                        matchingNation.Adults = prov.Adults;
                        matchingNation.Elders = prov.Elders;
                        matchingNation.Wounded = prov.Wounded;
                        conn.Update(matchingNation);
                    }

                    if (report.IsStarving)
                    {
                        if (logStarvationToConsole)
                        {
                            Debug.Log(
                                $"[EconomyController - Aclıq] Ay {month} (İl {year}) - {prov.Name}: " +
                                $"Qida qıtlığı yarandı (İstehsal: {report.FoodProduced:F1}, İstehlak: {report.FoodConsumed:F1}, Kəsir: {report.FoodBalance:F1}), " +
                                $"{report.StarvedInfants} Körpə, {report.StarvedElders} Yaşlı aclıqdan öldü.");
                        }

                        OnProvinceStarvationOccurred?.Invoke(report);
                    }
                }

                // Bütün cədvəlləri (Provinces, Nations, Economies, Populations) vahid tranzaksiya daxilində SQLite-a yazırıq (Auto-save)
                conn.UpdateAll(provinces, runInTransaction: false);
                conn.UpdateAll(nations, runInTransaction: false);
                conn.UpdateAll(economies, runInTransaction: false);
                conn.UpdateAll(populations, runInTransaction: false);
            });
        }
    }
}
