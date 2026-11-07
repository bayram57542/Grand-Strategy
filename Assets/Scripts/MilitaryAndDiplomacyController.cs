using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Səfərbərlik (DraftArmy) əməliyyatının nəticəsini saxlayan struktur.
    /// </summary>
    public struct DraftArmyResult
    {
        public bool Success;
        public string ProvinceName;
        public int ProvinceId;
        public Army CreatedArmy;
        public int YouthsBefore;
        public int YouthsAfter;
        public int AdultsBefore;
        public int AdultsAfter;
        public bool HadFoodDeficit;
        public float PreDraftFoodProduction;
        public float PreDraftTaxIncome;
    }

    /// <summary>
    /// Müddəti bitən diplomatik müqavilə haqqında məlumat strukturu.
    /// </summary>
    public struct DiplomacyExpirationReport
    {
        public int DiplomacyId;
        public int FactionA_Id;
        public int FactionB_Id;
        public string OldTreatyType;
        public string NewTreatyType;
        public int OldRelationshipScore;
        public int NewRelationshipScore;
        public int Month;
        public int Year;
    }

    /// <summary>
    /// Ordu səfərbərliyini (DraftArmy) və Diplomatik müqavilələrin zamanla (Tick)
    /// idarə olunmasını həyata keçirən alqoritm sinfi.
    /// </summary>
    public class MilitaryAndDiplomacyController : MonoBehaviour
    {
        public static MilitaryAndDiplomacyController Instance { get; private set; }

        [Header("Səfərbərlik Konfiqurasiyası (DraftArmy)")]
        [Tooltip("Səfərbərlik zamanı Gənclər (Youths) sinfindən orduya cəlb edilən faiz (0.50 = 50%)")]
        [SerializeField, Range(0f, 1f)] private float youthDraftRate = 0.50f;

        [Tooltip("Səfərbərlik zamanı Yetişkinlər (Adults) sinfindən orduya cəlb edilən faiz (0.30 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float adultDraftRate = 0.30f;

        [Tooltip("Normal ərzaq təminatı olan əyalətdən çıxan ordunun başlanğıc mənəviyyatı (1.0)")]
        [SerializeField, Range(0.1f, 1f)] private float defaultInitialMorale = 1.0f;

        [Tooltip("Qida kəsiri (aclıq) olan əyalətdən çıxan ordunun başlanğıc mənəviyyatı (0.7)")]
        [SerializeField, Range(0.1f, 1f)] private float starvingInitialMorale = 0.7f;

        [Tooltip("Qida kəsirini yoxlamaq üçün adambaşına aylıq qida istehlakı norması (0.1)")]
        [SerializeField, Min(0.01f)] private float foodConsumptionPerCapita = 0.1f;

        [Header("Diplomatiya Konfiqurasiyası (ProcessMonthlyDiplomacy)")]
        [Tooltip("Müqavilə müddəti bitdikdə təyin olunan yeni status adı")]
        [SerializeField] private string expiredTreatyStatus = "Neytral";

        [Tooltip("Müqavilə bitib 'Neytral' olduqda münasibət balının (RelationshipScore) 0-a yaxınlaşma nisbəti (0.5 = 50% sıfıra yaxınlaşır)")]
        [SerializeField, Range(0.1f, 1f)] private float relationshipConvergenceRatioOnExpiry = 0.50f;

        [Tooltip("Neytral dövlətlər arasında hər ay münasibət balının 0-a doğru dəyişmə addımı")]
        [SerializeField, Min(0)] private int monthlyNeutralDriftStep = 2;

        public event Action<DraftArmyResult> OnArmyDrafted;
        public event Action<DiplomacyExpirationReport> OnTreatyExpired;

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
        /// Əyalət adına görə səfərbərlik (DraftArmy) həyata keçirir.
        /// </summary>
        public DraftArmyResult DraftArmyByProvinceName(DatabaseManager dbManager, string provinceName)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogError("[MilitaryAndDiplomacyController] Səfərbərlik uğursuz oldu: DatabaseManager bağlıdır.");
                return default;
            }

            Province targetProvince = dbManager.Connection
                .Table<Province>()
                .FirstOrDefault(p => p.Name == provinceName);

            if (targetProvince == null)
            {
                Debug.LogError($"[MilitaryAndDiplomacyController] Səfərbərlik üçün '{provinceName}' əyaləti tapılmadı.");
                return default;
            }

            return DraftArmy(dbManager, targetProvince.Id);
        }

        /// <summary>
        /// Seçilmiş əyalətdəki Youths sinfinin 50%-ni (Elit Süvari) və Adults sinfinin 30%-ni (Ağır Piyada)
        /// əhalidən kəsib götürərək yeni bir Army obyekti kimi bazaya yazır.
        /// Əyalətdə qida kəsiri varsa, ordunun Morale dəyəri 0.7-yə düşür.
        /// </summary>
        public DraftArmyResult DraftArmy(DatabaseManager dbManager, int provinceId)
        {
            var result = new DraftArmyResult { Success = false, ProvinceId = provinceId };

            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogError("[MilitaryAndDiplomacyController] DraftArmy icra edilə bilmədi: Baza bağlıdır.");
                return result;
            }

            dbManager.RunInTransaction(conn =>
            {
                Province province = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                Population population = conn.Table<Population>().FirstOrDefault(pop => pop.ProvinceId == provinceId);
                Economy economy = conn.Table<Economy>().FirstOrDefault(eco => eco.ProvinceId == provinceId);

                if (province == null || population == null)
                {
                    Debug.LogError($"[MilitaryAndDiplomacyController] ProvinceId={provinceId} üçün əyalət və ya əhali tapılmadı.");
                    return;
                }

                result.ProvinceName = province.Name;
                result.YouthsBefore = population.Youths;
                result.AdultsBefore = population.Adults;

                if (economy != null)
                {
                    result.PreDraftFoodProduction = economy.FoodProduction;
                    result.PreDraftTaxIncome = economy.LocalTaxIncome;
                }

                // Əyalətdə qida kəsiri (aclıq vəziyyəti) olub-olmadığını yoxlayırıq
                float currentFoodNeed = population.TotalPopulation * foodConsumptionPerCapita;
                bool hasFoodDeficit = economy != null && (economy.FoodProduction - currentFoodNeed) < 0f;
                result.HadFoodDeficit = hasFoodDeficit;

                // Youths sinfinin 50%-i və Adults sinfinin 30%-i səfərbər edilir
                int draftedYouths = Mathf.Clamp(Mathf.RoundToInt(population.Youths * youthDraftRate), 0, population.Youths);
                int draftedAdults = Mathf.Clamp(Mathf.RoundToInt(population.Adults * adultDraftRate), 0, population.Adults);

                population.Youths -= draftedYouths;
                population.Adults -= draftedAdults;

                result.YouthsAfter = population.Youths;
                result.AdultsAfter = population.Adults;

                float initialMorale = hasFoodDeficit ? starvingInitialMorale : defaultInitialMorale;

                var newArmy = new Army
                {
                    OwnerFactionId = province.OwnerFactionId,
                    LocationProvinceId = province.Id,
                    YouthSoldiers = draftedYouths,
                    AdultSoldiers = draftedAdults,
                    Morale = initialMorale
                };

                conn.Update(population);
                conn.Insert(newArmy);

                result.CreatedArmy = newArmy;
                result.Success = true;
            });

            if (result.Success)
            {
                Debug.Log(
                    $"[MilitaryAndDiplomacyController - Səfərbərlik] '{result.ProvinceName}' əyalətindən yeni Ordu (#{result.CreatedArmy.Id}) yaradıldı! " +
                    $"Cəlb edilən əsgər: {result.CreatedArmy.TotalSoldiers} nəfər " +
                    $"(Elit Süvari/Gənc: {result.CreatedArmy.YouthSoldiers}, Ağır Piyada/Yetişkin: {result.CreatedArmy.AdultSoldiers}) | " +
                    $"Mənəviyyat (Morale): {result.CreatedArmy.Morale:F1} (Qida kəsiri: {(result.HadFoodDeficit ? "Bəli" : "Xeyr")}) | " +
                    $"Əyalətdə qalan İşçi Qüvvəsi (Adults): {result.AdultsBefore} -> {result.AdultsAfter} (-{result.AdultsBefore - result.AdultsAfter}), " +
                    $"Gənclər (Youths): {result.YouthsBefore} -> {result.YouthsAfter} (-{result.YouthsBefore - result.YouthsAfter}).");

                OnArmyDrafted?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// Hər Aylıq Tick zamanı bütün Diplomacy qeydlərindəki TreatyDuration müddətini 1 vahid azaldır.
        /// Müddət 0-a çatdıqda müqavilə ləğv edilir, TreatyType "Neytral"-a çevrilir və RelationshipScore 0-a yaxınlaşır.
        /// </summary>
        public void ProcessMonthlyDiplomacy(DatabaseManager dbManager, int month, int year, bool logExpirations = true)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                Debug.LogWarning("[MilitaryAndDiplomacyController] Aylıq diplomatiya yeniləməsi ötürüldü: DatabaseManager hazır deyil.");
                return;
            }

            var expiredReports = new List<DiplomacyExpirationReport>();

            dbManager.RunInTransaction(conn =>
            {
                List<Diplomacy> diplomacies = conn.Table<Diplomacy>().ToList();

                foreach (var dip in diplomacies)
                {
                    if (dip.TreatyDuration > 0)
                    {
                        dip.TreatyDuration -= 1;

                        // Müqavilə müddəti 0-a çatdıqda ləğv olunur və "Neytral"-a keçir
                        if (dip.TreatyDuration <= 0)
                        {
                            dip.TreatyDuration = 0;

                            string oldTreaty = dip.TreatyType;
                            int oldScore = dip.RelationshipScore;

                            dip.TreatyType = expiredTreatyStatus;

                            // RelationshipScore avtomatik 0-a yaxınlaşır
                            int deltaToZero = Mathf.RoundToInt(Mathf.Abs(oldScore) * relationshipConvergenceRatioOnExpiry);
                            dip.RelationshipScore = (int)Mathf.MoveTowards(oldScore, 0f, Mathf.Max(1, deltaToZero));

                            expiredReports.Add(new DiplomacyExpirationReport
                            {
                                DiplomacyId = dip.Id,
                                FactionA_Id = dip.FactionA_Id,
                                FactionB_Id = dip.FactionB_Id,
                                OldTreatyType = oldTreaty,
                                NewTreatyType = dip.TreatyType,
                                OldRelationshipScore = oldScore,
                                NewRelationshipScore = dip.RelationshipScore,
                                Month = month,
                                Year = year
                            });
                        }
                    }
                    else if (string.Equals(dip.TreatyType, expiredTreatyStatus, StringComparison.OrdinalIgnoreCase) && dip.RelationshipScore != 0)
                    {
                        // Artıq Neytral olan dövlətlərin münasibət balı hər ay tədricən 0-a yaxınlaşmağa davam edir
                        dip.RelationshipScore = (int)Mathf.MoveTowards(dip.RelationshipScore, 0f, monthlyNeutralDriftStep);
                    }
                }

                conn.UpdateAll(diplomacies, runInTransaction: false);
            });

            foreach (var exp in expiredReports)
            {
                if (logExpirations)
                {
                    Debug.Log(
                        $"[MilitaryAndDiplomacyController - Diplomatiya] Ay {exp.Month} (İl {exp.Year}): " +
                        $"Dövlət {exp.FactionA_Id} və Dövlət {exp.FactionB_Id} arasındakı '{exp.OldTreatyType}' müqaviləsinin müddəti bitdi -> " +
                        $"Yeni Status: '{exp.NewTreatyType}' | Münasibət balı 0-a yaxınlaşdı: {exp.OldRelationshipScore} -> {exp.NewRelationshipScore}.");
                }

                OnTreatyExpired?.Invoke(exp);
            }
        }
    }
}
