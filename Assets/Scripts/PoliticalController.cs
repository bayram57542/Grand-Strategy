using System;
using System.Collections.Generic;
using System.Text;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.UI;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Baş verən üsyan hadisəsinin təfərrüatlarını saxlayan hesabat strukturu.
    /// </summary>
    public struct RebellionReport
    {
        public int LeaderId;
        public string LeaderName;
        public float LoyaltyAtRebellion;
        public int ProvinceId;
        public string ProvinceName;
        public int PreviousOwnerFactionId;
        public int NewRebelFactionId;
        public int CreatedRebelArmyId;
        public int RebelYouthSoldiers;
        public int RebelAdultSoldiers;
        public float GoldSeized;
        public int Month;
        public int Year;
    }

    /// <summary>
    /// Faza 6D - Taxt Çevrilişi (Coup) nəticə strukturu.
    /// </summary>
    public struct CoupResult
    {
        public bool Success;
        public int NationId;
        public string OldDynastyName;
        public string NewDynastyName;
        public int NewRulerId;
        public string NewRulerName;
        public string Message;
    }

    /// <summary>
    /// Faza 6D - Genişlənmə Riski (Overextension) hesabat strukturu.
    /// </summary>
    public struct OverextensionReport
    {
        public int NationId;
        public int TotalProvinces;
        public int DirectControlledProvinces;
        public int MaxDirectControl;
        public int OverextensionCount;
        public float PenaltyRate;
    }

    /// <summary>
    /// Faza 6B-6D - Sədaqət, Üsyan və Taxt Çevrilişi Motoru (PoliticalController):
    /// Hər aylıq Tick döngüsündə liderlərin Sədaqətini (Loyalty) yoxlayır.
    /// Sədaqət 30-dan aşağı düşdükdə (Loyalty < 30), həmin qubernator üsyan qaldırır (IsRebel = true),
    /// təyin olunduğu əyalətin qızılını və işçi qüvvəsini istifadə edərək SQLite-da özünə yeni
    /// "Üsyançı Ordu" (Army) yaradır və əyaləti Kralın nəzarətindən çıxararaq ələ keçirir.
    /// Faza 6D: Taxt çevrilişi (ExecuteCoup) və Overextension cəzası tətbiq edir.
    /// </summary>
    public class PoliticalController : MonoBehaviour
    {
        public const float DefaultRebellionThreshold = 30f;
        public const int DefaultRebelFactionId = 99;

        private static PoliticalController _instance;
        public static PoliticalController Instance =>
            _instance != null ? _instance : (_instance = FindAnyObjectByType<PoliticalController>());

        [Header("Sədaqət və Üsyan Konfiqurasiyası")]
        [Tooltip("Sədaqət bu həddən aşağı düşdükdə (Loyalty < 30) qubernator üsyan qaldırır")]
        [SerializeField, Range(1f, 99f)] private float rebellionLoyaltyThreshold = DefaultRebellionThreshold;

        [Header("Dinamik Sədaqət Qaydaları (Faza 6B Təkmilləşdirmə)")]
        [Tooltip("Əgər əyalətdə Food <= 0 olarsa, qubernatorun sədaqəti bu qədər azalır (-10)")]
        [SerializeField] private float starvationLoyaltyPenalty = 10f;

        [Tooltip("Əgər əyalətin ProductivityMultiplier >= 1.5f (Kanal və s.) olarsa, sədaqət bonusu (+2)")]
        [SerializeField] private float developmentLoyaltyBonus = 2f;

        [Tooltip("Əgər əyalətin potensial gücü (Youths+Adults) Kralın hərbi gücünün 80%-dən çoxdursa, opportunizm cəzası (-10)")]
        [SerializeField] private float opportunismLoyaltyPenalty = 10f;

        [Tooltip("Opportunizm üçün güc nisbəti həddi (0.8 = 80%)")]
        [SerializeField] private float opportunismPowerRatio = 0.8f;

        [Tooltip("Üsyan zamanı əyalətdəki Gənclərin (Youths) hansı hissəsi Üsyançı Orduya cəlb olunur")]
        [SerializeField, Range(0.1f, 0.9f)] private float rebelDraftYouthRatio = 0.50f;

        [Tooltip("Üsyan zamanı əyalətdəki Yetişkinlərin (Adults) hansı hissəsi Üsyançı Orduya cəlb olunur")]
        [SerializeField, Range(0.1f, 0.9f)] private float rebelDraftAdultRatio = 0.30f;

        [Tooltip("Üsyan zamanı əyalət xəzinəsindən (Gold) üsyançı ordunun təchizatı üçün müsadirə olunan pay")]
        [SerializeField, Range(0.1f, 1.0f)] private float rebelTreasurySeizeRatio = 0.50f;

        [Tooltip("Üsyançı qubernatorun yaratdığı müstəqil/üsyançı fraksiya ID-si")]
        [SerializeField] private int rebelFactionId = DefaultRebelFactionId;

        public float RebellionLoyaltyThreshold => rebellionLoyaltyThreshold;
        public float StarvationLoyaltyPenalty => starvationLoyaltyPenalty;
        public float DevelopmentLoyaltyBonus => developmentLoyaltyBonus;
        public float OpportunismLoyaltyPenalty => opportunismLoyaltyPenalty;
        public float OpportunismPowerRatio => opportunismPowerRatio;
        public int RebelFactionId => rebelFactionId;

        public event Action<RebellionReport> OnRebellionTriggered;
        public event Action<CoupResult> OnCoupExecuted;
        public event Action<OverextensionReport> OnOverextensionUpdated;
        public string LastBetrayalMessage { get; private set; }
        public event Action<Leader, int> OnSecretPactBetrayalTriggered;
        public List<RebellionReport> LastRebellions { get; } = new List<RebellionReport>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>
        /// Siyasi Fürsətçilik Motoru (ProcessMonthlyPolitics): TimeManager / GameManager aylıq Tick döngüsündən çağırılır.
        /// Kralın və qubernatorların güc nisbətini (Power Ratio) hesablayır, fürsətçi qubernatorların
        /// sədaqətini azaldır və sədaqət 30-dan aşağı düşdükdə müstəqillik/üsyan elan edir.
        /// </summary>
        public List<RebellionReport> ProcessMonthlyPolitics()
        {
            return CheckRebellions();
        }

        public List<RebellionReport> ProcessMonthlyPolitics(DatabaseManager dbManager, int month, int year)
        {
            return CheckRebellions(dbManager, month, year, applyDynamicLoyalty: true);
        }

        /// <summary>
        /// TimeManager / GameManager-in aylıq Tick döngüsündən çağırılan əsas üsyan yoxlama funksiyası.
        /// </summary>
        public List<RebellionReport> CheckRebellions()
        {
            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            GameManager gm = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            int month = gm != null && gm.CurrentMonth > 0 ? gm.CurrentMonth : 1;
            int year = gm != null && gm.CurrentYear > 0 ? gm.CurrentYear : 1066;

            return CheckRebellions(db, month, year, applyDynamicLoyalty: true);
        }

        /// <summary>
        /// Hər ay liderlərin Sədaqətini (Loyalty) dinamik qaydalarla yoxlayır:
        /// 1. Aclıq Cəzası: Əgər əyalətdə Food <= 0 olarsa, Loyalty -= 10.
        /// 2. İnkişaf Bonusu: Əgər ProductivityMultiplier >= 1.5f (Kanal və s.), Loyalty += 2.
        /// 3. Güc Nisbəti Cəzası (Opportunism): Əgər əyalətin potensial gücü (Youths+Adults) > Kralın hərbi gücünün 80%-dən çoxdursa, Loyalty -= 15.
        /// 4. Sədaqət həmişə Mathf.Clamp(loyalty, 0, 100) ilə saxlanılır.
        /// Əgər Loyalty 30-dan aşağı düşərsə, IsRebel = true olur, qubernator olduğu əyalətin qızılını/işçi gücünü
        /// istifadə edərək SQLite-da özünə yeni "Üsyançı Ordu" (Army) yaradır və əyaləti ələ keçirir.
        /// </summary>
        public List<RebellionReport> CheckRebellions(
            DatabaseManager dbManager,
            int month,
            int year,
            bool applyDynamicLoyalty = true)
        {
            LastRebellions.Clear();

            if (dbManager == null)
            {
                dbManager = DatabaseManager.Instance != null
                    ? DatabaseManager.Instance
                    : FindAnyObjectByType<DatabaseManager>();
            }

            if (dbManager == null)
            {
                Debug.LogError("[PoliticalController] CheckRebellions uğursuz oldu: DatabaseManager tapılmadı!");
                return LastRebellions;
            }

            if (!dbManager.IsConnected && !dbManager.InitializeDatabase())
            {
                Debug.LogError("[PoliticalController] CheckRebellions uğursuz oldu: SQLite bazası bağlıdır!");
                return LastRebellions;
            }

            var logBuilder = new StringBuilder();
            List<RebellionReport> rebellionsThisMonth = new List<RebellionReport>();

            dbManager.RunInTransaction(conn =>
            {
                List<Leader> leaders = conn.Table<Leader>().ToList();
                List<Province> provinces = conn.Table<Province>().ToList();
                List<Population> populations = conn.Table<Population>().ToList();
                List<Economy> economies = conn.Table<Economy>().ToList();
                List<Nation> nations = conn.Table<Nation>().ToList();
                List<Army> armies = conn.Table<Army>().ToList();

                // Kralın ümumi aktiv hərbi gücünü (bütün aktiv ordu birliklərinin cəmini) hesablayırıq
                Leader kingLeader = leaders.Find(l => l != null && l.IsKing);
                int kingFactionId = 1;
                if (kingLeader != null && kingLeader.ParsedProvinceId.HasValue)
                {
                    Province kingProv = provinces.Find(p => p.Id == kingLeader.ParsedProvinceId.Value);
                    if (kingProv != null)
                    {
                        kingFactionId = kingProv.OwnerFactionId;
                    }
                }

                int kingTotalMilitaryPower = 0;
                for (int a = 0; a < armies.Count; a++)
                {
                    if (armies[a].OwnerFactionId == kingFactionId)
                    {
                        kingTotalMilitaryPower += armies[a].TotalSoldiers;
                    }
                }

                // Faza 6D: Overextension (Genişlənmə Riski) göstəricilərini yeniləyirik
                Nation kingNation = nations.Find(n => n.Id == kingFactionId) ?? (nations.Count > 0 ? nations[0] : null);
                if (kingNation != null)
                {
                    var governorProvinceIds = new HashSet<int>();
                    for (int g = 0; g < leaders.Count; g++)
                    {
                        Leader l = leaders[g];
                        if (l != null && !l.IsKing && !l.IsRebel && l.ParsedProvinceId.HasValue)
                        {
                            governorProvinceIds.Add(l.ParsedProvinceId.Value);
                        }
                    }

                    int directCount = 0;
                    int ownedCount = 0;
                    for (int p = 0; p < provinces.Count; p++)
                    {
                        if (provinces[p].OwnerFactionId == kingNation.Id)
                        {
                            ownedCount++;
                            if (!governorProvinceIds.Contains(provinces[p].Id))
                            {
                                directCount++;
                            }
                        }
                    }

                    kingNation.ControlledProvinceCount = ownedCount;
                    kingNation.DirectControlledProvinceCount = directCount;
                    conn.Update(kingNation);
                }

                for (int i = 0; i < leaders.Count; i++)
                {
                    Leader leader = leaders[i];
                    if (leader == null || leader.IsKing)
                    {
                        // Kralın sədaqəti həmişə 100 olaraq qalır (əgər sağdırsa)
                        if (leader != null && leader.IsKing && leader.Loyalty < 100f && leader.Loyalty > 0f)
                        {
                            leader.Loyalty = 100f;
                            conn.Update(leader);
                        }
                        continue;
                    }

                    // Artıq üsyan etmiş liderləri təkrar üsyana salmırıq
                    if (leader.IsRebel)
                    {
                        continue;
                    }

                    int? targetProvId = leader.ParsedProvinceId;
                    Province rebelProvince = null;
                    if (targetProvId.HasValue)
                    {
                        rebelProvince = provinces.Find(p => p.Id == targetProvId.Value);
                    }

                    if (rebelProvince == null && provinces.Count > 0)
                    {
                        rebelProvince = provinces.Find(p => p.Id != 1) ?? provinces[provinces.Count - 1];
                        leader.AssignedProvinceId = rebelProvince.Id.ToString();
                    }

                    // Dinamik Sədaqət Motoru (Faza 6B Təkmilləşdirmə):
                    if (applyDynamicLoyalty && rebelProvince != null)
                    {
                        Population pop = populations.Find(p => p.ProvinceId == rebelProvince.Id);
                        int provinceManpower = pop != null
                            ? (pop.Youths + pop.Adults)
                            : (rebelProvince.Youths + rebelProvince.Adults);

                        float loyaltyBefore = leader.Loyalty;

                        // 1. Aclıq Cəzası: Əgər qubernatorun idarə etdiyi əyalətdə Food 0 və ya mənfidirsə, Loyalty -= 10
                        if (rebelProvince.Food <= 0f)
                        {
                            leader.Loyalty -= starvationLoyaltyPenalty;
                        }

                        // 2. İnkişaf Bonusu: Əgər əyalətin ProductivityMultiplier >= 1.5f (Kanal və s. çəkilibsə), Loyalty += 2
                        if (rebelProvince.ProductivityMultiplier >= 1.5f)
                        {
                            leader.Loyalty += developmentLoyaltyBonus;
                        }

                        // 3. Güc Nisbəti Cəzası (Opportunism): Əgər əyalətdəki potensial güc (Youths + Adults) > Kralın aktiv hərbi gücünün 80%-dən çoxdursa, Loyalty -= 10
                        if (kingTotalMilitaryPower > 0 && provinceManpower > kingTotalMilitaryPower * opportunismPowerRatio)
                        {
                            leader.Loyalty -= opportunismLoyaltyPenalty;
                        }

                        // 4. Overextension Cəzası (Faza 6D): Əgər dövlətin birbaşa idarə etdiyi əyalət sayı > 3 olarsa,
                        // hər artıq əyalət başına qubernatorların sədaqəti -5 bal düşür.
                        if (kingNation != null && kingNation.OverextensionCount > 0)
                        {
                            leader.Loyalty -= (kingNation.OverextensionCount * 5.0f);
                        }

                        leader.Loyalty = Mathf.Clamp(leader.Loyalty, 0f, 100f);

                        logBuilder.AppendLine(
                            $"[PoliticalController] Dinamik Sədaqət Hesablandı -> Lider '{leader.Name}' ({rebelProvince.Name}): " +
                            $"Sədaqət {loyaltyBefore:F1} -> {leader.Loyalty:F1} | " +
                            $"Food={rebelProvince.Food:F1} (Aclıq Cəzası: {rebelProvince.Food <= 0f}), " +
                            $"Prod={rebelProvince.ProductivityMultiplier:F2}x (İnkişaf Bonusu: {rebelProvince.ProductivityMultiplier >= 1.5f}), " +
                            $"Əyalət Gücü={provinceManpower} vs Kral Gücü={kingTotalMilitaryPower} (80% Hədd: {kingTotalMilitaryPower * opportunismPowerRatio:F0}, Opportunizm: {provinceManpower > kingTotalMilitaryPower * opportunismPowerRatio}).");
                    }

                    // Faza 7.1: Vassalların Gizli Əlaqələri (Secret Pacts)
                    // Əgər Krala tabe olan qubernatorun sədaqəti 50-dən aşağıdırsa (Loyalty < 50) və
                    // xaricdə Kralın aktiv hərbi gücündən daha güclü bir düşmən dövlət (AI) varsa,
                    // həmin qubernator o düşmən dövlətlə gizli ittifaq qurur (SecretPactFactionId = enemyFactionId).
                    if (leader.Loyalty < 50f && !leader.SecretPactFactionId.HasValue)
                    {
                        int candidateEnemyFactionId = -1;
                        int highestForeignMilitaryPower = -1;

                        for (int nIdx = 0; nIdx < nations.Count; nIdx++)
                        {
                            int fId = nations[nIdx].Id;
                            if (fId == kingFactionId)
                            {
                                continue;
                            }

                            int foreignPower = 0;
                            for (int aIdx = 0; aIdx < armies.Count; aIdx++)
                            {
                                if (armies[aIdx].OwnerFactionId == fId)
                                {
                                    foreignPower += armies[aIdx].TotalSoldiers;
                                }
                            }

                            if (foreignPower > highestForeignMilitaryPower)
                            {
                                highestForeignMilitaryPower = foreignPower;
                                candidateEnemyFactionId = fId;
                            }
                        }

                        if (candidateEnemyFactionId > 0 && highestForeignMilitaryPower > kingTotalMilitaryPower)
                        {
                            leader.SecretPactFactionId = candidateEnemyFactionId;
                            logBuilder.AppendLine(
                                $"[PoliticalController] GİZLİ MÜQAVİLƏ QURULDU! Qubernator '{leader.Name}' (Sədaqət: {leader.Loyalty:F1} < 50) " +
                                $"daha güclü Fraksiya #{candidateEnemyFactionId} (Hərbi Güc: {highestForeignMilitaryPower} vs Kral Gücü: {kingTotalMilitaryPower}) ilə gizli ittifaq bağladı!");
                        }
                    }

                    // Əgər liderin gizli müqaviləsi varsa:
                    if (leader.SecretPactFactionId.HasValue)
                    {
                        int pactFaction = leader.SecretPactFactionId.Value;
                        List<Diplomacy> activeDiplomacies = conn.Table<Diplomacy>().ToList();
                        bool isAtWar = activeDiplomacies.Exists(d =>
                            ((d.FactionA_Id == kingFactionId && d.FactionB_Id == pactFaction) ||
                             (d.FactionA_Id == pactFaction && d.FactionB_Id == kingFactionId)) &&
                            d.TreatyType != null && d.TreatyType.IndexOf("Müharibə", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (isAtWar)
                        {
                            // Müharibə dövründə qubernator arxadan zərbə vuraraq xəyanət edir!
                            if (rebelProvince != null)
                            {
                                rebelProvince.OwnerFactionId = pactFaction;
                                conn.Update(rebelProvince);
                            }

                            int? assignedArmyId = leader.ParsedArmyId;
                            Army govArmy = null;
                            if (assignedArmyId.HasValue)
                            {
                                govArmy = armies.Find(a => a.Id == assignedArmyId.Value);
                            }
                            else if (rebelProvince != null)
                            {
                                govArmy = armies.Find(a => a.LocationProvinceId == rebelProvince.Id && a.OwnerFactionId == kingFactionId);
                                if (govArmy != null)
                                {
                                    leader.AssignedArmyId = govArmy.Id.ToString();
                                }
                            }

                            if (govArmy != null)
                            {
                                govArmy.OwnerFactionId = pactFaction;
                                conn.Update(govArmy);
                            }

                            leader.Loyalty = 100f;
                            leader.SecretPactFactionId = null;
                            conn.Update(leader);

                            string bMsg = $"XƏYANƏT! {leader.Name} düşmən tərəfə keçdi!";
                            LastBetrayalMessage = bMsg;
                            logBuilder.AppendLine($"[PoliticalController] {bMsg} (Müharibə anında xəyanət baş verdi: Fraksiya #{pactFaction})");
                            continue;
                        }

                        // Müharibə hələ başlamayıbsa, gizli müqaviləli qubernator müstəqil üsyan qaldırmır, müharibə anını gözləyir
                        conn.Update(leader);
                        continue;
                    }

                    // Əgər Loyalty 30-dan aşağı düşərsə -> ÜSYAN / MÜSTƏQİLLİK BAŞLAYIR!
                    if (leader.Loyalty < rebellionLoyaltyThreshold)
                    {
                        leader.IsIndependent = true;

                        if (rebelProvince != null)
                        {
                            Population pop = populations.Find(p => p.ProvinceId == rebelProvince.Id);
                            Economy eco = economies.Find(e => e.ProvinceId == rebelProvince.Id);

                            int currentYouths = pop != null ? pop.Youths : rebelProvince.Youths;
                            int currentAdults = pop != null ? pop.Adults : rebelProvince.Adults;

                            int draftedYouths = Mathf.Max(100, Mathf.RoundToInt(currentYouths * rebelDraftYouthRatio));
                            int draftedAdults = Mathf.Max(200, Mathf.RoundToInt(currentAdults * rebelDraftAdultRatio));

                            draftedYouths = Mathf.Min(currentYouths, draftedYouths);
                            draftedAdults = Mathf.Min(currentAdults, draftedAdults);

                            float goldSeized = Mathf.Max(0f, rebelProvince.Gold * rebelTreasurySeizeRatio);
                            rebelProvince.Gold = Mathf.Max(0f, rebelProvince.Gold - goldSeized);

                            // İşçi qüvvəsini əyalətdən çıxırıq
                            rebelProvince.Youths = Mathf.Max(0, currentYouths - draftedYouths);
                            rebelProvince.Adults = Mathf.Max(0, currentAdults - draftedAdults);

                            if (pop != null)
                            {
                                pop.Youths = rebelProvince.Youths;
                                pop.Adults = rebelProvince.Adults;
                                conn.Update(pop);
                            }

                            int prevOwnerFaction = rebelProvince.OwnerFactionId;
                            int assignedRebelFactionId = rebelFactionId + leader.Id;

                            // Əyalət Kralın/əvvəlki dövlətin nəzarətindən çıxır, üsyançı qubernatorun nəzarətinə keçir
                            rebelProvince.OwnerFactionId = assignedRebelFactionId;
                            conn.Update(rebelProvince);

                            // Əyalətin iqtisadi istehsalını azalmış işçi qüvvəsinə görə yeniləyirik
                            if (eco != null)
                            {
                                float prod = rebelProvince.ProductivityMultiplier > 0f ? rebelProvince.ProductivityMultiplier : 1.0f;
                                eco.FoodProduction = (rebelProvince.Adults / 100f) * rebelProvince.DevelopmentLevel * prod;
                                eco.RawMaterials = (rebelProvince.Adults / 200f) * rebelProvince.DevelopmentLevel * prod;
                                eco.LocalTaxIncome = ((rebelProvince.Adults * 0.5f) + (eco.LuxuryGoods * 2.0f)) * prod;
                                conn.Update(eco);
                            }

                            // Nation cədvəlini sinxronlaşdırırıq
                            if (rebelProvince.Id >= 1 && rebelProvince.Id <= nations.Count)
                            {
                                Nation nat = nations[rebelProvince.Id - 1];
                                nat.Gold = rebelProvince.Gold;
                                nat.Food = rebelProvince.Food;
                                nat.Youths = rebelProvince.Youths;
                                nat.Adults = rebelProvince.Adults;
                                conn.Update(nat);
                            }

                            // Faza 6D: Əgər üsyançı paytaxtı (Province #1 - Anadolu) ələ keçiribsə -> Taxt Çevrilişi baş verir!
                            if (rebelProvince.Id == 1)
                            {
                                string newDynasty = $"{leader.Name}-nın Xanlığı";
                                Nation capitalNation = nations.Find(n => n.Id == prevOwnerFaction) ?? (nations.Count > 0 ? nations[0] : null);
                                if (capitalNation != null)
                                {
                                    string oldDynasty = capitalNation.RulingDynastyName;
                                    capitalNation.RulingDynastyName = newDynasty;
                                    capitalNation.Name = newDynasty;
                                    conn.Update(capitalNation);
                                    logBuilder.AppendLine($"[PoliticalController] TAXT ÇEVRİLİŞİ! Paytaxt ələ keçirildi -> Yeni Hakim Sülalə: '{newDynasty}' (Köhnə: '{oldDynasty}')");
                                }
                            }

                            // SQLite-da qubernatorun özünə aid yeni "Üsyançı Ordu" (Army) yaradırıq
                            var rebelArmy = new Army
                            {
                                OwnerFactionId = assignedRebelFactionId,
                                LocationProvinceId = rebelProvince.Id,
                                YouthSoldiers = draftedYouths,
                                AdultSoldiers = draftedAdults,
                                Morale = 1.0f
                            };
                            conn.Insert(rebelArmy);

                            // Üsyançı qubernatoru bu yeni Üsyançı Ordunun sərkərdəsi təyin edirik
                            leader.AssignedArmyId = rebelArmy.Id.ToString();
                            conn.Update(leader);

                            // Kral fraksiyası (1) ilə Üsyançı fraksiya arasında avtomatik "Müharibə" elan edirik
                            var rebelWar = new Diplomacy
                            {
                                FactionA_Id = 1,
                                FactionB_Id = assignedRebelFactionId,
                                RelationshipScore = -100,
                                TreatyType = "Müharibə (Üsyan)",
                                TreatyDuration = 12
                            };
                            conn.Insert(rebelWar);

                            var rep = new RebellionReport
                            {
                                LeaderId = leader.Id,
                                LeaderName = leader.Name,
                                LoyaltyAtRebellion = leader.Loyalty,
                                ProvinceId = rebelProvince.Id,
                                ProvinceName = rebelProvince.Name,
                                PreviousOwnerFactionId = prevOwnerFaction,
                                NewRebelFactionId = assignedRebelFactionId,
                                CreatedRebelArmyId = rebelArmy.Id,
                                RebelYouthSoldiers = draftedYouths,
                                RebelAdultSoldiers = draftedAdults,
                                GoldSeized = goldSeized,
                                Month = month,
                                Year = year
                            };

                            rebellionsThisMonth.Add(rep);
                            logBuilder.AppendLine(
                                $"[PoliticalController] ÜSYAN BAŞ VERDİ! (Ay {month:D2}/{year:D4}) -> " +
                                $"Lider '{leader.Name}' (Sədaqət: {leader.Loyalty:F1} < {rebellionLoyaltyThreshold:F0}) IsRebel=true oldu! " +
                                $"'{rebelProvince.Name}' əyaləti ələ keçirildi (Köhnə Faction: {prevOwnerFaction} -> Yeni Üsyançı Faction: {assignedRebelFactionId}), " +
                                $"Müsadirə edilən Qızıl: {goldSeized:F1}, Yaradılan Üsyançı Ordu #{rebelArmy.Id}: " +
                                $"{rebelArmy.TotalSoldiers} əsgər ({draftedYouths} Elit Süvari, {draftedAdults} Ağır Piyada).");
                        }
                    }
                    else
                    {
                        conn.Update(leader);
                    }
                }
            });

            if (logBuilder.Length > 0)
            {
                Debug.LogWarning(logBuilder.ToString().TrimEnd());
            }

            for (int i = 0; i < rebellionsThisMonth.Count; i++)
            {
                RebellionReport rep = rebellionsThisMonth[i];
                LastRebellions.Add(rep);
                OnRebellionTriggered?.Invoke(rep);
            }

            // Əgər xəritə səhnəsindəyiksə və üsyan baş veribsə, əyalət rənglərini yeniləyirik
            if (rebellionsThisMonth.Count > 0)
            {
                MapController mapCtrl = MapController.Instance != null
                    ? MapController.Instance
                    : FindAnyObjectByType<MapController>();
                if (mapCtrl != null)
                {
                    mapCtrl.SpawnProvincesFromDatabase();
                }
            }

            return LastRebellions;
        }

        /// <summary>
        /// Verilmiş liderin Sədaqət (Loyalty) göstəricisini birbaşa dəyişdirir və SQLite-da yadda saxlayır.
        /// </summary>
        public bool SetLeaderLoyalty(string leaderNameOrId, float newLoyalty)
        {
            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return false;
            }

            int? parsedId = Leader.ParseNullableId(leaderNameOrId);
            Leader target = null;

            List<Leader> all = db.Connection.Table<Leader>().ToList();
            for (int i = 0; i < all.Count; i++)
            {
                if ((parsedId.HasValue && all[i].Id == parsedId.Value) ||
                    string.Equals(all[i].Name, leaderNameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    target = all[i];
                    break;
                }
            }

            if (target == null)
            {
                return false;
            }

            target.Loyalty = Mathf.Clamp(newLoyalty, 0f, 100f);
            db.Connection.Update(target);
            return true;
        }

        /// <summary>
        /// Qlobal Taxt Boşluğu Cəzası (Faza 6B Təkmilləşdirmə):
        /// Kral döyüşdə öldükdə SQLite bazasındakı bütün qubernatorların (Liderlərin) Loyalty dəyəri
        /// qlobal olaraq -30 bal aşağı salınır və dəyişikliklər avtomatik bazaya yazılır.
        /// </summary>
        public int ApplyThroneCrisisPenalty(DatabaseManager dbManager)
        {
            if (dbManager == null)
            {
                dbManager = DatabaseManager.Instance != null
                    ? DatabaseManager.Instance
                    : FindAnyObjectByType<DatabaseManager>();
            }

            if (dbManager == null || (!dbManager.IsConnected && !dbManager.InitializeDatabase()))
            {
                Debug.LogError("[PoliticalController] ApplyThroneCrisisPenalty xətası: DatabaseManager işlək deyil!");
                return 0;
            }

            int affectedGovernors = 0;
            dbManager.RunInTransaction(conn =>
            {
                List<Leader> leaders = conn.Table<Leader>().ToList();
                for (int i = 0; i < leaders.Count; i++)
                {
                    Leader leader = leaders[i];
                    if (leader == null)
                    {
                        continue;
                    }

                    if (leader.IsKing)
                    {
                        leader.Loyalty = 0f;
                        conn.Update(leader);
                    }
                    else
                    {
                        float oldLoyalty = leader.Loyalty;
                        leader.Loyalty = Mathf.Clamp(leader.Loyalty - 30f, 0f, 100f);
                        conn.Update(leader);
                        affectedGovernors++;
                        Debug.LogWarning($"[PoliticalController] TAXT BOŞLUĞU BÖHRANI -> Qubernator '{leader.Name}' Sədaqəti: {oldLoyalty:F1} -> {leader.Loyalty:F1} (-30 cəza tətbiq edildi).");
                    }
                }
            });

            return affectedGovernors;
        }

        #region Faza 6D - Taxt Çevrilişi və Genişlənmə Riski (Overextension)

        /// <summary>
        /// Faza 6D - Genişlənmə Riski (Overextension) hesablaması:
        /// Dövlətə aid əyalətləri və qubernatorsuz (birbaşa idarə olunan) əyalətlərin sayını tapır.
        /// Əgər birbaşa idarə > MaxDirectControl (3) olarsa, OverextensionCount və PenaltyRate hesablanır.
        /// </summary>
        public OverextensionReport CalculateNationOverextension(DatabaseManager dbManager = null, int nationId = 1)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return default;
            }

            OverextensionReport report = default;
            db.RunInTransaction(conn =>
            {
                List<Province> ownedProvinces = conn.Table<Province>().Where(p => p.OwnerFactionId == nationId).ToList();
                List<Leader> leaders = conn.Table<Leader>().ToList();

                var governorProvinceIds = new HashSet<int>();
                for (int i = 0; i < leaders.Count; i++)
                {
                    Leader l = leaders[i];
                    if (l != null && !l.IsKing && !l.IsRebel && l.ParsedProvinceId.HasValue)
                    {
                        governorProvinceIds.Add(l.ParsedProvinceId.Value);
                    }
                }

                int directCount = 0;
                for (int i = 0; i < ownedProvinces.Count; i++)
                {
                    if (!governorProvinceIds.Contains(ownedProvinces[i].Id))
                    {
                        directCount++;
                    }
                }

                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == nationId)
                                ?? conn.Table<Nation>().FirstOrDefault();

                int maxDirect = nation != null ? nation.MaxDirectControl : 3;
                int overextension = Mathf.Max(0, directCount - maxDirect);
                float penaltyRate = overextension * 0.05f;

                if (nation != null)
                {
                    nation.ControlledProvinceCount = ownedProvinces.Count;
                    nation.DirectControlledProvinceCount = directCount;
                    conn.Update(nation);
                }

                report = new OverextensionReport
                {
                    NationId = nationId,
                    TotalProvinces = ownedProvinces.Count,
                    DirectControlledProvinces = directCount,
                    MaxDirectControl = maxDirect,
                    OverextensionCount = overextension,
                    PenaltyRate = penaltyRate
                };
            });

            OnOverextensionUpdated?.Invoke(report);
            return report;
        }

        /// <summary>
        /// Faza 6D - Taxt Çevrilişi (ExecuteCoup):
        /// Yeni lider taxta çıxır, köhnə kral devrilir və dövlətin rəsmi hakim sülalə adı avtomatik dəyişir.
        /// Dəyişiklik SQLite bazasına yazılır və Qlobal Xəritə UI-da dərhal yenilənir.
        /// </summary>
        public CoupResult ExecuteCoup(string newRulerLeaderNameOrId, string customDynastyName = null, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return new CoupResult { Success = false, Message = "DatabaseManager hazır deyil." };
            }

            int? parsedId = Leader.ParseNullableId(newRulerLeaderNameOrId);
            Leader target = null;
            List<Leader> all = db.Connection.Table<Leader>().ToList();
            for (int i = 0; i < all.Count; i++)
            {
                if ((parsedId.HasValue && all[i].Id == parsedId.Value) ||
                    string.Equals(all[i].Name, newRulerLeaderNameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    target = all[i];
                    break;
                }
            }

            if (target == null)
            {
                return new CoupResult { Success = false, Message = $"Lider '{newRulerLeaderNameOrId}' tapılmadı." };
            }

            return ExecuteCoup(target.Id, customDynastyName, db);
        }

        /// <summary>
        /// int Leader ID ilə Taxt Çevrilişini icra edir.
        /// </summary>
        public CoupResult ExecuteCoup(int newRulerLeaderId, string customDynastyName = null, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return new CoupResult { Success = false, Message = "DatabaseManager hazır deyil." };
            }

            CoupResult result = default;
            db.RunInTransaction(conn =>
            {
                Leader newRuler = conn.Table<Leader>().FirstOrDefault(l => l.Id == newRulerLeaderId);
                if (newRuler == null)
                {
                    result = new CoupResult { Success = false, Message = $"Lider #{newRulerLeaderId} tapılmadı." };
                    return;
                }

                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == 1)
                                ?? conn.Table<Nation>().FirstOrDefault();

                if (nation == null)
                {
                    result = new CoupResult { Success = false, Message = "Dövlət tapılmadı." };
                    return;
                }

                string oldDynasty = nation.RulingDynastyName;
                string resolvedDynasty = !string.IsNullOrWhiteSpace(customDynastyName)
                    ? customDynastyName
                    : $"{newRuler.Name}-nın Xanlığı";

                // Köhnə kral devrilir
                List<Leader> allLeaders = conn.Table<Leader>().ToList();
                for (int i = 0; i < allLeaders.Count; i++)
                {
                    if (allLeaders[i].IsKing && allLeaders[i].Id != newRuler.Id)
                    {
                        allLeaders[i].Loyalty = 0f;
                        allLeaders[i].IsRuler = false;
                        allLeaders[i].AssignedArmyId = null;
                        allLeaders[i].AssignedProvinceId = null; // Devrilmiş hökmdarın əyalət təyinatı ləğv edilir
                        conn.Update(allLeaders[i]);
                    }
                }

                // Yeni lider taxta çıxır və hakimiyyəti ələ alır
                newRuler.Loyalty = 100f;
                newRuler.IsIndependent = false;
                newRuler.IsRuler = true;
                newRuler.AssignedProvinceId = null; // Hökmdar bütöv dövlətə rəhbərlik edir, ayrıca əyalət qubernatoru deyil!
                conn.Update(newRuler);

                // Dövlətin rəsmi hakim sülalə adı bazada yenilənir
                nation.RulingDynastyName = resolvedDynasty;
                nation.Name = resolvedDynasty;
                conn.Update(nation);

                result = new CoupResult
                {
                    Success = true,
                    NationId = nation.Id,
                    OldDynastyName = oldDynasty,
                    NewDynastyName = resolvedDynasty,
                    NewRulerId = newRuler.Id,
                    NewRulerName = newRuler.Name,
                    Message = $"Taxt çevrilişi baş tutdu! Yeni Hakim Sülalə: '{resolvedDynasty}' (Köhnə: '{oldDynasty}'), Yeni Hökmdar: '{newRuler.Name}'"
                };

                Debug.LogWarning($"[PoliticalController] TAXT ÇEVRİLİŞİ! Köhnə: '{oldDynasty}' -> Yeni Sülalə: '{resolvedDynasty}', Hökmdar: '{newRuler.Name}'.");
            });

            if (result.Success)
            {
                OnCoupExecuted?.Invoke(result);
                UIManager ui = UIManager.Instance != null ? UIManager.Instance : FindAnyObjectByType<UIManager>();
                if (ui != null)
                {
                    ui.RefreshDateTrackerUI();
                }
            }

            return result;
        }

        #endregion

        #region Faza 7.1 - Vassalların Gizli Əlaqələri (Secret Pacts) və Xəyanət Sistemi

        /// <summary>
        /// Faza 7.1 - Vassal ilə xarici dövlət arasında birbaşa gizli ittifaq bağlayır.
        /// </summary>
        public bool FormSecretPact(string leaderNameOrId, int enemyFactionId, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                return false;
            }

            int? parsedId = Leader.ParseNullableId(leaderNameOrId);
            Leader target = null;
            List<Leader> all = db.Connection.Table<Leader>().ToList();
            for (int i = 0; i < all.Count; i++)
            {
                if ((parsedId.HasValue && all[i].Id == parsedId.Value) ||
                    string.Equals(all[i].Name, leaderNameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    target = all[i];
                    break;
                }
            }

            if (target == null)
            {
                return false;
            }

            target.SecretPactFactionId = enemyFactionId;
            db.Connection.Update(target);
            return true;
        }

        /// <summary>
        /// Faza 7.1 - Xəyanətin İşə Düşməsi:
        /// Kral həmin düşmən dövlətə müharibə elan etdikdə (və ya düşmən Krala hücum etdikdə / aralarında müharibə başladıqda):
        /// Gizli müqaviləsi olan qubernator dərhal Krala arxadan zərbə vuraraq xəyanət edir!
        /// İdarə etdiyi əyalət və ordusu ilə birlikdə birbaşa həmin düşmən dövlətin tərkibinə keçir (Province və Army-nin OwnerFactionId-si dəyişir).
        /// Ekranda "XƏYANƏT! [Vassal Adı] düşmən tərəfə keçdi!" xəbərdarlığı çıxır.
        /// </summary>
        public List<Leader> TriggerSecretPactBetrayal(int enemyFactionId, int kingFactionId = 1, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || (!db.IsConnected && !db.InitializeDatabase()))
            {
                Debug.LogError("[PoliticalController] TriggerSecretPactBetrayal: DatabaseManager hazır deyil.");
                return new List<Leader>();
            }

            var betrayedLeaders = new List<Leader>();

            db.RunInTransaction(conn =>
            {
                List<Leader> leaders = conn.Table<Leader>().ToList();
                List<Province> provinces = conn.Table<Province>().ToList();
                List<Army> armies = conn.Table<Army>().ToList();

                for (int i = 0; i < leaders.Count; i++)
                {
                    Leader leader = leaders[i];
                    if (leader == null || leader.IsKing || leader.IsRebel)
                    {
                        continue;
                    }

                    if (leader.SecretPactFactionId.HasValue && leader.SecretPactFactionId.Value == enemyFactionId)
                    {
                        // Qubernator xəyanət edir!
                        // 1. İdarə etdiyi əyalət düşmən dövlətin nəzarətinə keçir
                        int? provId = leader.ParsedProvinceId;
                        Province govProvince = null;
                        if (provId.HasValue)
                        {
                            govProvince = provinces.Find(p => p.Id == provId.Value);
                        }

                        if (govProvince != null)
                        {
                            govProvince.OwnerFactionId = enemyFactionId;
                            conn.Update(govProvince);
                        }

                        // 2. İdarə etdiyi ordu (əgər varsa) düşmən dövlətin nəzarətinə keçir
                        int? armyId = leader.ParsedArmyId;
                        Army govArmy = null;
                        if (armyId.HasValue)
                        {
                            govArmy = armies.Find(a => a.Id == armyId.Value);
                        }
                        else if (govProvince != null)
                        {
                            // Əgər qubernatorun ordusu təyin edilməyibsə, həmin əyalətdəki Krala aid orduya baxırıq
                            govArmy = armies.Find(a => a.LocationProvinceId == govProvince.Id && a.OwnerFactionId == kingFactionId);
                            if (govArmy != null)
                            {
                                leader.AssignedArmyId = govArmy.Id.ToString();
                            }
                        }

                        if (govArmy != null)
                        {
                            govArmy.OwnerFactionId = enemyFactionId;
                            conn.Update(govArmy);
                        }

                        // 3. Lider yeni dövlətinə sədaqətli olur və gizli müqavilə reallaşır
                        leader.Loyalty = 100f;
                        leader.SecretPactFactionId = null;
                        conn.Update(leader);

                        betrayedLeaders.Add(leader);

                        string msg = $"XƏYANƏT! {leader.Name} düşmən tərəfə keçdi!";
                        LastBetrayalMessage = msg;
                        Debug.LogWarning(
                            $"[PoliticalController] {msg} -> Qubernator '{leader.Name}' " +
                            $"(Əyalət: {govProvince?.Name ?? "N/A"}, Ordu: {(govArmy != null ? govArmy.Id.ToString() : "N/A")}) " +
                            $"artıq Fraksiya #{enemyFactionId} tərkibindədir!");
                    }
                }
            });

            for (int i = 0; i < betrayedLeaders.Count; i++)
            {
                Leader ldr = betrayedLeaders[i];
                OnSecretPactBetrayalTriggered?.Invoke(ldr, enemyFactionId);
            }

            if (betrayedLeaders.Count > 0)
            {
                MapController mapCtrl = MapController.Instance != null
                    ? MapController.Instance
                    : FindAnyObjectByType<MapController>();
                if (mapCtrl != null)
                {
                    mapCtrl.SpawnProvincesFromDatabase();
                }

                UIManager ui = UIManager.Instance != null
                    ? UIManager.Instance
                    : FindAnyObjectByType<UIManager>();
                if (ui != null)
                {
                    ui.RefreshDateTrackerUI();
                }
            }

            return betrayedLeaders;
        }

        #endregion
    }
}
