using System;
using System.Collections.Generic;
using System.Text;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Tactical;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Oyundakı zaman axını sürətləri ("Real-time with pause").
    /// </summary>
    public enum GameSpeed
    {
        Paused = 0,
        Normal1x = 1,
        Fast2x = 2,
        VeryFast3x = 3,
        UltraFast5x = 5
    }

    /// <summary>
    /// Oyunun qlobal vaxt axınını ("Real-time with pause" və "Tick" sistemi) idarə edən,
    /// DatabaseManager, DemographicsController, EconomyController və MilitaryAndDiplomacyController
    /// ilə sinxron işləyən mərkəzi Singleton menecer.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string GlobalMapSceneName = "GlobalMapScene";
        public const string TacticalBattleSceneName = "TacticalBattleScene";

        private static GameManager _instance;
        public static GameManager Instance
        {
            get => _instance != null ? _instance : (_instance = FindAnyObjectByType<GameManager>());
            private set => _instance = value;
        }

        [Header("Faza 4 - Səhnələr Arası Döyüş Parametrləri")]
        [SerializeField] private string currentAttackerArmyId = "Army_1";
        [SerializeField] private string currentDefenderArmyId = "Army_2";

        public string CurrentAttackerArmyId
        {
            get => currentAttackerArmyId;
            set => currentAttackerArmyId = value;
        }

        public string CurrentDefenderArmyId
        {
            get => currentDefenderArmyId;
            set => currentDefenderArmyId = value;
        }

        [Header("Faza 3.2 - Döyüş Mühiti (Plains, Desert, Siege)")]
        [SerializeField] private BattleEnvironment currentBattleEnvironment = BattleEnvironment.Plains;

        public BattleEnvironment CurrentBattleEnvironment
        {
            get => currentBattleEnvironment;
            set => currentBattleEnvironment = value;
        }

        public string LastLoadedSceneName { get; private set; } = GlobalMapSceneName;
        public int LastDefeatedArmyId { get; private set; } = -1;
        public string LastWinnerArmyId { get; private set; } = string.Empty;

        [Header("Zaman və Tick Konfiqurasiyası")]
        [Tooltip("1x sürətdə iki Tick arasında keçən real saniyə müddəti")]
        [SerializeField, Min(0.05f)] private float baseTickIntervalSeconds = 1.0f;

        [Tooltip("Oyun başlayarkən aktiv olan sürət rejimi")]
        [SerializeField] private GameSpeed initialSpeed = GameSpeed.Paused;

        [Header("Orta Əsr Təqvimi (Başlanğıc Tarixi)")]
        [SerializeField, Range(1, 30)] private int startDay = 1;
        [SerializeField, Range(1, 12)] private int startMonth = 1;
        [SerializeField] private int startYear = 1066;

        [Header("Alt-Sistem Referansları")]
        [Tooltip("Səhnədəki DatabaseManager referansı (boş olarsa avtomatik tapılır və ya yaradılır)")]
        [SerializeField] private DatabaseManager databaseManager;

        [Tooltip("Səhnədəki DemographicsController referansı (boş olarsa avtomatik tapılır və ya yaradılır)")]
        [SerializeField] private DemographicsController demographicsController;

        [Tooltip("Səhnədəki EconomyController referansı (boş olarsa avtomatik tapılır və ya yaradılır)")]
        [SerializeField] private EconomyController economyController;

        [Tooltip("Səhnədəki MilitaryAndDiplomacyController referansı (boş olarsa avtomatik tapılır və ya yaradılır)")]
        [SerializeField] private MilitaryAndDiplomacyController militaryAndDiplomacyController;

        [Tooltip("Səhnədəki InfrastructureController referansı (boş olarsa avtomatik tapılır və ya yaradılır)")]
        [SerializeField] private InfrastructureController infrastructureController;

        [Tooltip("Səhnədəki PoliticalController referansı (Faza 6B: Sədaqət və Üsyan Motoru)")]
        [SerializeField] private PoliticalController politicalController;

        [Tooltip("Səhnədəki SiegeController referansı (Faza 6C: Mühasirə Logistikası və Taktikaları)")]
        [SerializeField] private SiegeController siegeController;

        [Tooltip("Səhnədəki AIDiplomacyController referansı (Faza 7.2: Proaktiv AI və Koalisiya Motoru)")]
        [SerializeField] private AIDiplomacyController aiDiplomacyController;

        private float _tickTimer;
        private bool _eventsBound;

        /// <summary>
        /// Oyun başlayandan bəri keçən ümumi Tick sayı.
        /// </summary>
        public long CurrentTick { get; private set; }

        /// <summary>
        /// Cari oyun günü (1-30).
        /// </summary>
        public int CurrentDay { get; private set; }

        /// <summary>
        /// Cari oyun ayı (1-12).
        /// </summary>
        public int CurrentMonth { get; private set; }

        /// <summary>
        /// Cari oyun ili.
        /// </summary>
        public int CurrentYear { get; private set; }

        /// <summary>
        /// Cari zaman axını sürəti.
        /// </summary>
        public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Paused;

        /// <summary>
        /// Oyunun fasilədə (Pause) olub-olmadığını göstərir.
        /// </summary>
        public bool IsPaused => CurrentSpeed == GameSpeed.Paused;

        // UI və alt-sistemlər üçün zaman hadisələri (Events)
        public event Action<long, int, int, int> OnTickAdvanced;
        public event Action<int, int, int> OnDayPassed;
        public event Action<int, int> OnMonthPassed;
        public event Action<int> OnYearPassed;
        public event Action<GameSpeed> OnGameSpeedChanged;

        private GameSpeed _lastNonPausedSpeed = GameSpeed.Normal1x;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            InitializeCalendar();
            EnsureDatabaseConnection();
            EnsureSubsystemsBinding();
            SetGameSpeed(initialSpeed);
        }

        private void Update()
        {
            if (IsPaused)
            {
                return;
            }

            // Əgər səhnədə TimeManager (Coroutine əsaslı Aylıq Tick motoru) aktivdirsə,
            // zaman axınını TimeManager idarə edir.
            if (TimeManager.Instance != null && TimeManager.Instance.isActiveAndEnabled)
            {
                return;
            }

            float speedMultiplier = (int)CurrentSpeed;
            _tickTimer += Time.deltaTime * speedMultiplier;

            while (_tickTimer >= baseTickIntervalSeconds)
            {
                _tickTimer -= baseTickIntervalSeconds;
                AdvanceTick();
            }
        }

        /// <summary>
        /// Təqvimi başlanğıc Orta Əsr tarixinə sıfırlayır.
        /// </summary>
        public void InitializeCalendar()
        {
            CurrentTick = 0;
            CurrentDay = Mathf.Clamp(startDay, 1, 30);
            CurrentMonth = Mathf.Clamp(startMonth, 1, 12);
            CurrentYear = startYear;
            _tickTimer = 0f;
        }

        /// <summary>
        /// DatabaseManager ilə təməl əlaqəni qurur və bazanın işlək olduğunu təsdiqləyir.
        /// </summary>
        public void EnsureDatabaseConnection()
        {
            if (databaseManager == null)
            {
                databaseManager = DatabaseManager.Instance;
            }

            if (databaseManager == null)
            {
                databaseManager = FindAnyObjectByType<DatabaseManager>();
            }

            if (databaseManager == null)
            {
                var dbObject = new GameObject("DatabaseManager");
                databaseManager = dbObject.AddComponent<DatabaseManager>();
            }

            if (!databaseManager.IsConnected)
            {
                databaseManager.InitializeDatabase();
            }
        }

        /// <summary>
        /// Bütün alt-sistem menecerlərini (Demographics, Economy, MilitaryAndDiplomacy) tapır/yaradır
        /// və OnMonthPassed / OnYearPassed hadisələrinə bağlayır.
        /// </summary>
        public void EnsureSubsystemsBinding()
        {
            if (demographicsController == null)
            {
                demographicsController = DemographicsController.Instance;
            }

            if (demographicsController == null)
            {
                demographicsController = FindAnyObjectByType<DemographicsController>();
            }

            if (demographicsController == null)
            {
                var demoObject = new GameObject("DemographicsController");
                demographicsController = demoObject.AddComponent<DemographicsController>();
            }

            if (economyController == null)
            {
                economyController = EconomyController.Instance;
            }

            if (economyController == null)
            {
                economyController = FindAnyObjectByType<EconomyController>();
            }

            if (economyController == null)
            {
                var ecoObject = new GameObject("EconomyController");
                economyController = ecoObject.AddComponent<EconomyController>();
            }

            if (militaryAndDiplomacyController == null)
            {
                militaryAndDiplomacyController = MilitaryAndDiplomacyController.Instance;
            }

            if (militaryAndDiplomacyController == null)
            {
                militaryAndDiplomacyController = FindAnyObjectByType<MilitaryAndDiplomacyController>();
            }

            if (militaryAndDiplomacyController == null)
            {
                var milObject = new GameObject("MilitaryAndDiplomacyController");
                militaryAndDiplomacyController = milObject.AddComponent<MilitaryAndDiplomacyController>();
            }

            if (infrastructureController == null)
            {
                infrastructureController = InfrastructureController.Instance;
            }

            if (infrastructureController == null)
            {
                infrastructureController = FindAnyObjectByType<InfrastructureController>();
            }

            if (infrastructureController == null)
            {
                var infraObject = new GameObject("InfrastructureController");
                infrastructureController = infraObject.AddComponent<InfrastructureController>();
            }

            if (politicalController == null)
            {
                politicalController = PoliticalController.Instance;
            }

            if (politicalController == null)
            {
                var polObject = new GameObject("PoliticalController");
                politicalController = polObject.AddComponent<PoliticalController>();
            }

            if (siegeController == null)
            {
                siegeController = SiegeController.Instance;
            }

            if (siegeController == null)
            {
                var siegeObj = new GameObject("SiegeController");
                siegeController = siegeObj.AddComponent<SiegeController>();
            }

            if (aiDiplomacyController == null)
            {
                aiDiplomacyController = AIDiplomacyController.Instance;
            }

            if (aiDiplomacyController == null)
            {
                aiDiplomacyController = FindAnyObjectByType<AIDiplomacyController>();
            }

            if (aiDiplomacyController == null)
            {
                var aiObj = new GameObject("AIDiplomacyController");
                aiDiplomacyController = aiObj.AddComponent<AIDiplomacyController>();
            }

            if (!_eventsBound)
            {
                OnMonthPassed -= HandleMonthPassed;
                OnYearPassed -= HandleYearPassed;

                OnMonthPassed += HandleMonthPassed;
                OnYearPassed += HandleYearPassed;
                _eventsBound = true;
            }
        }

        /// <summary>
        /// Geri-uyğunluq üçün köhnə metod adı.
        /// </summary>
        public void EnsureDemographicsBinding()
        {
            EnsureSubsystemsBinding();
        }

        private void HandleMonthPassed(int month, int year)
        {
            if (databaseManager == null)
            {
                return;
            }

            // 1. Öncə Aylıq Demoqrafiya: Doğum səviyyəsi və Yaralıların sağalması/ölümü
            if (demographicsController != null)
            {
                demographicsController.ProcessMonthlyDemographics(databaseManager, month, year);
            }

            // 2. Aylıq İnfrastruktur Tikintisi: ConstructionMonthsLeft azalır, 0 olduqda ProductivityMultiplier artır
            if (infrastructureController != null)
            {
                infrastructureController.ProcessMonthlyInfrastructure(databaseManager, month, year);
            }

            // 3. Dərhal sonra Aylıq İqtisadiyyat və Qida İstehlakı (ProductivityMultiplier nəzərə alınmaqla)
            if (economyController != null)
            {
                economyController.ProcessMonthlyEconomy(databaseManager, month, year);
            }

            // 4. İqtisadiyyatdan dərhal sonra Aylıq Diplomatiya (Müqavilə müddətlərinin azaldılması)
            if (militaryAndDiplomacyController != null)
            {
                militaryAndDiplomacyController.ProcessMonthlyDiplomacy(databaseManager, month, year);
            }

            // 5. Aylıq Siyasət (ProcessMonthlyPolitics): Liderlərin Sədaqəti güc nisbətinə görə yoxlanılır, Loyalty < 30 olduqda Müstəqillik/Üsyan başlayır
            if (politicalController != null)
            {
                politicalController.ProcessMonthlyPolitics(databaseManager, month, year);
            }

            // 6. Aylıq Mühasirə və İstismar (ProcessMonthlySiege): İstismar olunan əyalətlərdə PublicOrder -= 5, ərzaq mühasirəçilərə axır
            if (siegeController != null)
            {
                siegeController.ProcessMonthlySiege(databaseManager, month, year);
            }

            // 7. Aylıq Proaktiv Süni İntellekt (ProcessMonthlyAI): Koalisiya və Fürsətçi Müharibə qiymətləndirilməsi
            if (aiDiplomacyController != null)
            {
                aiDiplomacyController.ProcessMonthlyAI(databaseManager, month, year);
            }
        }

        private void HandleYearPassed(int year)
        {
            if (demographicsController != null && databaseManager != null)
            {
                // İllik Tick: Yaşlanma keçidləri (Infants->Youths->Adults->Elders) və Təbii Ölüm
                demographicsController.ProcessYearlyDemographics(databaseManager, year);
            }
        }

        /// <summary>
        /// Oyunu 1 Tick (1 Gün) irəli aparır, təqvimi yeniləyir və mərkəzi verilənlər bazası ilə sinxronlaşdırır.
        /// </summary>
        public void AdvanceTick()
        {
            if (CurrentYear == 0)
            {
                InitializeCalendar();
            }

            EnsureDatabaseConnection();
            EnsureSubsystemsBinding();

            CurrentTick++;

            bool monthChanged = false;
            bool yearChanged = false;

            CurrentDay++;
            if (CurrentDay > 30)
            {
                CurrentDay = 1;
                CurrentMonth++;
                monthChanged = true;

                if (CurrentMonth > 12)
                {
                    CurrentMonth = 1;
                    CurrentYear++;
                    yearChanged = true;
                }
            }

            // Gündəlik Tick baza sinxronizasiyası
            SyncTickWithDatabase();

            // Hadisələri (Events) tətikləyirik
            OnTickAdvanced?.Invoke(CurrentTick, CurrentDay, CurrentMonth, CurrentYear);
            OnDayPassed?.Invoke(CurrentDay, CurrentMonth, CurrentYear);

            if (monthChanged)
            {
                OnMonthPassed?.Invoke(CurrentMonth, CurrentYear);
            }

            if (yearChanged)
            {
                OnYearPassed?.Invoke(CurrentYear);
            }
        }

        /// <summary>
        /// TimeManager tərəfindən hər 1 Aylıq Tick (2 saniyə) tamamlandıqda çağırılır.
        /// Təqvimi 1 Ay irəli aparır, Aylıq İstehsal/İstehlak/Doğum və (12-ci aydan sonra) İllik Yaşlanma
        /// alqoritmlərini icra edir və SQLite bazasına avtomatik yadda saxlayır (Auto-save).
        /// </summary>
        public void AdvanceOneMonthFromTimeManager(out int currentMonth, out int currentYear, out bool yearPassed)
        {
            if (CurrentYear == 0)
            {
                InitializeCalendar();
            }

            EnsureDatabaseConnection();
            EnsureSubsystemsBinding();

            CurrentTick++;
            CurrentDay = 1;
            CurrentMonth++;
            yearPassed = false;

            if (CurrentMonth > 12)
            {
                CurrentMonth = 1;
                CurrentYear++;
                yearPassed = true;
            }

            currentMonth = CurrentMonth;
            currentYear = CurrentYear;

            SyncTickWithDatabase();

            OnTickAdvanced?.Invoke(CurrentTick, CurrentDay, CurrentMonth, CurrentYear);
            OnDayPassed?.Invoke(CurrentDay, CurrentMonth, CurrentYear);
            OnMonthPassed?.Invoke(CurrentMonth, CurrentYear);

            if (yearPassed)
            {
                OnYearPassed?.Invoke(CurrentYear);
            }
        }

        /// <summary>
        /// Sürətli Simulyasiya Testi:
        /// - 1-ci Ayda "Anadolu" əyalətindən Ordu səfərbər edir (DraftArmy) və işçi qüvvəsinin azalmasını çap edir.
        /// - Ardıcıl olaraq 12 Ay (1 İl = 360 Günlük Tick) simulyasiyanı irəli aparır.
        /// - İlin sonunda Anadoluda işçi qüvvəsi azaldığı üçün iqtisadi (ərzaq/xammal/vergi) istehsalın necə düşdüyünü
        ///   və Diplomatiya cədvəlindəki 12 aylıq müharibə müqaviləsinin müddətinin bitib "Neytral"-a keçdiyini çap edir.
        /// </summary>
        [ContextMenu("Sürətli Simulyasiya (12 Ay / 1 İl - Səfərbərlik, İqtisadiyyat və Diplomatiya)")]
        public string RunFastSimulation(int yearsToSimulate = 1, string draftProvinceName = "Anadolu")
        {
            EnsureDatabaseConnection();
            EnsureSubsystemsBinding();

            if (CurrentYear == 0)
            {
                InitializeCalendar();
            }

            var reportBuilder = new StringBuilder();
            yearsToSimulate = Mathf.Max(1, yearsToSimulate);

            List<Province> provinces = databaseManager.Connection.Table<Province>().ToList();
            if (provinces.Count == 0)
            {
                string emptyMsg = "[GameManager] Bazada heç bir əyalət tapılmadı.";
                Debug.LogError(emptyMsg);
                return emptyMsg;
            }

            int simulatedMonthCounter = 0;

            void OnTreatyExpiredLogged(DiplomacyExpirationReport exp)
            {
                string expLine =
                    $"[Diplomatiya Hadisəsi - Ay {simulatedMonthCounter} / İl {exp.Year}] " +
                    $"Dövlət {exp.FactionA_Id} və Dövlət {exp.FactionB_Id} arasındakı '{exp.OldTreatyType}' müqaviləsinin müddəti bitdi (0 Tick) -> " +
                    $"Yeni Status: '{exp.NewTreatyType}' | Münasibət balı 0-a yaxınlaşdı ({exp.OldRelationshipScore} -> {exp.NewRelationshipScore}).";
                reportBuilder.AppendLine(expLine);
            }

            militaryAndDiplomacyController.OnTreatyExpired += OnTreatyExpiredLogged;

            try
            {
                for (int y = 0; y < yearsToSimulate; y++)
                {
                    // Səfərbərlikdən əvvəlki başlanğıc iqtisadi və demoqrafik göstəriciləri saxlayırıq
                    var startPopSnapshot = new Dictionary<int, Population>();
                    foreach (var p in databaseManager.Connection.Table<Population>().ToList())
                    {
                        startPopSnapshot[p.ProvinceId] = p;
                    }

                    var startEcoSnapshot = new Dictionary<int, Economy>();
                    foreach (var e in databaseManager.Connection.Table<Economy>().ToList())
                    {
                        startEcoSnapshot[e.ProvinceId] = e;
                    }

                    int yearBefore = CurrentYear;

                    // 1-ci Ayda "Anadolu" əyalətindən Ordu səfərbər edilir (DraftArmy)
                    DraftArmyResult draftResult = militaryAndDiplomacyController.DraftArmyByProvinceName(databaseManager, draftProvinceName);
                    if (draftResult.Success)
                    {
                        string draftLog =
                            $"[Ay 1 - SƏFƏRBƏRLİK ({draftResult.ProvinceName})] " +
                            $"Yaradılan Ordu: #{draftResult.CreatedArmy.Id} | Ümumi Əsgər: {draftResult.CreatedArmy.TotalSoldiers} nəfər " +
                            $"(Elit Süvari/YouthSoldiers: {draftResult.CreatedArmy.YouthSoldiers}, Ağır Piyada/AdultSoldiers: {draftResult.CreatedArmy.AdultSoldiers}, Morale: {draftResult.CreatedArmy.Morale:F1}) | " +
                            $"Əyalətdə qalan İşçi Qüvvəsi (Adults): {draftResult.AdultsBefore} -> {draftResult.AdultsAfter} (-{draftResult.AdultsBefore - draftResult.AdultsAfter}, -30%), " +
                            $"Gənclər (Youths): {draftResult.YouthsBefore} -> {draftResult.YouthsAfter} (-{draftResult.YouthsBefore - draftResult.YouthsAfter}, -50%).";
                        Debug.Log(draftLog);
                        reportBuilder.AppendLine(draftLog);
                    }

                    // 12 Ay (hər ay 30 gün) ardıcıl simulyasiya edilir
                    for (int m = 1; m <= 12; m++)
                    {
                        simulatedMonthCounter++;
                        for (int d = 0; d < 30; d++)
                        {
                            AdvanceTick();
                        }
                    }

                    reportBuilder.AppendLine($"--- İL {CurrentYear} YEKUN HESABATI ({yearBefore} -> {CurrentYear}) ---");

                    foreach (var prov in provinces)
                    {
                        Population beforePop = startPopSnapshot[prov.Id];
                        Economy beforeEco = startEcoSnapshot[prov.Id];
                        Population afterPop = databaseManager.Connection
                            .Table<Population>()
                            .FirstOrDefault(pop => pop.ProvinceId == prov.Id);
                        Economy afterEco = databaseManager.Connection
                            .Table<Economy>()
                            .FirstOrDefault(eco => eco.ProvinceId == prov.Id);

                        float dFood = afterEco.FoodProduction - beforeEco.FoodProduction;
                        float dRaw = afterEco.RawMaterials - beforeEco.RawMaterials;
                        float dTax = afterEco.LocalTaxIncome - beforeEco.LocalTaxIncome;

                        string ecoImpactLine =
                            $"İl {CurrentYear} - {prov.Name} İqtisadi Müqayisəsi (Səfərbərlik Öncəsi vs İlin Sonu): " +
                            $"İşçi Qüvvəsi (Adults): {beforePop.Adults} -> {afterPop.Adults} ({FormatDelta(afterPop.Adults - beforePop.Adults)}) | " +
                            $"Ərzaq İstehsalı: {beforeEco.FoodProduction:F1} -> {afterEco.FoodProduction:F1} ({FormatFloatDelta(dFood)}) | " +
                            $"Xammal İstehsalı: {beforeEco.RawMaterials:F1} -> {afterEco.RawMaterials:F1} ({FormatFloatDelta(dRaw)}) | " +
                            $"Vergi Gəliri: {beforeEco.LocalTaxIncome:F1} -> {afterEco.LocalTaxIncome:F1} ({FormatFloatDelta(dTax)})";

                        Debug.Log($"[GameManager - İqtisadi Təsir] {ecoImpactLine}");
                        reportBuilder.AppendLine(ecoImpactLine);
                    }

                    reportBuilder.AppendLine($"--- İL {CurrentYear} DİPLOMATİYA VƏ ORDU CƏDVƏLLƏRİ ---");
                    foreach (var dip in databaseManager.Connection.Table<Diplomacy>().ToList())
                    {
                        Debug.Log($"[GameManager - Diplomatiya Yekun] {dip}");
                        reportBuilder.AppendLine(dip.ToString());
                    }

                    foreach (var army in databaseManager.Connection.Table<Army>().ToList())
                    {
                        Debug.Log($"[GameManager - Ordu Yekun] {army}");
                        reportBuilder.AppendLine(army.ToString());
                    }
                }
            }
            finally
            {
                militaryAndDiplomacyController.OnTreatyExpired -= OnTreatyExpiredLogged;
            }

            return reportBuilder.ToString();
        }

        private static string FormatDelta(int delta)
        {
            return delta >= 0 ? $"+{delta}" : delta.ToString();
        }

        private static string FormatFloatDelta(float delta)
        {
            return delta >= 0f ? $"+{delta:F1}" : $"{delta:F1}";
        }

        /// <summary>
        /// Hər Tick artımını DatabaseManager-ə ötürərək atomik baza tranzaksiyasını işə salır.
        /// </summary>
        private void SyncTickWithDatabase()
        {
            if (databaseManager == null)
            {
                EnsureDatabaseConnection();
            }

            if (databaseManager != null && databaseManager.IsConnected)
            {
                databaseManager.ProcessGameTick(CurrentTick, CurrentDay, CurrentMonth, CurrentYear);
            }
            else
            {
                Debug.LogWarning($"[GameManager] Tick #{CurrentTick}: DatabaseManager bağlantısı tapılmadı!");
            }
        }

        #region Faza 6C Əmrləri (Mühasirə, Yaralıların Edamı və Fövqəladə Vəziyyət)

        public bool ExecuteWounded(string provinceId)
        {
            EnsureSubsystemsBinding();
            return demographicsController != null && demographicsController.ExecuteWounded(provinceId, databaseManager);
        }

        public PlunderResult Plunder(string provinceId)
        {
            EnsureSubsystemsBinding();
            return siegeController != null ? siegeController.Plunder(provinceId, databaseManager) : default;
        }

        public bool Exploit(string provinceId)
        {
            EnsureSubsystemsBinding();
            return siegeController != null && siegeController.Exploit(provinceId, databaseManager);
        }

        public bool SetBesieged(string provinceId, bool isBesieged)
        {
            EnsureSubsystemsBinding();
            return siegeController != null && siegeController.SetBesieged(provinceId, isBesieged, databaseManager);
        }

        public EmergencyConscriptionResult CallEmergencyConscription(string provinceId)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null
                ? militaryAndDiplomacyController.CallEmergencyConscription(provinceId, databaseManager)
                : default;
        }

        #region Faza 6D - Taxt Çevrilişi və Genişlənmə Riski (Overextension)

        public CoupResult ExecuteCoup(string newRulerLeaderNameOrId, string customDynastyName = null)
        {
            EnsureSubsystemsBinding();
            return politicalController != null
                ? politicalController.ExecuteCoup(newRulerLeaderNameOrId, customDynastyName, databaseManager)
                : default;
        }

        public OverextensionReport CalculateNationOverextension(int nationId = 1)
        {
            EnsureSubsystemsBinding();
            return politicalController != null
                ? politicalController.CalculateNationOverextension(databaseManager, nationId)
                : default;
        }

        #endregion

        #region Faza 7.1 - Diplomatiya: Atəşkəs və Gizli Vassal Əlaqələri

        public bool ProposeCeasefire(int factionA, int factionB, int durationMonths)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.ProposeCeasefire(factionA, factionB, durationMonths, databaseManager);
        }

        public bool IsCeasefireActive(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.IsCeasefireActive(factionA, factionB, databaseManager);
        }

        public bool DeclareWar(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.DeclareWar(factionA, factionB, databaseManager);
        }

        public List<Leader> TriggerSecretPactBetrayal(int enemyFactionId, int kingFactionId = 1)
        {
            EnsureSubsystemsBinding();
            return politicalController != null
                ? politicalController.TriggerSecretPactBetrayal(enemyFactionId, kingFactionId, databaseManager)
                : new List<Leader>();
        }

        #region Faza 7.2 - Müqavilələr və Proaktiv AI

        public bool ProposeNonAggressionPact(int factionA, int factionB, int durationMonths)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.ProposeNonAggressionPact(factionA, factionB, durationMonths, databaseManager);
        }

        public bool IsNonAggressionPactActive(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.IsNonAggressionPactActive(factionA, factionB, databaseManager);
        }

        public bool FormMilitaryAlliance(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.FormMilitaryAlliance(factionA, factionB, databaseManager);
        }

        public bool AreAllies(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.AreAllies(factionA, factionB, databaseManager);
        }

        public bool BreakTreatyAndDeclareWar(int factionA, int factionB)
        {
            EnsureSubsystemsBinding();
            return militaryAndDiplomacyController != null &&
                   militaryAndDiplomacyController.BreakTreatyAndDeclareWar(factionA, factionB, databaseManager);
        }

        #endregion

        #endregion

        #endregion

        /// <summary>
        /// Oyun sürətini dəyişdirir ("Real-time with pause").
        /// </summary>
        public void SetGameSpeed(GameSpeed newSpeed)
        {
            if (newSpeed != GameSpeed.Paused)
            {
                _lastNonPausedSpeed = newSpeed;
            }

            if (CurrentSpeed == newSpeed)
            {
                return;
            }

            CurrentSpeed = newSpeed;
            OnGameSpeedChanged?.Invoke(CurrentSpeed);
            Debug.Log($"[GameManager] Zaman sürəti dəyişdirildi: {CurrentSpeed} | Tarix: {GetFormattedDate()} (Tick #{CurrentTick})");
        }

        public void SetGameSpeedFromTimeManager(GameSpeed newSpeed)
        {
            SetGameSpeed(newSpeed);
        }

        /// <summary>
        /// Oyunu fasiləyə (Pause) alır.
        /// </summary>
        public void PauseGame()
        {
            SetGameSpeed(GameSpeed.Paused);
        }

        /// <summary>
        /// Oyunu fasilədən çıxararaq son aktiv sürətlə davam etdirir.
        /// </summary>
        public void ResumeGame()
        {
            SetGameSpeed(_lastNonPausedSpeed == GameSpeed.Paused ? GameSpeed.Normal1x : _lastNonPausedSpeed);
        }

        /// <summary>
        /// Fasilə (Pause) və aktiv oyun rejimi arasında keçid edir.
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        /// <summary>
        /// Cari tarixi oxunaqlı formatda qaytarır (GG.AA.İİİİ).
        /// </summary>
        public string GetFormattedDate()
        {
            return $"{CurrentDay:D2}.{CurrentMonth:D2}.{CurrentYear:D4}";
        }

        /// <summary>
        /// "Army_1", "Ordu 1" və ya "1" formatındakı string Ordu ID-sini tam ədədə (int) çevirir.
        /// </summary>
        public static int ParseArmyId(string rawArmyId, int fallbackId = 1)
        {
            if (string.IsNullOrWhiteSpace(rawArmyId))
            {
                return fallbackId;
            }

            if (int.TryParse(rawArmyId.Trim(), out int directId) && directId > 0)
            {
                return directId;
            }

            var digits = new StringBuilder();
            for (int i = 0; i < rawArmyId.Length; i++)
            {
                if (char.IsDigit(rawArmyId[i]))
                {
                    digits.Append(rawArmyId[i]);
                }
            }

            if (digits.Length > 0 && int.TryParse(digits.ToString(), out int extractedId) && extractedId > 0)
            {
                return extractedId;
            }

            return fallbackId;
        }

        /// <summary>
        /// Metod 1 (StartBattle): İki ordunun ID-sini qəbul edir, CurrentAttackerArmyId və CurrentDefenderArmyId
        /// dəyişənlərində yadda saxlayır və TacticalBattleScene səhnəsini yükləyir.
        /// </summary>
        public void StartBattle(string attackerArmyId, string defenderArmyId)
        {
            StartBattle(attackerArmyId, defenderArmyId, null);
        }

        /// <summary>
        /// Faza 3.2: Döyüşü müəyyən mühitlə (Plains, Desert, Siege) və ya əyalətin relyefinə görə başladır.
        /// </summary>
        public void StartBattle(string attackerArmyId, string defenderArmyId, BattleEnvironment? environment)
        {
            CurrentAttackerArmyId = string.IsNullOrWhiteSpace(attackerArmyId) ? "Army_1" : attackerArmyId.Trim();
            CurrentDefenderArmyId = string.IsNullOrWhiteSpace(defenderArmyId) ? "Army_2" : defenderArmyId.Trim();

            PauseGame();
            EnsureDatabaseConnection();
            EnsureSubsystemsBinding();

            int parsedAttackerId = ParseArmyId(CurrentAttackerArmyId, 1);
            int parsedDefenderId = ParseArmyId(CurrentDefenderArmyId, 2);

            // Bazada döyüşəcək iki ordunun mövcud olduğundan əmin oluruq
            EnsureBattleArmiesExistInDatabase(parsedAttackerId, parsedDefenderId);

            // Faza 7.1: Atəşkəs yoxlaması - Əgər iki fraksiya arasında atəşkəs qüvvədədirsə, döyüş başlaya bilməz!
            if (databaseManager != null && militaryAndDiplomacyController != null)
            {
                Army atkArmy = databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == parsedAttackerId);
                Army defArmy = databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == parsedDefenderId);
                if (atkArmy != null && defArmy != null && militaryAndDiplomacyController.IsCeasefireActive(atkArmy.OwnerFactionId, defArmy.OwnerFactionId, databaseManager))
                {
                    Debug.LogWarning($"[GameManager] StartBattle ləğv edildi: Fraksiya {atkArmy.OwnerFactionId} və Fraksiya {defArmy.OwnerFactionId} arasında Atəşkəs qüvvədədir!");
                    return;
                }
            }

            if (environment.HasValue)
            {
                CurrentBattleEnvironment = environment.Value;
            }
            else
            {
                Army defArmy = databaseManager != null ? databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == parsedDefenderId) : null;
                Province prov = defArmy != null && databaseManager != null ? databaseManager.Connection.Table<Province>().FirstOrDefault(p => p.Id == defArmy.LocationProvinceId) : null;
                if (prov != null && prov.IsBesieged)
                {
                    CurrentBattleEnvironment = BattleEnvironment.Siege;
                }
                else if (prov != null && !string.IsNullOrEmpty(prov.Climate) &&
                         (prov.Climate.IndexOf("Səhra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          prov.Climate.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          prov.Climate.IndexOf("Quraq", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    CurrentBattleEnvironment = BattleEnvironment.Desert;
                }
                else
                {
                    CurrentBattleEnvironment = BattleEnvironment.Plains;
                }
            }

            Debug.Log(
                $"[GameManager] StartBattle çağırıldı -> Hücum: {CurrentAttackerArmyId} (DB #{parsedAttackerId}) vs " +
                $"Müdafiə: {CurrentDefenderArmyId} (DB #{parsedDefenderId}) | Mühit: {CurrentBattleEnvironment}. '{TacticalBattleSceneName}' səhnəsi yüklənir...");

            LastLoadedSceneName = TacticalBattleSceneName;

            if (Application.isPlaying)
            {
                SceneManager.LoadScene(TacticalBattleSceneName);
            }
#if UNITY_EDITOR
            else
            {
                string savedAttacker = CurrentAttackerArmyId;
                string savedDefender = CurrentDefenderArmyId;
                BattleEnvironment savedEnv = CurrentBattleEnvironment;
                if (databaseManager != null)
                {
                    databaseManager.CloseConnection();
                }

                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    $"Assets/Scenes/{TacticalBattleSceneName}.unity",
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

                GameManager newSceneGm = Instance;
                if (newSceneGm != null)
                {
                    newSceneGm.CurrentAttackerArmyId = savedAttacker;
                    newSceneGm.CurrentDefenderArmyId = savedDefender;
                    newSceneGm.CurrentBattleEnvironment = savedEnv;
                    newSceneGm.LastLoadedSceneName = TacticalBattleSceneName;
                }
            }
#endif
        }

        /// <summary>
        /// Metod 2 (EndBattle): Qalib ordunun ID-sini qəbul edir, bazada (DatabaseManager)
        /// məğlub olan ordunu silir, əgər Kral ölübsə taxt boşluğu cəzasını tətbiq edir
        /// və yenidən GlobalMapScene səhnəsini yükləyir.
        /// </summary>
        public void EndBattle(string winnerArmyId)
        {
            EndBattle(winnerArmyId, wasKingKilled: false);
        }

        public void EndBattle(string winnerArmyId, bool wasKingKilled)
        {
            EnsureDatabaseConnection();

            int winnerIntId = ParseArmyId(winnerArmyId, 1);
            int attackerIntId = ParseArmyId(CurrentAttackerArmyId, 1);
            int defenderIntId = ParseArmyId(CurrentDefenderArmyId, 2);

            int defeatedIntId = (winnerIntId == attackerIntId) ? defenderIntId : attackerIntId;
            LastWinnerArmyId = string.IsNullOrWhiteSpace(winnerArmyId) ? $"Army_{winnerIntId}" : winnerArmyId;
            LastDefeatedArmyId = defeatedIntId;

            if (databaseManager != null && databaseManager.IsConnected)
            {
                Army defeatedArmy = databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == defeatedIntId);
                if (defeatedArmy != null)
                {
                    databaseManager.Connection.Delete<Army>(defeatedArmy.Id);
                    Debug.Log(
                        $"[GameManager] EndBattle -> Qalib: {LastWinnerArmyId} (DB #{winnerIntId}) | " +
                        $"Məğlub Ordu #{defeatedIntId} bazadan (Armies cədvəlindən) silindi!");
                }
                else
                {
                    Debug.Log(
                        $"[GameManager] EndBattle -> Qalib: {LastWinnerArmyId} (DB #{winnerIntId}) | " +
                        $"Məğlub Ordu #{defeatedIntId} artıq bazadan silinib.");
                }

                // Faza 6B Təkmilləşdirmə: Qlobal Taxt Boşluğu Cəzası
                if (wasKingKilled)
                {
                    if (politicalController != null)
                    {
                        politicalController.ApplyThroneCrisisPenalty(databaseManager);
                    }
                    else
                    {
                        databaseManager.RunInTransaction(conn =>
                        {
                            List<Leader> leaders = conn.Table<Leader>().ToList();
                            for (int i = 0; i < leaders.Count; i++)
                            {
                                Leader l = leaders[i];
                                if (l == null) continue;
                                if (l.IsKing)
                                {
                                    l.Loyalty = 0f;
                                    conn.Update(l);
                                }
                                else
                                {
                                    l.Loyalty = Mathf.Clamp(l.Loyalty - 30f, 0f, 100f);
                                    conn.Update(l);
                                }
                            }
                        });
                    }
                    Debug.LogWarning("[GameManager] TAXT BÖHRANI! Kral döyüşdə həlak olduğu üçün bazadakı qubernatorların sədaqəti -30 azaldıldı!");
                }
            }

            LastLoadedSceneName = GlobalMapSceneName;
            Debug.Log($"[GameManager] '{GlobalMapSceneName}' səhnəsinə geri qayıdılır...");

            if (Application.isPlaying)
            {
                SceneManager.LoadScene(GlobalMapSceneName);
            }
