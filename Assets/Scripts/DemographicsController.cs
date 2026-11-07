using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Oyun dünyasındakı bütün demoqrafik riyaziyyatı (doğum, yaşlanma keçidləri,
    /// təbii ölüm və yaralıların sağalması) idarə edən alqoritm sinfi.
    /// </summary>
    public class DemographicsController : MonoBehaviour
    {
        public static DemographicsController Instance { get; private set; }

        [Header("Aylıq Doğum Konfiqurasiyası (Birth Rate)")]
        [Tooltip("Minimum inkişaf səviyyəsində Yetişkinlərin (Adults) aylıq doğum faizi (0.005 = 0.5%)")]
        [SerializeField, Range(0f, 0.05f)] private float baseMonthlyBirthRate = 0.005f;

        [Tooltip("Yüksək inkişaf səviyyəsində Yetişkinlərin (Adults) maksimum aylıq doğum faizi (0.008 = 0.8%)")]
        [SerializeField, Range(0f, 0.05f)] private float maxMonthlyBirthRate = 0.008f;

        [Tooltip("Maksimum doğum bonusuna çatmaq üçün tələb olunan Əyalət İnkişaf Səviyyəsi (DevelopmentLevel)")]
        [SerializeField, Min(2)] private int maxDevelopmentLevelForBonus = 25;

        [Header("Aylıq Yaralıların Sağalması və Ölümü (Wounded)")]
        [Tooltip("Hər ay sağalaraq birbaşa Yetişkinlər (Adults) sinfinə qayıdan yaralı faizi (0.30 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float monthlyWoundedHealRate = 0.30f;

        [Tooltip("Hər ay sağalmayaraq həyatını itirən yaralı faizi (0.05 = 5%)")]
        [SerializeField, Range(0f, 1f)] private float monthlyWoundedDeathRate = 0.05f;

        [Header("İllik Yaşlanma Keçidləri (Aging Transitions)")]
        [Tooltip("Hər il Körpələr (Infants) sinfindən Gənclər (Youths) sinfinə keçid faizi (0.15 = 15%)")]
        [SerializeField, Range(0f, 1f)] private float yearlyInfantToYouthRate = 0.15f;

        [Tooltip("Hər il Gənclər (Youths) sinfindən Yetişkinlər (Adults) sinfinə keçid faizi (0.10 = 10%)")]
        [SerializeField, Range(0f, 1f)] private float yearlyYouthToAdultRate = 0.10f;

        [Tooltip("Hər il Yetişkinlər (Adults) sinfindən Yaşlılar (Elders) sinfinə keçid faizi (0.02 = 2%)")]
        [SerializeField, Range(0f, 1f)] private float yearlyAdultToElderRate = 0.02f;

        [Header("İllik Təbii Ölüm (Natural Death)")]
        [Tooltip("Hər il dünyasını dəyişən Yaşlılar (Elders) faizi (0.20 = 20%)")]
        [SerializeField, Range(0f, 1f)] private float yearlyElderDeathRate = 0.20f;

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
        /// Əyalətin inkişaf səviyyəsinə (DevelopmentLevel) əsasən effektiv aylıq doğum faizini hesablayır (0.5% - 0.8%).
        /// </summary>
        public float CalculateEffectiveBirthRate(int developmentLevel)
        {
            float normalizedDev = Mathf.Clamp01((developmentLevel - 1f) / Mathf.Max(1f, maxDevelopmentLevelForBonus - 1f));
            return Mathf.Lerp(baseMonthlyBirthRate, maxMonthlyBirthRate, normalizedDev);
        }

        /// <summary>
        /// Tək bir əyalətin əhalisinə Aylıq demoqrafik qaydaları (Doğum və Yaralıların sağalması/ölümü) tətbiq edir.
        /// </summary>
        public void ApplyMonthlyRulesToPopulation(Population pop, int developmentLevel)
        {
            if (pop == null)
            {
                return;
            }

            int currentAdults = pop.Adults;
            int currentWounded = pop.Wounded;

            // 1. Doğum Səviyyəsi: Adults sayının 0.5% - 0.8%-i qədər yeni Infants doğulur
            float birthRate = CalculateEffectiveBirthRate(developmentLevel);
            int newBirths = Mathf.RoundToInt(currentAdults * birthRate);

            // 2. Yaralıların Sağalması: Wounded sinfinin 30%-i sağalıb Adults olur, 5%-i ölür
            int healedWounded = Mathf.RoundToInt(currentWounded * monthlyWoundedHealRate);
            int diedWounded = Mathf.RoundToInt(currentWounded * monthlyWoundedDeathRate);

            // Yuvarlaqlaşdırma nəticəsində yaralı sayının mənfiyə düşməməsini təmin edirik
            if (healedWounded + diedWounded > currentWounded)
            {
                healedWounded = Mathf.Min(currentWounded, healedWounded);
                diedWounded = Mathf.Max(0, currentWounded - healedWounded);
            }

            pop.Infants += newBirths;
            pop.Adults += healedWounded;
            pop.Wounded -= (healedWounded + diedWounded);
        }

        /// <summary>
        /// Tək bir əyalətin əhalisinə İllik demoqrafik qaydaları (Yaşlanma keçidləri və Təbii ölüm) tətbiq edir.
        /// </summary>
        public void ApplyYearlyRulesToPopulation(Population pop)
        {
            if (pop == null)
            {
                return;
            }

            // İlin əvvəlindəki sinif saylarını snapshot olaraq götürürük ki, eyni ildə ikiqat keçid olmasın
            int startInfants = pop.Infants;
            int startYouths = pop.Youths;
            int startAdults = pop.Adults;
            int startElders = pop.Elders;

            // 1. Yaşlanma (Keçid Mexanikası):
            // - Infants sinfinin 15%-i -> Youths
            // - Youths sinfinin 10%-i -> Adults
            // - Adults sinfinin 2%-i -> Elders
            int infantsToYouths = Mathf.Clamp(Mathf.RoundToInt(startInfants * yearlyInfantToYouthRate), 0, startInfants);
            int youthsToAdults = Mathf.Clamp(Mathf.RoundToInt(startYouths * yearlyYouthToAdultRate), 0, startYouths);
            int adultsToElders = Mathf.Clamp(Mathf.RoundToInt(startAdults * yearlyAdultToElderRate), 0, startAdults);

            // 2. Təbii Ölüm:
            // - Elders sinfinin 20%-i dünyasını dəyişir
            int eldersDied = Mathf.Clamp(Mathf.RoundToInt(startElders * yearlyElderDeathRate), 0, startElders);

            pop.Infants = startInfants - infantsToYouths;
            pop.Youths = startYouths + infantsToYouths - youthsToAdults;
            pop.Adults = startAdults + youthsToAdults - adultsToElders;
            pop.Elders = startElders + adultsToElders - eldersDied;
        }

        /// <summary>
        /// Hər Aylıq Tick-də DatabaseManager daxilindəki bütün əyalətlərin Doğum və Yaralıların sağalması
        /// məntiqini vahid RunInTransaction daxilində toplu şəkildə yeniləyir.
        /// </summary>
        public void ProcessMonthlyDemographics(DatabaseManager dbManager, int month, int year)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogWarning("[DemographicsController] Aylıq demoqrafik yeniləmə ötürüldü: DatabaseManager hazır deyil.");
                return;
            }

            dbManager.RunInTransaction(conn =>
            {
                List<Province> provinces = conn.Table<Province>().ToList();
                var devLevelsByProvince = new Dictionary<int, int>(provinces.Count);
                foreach (var prov in provinces)
                {
                    devLevelsByProvince[prov.Id] = prov.DevelopmentLevel;
                }

                List<Population> populations = conn.Table<Population>().ToList();
                foreach (var pop in populations)
                {
                    int devLevel = devLevelsByProvince.TryGetValue(pop.ProvinceId, out int level) ? level : 1;
                    ApplyMonthlyRulesToPopulation(pop, devLevel);
                }

                conn.UpdateAll(populations, runInTransaction: false);
            });
        }

        /// <summary>
        /// Hər İllik Tick-də DatabaseManager daxilindəki bütün əyalətlərin Yaşlanma və Təbii Ölüm
        /// məntiqini vahid RunInTransaction daxilində toplu şəkildə yeniləyir.
        /// </summary>
        public void ProcessYearlyDemographics(DatabaseManager dbManager, int year)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogWarning("[DemographicsController] İllik demoqrafik yeniləmə ötürüldü: DatabaseManager hazır deyil.");
                return;
            }

            dbManager.RunInTransaction(conn =>
            {
                List<Population> populations = conn.Table<Population>().ToList();
                foreach (var pop in populations)
                {
                    ApplyYearlyRulesToPopulation(pop);
                }

                conn.UpdateAll(populations, runInTransaction: false);
            });
        }
    }
}
