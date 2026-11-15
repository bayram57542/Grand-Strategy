using System;
using System.Collections.Generic;
using System.Globalization;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Military;
using GrandStrategy.UI;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 10.4 - "Real-Time with Pause" Zaman Mühərriki (GameTimeManager):
    /// Paradox Interactive (EU4 / CK3) üslubunda dayandırıla bilən real-zamanlı zaman axını,
    /// gün və ay əsaslı ikiqat tick arxitekturası (Daily & Monthly Ticks),
    /// və dövlət iqtisadiyyatının (Xəzinə, Ərzaq, Dəmir və Ordu aclıq itkiləri) idarə edilməsi.
    /// </summary>
    public class GameTimeManager : MonoBehaviour
    {
        private static GameTimeManager _instance;
        public static GameTimeManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = UnityEngine.Object.FindAnyObjectByType<GameTimeManager>();
#else
                    _instance = UnityEngine.Object.FindObjectOfType<GameTimeManager>();
#endif
                }
                return _instance;
            }
        }

        [Header("Tarix Parametrləri (1 Yanvar 1187)")]
        [SerializeField] private int currentYear = 1187;
        [SerializeField] private int currentMonth = 1;
        [SerializeField] private int currentDay = 1;

        [Header("Zaman Rejimi və Sürət")]
        [Tooltip("0 = Paused, 1 = 1x (1s=1gün), 2 = 2x, 3 = 3x, 4 = 4x, 5 = 5x")]
        [SerializeField] private int currentSpeedMultiplier = 0; // Başlanğıcda Paused
        [SerializeField] private int lastActiveSpeedMultiplier = 1;

        [Header("Oyunçunun Dövlət Xəzinəsi")]
        [SerializeField] private string playerFactionName = "Eldəgəzlər (Atabəylər)";
        [SerializeField] private string playerLeaderName = "Qızıl Arslan";
        [SerializeField] private Color playerFactionColor = new Color(0.122f, 0.380f, 0.553f, 1f); // #1F618D

        [SerializeField] private float playerGold = 2500f;
        [SerializeField] private float playerFood = 1800f;
        [SerializeField] private float playerIron = 650f;

        [Header("Aylıq Balanslar")]
        [SerializeField] private float monthlyGoldIncome = 410f;
        [SerializeField] private float monthlyFoodNet = 125f;
        [SerializeField] private float monthlyIronIncome = 85f;
        [SerializeField] private int totalPlayerTroops = 35000;

        [Header("Faza 10.5 - Demoqrafiya Xərcləri")]
        [SerializeField] private float monthlyHealingExpense = 0f;
        public float MonthlyHealingExpense => monthlyHealingExpense;

        [Header("Tick Hesablayıcıları")]
        private float _dayTimer = 0f;
        private int _totalDaysPassed = 0;
        private int _totalMonthsPassed = 0;

        public int CurrentYear => currentYear;
        public int CurrentMonth => currentMonth;
        public int CurrentDay => currentDay;
        public int CurrentSpeedMultiplier => currentSpeedMultiplier;
        public bool IsPaused => currentSpeedMultiplier <= 0;

        public string PlayerFactionName => playerFactionName;
        public string PlayerLeaderName => playerLeaderName;
        public Color PlayerFactionColor => playerFactionColor;

        public float PlayerGold { get => playerGold; set => playerGold = value; }
        public float PlayerFood { get => playerFood; set => playerFood = value; }
        public float PlayerIron { get => playerIron; set => playerIron = value; }

        public float MonthlyGoldIncome => monthlyGoldIncome;
        public float MonthlyFoodNet => monthlyFoodNet;
        public float MonthlyIronIncome => monthlyIronIncome;
        public int TotalPlayerTroops => totalPlayerTroops;

        public int TotalDaysPassed => _totalDaysPassed;
        public int TotalMonthsPassed => _totalMonthsPassed;

        // Hadisələr (Events)
        public event Action<int, int, int> OnDailyTick;          // (day, month, year)
        public event Action<int, int> OnMonthlyTick;             // (month, year)
        public event Action<int, bool> OnSpeedChanged;           // (speed, isPaused)
        public event Action OnEconomyUpdated;

        private static readonly string[] MonthNamesAz = new string[]
        {
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "İyun",
            "İyul", "Avqust", "Sentyabr", "Oktyabr", "Noyabr", "Dekabr"
        };

        public string FormattedDate => $"{currentDay} {GetAzerbaijaniMonthName(currentMonth)} {currentYear}";
        public string DateWithPauseStatus => IsPaused ? $"{FormattedDate}  <color=#E74C3C>⏸ PAUSED</color>" : FormattedDate;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            RecalculateMonthlyBalances();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.EnsureTopBarHierarchy();
                UIManager.Instance.UpdateTopBar();
                UIManager.Instance.UpdateTopBarSpeedButtons();
            }
        }

        private void Update()
        {
            HandleKeyboardInput();

            if (!IsPaused)
            {
                float interval = GetSecondsPerDay();
                _dayTimer += Time.deltaTime;

                while (_dayTimer >= interval && !IsPaused)
                {
                    _dayTimer -= interval;
                    AdvanceOneDay();
                }
            }
        }

        /// <summary>
        /// Klaviaturadan qısa yolları (Space və 1-5 rəqəmləri) təhlükəsiz oxuyur.
        /// </summary>
        private void HandleKeyboardInput()
        {
            // 1. Space: Pause / Unpause
            if (GetSafeKeyDown(KeyCode.Space))
            {
                TogglePause();
            }

            // 2. 1-5 rəqəmləri: Sürət rejimləri
            if (GetSafeKeyDown(KeyCode.Alpha1) || GetSafeKeyDown(KeyCode.Keypad1)) SetSpeed(1);
            else if (GetSafeKeyDown(KeyCode.Alpha2) || GetSafeKeyDown(KeyCode.Keypad2)) SetSpeed(2);
            else if (GetSafeKeyDown(KeyCode.Alpha3) || GetSafeKeyDown(KeyCode.Keypad3)) SetSpeed(3);
            else if (GetSafeKeyDown(KeyCode.Alpha4) || GetSafeKeyDown(KeyCode.Keypad4)) SetSpeed(4);
            else if (GetSafeKeyDown(KeyCode.Alpha5) || GetSafeKeyDown(KeyCode.Keypad5)) SetSpeed(5);
        }

        private bool GetSafeKeyDown(KeyCode key)
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    switch (key)
                    {
                        case KeyCode.Space: if (kb.spaceKey.wasPressedThisFrame) return true; break;
                        case KeyCode.Alpha1:
                        case KeyCode.Keypad1: if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return true; break;
                        case KeyCode.Alpha2:
                        case KeyCode.Keypad2: if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return true; break;
                        case KeyCode.Alpha3:
                        case KeyCode.Keypad3: if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return true; break;
                        case KeyCode.Alpha4:
                        case KeyCode.Keypad4: if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return true; break;
                        case KeyCode.Alpha5:
                        case KeyCode.Keypad5: if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return true; break;
                    }
                }
