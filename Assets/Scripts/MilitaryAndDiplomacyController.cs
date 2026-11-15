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
    /// Faza 6C - Fövqəladə Vəziyyət (Qadınların Orduya Çağırılması) nəticə strukturu.
    /// </summary>
    public struct EmergencyConscriptionResult
    {
        public bool Success;
        public int ProvinceId;
        public string ProvinceName;
        public Army CreatedMilitiaArmy;
        public int ConscriptedAdults;
        public int AdultsRemaining;
        public float OldBirthRateModifier;
        public float NewBirthRateModifier;
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

        public string LastCeasefireExpiredMessage { get; private set; }
        public string LastTreatyBrokenMessage { get; private set; }
        public event Action<DraftArmyResult> OnArmyDrafted;
        public event Action<DiplomacyExpirationReport> OnTreatyExpired;
        public event Action<int, int> OnCeasefireExpired;
        public event Action<int, int, int> OnCeasefireProposed;
        public event Action<int, int> OnWarDeclared;
        public event Action<int, int> OnTreatyBroken;
        public event Action<int, int, int> OnNonAggressionPactProposed;
        public event Action<int, int> OnMilitaryAllianceFormed;
        public event Action<int, int, int> OnAllyJoinedWar;

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
                province.Youths = population.Youths;
                province.Adults = population.Adults;

                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == province.Id);
                if (nation != null)
                {
                    nation.Youths = province.Youths;
                    nation.Adults = province.Adults;
                    conn.Update(nation);
                }

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

                conn.Update(province);
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
            var expiredCeasefires = new List<(int factionA, int factionB)>();
            var expiredNAPs = new List<(int factionA, int factionB)>();

            dbManager.RunInTransaction(conn =>
            {
                List<Diplomacy> diplomacies = conn.Table<Diplomacy>().ToList();

                foreach (var dip in diplomacies)
                {
                    // Faza 7.1: Atəşkəs müddətinin azaldılması
                    if (dip.CeasefireMonthsLeft > 0)
                    {
                        dip.CeasefireMonthsLeft -= 1;
                        if (dip.CeasefireMonthsLeft == 0)
                        {
                            LastCeasefireExpiredMessage = "Atəşkəs bitdi!";
                            if (string.Equals(dip.TreatyType, "Atəşkəs", StringComparison.OrdinalIgnoreCase))
                            {
                                dip.TreatyType = expiredTreatyStatus;
                            }
                            expiredCeasefires.Add((dip.FactionA_Id, dip.FactionB_Id));
                        }
                    }

                    // Faza 7.2: Hücum Etməmək Paktı müddətinin azaldılması
                    if (dip.NonAggressionMonthsLeft > 0)
                    {
                        dip.NonAggressionMonthsLeft -= 1;
                        if (dip.NonAggressionMonthsLeft == 0)
                        {
                            if (string.Equals(dip.TreatyType, "Hücum Etməmək Paktı", StringComparison.OrdinalIgnoreCase))
                            {
                                dip.TreatyType = expiredTreatyStatus;
                            }
                            expiredNAPs.Add((dip.FactionA_Id, dip.FactionB_Id));
                        }
                    }

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

            foreach (var cf in expiredCeasefires)
            {
                if (logExpirations)
                {
                    Debug.LogWarning(
                        $"[MilitaryAndDiplomacyController - Diplomatiya] Ay {month} (İl {year}): " +
                        $"Atəşkəs bitdi! Fraksiya {cf.factionA} və Fraksiya {cf.factionB} arasındakı atəşkəs müddəti başa çatdı.");
                }

                OnCeasefireExpired?.Invoke(cf.factionA, cf.factionB);
            }

            foreach (var nap in expiredNAPs)
            {
                if (logExpirations)
                {
                    Debug.Log(
                        $"[MilitaryAndDiplomacyController - Diplomatiya] Ay {month} (İl {year}): " +
                        $"Hücum Etməmək Paktı bitdi! Fraksiya {nap.factionA} və Fraksiya {nap.factionB} arasındakı pakt başa çatdı.");
                }
            }
        }

        /// <summary>
        /// Faza 6C - Fövqəladə Vəziyyət (Qadınların Orduya Çağırılması):
        /// Əyalətdəki Adults sayının bir hissəsi (qadınları təmsil edən 20%) orduya (Milis) cəlb edilir.
        /// Ağır Cəza: Əyalətin BirthRateModifier dəyəri uzunmüddətli olaraq 0.2f-ə (standartın 20%-nə) düşür (doğum çökür).
        /// </summary>
        public EmergencyConscriptionResult CallEmergencyConscription(string provinceId, DatabaseManager dbManager = null)
        {
            if (string.IsNullOrWhiteSpace(provinceId))
            {
                Debug.LogWarning("[MilitaryAndDiplomacyController] CallEmergencyConscription uğursuz: provinceId boşdur.");
                return default;
            }

            int id = -1;
            if (!int.TryParse(provinceId, out id))
            {
                string digits = System.Text.RegularExpressions.Regex.Match(provinceId, @"\d+").Value;
                int.TryParse(digits, out id);
            }

            return CallEmergencyConscription(id, dbManager);
        }

        /// <summary>
        /// int ID ilə Fövqəladə Vəziyyət (CallEmergencyConscription) əmrini icra edir.
        /// </summary>
        public EmergencyConscriptionResult CallEmergencyConscription(int provinceId, DatabaseManager dbManager = null)
        {
            var result = new EmergencyConscriptionResult { Success = false, ProvinceId = provinceId };

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                Debug.LogError("[MilitaryAndDiplomacyController] CallEmergencyConscription uğursuz: DatabaseManager bağlıdır.");
                return result;
            }

            db.RunInTransaction(conn =>
            {
                Province province = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                Population population = conn.Table<Population>().FirstOrDefault(pop => pop.ProvinceId == provinceId);

                if (province == null || population == null)
                {
                    Debug.LogError($"[MilitaryAndDiplomacyController] CallEmergencyConscription: ProvinceId={provinceId} tapılmadı.");
                    return;
                }

                result.ProvinceName = province.Name;
                result.OldBirthRateModifier = province.BirthRateModifier;

                // Əyalətdəki Adults sayının 20%-i (qadınları təmsil edən hissə) səfərbər edilir
                int conscriptedWomen = Mathf.Clamp(Mathf.RoundToInt(population.Adults * 0.20f), 0, population.Adults);

                population.Adults -= conscriptedWomen;
                province.Adults = population.Adults;

                // Ağır Cəza: Əyalətin BirthRateModifier dəyəri 0.2f-ə düşür (doğum çökür)
                province.BirthRateModifier = 0.2f;

                // Xalqın moralına da cəza veririk (həyəcan və itaətsizlik riski)
                province.PublicOrder = Mathf.Clamp(province.PublicOrder - 20f, 0f, 100f);

                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == province.OwnerFactionId);
                if (nation != null)
                {
                    nation.Adults = province.Adults;
                    conn.Update(nation);
                }

                var militiaArmy = new Army
                {
                    OwnerFactionId = province.OwnerFactionId,
                    LocationProvinceId = province.Id,
                    YouthSoldiers = 0,
                    AdultSoldiers = conscriptedWomen,
                    Morale = 0.8f
                };

                conn.Update(province);
                conn.Update(population);
                conn.Insert(militiaArmy);

                result.CreatedMilitiaArmy = militiaArmy;
                result.ConscriptedAdults = conscriptedWomen;
                result.AdultsRemaining = population.Adults;
                result.NewBirthRateModifier = province.BirthRateModifier;
                result.Success = true;

                Debug.LogWarning(
                    $"[MilitaryAndDiplomacyController] FÖVQƏLADƏ VƏZİYYƏT ELAN EDİLDİ! -> Əyalət '{province.Name}': " +
                    $"{conscriptedWomen} qadın/yetkin orduya cəlb edildi (Milis Ordusu #{militiaArmy.Id}). " +
                    $"AĞIR CƏZA: Doğum Çarpanı (BirthRateModifier) 0.2x (20%)-ə düşdü!");
            });

            return result;
        }

        #region Faza 7.1 - Atəşkəs (Ceasefire) və Müharibə Sistemi

        /// <summary>
        /// Faza 7.1 - İki fraksiya arasında atəşkəs təklif edir və qüvvəyə mindirir.
        /// Müharibə vəziyyətini dayandırır və CeasefireMonthsLeft dəyərini (məsələn, 12 və ya 60 ay) təyin edir.
        /// </summary>
        public bool ProposeCeasefire(int factionA, int factionB, int durationMonths, DatabaseManager dbManager = null)
        {
            if (durationMonths <= 0)
            {
                Debug.LogWarning("[MilitaryAndDiplomacyController] ProposeCeasefire xətası: Müddət 0-dan böyük olmalıdır.");
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                Debug.LogError("[MilitaryAndDiplomacyController] ProposeCeasefire uğursuz: DatabaseManager hazır deyil.");
                return false;
            }

            bool success = false;
            db.RunInTransaction(conn =>
            {
                Diplomacy dip = conn.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                    (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

                if (dip != null)
                {
                    dip.TreatyType = "Atəşkəs";
                    dip.CeasefireMonthsLeft = durationMonths;
                    dip.TreatyDuration = Mathf.Max(dip.TreatyDuration, durationMonths);
                    if (dip.RelationshipScore < 0)
                    {
                        dip.RelationshipScore = 0;
                    }
                    conn.Update(dip);
                }
                else
                {
                    dip = new Diplomacy
                    {
                        FactionA_Id = factionA,
                        FactionB_Id = factionB,
                        RelationshipScore = 0,
                        TreatyType = "Atəşkəs",
                        TreatyDuration = durationMonths,
                        CeasefireMonthsLeft = durationMonths
                    };
                    conn.Insert(dip);
                }

                success = true;
            });

            if (success)
            {
                Debug.Log($"[MilitaryAndDiplomacyController] ATƏŞKƏS BAĞLANDI! Fraksiya {factionA} <-> Fraksiya {factionB} ({durationMonths} ay).");
                OnCeasefireProposed?.Invoke(factionA, factionB, durationMonths);
            }

            return success;
        }

        /// <summary>
        /// Faza 7.1 - İki fraksiya arasında hazırda aktiv atəşkəs olub-olmadığını yoxlayır.
        /// </summary>
        public bool IsCeasefireActive(int factionA, int factionB, DatabaseManager dbManager = null)
        {
            if (factionA == factionB)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                return false;
            }

            Diplomacy dip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

            return dip != null && dip.CeasefireMonthsLeft > 0;
        }

        /// <summary>
        /// İki fraksiyanın bir-birinə hücum edə bilib-bilməyəcəyini yoxlayır.
        /// Atəşkəs, Hücum Etməmək Paktı və ya Hərbi İttifaq qüvvədə olduğu müddətdə ordular bir-birinə hücum edə bilməz.
        /// </summary>
        public bool CanAttack(int attackerFactionId, int defenderFactionId, DatabaseManager dbManager = null)
        {
            if (attackerFactionId == defenderFactionId)
            {
                return false;
            }

            if (IsCeasefireActive(attackerFactionId, defenderFactionId, dbManager))
            {
                return false;
            }

            if (IsNonAggressionPactActive(attackerFactionId, defenderFactionId, dbManager))
            {
                return false;
            }

            if (AreAllies(attackerFactionId, defenderFactionId, dbManager))
            {
                return false;
            }

            return true;
        }

        #region Faza 7.2 - Hücum Etməmək Paktı (Non-Aggression Pact) və Hərbi İttifaq (Military Alliance)

        /// <summary>
        /// Faza 7.2 - İki fraksiya arasında Hücum Etməmək Paktı təklif edir və qüvvəyə mindirir.
        /// Əgər təklif edən dövlətin DiplomaticReputation göstəricisi 50-dən aşağıdırsa, digər dövlət müqaviləni rədd edir.
        /// </summary>
        public bool ProposeNonAggressionPact(int factionA, int factionB, int durationMonths, DatabaseManager dbManager = null)
        {
            if (durationMonths <= 0 || factionA == factionB)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return false;
            }

            bool success = false;
            string failReason = null;

            db.RunInTransaction(conn =>
            {
                Nation natA = conn.Table<Nation>().FirstOrDefault(n => n.Id == factionA);
                if (natA != null && natA.DiplomaticReputation < 50f)
                {
                    failReason = $"Fraksiya #{factionA}-nın diplomatik nüfuzu ({natA.DiplomaticReputation:F0}/100) çox aşağıdır (< 50)!";
                    return;
                }

                Diplomacy dip = conn.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                    (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

                if (dip != null)
                {
                    dip.TreatyType = "Hücum Etməmək Paktı";
                    dip.NonAggressionMonthsLeft = durationMonths;
                    dip.TreatyDuration = Mathf.Max(dip.TreatyDuration, durationMonths);
                    dip.RelationScore = Mathf.Clamp(dip.RelationScore + 20, -100, 100);
                    conn.Update(dip);
                }
                else
                {
                    dip = new Diplomacy
                    {
                        FactionA_Id = factionA,
                        FactionB_Id = factionB,
                        RelationScore = 20,
                        TreatyType = "Hücum Etməmək Paktı",
                        TreatyDuration = durationMonths,
                        NonAggressionMonthsLeft = durationMonths
                    };
                    conn.Insert(dip);
                }

                success = true;
            });

            if (!success && !string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning($"[MilitaryAndDiplomacyController] Hücum Etməmək Paktı rədd edildi: {failReason}");
                return false;
            }

            if (success)
            {
                Debug.Log($"[MilitaryAndDiplomacyController] HÜCUM ETMƏMƏK PAKTI BAĞLANDI! Fraksiya {factionA} <-> Fraksiya {factionB} ({durationMonths} ay).");
                OnNonAggressionPactProposed?.Invoke(factionA, factionB, durationMonths);
            }

            return success;
        }

        /// <summary>
        /// Faza 7.2 - İki fraksiya arasında hazırda Hücum Etməmək Paktının aktiv olub-olmadığını yoxlayır.
        /// </summary>
        public bool IsNonAggressionPactActive(int factionA, int factionB, DatabaseManager dbManager = null)
        {
            if (factionA == factionB)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                return false;
            }

            Diplomacy dip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

            return dip != null && dip.IsNonAggressionPactActive;
        }

        /// <summary>
        /// Faza 7.2 - İki fraksiya arasında Hərbi İttifaq (Military Alliance) qurur.
        /// İttifaq qurulduqda RelationScore ən azı +60 (Müttəfiq) olur.
        /// Əgər təklif edən dövlətin DiplomaticReputation göstəricisi 50-dən aşağıdırsa, təklif rədd edilir.
        /// </summary>
        public bool FormMilitaryAlliance(int factionA, int factionB, DatabaseManager dbManager = null)
        {
            if (factionA == factionB)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return false;
            }

            bool success = false;
            string failReason = null;

            db.RunInTransaction(conn =>
            {
                Nation natA = conn.Table<Nation>().FirstOrDefault(n => n.Id == factionA);
                if (natA != null && natA.DiplomaticReputation < 50f)
                {
                    failReason = $"Fraksiya #{factionA}-nın diplomatik nüfuzu ({natA.DiplomaticReputation:F0}/100) çox aşağıdır (< 50)!";
                    return;
                }

                Diplomacy dip = conn.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                    (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

                if (dip != null)
                {
                    dip.TreatyType = "Hərbi İttifaq";
                    dip.RelationScore = Mathf.Max(60, dip.RelationScore);
                    dip.TreatyDuration = 120;
                    conn.Update(dip);
                }
                else
                {
                    dip = new Diplomacy
                    {
                        FactionA_Id = factionA,
                        FactionB_Id = factionB,
                        RelationScore = 80,
                        TreatyType = "Hərbi İttifaq",
                        TreatyDuration = 120
                    };
                    conn.Insert(dip);
                }

                success = true;
            });

            if (!success && !string.IsNullOrEmpty(failReason))
            {
                Debug.LogWarning($"[MilitaryAndDiplomacyController] Hərbi İttifaq rədd edildi: {failReason}");
                return false;
            }

            if (success)
            {
                Debug.Log($"[MilitaryAndDiplomacyController] HƏRBİ İTTİFAQ QURULDU! Fraksiya {factionA} <-> Fraksiya {factionB} (Müttəfiq).");
                OnMilitaryAllianceFormed?.Invoke(factionA, factionB);
            }

            return success;
        }

        /// <summary>
        /// Faza 7.2 - İki fraksiyanın müttəfiq (Hərbi İttifaq) olub-olmadığını yoxlayır.
        /// </summary>
        public bool AreAllies(int factionA, int factionB, DatabaseManager dbManager = null)
        {
            if (factionA == factionB)
            {
                return true;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                return false;
            }

            Diplomacy dip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

            return dip != null && dip.IsAlliance;
        }

        /// <summary>
        /// Fraksiyanın bütün hərbi müttəfiqlərinin siyahısını qaytarır.
        /// </summary>
        public List<int> GetAllies(int factionId, DatabaseManager dbManager = null)
        {
            var allies = new List<int>();
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                return allies;
            }

            List<Diplomacy> diplomacies = db.Connection.Table<Diplomacy>().ToList();
            for (int i = 0; i < diplomacies.Count; i++)
            {
                Diplomacy dip = diplomacies[i];
                if (dip.IsAlliance)
                {
                    if (dip.FactionA_Id == factionId && dip.FactionB_Id != factionId)
                    {
                        allies.Add(dip.FactionB_Id);
                    }
                    else if (dip.FactionB_Id == factionId && dip.FactionA_Id != factionId)
                    {
                        allies.Add(dip.FactionA_Id);
                    }
                }
            }

            return allies;
        }

        /// <summary>
        /// Faza 7.2 - Qüvvədə olan müqaviləni pozaraq birbaşa müharibə elan edir (Nüfuz cəzası ilə).
        /// </summary>
        public bool BreakTreatyAndDeclareWar(int factionA, int factionB, DatabaseManager dbManager = null)
        {
            return DeclareWar(factionA, factionB, dbManager, breakTreaties: true);
        }

        #endregion

        /// <summary>
        /// Faza 7.1 & 7.2 - Müharibə elan edir.
        /// Əgər breakTreaties=false olarsa və atəşkəs/pakt qüvvədədirsə, müharibə elanı bloklanır və false qaytarır.
        /// Əgər breakTreaties=true olarsa, qüvvədə olan müqavilə pozulur və DiplomaticReputation kəskin (-30 bal) düşür.
        /// Həmçinin, Hərbi İttifaq (Call to Arms) mexanikası ilə hər iki tərəfin müttəfiqləri avtomatik müharibəyə qoşulur.
        /// </summary>
        public bool DeclareWar(int factionA, int factionB, DatabaseManager dbManager = null, bool breakTreaties = false)
        {
            if (factionA == factionB)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return false;
            }

            bool hasActiveCeasefire = IsCeasefireActive(factionA, factionB, db);
            bool hasActiveNAP = IsNonAggressionPactActive(factionA, factionB, db);
            bool hasActiveAlliance = AreAllies(factionA, factionB, db);

            if (!breakTreaties && (hasActiveCeasefire || hasActiveNAP || hasActiveAlliance))
            {
                Debug.LogWarning($"[MilitaryAndDiplomacyController] Müharibə elan edilə bilməz: Fraksiya {factionA} və {factionB} arasında Atəşkəs/Pakt qüvvədədir!");
                return false;
            }

            var alliesOfA = new List<int>();
            var alliesOfB = new List<int>();

            db.RunInTransaction(conn =>
            {
                // Əgər qüvvədə olan müqavilə pozulubsa -> DiplomaticReputation kəskin (-30 bal) düşür!
                if (hasActiveCeasefire || hasActiveNAP || hasActiveAlliance)
                {
                    Nation natA = conn.Table<Nation>().FirstOrDefault(n => n.Id == factionA);
                    if (natA != null)
                    {
                        float oldRep = natA.DiplomaticReputation;
                        natA.DiplomaticReputation = Mathf.Clamp(natA.DiplomaticReputation - 30f, 0f, 100f);
                        conn.Update(natA);

                        LastTreatyBrokenMessage = $"MÜQAVİLƏ POZULDU! Fraksiya #{factionA} qüvvədə olan paktı pozaraq müharibə elan etdi! DiplomaticReputation -30 bal azaldı ({oldRep:F0} -> {natA.DiplomaticReputation:F0})!";
                        Debug.LogWarning($"[MilitaryAndDiplomacyController] {LastTreatyBrokenMessage}");
                        OnTreatyBroken?.Invoke(factionA, factionB);
                    }
                }

                // Əsas iki fraksiya arasında müharibə təyin edilir
                Diplomacy dip = conn.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == factionA && d.FactionB_Id == factionB) ||
                    (d.FactionA_Id == factionB && d.FactionB_Id == factionA));

                if (dip != null)
                {
                    dip.TreatyType = "Müharibə";
                    dip.RelationScore = -100;
                    dip.CeasefireMonthsLeft = 0;
                    dip.NonAggressionMonthsLeft = 0;
                    dip.TreatyDuration = 24;
                    conn.Update(dip);
                }
                else
                {
                    dip = new Diplomacy
                    {
                        FactionA_Id = factionA,
                        FactionB_Id = factionB,
                        RelationScore = -100,
                        TreatyType = "Müharibə",
                        TreatyDuration = 24,
                        CeasefireMonthsLeft = 0,
                        NonAggressionMonthsLeft = 0
                    };
                    conn.Insert(dip);
                }

                // Faza 7.2 - Hərbi İttifaq (Call to Arms):
                // Biri müharibəyə girdikdə, digəri də avtomatik müharibəyə qoşulur
                List<Diplomacy> allDips = conn.Table<Diplomacy>().ToList();
                for (int i = 0; i < allDips.Count; i++)
                {
                    Diplomacy d = allDips[i];
                    if (d.IsAlliance)
                    {
                        if (d.FactionA_Id == factionA && d.FactionB_Id != factionB) alliesOfA.Add(d.FactionB_Id);
                        else if (d.FactionB_Id == factionA && d.FactionA_Id != factionB) alliesOfA.Add(d.FactionA_Id);

                        if (d.FactionA_Id == factionB && d.FactionB_Id != factionA) alliesOfB.Add(d.FactionB_Id);
                        else if (d.FactionB_Id == factionB && d.FactionA_Id != factionA) alliesOfB.Add(d.FactionA_Id);
                    }
                }

                // Fraksiya A-nın müttəfiqləri Fraksiya B-yə müharibə elan edir
                for (int i = 0; i < alliesOfA.Count; i++)
                {
                    int allyId = alliesOfA[i];
                    SetDirectWar(conn, allyId, factionB);
                }

                // Fraksiya B-nin müttəfiqləri Fraksiya A-ya qarşı müdafiə müharibəsinə qoşulur
                for (int i = 0; i < alliesOfB.Count; i++)
                {
                    int allyId = alliesOfB[i];
                    SetDirectWar(conn, allyId, factionA);
                }
            });

            Debug.LogWarning($"[MilitaryAndDiplomacyController] MÜHARİBƏ ELAN EDİLDİ! Fraksiya {factionA} <-> Fraksiya {factionB}");
            OnWarDeclared?.Invoke(factionA, factionB);

            for (int i = 0; i < alliesOfA.Count; i++)
            {
                OnAllyJoinedWar?.Invoke(alliesOfA[i], factionA, factionB);
            }
            for (int i = 0; i < alliesOfB.Count; i++)
            {
                OnAllyJoinedWar?.Invoke(alliesOfB[i], factionB, factionA);
            }

            // Gizli ittifaqı (Secret Pact) olan qubernatorların xəyanətini işə salırıq
            PoliticalController pol = PoliticalController.Instance;
            if (pol != null)
            {
                pol.TriggerSecretPactBetrayal(factionB, factionA, db);
                pol.TriggerSecretPactBetrayal(factionA, factionB, db);
            }

            return true;
        }

        private void SetDirectWar(SQLite.SQLiteConnection conn, int factionX, int factionY)
        {
            if (factionX == factionY) return;

            Diplomacy dip = conn.Table<Diplomacy>().FirstOrDefault(d =>
                (d.FactionA_Id == factionX && d.FactionB_Id == factionY) ||
                (d.FactionA_Id == factionY && d.FactionB_Id == factionX));

            if (dip != null)
            {
                dip.TreatyType = "Müharibə";
                dip.RelationScore = -100;
                dip.CeasefireMonthsLeft = 0;
                dip.NonAggressionMonthsLeft = 0;
                dip.TreatyDuration = 24;
                conn.Update(dip);
            }
            else
            {
                dip = new Diplomacy
                {
                    FactionA_Id = factionX,
                    FactionB_Id = factionY,
                    RelationScore = -100,
                    TreatyType = "Müharibə",
                    TreatyDuration = 24,
                    CeasefireMonthsLeft = 0,
                    NonAggressionMonthsLeft = 0
                };
                conn.Insert(dip);
            }

            Debug.LogWarning($"[MilitaryAndDiplomacyController] MÜTTƏFİQ DÖYÜŞƏ QOŞULDU! Fraksiya #{factionX} və Fraksiya #{factionY} arasında müharibə vəziyyəti yarandı.");
        }

        #endregion
    }
}
