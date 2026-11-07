using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// 2D Qlobal Xəritədəki "Əyalət Məlumat Paneli"ni, "Səfərbərlik (Draft Army)" düyməsini
    /// və ekranın yuxarı küncündəki "Tick / Tarix İzləyicisi"ni idarə edən UI meneceri.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<UIManager>());

        [Header("Yuxarı Künc - Tarix və Tick İzləyicisi")]
        [SerializeField] private Text dateTrackerText;
        [SerializeField] private Text speedStatusText;

        [Header("Sağ Tərəf - Əyalət Məlumat Paneli")]
        [SerializeField] private GameObject provinceInfoPanel;
        [SerializeField] private Text provinceTitleText;
        [SerializeField] private Text climateAndDevText;
        [SerializeField] private Text totalPopulationText;
        [SerializeField] private Text populationBreakdownText;
        [SerializeField] private Text foodProductionText;
        [SerializeField] private Text economyDetailsText;
        [SerializeField] private Text actionStatusText;
        [SerializeField] private Button draftArmyButton;
        [SerializeField] private Button closePanelButton;

        private int _selectedProvinceId = -1;
        private GameManager _boundGameManager;

        public int SelectedProvinceId => _selectedProvinceId;
        public bool IsProvincePanelOpen => provinceInfoPanel != null && provinceInfoPanel.activeSelf;

        // Test və doğrulama üçün cari ekranda görünən mətn dəyərləri
        public string CurrentDisplayedDate => dateTrackerText != null ? dateTrackerText.text : string.Empty;
        public string CurrentDisplayedProvinceTitle => provinceTitleText != null ? provinceTitleText.text : string.Empty;
        public string CurrentDisplayedPopulation => totalPopulationText != null ? totalPopulationText.text : string.Empty;
        public string CurrentDisplayedFood => foodProductionText != null ? foodProductionText.text : string.Empty;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            WireButtons();
        }

        private void OnEnable()
        {
            BindToGameManagerEvents();
            RefreshDateTrackerUI();
        }

        private void Start()
        {
            BindToGameManagerEvents();
            RefreshDateTrackerUI();

            if (_selectedProvinceId <= 0 && provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(false);
            }
        }

        private void OnDisable()
        {
            UnbindFromGameManagerEvents();
        }

        private void WireButtons()
        {
            if (draftArmyButton != null)
            {
                draftArmyButton.onClick.RemoveListener(OnDraftArmyButtonClicked);
                draftArmyButton.onClick.AddListener(OnDraftArmyButtonClicked);
            }

            if (closePanelButton != null)
            {
                closePanelButton.onClick.RemoveListener(HideProvincePanel);
                closePanelButton.onClick.AddListener(HideProvincePanel);
            }
        }

        /// <summary>
        /// GameManager-in zaman hadisələrinə (OnDayPassed, OnMonthPassed, OnYearPassed) abunə olur.
        /// </summary>
        public void BindToGameManagerEvents()
        {
            GameManager gm = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gm == null || _boundGameManager == gm)
            {
                return;
            }

            UnbindFromGameManagerEvents();

            _boundGameManager = gm;
            _boundGameManager.OnDayPassed += HandleDayPassed;
            _boundGameManager.OnMonthPassed += HandleMonthPassed;
            _boundGameManager.OnYearPassed += HandleYearPassed;
            _boundGameManager.OnGameSpeedChanged += HandleSpeedChanged;
        }

        private void UnbindFromGameManagerEvents()
        {
            if (_boundGameManager == null)
            {
                return;
            }

            _boundGameManager.OnDayPassed -= HandleDayPassed;
            _boundGameManager.OnMonthPassed -= HandleMonthPassed;
            _boundGameManager.OnYearPassed -= HandleYearPassed;
            _boundGameManager.OnGameSpeedChanged -= HandleSpeedChanged;
            _boundGameManager = null;
        }

        private void HandleDayPassed(int day, int month, int year)
        {
            RefreshDateTrackerUI();
        }

        private void HandleMonthPassed(int month, int year)
        {
            RefreshDateTrackerUI();
            if (_selectedProvinceId > 0 && IsProvincePanelOpen)
            {
                RefreshSelectedProvinceUI();
            }
        }

        private void HandleYearPassed(int year)
        {
            RefreshDateTrackerUI();
            if (_selectedProvinceId > 0 && IsProvincePanelOpen)
            {
                RefreshSelectedProvinceUI();
            }
        }

        private void HandleSpeedChanged(GameSpeed speed)
        {
            RefreshDateTrackerUI();
        }

        /// <summary>
        /// Ekranın yuxarı küncündəki Tarix (İl, Ay, Gün) və Tick mətnini yeniləyir.
        /// </summary>
        public void RefreshDateTrackerUI()
        {
            GameManager gm = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gm == null)
            {
                return;
            }

            if (gm.CurrentYear == 0)
            {
                gm.InitializeCalendar();
            }

            if (dateTrackerText != null)
            {
                dateTrackerText.text = $"Tarix: İl {gm.CurrentYear:D4}, Ay {gm.CurrentMonth:D2} (Gün {gm.CurrentDay:D2}) | Tick #{gm.CurrentTick}";
            }

            if (speedStatusText != null)
            {
                string speedLabel = gm.IsPaused ? "FASİLƏ (PAUSED)" : $"SÜRƏT: {(int)gm.CurrentSpeed}x";
                speedStatusText.text = speedLabel;
            }
        }

        /// <summary>
        /// Oyunçu ProvinceNode üzərinə tıkladıqda sağ tərəfdəki Əyalət Məlumat Panelini açır
        /// və ən son baza məlumatlarını göstərir.
        /// </summary>
        public void ShowProvincePanel(int provinceId)
        {
            _selectedProvinceId = provinceId;

            if (provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(true);
            }

            if (actionStatusText != null)
            {
                actionStatusText.text = "Əyalət seçildi. Səfərbərlik üçün hazır.";
            }

            RefreshSelectedProvinceUI();
        }

        public void HideProvincePanel()
        {
            _selectedProvinceId = -1;
            if (provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Seçilmiş əyalətin ən son məlumatlarını DatabaseManager-dən oxuyaraq UI mətnlərini yeniləyir.
        /// </summary>
        public void RefreshSelectedProvinceUI()
        {
            if (_selectedProvinceId <= 0)
            {
                return;
            }

            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db == null)
            {
                return;
            }

            if (!db.IsConnected)
            {
                db.InitializeDatabase();
            }

            Province province = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == _selectedProvinceId);
            Population population = db.Connection.Table<Population>().FirstOrDefault(pop => pop.ProvinceId == _selectedProvinceId);
            Economy economy = db.Connection.Table<Economy>().FirstOrDefault(eco => eco.ProvinceId == _selectedProvinceId);

            if (province == null || population == null || economy == null)
            {
                return;
            }

            if (provinceTitleText != null)
            {
                provinceTitleText.text = $"Əyalət: {province.Name}";
            }

            if (climateAndDevText != null)
            {
                climateAndDevText.text = $"İqlim: {province.Climate}  |  İnkişaf Səviyyəsi: {province.DevelopmentLevel}";
            }

            if (totalPopulationText != null)
            {
                totalPopulationText.text = $"Ümumi Əhali: {population.TotalPopulation} nəfər";
            }

            if (populationBreakdownText != null)
            {
                populationBreakdownText.text =
                    $"Yetişkin (İşçi/Piyada): {population.Adults} | Gənc (Süvari): {population.Youths}\n" +
                    $"Körpə: {population.Infants} | Yaşlı: {population.Elders} | Yaralı: {population.Wounded}";
            }

            if (foodProductionText != null)
            {
                float consumption = population.TotalPopulation * 0.1f;
                float balance = economy.FoodProduction - consumption;
                string balanceSign = balance >= 0f ? $"+{balance:F1}" : $"{balance:F1}";
                foodProductionText.text = $"İstehsal Edilən Ərzaq: {economy.FoodProduction:F1} (Balans: {balanceSign})";
            }

            if (economyDetailsText != null)
            {
                economyDetailsText.text = $"Xammal: {economy.RawMaterials:F1}  |  Vergi Gəliri: {economy.LocalTaxIncome:F1}";
            }

            Debug.Log(
                $"[UIManager] Əyalət Paneli Yeniləndi -> {province.Name} (İqlim: {province.Climate}, İnkişaf: {province.DevelopmentLevel}) | " +
                $"Ümumi Əhali: {population.TotalPopulation} (Yetişkin: {population.Adults}, Gənc: {population.Youths}) | " +
                $"Ərzaq İstehsalı: {economy.FoodProduction:F1} | Vergi: {economy.LocalTaxIncome:F1}");
        }

        /// <summary>
        /// "Səfərbərlik (Draft Army)" düyməsinə basıldıqda çağırılır:
        /// 1. MilitaryAndDiplomacyController.DraftArmy() metodunu icra edir.
        /// 2. İşçi qüvvəsi (Adults) azaldığı üçün əyalətin cari ərzaq/xammal/vergi istehsalını dərhal yenidən hesablayır.
        /// 3. UI panelini anında yeniləyir ki, oyunçu əhali və ərzaq istehsalının düşməsini dərhal görsün.
        /// </summary>
        public void OnDraftArmyButtonClicked()
        {
            if (_selectedProvinceId <= 0)
            {
                return;
            }

            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            MilitaryAndDiplomacyController milController = MilitaryAndDiplomacyController.Instance != null
                ? MilitaryAndDiplomacyController.Instance
                : FindAnyObjectByType<MilitaryAndDiplomacyController>();

            EconomyController ecoController = EconomyController.Instance != null
                ? EconomyController.Instance
                : FindAnyObjectByType<EconomyController>();

            if (db == null || milController == null)
            {
                Debug.LogError("[UIManager] Səfərbərlik üçün DatabaseManager və ya MilitaryAndDiplomacyController tapılmadı!");
                return;
            }

            if (!db.IsConnected)
            {
                db.InitializeDatabase();
            }

            // 1. Səfərbərliyi icra edirik (Youths 50% və Adults 30% azalır, yeni Army yaradılır)
            DraftArmyResult draftResult = milController.DraftArmy(db, _selectedProvinceId);
            if (!draftResult.Success)
            {
                return;
            }

            // 2. İşçi qüvvəsi (Adults) azaldığı üçün həmin əyalətin cari iqtisadi istehsalını (FoodProduction, RawMaterials, Tax)
            // dərhal bazada yeniləyirik ki, oyunçu UI-da ərzaq istehsalının anında necə düşdüyünü görsün
            db.RunInTransaction(conn =>
            {
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.Id == _selectedProvinceId);
                Population pop = conn.Table<Population>().FirstOrDefault(p => p.ProvinceId == _selectedProvinceId);
                Economy eco = conn.Table<Economy>().FirstOrDefault(e => e.ProvinceId == _selectedProvinceId);

                if (prov != null && pop != null && eco != null)
                {
                    float climateBonus = ecoController != null
                        ? ecoController.GetClimateFoodMultiplier(prov.Climate)
                        : 1.0f;

                    eco.FoodProduction = (pop.Adults / 100f) * prov.DevelopmentLevel * climateBonus;
                    eco.RawMaterials = (pop.Adults / 200f) * prov.DevelopmentLevel;
                    eco.LocalTaxIncome = (pop.Adults * 0.5f) + (eco.LuxuryGoods * 2.0f);
                    conn.Update(eco);
                }
            });

            if (actionStatusText != null)
            {
                actionStatusText.text =
                    $"Ordu #{draftResult.CreatedArmy.Id} yaradıldı! ({draftResult.CreatedArmy.TotalSoldiers} əsgər: " +
                    $"{draftResult.CreatedArmy.YouthSoldiers} Süvari, {draftResult.CreatedArmy.AdultSoldiers} Piyada)";
            }

            // 3. UI Panelini dərhal yeniləyirik
            RefreshSelectedProvinceUI();
        }
    }
}