#if UNITY_EDITOR
            else
            {
                string savedAttacker = CurrentAttackerArmyId;
                string savedDefender = CurrentDefenderArmyId;
                string savedWinner = LastWinnerArmyId;
                int savedDefeated = LastDefeatedArmyId;

                if (databaseManager != null)
                {
                    databaseManager.CloseConnection();
                }

                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    $"Assets/Scenes/{GlobalMapSceneName}.unity",
                    UnityEditor.SceneManagement.OpenSceneMode.Single);

                GameManager mapGm = Instance;
                if (mapGm != null)
                {
                    mapGm.CurrentAttackerArmyId = savedAttacker;
                    mapGm.CurrentDefenderArmyId = savedDefender;
                    mapGm.LastWinnerArmyId = savedWinner;
                    mapGm.LastDefeatedArmyId = savedDefeated;
                    mapGm.LastLoadedSceneName = GlobalMapSceneName;
                }
            }
#endif
        }

        private void EnsureBattleArmiesExistInDatabase(int attackerId, int defenderId)
        {
            if (databaseManager == null || !databaseManager.IsConnected)
            {
                return;
            }

            if (databaseManager.Connection.Table<Province>().Count() == 0)
            {
                databaseManager.SeedAndTestDummyData(forceReseed: true);
            }

            Army attacker = databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == attackerId);
            if (attacker == null)
            {
                if (militaryAndDiplomacyController != null)
                {
                    militaryAndDiplomacyController.DraftArmy(databaseManager, 1);
                }
                else
                {
                    databaseManager.Connection.Insert(new Army
                    {
                        OwnerFactionId = 1,
                        LocationProvinceId = 1,
                        YouthSoldiers = 1250,
                        AdultSoldiers = 2040,
                        Morale = 1.0f
                    });
                }
            }

            Army defender = databaseManager.Connection.Table<Army>().FirstOrDefault(a => a.Id == defenderId);
            if (defender == null)
            {
                if (militaryAndDiplomacyController != null)
                {
                    militaryAndDiplomacyController.DraftArmy(databaseManager, 2);
                }
                else
                {
                    databaseManager.Connection.Insert(new Army
                    {
                        OwnerFactionId = 2,
                        LocationProvinceId = 2,
                        YouthSoldiers = 400,
                        AdultSoldiers = 1620,
                        Morale = 1.0f
                    });
                }
            }
        }

        private void OnDestroy()
        {
            if (_eventsBound)
            {
                OnMonthPassed -= HandleMonthPassed;
                OnYearPassed -= HandleYearPassed;
                _eventsBound = false;
            }

            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
