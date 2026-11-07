using System;
using System.Collections.Generic;
using System.Text;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

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
        public static GameManager Instance { get; private set; }

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
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

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

            // 2. Dərhal sonra Aylıq İqtisadiyyat və Qida İstehlakı (Aclıq/Ölüm)
            if (economyController != null)
            {
                economyController.ProcessMonthlyEconomy(databaseManager, month, year);
            }

            // 3. İqtisadiyyatdan dərhal sonra Aylıq Diplomatiya (Müqavilə müddətlərinin azaldılması)
            if (militaryAndDiplomacyController != null)
            {
                militaryAndDiplomacyController.ProcessMonthlyDiplomacy(databaseManager, month, year);
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

        private void OnDestroy()
        {
            if (_eventsBound)
            {
                OnMonthPassed -= HandleMonthPassed;
                OnYearPassed -= HandleYearPassed;
                _eventsBound = false;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