#endif
            }
            catch { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetKeyDown(key)) return true;
#endif
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Seçilmiş sürətə görə 1 oyun gününün real saniyə qarşılığını qaytarır.
        /// </summary>
        public float GetSecondsPerDay()
        {
            return currentSpeedMultiplier switch
            {
                1 => 1.0f,    // 1 san = 1 gün
                2 => 0.5f,    // 0.5 san = 1 gün (2x)
                3 => 0.25f,   // 0.25 san = 1 gün (4x)
                4 => 0.125f,  // 0.125 san = 1 gün (8x)
                5 => 0.05f,   // 0.05 san = 1 gün (20x)
                _ => 1.0f
            };
        }

        /// <summary>
        /// Tarixi və vaxt sayğaclarını ilkin başlanğıc vəziyyətinə sıfırlayır.
        /// </summary>
        public void ResetDate(int day = 1, int month = 1, int year = 1187)
        {
            currentDay = day;
            currentMonth = month;
            currentYear = year;
            _dayTimer = 0f;
            _totalDaysPassed = 0;
            _totalMonthsPassed = 0;
            currentSpeedMultiplier = 0;
            lastActiveSpeedMultiplier = 1;

            if (UIManager.Instance != null)
            {
                UIManager.Instance.RefreshTopBarDate();
                UIManager.Instance.UpdateTopBarSpeedButtons();
            }
        }

        /// <summary>
        /// İqtisadi ehtiyatları başlanğıc dəyərlərinə sıfırlayır.
        /// </summary>
        public void ResetEconomy(float gold = 2500f, float food = 1800f, float iron = 650f)
        {
            playerGold = gold;
            playerFood = food;
            playerIron = iron;
            RecalculateMonthlyBalances();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTopBar();
            }
        }

        /// <summary>
        /// Zamanı dayandırır (0x).
        /// </summary>
        public void Pause()
        {
            SetSpeed(0);
        }

        /// <summary>
        /// Zaman axınını bərpa edir.
        /// </summary>
        public void Unpause()
        {
            SetSpeed(lastActiveSpeedMultiplier > 0 ? lastActiveSpeedMultiplier : 1);
        }

        /// <summary>
        /// Pause vəziyyətini dəyişir (Toggle).
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused) Unpause();
            else Pause();
        }

        /// <summary>
        /// Zaman sürətini təyin edir (0 = Paused, 1..5 = Speed modes).
        /// </summary>
        public void SetSpeed(int speed)
        {
            int clamped = Mathf.Clamp(speed, 0, 5);
            if (clamped > 0)
            {
                lastActiveSpeedMultiplier = clamped;
            }

            currentSpeedMultiplier = clamped;

            OnSpeedChanged?.Invoke(currentSpeedMultiplier, IsPaused);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTopBarSpeedButtons();
                UIManager.Instance.RefreshTopBarDate();
            }

            Debug.Log($"[GameTimeManager] ⏱️ Sürət Rejimi: {(IsPaused ? "PAUSED (0x)" : $"{currentSpeedMultiplier}x")} | Tarix: {FormattedDate}");
        }

        /// <summary>
        /// Tarixi tam 1 gün irəli aparır (OnDailyTick).
        /// Əgər ay başa çatarsa, OnMonthlyTick çağırılır və ağır iqtisadi hesablamalar aparılır.
        /// </summary>
        public void AdvanceOneDay()
        {
            currentDay++;
            _totalDaysPassed++;

            int daysInCurrentMonth = GetDaysInMonth(currentYear, currentMonth);
            bool monthRollover = false;

            if (currentDay > daysInCurrentMonth)
            {
                currentDay = 1;
                currentMonth++;
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }
                monthRollover = true;
                _totalMonthsPassed++;
            }

            // 1. Günlük Tick Hadisəsi
            OnDailyTick?.Invoke(currentDay, currentMonth, currentYear);

            // 2. Ay Rollover olubsa -> Aylıq Tick Hadisəsi və İqtisadiyyat
            if (monthRollover)
            {
                ExecuteMonthlyEconomy();
                OnMonthlyTick?.Invoke(currentMonth, currentYear);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.RefreshTopBarDate();
                if (monthRollover)
                {
                    UIManager.Instance.UpdateTopBar();
                }
            }
        }

        /// <summary>
        /// Verilmiş sayda günü ardıcıl olaraq simulyasiya edir (Testlər və sürətli irəliləmə üçün).
        /// </summary>
        public void SimulateDays(int daysCount)
        {
            int count = Mathf.Max(1, daysCount);
            for (int i = 0; i < count; i++)
            {
                AdvanceOneDay();
            }
        }

        /// <summary>
        /// Verilmiş sayda ayı simulyasiya edir.
        /// </summary>
        public void SimulateMonths(int monthsCount)
        {
            int count = Mathf.Max(1, monthsCount);
            for (int m = 0; m < count; m++)
            {
                int days = GetDaysInMonth(currentYear, currentMonth) - currentDay + 1;
                SimulateDays(days);
            }
        }

        /// <summary>
        /// Faza 10.4 - Aylıq Ağır İqtisadiyyat Hesablaması:
        /// 1. Oyunçunun dövlətinə məxsus və mühasirədə olmayan (!IsBesieged) əyalətlərin gəlirlərini toplayır.
        /// 2. Orduların aylıq ərzaq tələbatını çıxır.
        /// 3. Ərzaq anbarı 0-a düşərsə, ərzaq 0-da qalır və oyunçu ordularında 5% aclıq itkisi (Attrition) baş verir.
        /// </summary>
        public void ExecuteMonthlyEconomy()
        {
            // 0. Faza 10.5: Aylıq demoqrafik keçid (doğum, yaşlanma, sağalma) — istehsal yeni siniflərə görə hesablanır
            DemographicsSystem.ProcessAllProvinces(out monthlyHealingExpense);

            RecalculateMonthlyBalances();

            // 1. Xəzinə və Dəmir artımı
            playerGold += monthlyGoldIncome;
            playerIron += monthlyIronIncome;

            // 2. Ərzaq Hesablaması və Aclıq Yoxlanışı
            if (playerFood + monthlyFoodNet < 0f)
            {
                // Anbar tamamilə boşaldı!
                playerFood = 0f;
                ApplyStarvationAttrition(0.05f);
            }
            else
            {
                playerFood += monthlyFoodNet;
            }

            // 3. SQLite verilənlər bazası ilə sinxronizasiya
            SyncWithDatabase();

            OnEconomyUpdated?.Invoke();

            Debug.Log($"[GameTimeManager] 📊 AYLIK İQTİSADİYYAT HESABLANDI (Ay: {currentMonth:D2}/{currentYear}): " +
                      $"Qızıl: {playerGold:N0} (+{monthlyGoldIncome:N0}) | Ərzaq: {playerFood:N0} ({(monthlyFoodNet >= 0 ? "+" : "")}{monthlyFoodNet:N0}) | Dəmir: {playerIron:N0} (+{monthlyIronIncome:N0})");
        }

        /// <summary>
        /// Əyalətlər və ordular üzrə cari aylıq balansları yenidən hesablayır.
        /// </summary>
        public void RecalculateMonthlyBalances()
        {
            float totalGold = 0f;
            float totalFood = 0f;
            float totalIron = 0f;
            int provinceCount = 0;

            // 1. Əyalətləri toplayırıq
            var mapLoader = GISMapLoader.Instance;
            IReadOnlyList<MapInteractionController> provinces = mapLoader != null && mapLoader.SpawnedProvinces != null && mapLoader.SpawnedProvinces.Count > 0
                ? mapLoader.SpawnedProvinces
                : null;

            if (provinces == null)
            {
#if UNITY_2023_1_OR_NEWER
                var foundProvinces = UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude);
#else
                var foundProvinces = UnityEngine.Object.FindObjectsOfType<MapInteractionController>();
#endif
                if (foundProvinces != null && foundProvinces.Length > 0)
                {
                    provinces = foundProvinces;
                }
            }

            if (provinces != null && provinces.Count > 0)
            {
                foreach (var prov in provinces)
                {
                    if (prov == null) continue;

                    bool isPlayerOwned = string.Equals(prov.ProvinceOwner, playerFactionName, StringComparison.OrdinalIgnoreCase)
                                         || prov.FactionId == 1;
                    if (isPlayerOwned)
                    {
                        provinceCount++;
                        // Mühasirədə olan əyalətin gəliri 0 olur
                        if (!prov.IsBesieged)
                        {
                            var eco = prov.EconomyData;
                            totalGold += eco != null ? eco.GoldIncome : 75f;
                            totalFood += eco != null ? eco.FoodProduction : 95f;
                            totalIron += eco != null ? eco.IronProduction : 65f;
                        }
                    }
                }
            }

            // Əgər xəritə əyalətləri hələ səhnədə tam açılmayıbsa, standart 1187 Eldəgəzlər balanslarını istifadə edirik
            if (provinceCount == 0 || totalGold <= 0f)
            {
                totalGold = 410f;
                totalFood = 160f;
                totalIron = 85f;
            }

            // 2. Ordu sayını və ərzaq istehlakını toplayırıq
            int totalTroops = 0;
            float totalArmyConsumption = 0f;

            if (ArmyManager.Instance != null && ArmyManager.Instance.ActiveArmies != null)
            {
                foreach (var army in ArmyManager.Instance.ActiveArmies)
                {
                    if (army != null && army.ArmyData != null &&
                        string.Equals(army.ArmyData.Faction, playerFactionName, StringComparison.OrdinalIgnoreCase))
                    {
                        totalTroops += army.ArmyData.TroopCount;
                        totalArmyConsumption += (army.ArmyData.TroopCount / 1000f) * 1.0f;
                    }
                }
            }

            if (totalTroops <= 0)
            {
                totalTroops = 35000;
                totalArmyConsumption = 35.0f;
            }

            totalPlayerTroops = totalTroops;
            monthlyGoldIncome = totalGold - monthlyHealingExpense; // Faza 10.5: xalis (müalicə xərci çıxılmış)
            monthlyIronIncome = totalIron;
            monthlyFoodNet = totalFood - totalArmyConsumption;
        }

        /// <summary>
        /// Aclıq zamanı oyunçu ordularında müəyyən faizlə (məs: 5%) əsgər itkisi (Attrition) tətbiq edir.
        /// </summary>
        public void ApplyStarvationAttrition(float lossRate = 0.05f)
        {
            if (ArmyManager.Instance == null || ArmyManager.Instance.ActiveArmies == null) return;

            foreach (var army in ArmyManager.Instance.ActiveArmies)
            {
                if (army != null && army.ArmyData != null &&
                    string.Equals(army.ArmyData.Faction, playerFactionName, StringComparison.OrdinalIgnoreCase))
                {
                    int losses = Mathf.CeilToInt(army.ArmyData.TroopCount * lossRate);
                    army.ArmyData.TroopCount = Mathf.Max(100, army.ArmyData.TroopCount - losses);
                    army.ApplyData();
                    Debug.LogWarning($"[GameTimeManager] ⚠️ ACLIQ İTKİSİ: '{army.ArmyData.CommanderName}' ordusu -{losses} əsgər itirdi! Yeni say: {army.ArmyData.TroopCount}");
                }
            }

            RecalculateMonthlyBalances();
        }

        /// <summary>
        /// Qənimət və ya hadisələrdən gələn resursları xəzinəyə əlavə edir (Məs: Yağmalama qəniməti: +2000 Qızıl, +1500 Ərzaq).
        /// </summary>
        public void AddPlayerResources(float gold, float food, float iron = 0f)
        {
            playerGold += gold;
            playerFood += food;
            playerIron += iron;

            SyncWithDatabase();
            OnEconomyUpdated?.Invoke();

            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateTopBar();
            }

            Debug.Log($"[GameTimeManager] 💰 XƏZİNƏYƏ ƏLAVƏ EDİLDİ: +{gold:N0} Qızıl, +{food:N0} Ərzaq, +{iron:N0} Dəmir | Cari: Qızıl={playerGold:N0}, Ərzaq={playerFood:N0}");
        }

        /// <summary>
        /// SQLite verilənlər bazası ilə sinxronizasiya edir.
        /// </summary>
        private void SyncWithDatabase()
        {
            var db = DatabaseManager.Instance;
            if (db != null && db.IsConnected)
            {
                try
                {
                    var nation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1)
                                 ?? db.Connection.Table<Nation>().FirstOrDefault();
                    if (nation != null)
                    {
                        nation.Gold = playerGold;
                        nation.Food = playerFood;
                        db.Connection.Update(nation);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameTimeManager] SQLite sinxronizasiya xətası: {ex.Message}");
                }
            }
        }

        public static string GetAzerbaijaniMonthName(int month)
        {
            int idx = Mathf.Clamp(month, 1, 12) - 1;
            return MonthNamesAz[idx];
        }

        public static int GetDaysInMonth(int year, int month)
        {
            return DateTime.DaysInMonth(Mathf.Max(1, year), Mathf.Clamp(month, 1, 12));
        }
    }
}
