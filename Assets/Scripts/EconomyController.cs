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
            int year)
        {
            float climateBonus = GetClimateFoodMultiplier(province.Climate);
            int devLevel = Mathf.Max(1, province.DevelopmentLevel);
            int adults = Mathf.Max(0, population.Adults);

            // 1. Aylıq İstehsal və Vergi Hesablanması:
            // FoodProduction = (Adults / 100) * DevelopmentLevel * İqlim Bonusu
            economy.FoodProduction = (adults / adultsFoodDivisor) * devLevel * climateBonus;

            // RawMaterials = (Adults / 200) * DevelopmentLevel
            economy.RawMaterials = (adults / adultsRawMaterialDivisor) * devLevel;

            // LocalTaxIncome = (Adults * 0.5) + (LuxuryGoods * 2)
            economy.LocalTaxIncome = (adults * taxPerAdult) + (economy.LuxuryGoods * taxPerLuxuryGood);

            // 2. Aylıq Qida İstehlakı və Aclıq Yoxlaması:
            int totalPop = population.TotalPopulation;
            float foodConsumed = totalPop * foodConsumptionPerPerson;
            float foodBalance = economy.FoodProduction - foodConsumed;

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

            // Əgər nəticə mənfidirsə (Qida çatmırsa): "Aclıq" statusu işə düşür
            if (foodBalance < 0f)
            {
                float missingFood = Mathf.Abs(foodBalance);
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

            return report;
        }

        /// <summary>
        /// Hər Aylıq Tick-də bütün əyalətlərin İstehsal, Vergi və Qida İstehlakı (Aclıq/Ölüm)
        /// əməliyyatlarını DatabaseManager.RunInTransaction daxilində toplu şəkildə yeniləyir.
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

                foreach (var prov in provinces)
                {
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

                    MonthlyEconomyReport report = ApplyMonthlyEconomyAndConsumption(prov, pop, eco, month, year);
                    LastMonthlyReports.Add(report);

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

                // Həm Economy, həm də Population cədvəllərini eyni tranzaksiya daxilində toplu yeniləyirik
                conn.UpdateAll(economies, runInTransaction: false);
                conn.UpdateAll(populations, runInTransaction: false);
            });
        }
    }
}
