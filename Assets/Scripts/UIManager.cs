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

        [Header("Yuxarı Künc - Tarix, Qızıl/Ərzaq, Demoqrafiya və Zaman Düymələri (Faza 5)")]
        [SerializeField] private Text dateTrackerText;
        [SerializeField] private Text speedStatusText;
        [SerializeField] private Text goldAndFoodText;
        [SerializeField] private Text demographicsSummaryText;
        [SerializeField] private Button pauseTimeButton;
        [SerializeField] private Button playTimeButton;
        [SerializeField] private Button fastSpeedButton;

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

        [Header("Sol Aşağı Künc - Google Imagen Test Paneli")]
        [SerializeField] private Button generateImageButton;
        [SerializeField] private RawImage generatedPreviewRawImage;
        [SerializeField] private Text imagenStatusText;
        [SerializeField] private string imagenTestPrompt = GoogleImageManager.DefaultProvinceIconPrompt;

        [Header("Faza 4 - Səhnələr Arası Keçid (Test Döyüşü)")]
        [SerializeField] private Button startTestBattleButton;

        [Header("Faza 6A - Əyalət İdarəetmə Paneli (İnfrastruktur və Qlobal Əmrlər)")]
        [SerializeField] private GameObject provinceActionPanel;
        [SerializeField] private Text productivityStatusText;
        [SerializeField] private Text constructionStatusText;
        [SerializeField] private Button buildCanalButton;

        [Header("Sülalə və İdarəetmə Göstəricisi (Faza 6D)")]
        [Tooltip("Dövlətin hakim sülalə adını və birbaşa idarəetmə həddini göstərən mətn")]
        [SerializeField] private Text dynastyAndControlText;

        private int _selectedProvinceId = -1;
        private GameManager _boundGameManager;

        public int SelectedProvinceId => _selectedProvinceId;
        public bool IsProvincePanelOpen => provinceInfoPanel != null && provinceInfoPanel.activeSelf;
        public RawImage GeneratedPreviewRawImage => generatedPreviewRawImage;
        public string CurrentImagenStatus => imagenStatusText != null ? imagenStatusText.text : string.Empty;
        public string ImagenTestPrompt => imagenTestPrompt;
        public Button StartTestBattleButton => startTestBattleButton;
        public Button PauseTimeButton => pauseTimeButton;
        public Button PlayTimeButton => playTimeButton;
        public Button FastSpeedButton => fastSpeedButton;
        public GameObject ProvinceActionPanel => provinceActionPanel;
        public Button BuildCanalButton => buildCanalButton;
        public Text DynastyAndControlText => dynastyAndControlText;

        public string CurrentDisplayedDynasty { get; private set; } = string.Empty;
        public string CurrentDisplayedDirectControl { get; private set; } = string.Empty;

        // Test və doğrulama üçün cari ekranda görünən mətn dəyərləri
        public string CurrentDisplayedDate => dateTrackerText != null ? dateTrackerText.text : string.Empty;
        public string CurrentDisplayedSpeed => speedStatusText != null ? speedStatusText.text : string.Empty;
        public string CurrentDisplayedGoldAndFood => goldAndFoodText != null ? goldAndFoodText.text : string.Empty;
        public string CurrentDisplayedDemographicsSummary => demographicsSummaryText != null ? demographicsSummaryText.text : string.Empty;
        public string CurrentDisplayedProvinceTitle => provinceTitleText != null ? provinceTitleText.text : string.Empty;
        public string CurrentDisplayedPopulation => totalPopulationText != null ? totalPopulationText.text : string.Empty;
        public string CurrentDisplayedFood => foodProductionText != null ? foodProductionText.text : string.Empty;
        public string CurrentDisplayedProductivity => productivityStatusText != null ? productivityStatusText.text : string.Empty;
        public string CurrentDisplayedConstructionStatus => constructionStatusText != null ? constructionStatusText.text : string.Empty;
        public string LastCaravanRaidAlert { get; private set; } = string.Empty;

        public void DisplayCaravanRaidAlert(string message)
        {
            LastCaravanRaidAlert = message;
            if (actionStatusText != null)
            {
                actionStatusText.text = $"<color=red><b>{message}</b></color>";
            }
        }

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

            if (generateImageButton != null)
            {
                generateImageButton.onClick.RemoveListener(OnGenerateImageButtonClicked);
                generateImageButton.onClick.AddListener(OnGenerateImageButtonClicked);
            }

            if (startTestBattleButton != null)
            {
                startTestBattleButton.onClick.RemoveListener(OnStartTestBattleButtonClicked);
                startTestBattleButton.onClick.AddListener(OnStartTestBattleButtonClicked);
            }

            if (pauseTimeButton != null)
            {
                pauseTimeButton.onClick.RemoveListener(OnPauseTimeButtonClicked);
                pauseTimeButton.onClick.AddListener(OnPauseTimeButtonClicked);
            }

            if (playTimeButton != null)
            {
                playTimeButton.onClick.RemoveListener(OnPlayTimeButtonClicked);
                playTimeButton.onClick.AddListener(OnPlayTimeButtonClicked);
            }

            if (fastSpeedButton != null)
            {
                fastSpeedButton.onClick.RemoveListener(OnFastSpeedButtonClicked);
                fastSpeedButton.onClick.AddListener(OnFastSpeedButtonClicked);
            }

            if (buildCanalButton != null)
            {
                buildCanalButton.onClick.RemoveListener(OnBuildCanalButtonClicked);
                buildCanalButton.onClick.AddListener(OnBuildCanalButtonClicked);
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

            if (TradeController.Instance != null)
            {
                TradeController.Instance.OnCaravanRaided -= HandleCaravanRaided;
                TradeController.Instance.OnCaravanRaided += HandleCaravanRaided;
            }
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

            if (TradeController.Instance != null)
            {
                TradeController.Instance.OnCaravanRaided -= HandleCaravanRaided;
            }
        }

        private void HandleCaravanRaided(RaidResult res)
        {
            DisplayCaravanRaidAlert(res.Message);
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
        /// Ekranın yuxarı panelindəki Tarix (Ay və İl), Qızıl/Ərzaq və Ümumi Əhali Bölgüsü
        /// (Körpə, Gənc, Yetişkin, Yaşlı, Yaralı) mətnlərini bazadan oxuyaraq yeniləyir.
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

            RefreshGlobalEconomyAndDemographicsHUD();

            if (dateTrackerText != null)
            {
                string dynastyTag = !string.IsNullOrWhiteSpace(CurrentDisplayedDynasty)
                    ? $" | {CurrentDisplayedDynasty} ({CurrentDisplayedDirectControl})"
                    : "";
                dateTrackerText.text = $"Tarix: Ay {gm.CurrentMonth:D2}, İl {gm.CurrentYear:D4} (Gün {gm.CurrentDay:D2}) | Tick #{gm.CurrentTick}{dynastyTag}";
            }

            if (speedStatusText != null)
            {
                string speedLabel = gm.IsPaused ? "FASİLƏ (PAUSED)" : $"SÜRƏT: {(int)gm.CurrentSpeed}x";
                speedStatusText.text = speedLabel;
            }
        }

        private void RefreshGlobalEconomyAndDemographicsHUD()
        {
            if (goldAndFoodText == null
                && demographicsSummaryText == null
                && productivityStatusText == null
                && constructionStatusText == null)
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

            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;
            Province prov = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == targetProvinceId)
                            ?? db.Connection.Table<Province>().FirstOrDefault();

            if (prov == null)
            {
                return;
            }

            Population pop = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == prov.Id);
            int babies = pop != null ? pop.Infants : prov.Babies;
            int youths = pop != null ? pop.Youths : prov.Youths;
            int adults = pop != null ? pop.Adults : prov.Adults;
            int elders = pop != null ? pop.Elders : prov.Elders;
            int wounded = pop != null ? pop.Wounded : prov.Wounded;
            int total = babies + youths + adults + elders + wounded;

            if (goldAndFoodText != null)
            {
                string siegeTag = prov.IsBesieged ? " [MÜHASİRƏ]" : "";
                goldAndFoodText.text = $"Qızıl: {prov.Gold:F0}  |  Ərzaq: {prov.Food:F0}  |  Dəmir: {prov.Iron:F0}  |  At: {prov.Horses:F0}  |  İpək: {prov.Silk:F0}  |  Moral: {prov.PublicOrder:F0}%  [{prov.Name}{siegeTag}]";
            }

            if (demographicsSummaryText != null)
            {
                demographicsSummaryText.text =
                    $"Əhali ({total}): Körpə: {babies} | Gənc: {youths} | Yetişkin: {adults} | Yaşlı: {elders} | Yaralı: {wounded}";
            }

            Nation nation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == prov.OwnerFactionId)
                            ?? db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1)
                            ?? db.Connection.Table<Nation>().FirstOrDefault();

            if (nation != null)
            {
                CurrentDisplayedDynasty = nation.RulingDynastyName;
                CurrentDisplayedDirectControl = $"Birbaşa İdarə: {nation.DirectControlledProvinceCount}/{nation.MaxDirectControl}";
                string overextTag = nation.OverextensionCount > 0
                    ? $" [OVEREXTENSION: +{nation.OverextensionCount} (-{nation.OverextensionPenaltyRate * 100f:F0}% Vergi/Sədaqət)]"
                    : "";

                if (dynastyAndControlText != null)
                {
                    dynastyAndControlText.text = $"Dövlət: {nation.RulingDynastyName}  |  {CurrentDisplayedDirectControl}{overextTag}";
                }
            }

            RefreshInfrastructureActionPanel(prov);
        }

        private void RefreshInfrastructureActionPanel(Province prov)
        {
            if (prov == null)
            {
                return;
            }

            float prodMultiplier = prov.ProductivityMultiplier > 0f ? prov.ProductivityMultiplier : 1.0f;
            float prodPercent = prodMultiplier * 100f;

            if (productivityStatusText != null)
            {
                productivityStatusText.text = $"Məhsuldarlıq: {prodPercent:F0}% ({prov.Name})";
            }

            if (constructionStatusText != null)
            {
                if (prov.HasActiveConstruction)
                {
                    constructionStatusText.text = $"{prov.ActiveConstruction} çəkilir... {prov.ConstructionMonthsLeft} ay qaldı";
                }
                else
                {
                    constructionStatusText.text = "Aktiv Tikinti: Yoxdur";
                }
            }

            if (buildCanalButton != null)
            {
                buildCanalButton.interactable = !prov.HasActiveConstruction && prov.Gold >= InfrastructureController.DefaultCanalGoldCost;
            }
        }

        /// <summary>
        /// "Pause (||)" düyməsinə basıldıqda zaman axınını dayandırır.
        /// </summary>
        public void OnPauseTimeButtonClicked()
        {
            TimeManager tm = TimeManager.Instance != null
                ? TimeManager.Instance
                : FindAnyObjectByType<TimeManager>();

            if (tm != null)
            {
                tm.Pause();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.PauseGame();
            }

            RefreshDateTrackerUI();
        }

        /// <summary>
        /// "Play (>)" düyməsinə basıldıqda zaman axınını 1x sürətlə başladır.
        /// </summary>
        public void OnPlayTimeButtonClicked()
        {
            TimeManager tm = TimeManager.Instance != null
                ? TimeManager.Instance
                : FindAnyObjectByType<TimeManager>();

            if (tm != null)
            {
                tm.SetSpeed(1);
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameSpeed(GameSpeed.Normal1x);
            }

            RefreshDateTrackerUI();
        }

        /// <summary>
        /// "2x (>>)" düyməsinə basıldıqda zaman axınını 2x sürətləndirir.
        /// </summary>
        public void OnFastSpeedButtonClicked()
        {
            TimeManager tm = TimeManager.Instance != null
                ? TimeManager.Instance
                : FindAnyObjectByType<TimeManager>();

            if (tm != null)
            {
                tm.SetSpeed(2);
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameSpeed(GameSpeed.Fast2x);
            }

            RefreshDateTrackerUI();
        }

        /// <summary>
        /// Oyunçu ProvinceNode üzərinə tıkladıqda sağ tərəfdəki Əyalət Məlumat Panelini açır
        /// və ən son baza məlumatlarını göstərir.
        /// </summary>
        /// <summary>
        /// Əyalət paneli açıb göstərir və ya seçir.
        /// </summary>
        public void SelectProvince(int provinceId)
        {
            ShowProvincePanel(provinceId);
        }

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
                _selectedProvinceId = 1;
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
                string siegeStr = province.IsBesieged ? (province.IsExploited ? "Mühasirədə (İstismar)" : "Mühasirədə") : "Açıq";
                economyDetailsText.text =
                    $"Qızıl: {province.Gold:F1} | Ərzaq: {province.Food:F1} | Dəmir: {province.Iron:F0} | At: {province.Horses:F0} | İpək: {province.Silk:F0}\n" +
                    $"Moral: {province.PublicOrder:F0}/100 | Vəziyyət: {siegeStr} | Doğum: {province.BirthRateModifier * 100f:F0}% | Vergi: {economy.LocalTaxIncome:F1}";
            }

            RefreshInfrastructureActionPanel(province);
            RefreshGlobalEconomyAndDemographicsHUD();

            Debug.Log(
                $"[UIManager] Əyalət Paneli Yeniləndi -> {province.Name} (İqlim: {province.Climate}, İnkişaf: {province.DevelopmentLevel}, Məhsuldarlıq: {province.ProductivityMultiplier * 100f:F0}%) | " +
                $"Ümumi Əhali: {population.TotalPopulation} (Yetişkin: {population.Adults}, Gənc: {population.Youths}) | " +
                $"Ərzaq İstehsalı: {economy.FoodProduction:F1} | Vergi: {economy.LocalTaxIncome:F1}");
        }

        /// <summary>
        /// "Kanal Çək (Xərc: 5000 Qızıl, Müddət: 6 Ay)" düyməsinə basıldıqda çağırılır:
        /// InfrastructureController.StartConstruction funksiyasını işə salır və UI-ı yeniləyir.
        /// </summary>
        public void OnBuildCanalButtonClicked()
        {
            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;

            InfrastructureController infraController = InfrastructureController.Instance != null
                ? InfrastructureController.Instance
                : FindAnyObjectByType<InfrastructureController>();

            if (infraController == null)
            {
                Debug.LogError("[UIManager] Səhnədə InfrastructureController tapılmadı!");
                return;
            }

            ConstructionStartResult startResult = infraController.StartConstruction(
                provinceId: targetProvinceId.ToString(),
                projectName: InfrastructureController.DefaultCanalProjectName,
                goldCost: InfrastructureController.DefaultCanalGoldCost,
                durationMonths: InfrastructureController.DefaultCanalDurationMonths);

            if (actionStatusText != null)
            {
                actionStatusText.text = startResult.Success
                    ? $"'{InfrastructureController.DefaultCanalProjectName}' tikintisi başladı! ({InfrastructureController.DefaultCanalDurationMonths} ay)"
                    : startResult.Message;
            }

            RefreshSelectedProvinceUI();
            RefreshDateTrackerUI();
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
                    float prodMultiplier = prov.ProductivityMultiplier > 0f ? prov.ProductivityMultiplier : 1.0f;

                    eco.FoodProduction = (pop.Adults / 100f) * prov.DevelopmentLevel * climateBonus * prodMultiplier;
                    eco.RawMaterials = (pop.Adults / 200f) * prov.DevelopmentLevel * prodMultiplier;
                    eco.LocalTaxIncome = ((pop.Adults * 0.5f) + (eco.LuxuryGoods * 2.0f)) * prodMultiplier;
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

        #region Faza 6C Əmrləri (Mühasirə, Yaralıların Edamı və Fövqəladə Vəziyyət)

        /// <summary>
        /// Faza 6C: "Yaralıları Edam Et" düyməsi:
        /// Ərzağa qənaət etmək üçün yaralılar (Wounded) sıfırlanır, lakin PublicOrder -= 40 düşür.
        /// </summary>
        public void OnExecuteWoundedButtonClicked()
        {
            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;
            DemographicsController demoController = DemographicsController.Instance != null
                ? DemographicsController.Instance
                : FindAnyObjectByType<DemographicsController>();

            if (demoController != null && demoController.ExecuteWounded(targetProvinceId))
            {
                if (actionStatusText != null)
                {
                    actionStatusText.text = "Yaralılar edam edildi! Ərzaq xərci azaldı, lakin Xalqın Moralı kəskin düşdü (-40).";
                }
            }

            RefreshSelectedProvinceUI();
            RefreshDateTrackerUI();
        }

        /// <summary>
        /// Faza 6C: "Yağmala (Plunder)" düyməsi:
        /// 2000 Qızıl və Ərzaq əldə edilir, lakin məhsuldarlıq 0.2f-ə düşür, PublicOrder 0 olur.
        /// </summary>
        public void OnPlunderButtonClicked()
        {
            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;
            SiegeController siegeController = SiegeController.Instance != null
                ? SiegeController.Instance
                : FindAnyObjectByType<SiegeController>();

            if (siegeController != null)
            {
                PlunderResult res = siegeController.Plunder(targetProvinceId);
                if (res.Success && actionStatusText != null)
                {
                    actionStatusText.text = $"Əyalət yağmalandı! +{res.GoldGained:F0} Qızıl, +{res.FoodGained:F0} Ərzaq. Kənd dağıldı (Məhsuldarlıq: 20%, Moral: 0).";
                }
            }

            RefreshSelectedProvinceUI();
            RefreshDateTrackerUI();
        }

        /// <summary>
        /// Faza 6C: "İstismar Et (Exploit)" düyməsi:
        /// Aylıq ərzaq orduya axır, lakin xalq əzildiyi üçün hər ay PublicOrder -= 5 düşür.
        /// </summary>
        public void OnExploitButtonClicked()
        {
            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;
            SiegeController siegeController = SiegeController.Instance != null
                ? SiegeController.Instance
                : FindAnyObjectByType<SiegeController>();

            if (siegeController != null && siegeController.Exploit(targetProvinceId))
            {
                if (actionStatusText != null)
                {
                    actionStatusText.text = "Mühasirə istismarı başladıldı! Aylıq ərzaq orduya yönləndirilir, PublicOrder hər ay -5 düşəcək.";
                }
            }

            RefreshSelectedProvinceUI();
            RefreshDateTrackerUI();
        }

        /// <summary>
        /// Faza 6C: "Fövqəladə Vəziyyət (Emergency Conscription)" düyməsi:
        /// Yetişkin əhalinin 20%-i (qadınlar) orduya çağırılır, cəza olaraq BirthRateModifier 0.2f-ə düşür.
        /// </summary>
        public void OnEmergencyConscriptionButtonClicked()
        {
            int targetProvinceId = _selectedProvinceId > 0 ? _selectedProvinceId : 1;
            MilitaryAndDiplomacyController milController = MilitaryAndDiplomacyController.Instance != null
                ? MilitaryAndDiplomacyController.Instance
                : FindAnyObjectByType<MilitaryAndDiplomacyController>();

            if (milController != null)
            {
                EmergencyConscriptionResult res = milController.CallEmergencyConscription(targetProvinceId);
                if (res.Success && actionStatusText != null)
                {
                    actionStatusText.text = $"Fövqəladə Vəziyyət! {res.ConscriptedAdults} qadın/yetkin orduya cəlb edildi. CƏZA: Doğum çarpanı 0.2x oldu!";
                }
            }

            RefreshSelectedProvinceUI();
            RefreshDateTrackerUI();
        }

        #endregion

        /// <summary>
        /// "Şəkil Yarat" düyməsinə basıldıqda GoogleImageManager vasitəsilə
        /// "medieval strategy game province icon, 2D UI element, transparent background"
        /// promptunu göndərir və nəticəni ekrandakı RawImage üzərində göstərir.
        /// </summary>
        public void OnGenerateImageButtonClicked()
        {
            GoogleImageManager imgManager = GoogleImageManager.Instance != null
                ? GoogleImageManager.Instance
                : FindAnyObjectByType<GoogleImageManager>();

            if (imgManager == null)
            {
                const string missingErr = "[UIManager] Səhnədə GoogleImageManager tapılmadı!";
                Debug.LogError(missingErr);
                if (imagenStatusText != null)
                {
                    imagenStatusText.text = missingErr;
                }
                return;
            }

            string promptToUse = string.IsNullOrWhiteSpace(imagenTestPrompt)
                ? GoogleImageManager.DefaultProvinceIconPrompt
                : imagenTestPrompt;

            if (imagenStatusText != null)
            {
                imagenStatusText.text = $"Google Imagen sorğusu göndərilir...\nPrompt: \"{promptToUse}\"";
            }

            imgManager.GenerateImage(
                promptToUse,
                onSuccess: ApplyGeneratedTextureToRawImage,
                onError: errMsg =>
                {
                    if (imagenStatusText != null)
                    {
                        imagenStatusText.text = errMsg;
                    }
                });
        }

        /// <summary>
        /// GoogleImageManager-dən qayıdan Texture2D obyektini ekrandakı RawImage üzərinə təyin edir.
        /// </summary>
        public void ApplyGeneratedTextureToRawImage(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (generatedPreviewRawImage != null)
            {
                generatedPreviewRawImage.texture = texture;
                generatedPreviewRawImage.color = Color.white;
            }

            if (imagenStatusText != null)
            {
                imagenStatusText.text = $"Şəkil uğurla yaradıldı! ({texture.width}x{texture.height})";
            }

            Debug.Log($"[UIManager] Google Imagen nəticəsi RawImage üzərində göstərildi ({texture.width}x{texture.height}).");
        }

        /// <summary>
        /// "Test Döyüşünə Başla" düyməsinə basıldıqda bazada test ordularının (Army_1 və Army_2)
        /// mövcudluğunu təmin edir və GameManager.Instance.StartBattle("Army_1", "Army_2") metodunu işə salır.
        /// </summary>
        public void OnStartTestBattleButtonClicked()
        {
            GameManager gm = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gm == null)
            {
                Debug.LogError("[UIManager] Səhnədə GameManager tapılmadı!");
                return;
            }

            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db != null)
            {
                if (!db.IsConnected)
                {
                    db.InitializeDatabase();
                }

                EnsureTestArmiesExistInDatabase(db);
            }

            gm.StartBattle("Army_1", "Army_2");
        }

        private static void EnsureTestArmiesExistInDatabase(DatabaseManager db)
        {
            if (db == null || !db.IsConnected)
            {
                return;
            }

            db.RunInTransaction(conn =>
            {
                Army army1 = conn.Table<Army>().FirstOrDefault(a => a.Id == 1);
                if (army1 == null)
                {
                    conn.Insert(new Army
                    {
                        Id = 1,
                        OwnerFactionId = 1,
                        LocationProvinceId = 1,
                        YouthSoldiers = 1500,
                        AdultSoldiers = 1800,
                        Morale = 1.0f
                    });
                }

                Army army2 = conn.Table<Army>().FirstOrDefault(a => a.Id == 2);
                if (army2 == null)
                {
                    conn.Insert(new Army
                    {
                        Id = 2,
                        OwnerFactionId = 2,
                        LocationProvinceId = 2,
                        YouthSoldiers = 1000,
                        AdultSoldiers = 1200,
                        Morale = 1.0f
                    });
                }
            });
        }
    }
}
