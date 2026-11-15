using System;
using System.Globalization;
using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Military;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

        [Header("Faza 9.7 & 9.8 - Premium Əyalət Məlumat Paneli")]
        [SerializeField] private Text provinceNameText;
        [SerializeField] private GameObject ownerBanner;
        [SerializeField] private Image ownerColorBadge;
        [SerializeField] private Text ownerText;
        [SerializeField] private Text rulerText;
        [SerializeField] private Text populationText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text foodText;
        [SerializeField] private Text ironText;
        [SerializeField] private Button closeButton;

        private MapInteractionController _selectedMapProvince;

        public GameObject ProvinceInfoPanel => provinceInfoPanel;
        public GameObject OwnerBanner => ownerBanner;
        public Image OwnerColorBadge => ownerColorBadge;
        public Text ProvinceNameText => provinceNameText != null ? provinceNameText : provinceTitleText;
        public Text OwnerText => ownerText;
        public Text RulerText => rulerText;
        public Text PopulationText => populationText != null ? populationText : totalPopulationText;
        public Text GoldText => goldText;
        public Text FoodText => foodText != null ? foodText : foodProductionText;
        public Text IronText => ironText;
        public Button CloseButton => closeButton != null ? closeButton : closePanelButton;
        public MapInteractionController SelectedMapProvince => _selectedMapProvince;
        public ProvinceEconomyData CurrentProvinceEconomyData { get; private set; }

        [Header("Faza 10.3 - Mühasirə və Real Logistika Paneli")]
        [SerializeField] private GameObject siegeLogisticsPanel;
        [SerializeField] private Text siegeTitleText;
        [SerializeField] private Text siegeProvinceText;
        [SerializeField] private Text siegeArmyText;
        [SerializeField] private Text siegeNeedText;
        [SerializeField] private Text siegeSupplyText;
        [SerializeField] private Text siegeResultText;
        [SerializeField] private Image siegeResultBackground;
        [SerializeField] private Button pillageButton;
        [SerializeField] private Button exploitButton;
        [SerializeField] private Button closeSiegeButton;

        private SiegeLogisticsData _activeSiegeData;

        public GameObject SiegeLogisticsPanel => siegeLogisticsPanel;
        public bool IsSiegePanelOpen => siegeLogisticsPanel != null && siegeLogisticsPanel.activeSelf;
        public Text SiegeTitleText => siegeTitleText;
        public Text SiegeProvinceText => siegeProvinceText;
        public Text SiegeArmyText => siegeArmyText;
        public Text SiegeNeedText => siegeNeedText;
        public Text SiegeSupplyText => siegeSupplyText;
        public Text SiegeResultText => siegeResultText;
        public Button PillageButton => pillageButton;
        public Button ExploitButton => exploitButton;
        public Button CloseSiegeButton => closeSiegeButton;
        public SiegeLogisticsData ActiveSiegeData => _activeSiegeData;

        [Header("Faza 10.12 - Hökmdar Döyüş Qərarı (RoyalBattlePrompt)")]
        [SerializeField] private GameObject royalBattlePromptPanel;
        [SerializeField] private TextMeshProUGUI royalBattleTitleText;
        [SerializeField] private TextMeshProUGUI royalBattleBodyText;
        [SerializeField] private Button automateBattleButton;
        [SerializeField] private Button leadBattlePersonallyButton;

        private CombatManager.ActiveBattleData _activeRoyalBattle;

        public GameObject RoyalBattlePromptPanel => royalBattlePromptPanel;
        public bool IsRoyalBattlePromptOpen => royalBattlePromptPanel != null && royalBattlePromptPanel.activeSelf;
        public TextMeshProUGUI RoyalBattleTitleText => royalBattleTitleText;
        public TextMeshProUGUI RoyalBattleBodyText => royalBattleBodyText;
        public Button AutomateBattleButton => automateBattleButton;
        public Button LeadBattlePersonallyButton => leadBattlePersonallyButton;
        public CombatManager.ActiveBattleData ActiveRoyalBattle => _activeRoyalBattle;

        [Header("Faza 10.4 - Yuxarı Dövlət Paneli (Top Bar UI)")]
        [SerializeField] private GameObject topBarPanel;
        [SerializeField] private Image topBarOwnerBadge;
        [SerializeField] private Text topBarFactionLeaderText;
        [SerializeField] private Text topBarGoldText;
        [SerializeField] private Text topBarFoodText;
        [SerializeField] private Text topBarIronText;
        [SerializeField] private Text topBarTroopsText;
        [SerializeField] private Text topBarDateText;
        [SerializeField] private Button topBarPauseBtn;
        [SerializeField] private Button topBarSpeed1Btn;
        [SerializeField] private Button topBarSpeed2Btn;
        [SerializeField] private Button topBarSpeed3Btn;
        [SerializeField] private Button topBarSpeed4Btn;
        [SerializeField] private Button topBarSpeed5Btn;

        public GameObject TopBarPanel => topBarPanel;
        public Image TopBarOwnerBadge => topBarOwnerBadge;
        public Text TopBarFactionLeaderText => topBarFactionLeaderText;
        public Text TopBarGoldText => topBarGoldText;
        public Text TopBarFoodText => topBarFoodText;
        public Text TopBarIronText => topBarIronText;
        public Text TopBarTroopsText => topBarTroopsText;
        public Text TopBarDateText => topBarDateText;
        public Button TopBarPauseButton => topBarPauseBtn;
        public Button TopBarSpeed1Button => topBarSpeed1Btn;
        public Button TopBarSpeed2Button => topBarSpeed2Btn;
        public Button TopBarSpeed3Button => topBarSpeed3Btn;
        public Button TopBarSpeed4Button => topBarSpeed4Btn;
        public Button TopBarSpeed5Button => topBarSpeed5Btn;

        [Header("Faza 10.5 - Demoqrafiya və İdarəetmə Bölməsi")]
        [SerializeField] private GameObject demographicsPanel;
        [SerializeField] private Text demoInfantsText;
        [SerializeField] private Text demoYouthsText;
        [SerializeField] private Text demoAdultMenText;
        [SerializeField] private Text demoAdultWomenText;
        [SerializeField] private Text demoElderlyText;
        [SerializeField] private Text demoSickText;
        [SerializeField] private Text demoWoundedText;
        [SerializeField] private Text demoPublicOrderText;
        [SerializeField] private Text demoStatusText;
        [SerializeField] private Button conscriptMenButton;
        [SerializeField] private Button trainEliteButton;
        [SerializeField] private Button mobilizeWomenButton;
        [SerializeField] private Button executeSickButton;

        public GameObject DemographicsPanel => demographicsPanel;
        public Text DemoInfantsText => demoInfantsText;
        public Text DemoYouthsText => demoYouthsText;
        public Text DemoAdultMenText => demoAdultMenText;
        public Text DemoAdultWomenText => demoAdultWomenText;
        public Text DemoElderlyText => demoElderlyText;
        public Text DemoSickText => demoSickText;
        public Text DemoWoundedText => demoWoundedText;
        public Text DemoPublicOrderText => demoPublicOrderText;
        public Text DemoStatusText => demoStatusText;
        public Button ConscriptMenButton => conscriptMenButton;
        public Button TrainEliteButton => trainEliteButton;
        public Button MobilizeWomenButton => mobilizeWomenButton;
        public Button ExecuteSickButton => executeSickButton;

        [Header("Faza 10.8 - AAA Grand Strategy HUD Panelləri")]
        [Header("1. Sol Yuxarı - Dövlət və Hökmdar Paneli (Top-Left Anchor)")]
        [SerializeField] private GameObject factionProfilePanel;
        [SerializeField] private Image factionCrestImage;
        [SerializeField] private TextMeshProUGUI factionNameText;
        [SerializeField] private TextMeshProUGUI factionLeaderText;

        [Header("2. Sol Aşağı - Bildirişlər və Salnamə Paneli (Bottom-Left Anchor)")]
        [SerializeField] private GameObject eventLogPanel;
        [SerializeField] private ScrollRect eventLogScrollRect;
        [SerializeField] private RectTransform eventLogContent;

        public GameObject FactionProfilePanel => factionProfilePanel;
        public Image FactionCrestImage => factionCrestImage;
        public TextMeshProUGUI FactionNameText => factionNameText;
        public TextMeshProUGUI FactionLeaderText => factionLeaderText;

        public GameObject EventLogPanel => eventLogPanel;
        public ScrollRect EventLogScrollRect => eventLogScrollRect;
        public RectTransform EventLogContent => eventLogContent;

        [Header("Faza 10.9 - Modulyar UI Tema Mühərriki")]
        [SerializeField] private UIThemeManager themeManager;
        public UIThemeManager ThemeManager => themeManager;

        [Header("Faza 10.10 - AAA Qlobal Diplomatiya Mərkəzi (Diplomacy Ledger)")]
        [SerializeField] private DiplomacyUIManager diplomacyUIManager;
        public DiplomacyUIManager DiplomacyUIManager => diplomacyUIManager;
        public GameObject DiplomacyLedgerPanel => diplomacyUIManager != null ? diplomacyUIManager.DiplomacyLedgerPanel : null;

        [Header("Faza 4 - Səhnələr Arası Keçid (Test Döyüşü)")]
        [SerializeField] private Button startTestBattleButton;

        [Header("Sülalə və İdarəetmə Göstəricisi (Faza 6D)")]
        [Tooltip("Dövlətin hakim sülalə adını və birbaşa idarəetmə həddini göstərən mətn")]
        [SerializeField] private Text dynastyAndControlText;

        private int _selectedProvinceId = -1;
        private GameManager _boundGameManager;

        public int SelectedProvinceId => _selectedProvinceId;
        public bool IsProvincePanelOpen => provinceInfoPanel != null && provinceInfoPanel.activeSelf;
        public Button StartTestBattleButton => startTestBattleButton;
        public Button PauseTimeButton => pauseTimeButton;
        public Button PlayTimeButton => playTimeButton;
        public Button FastSpeedButton => fastSpeedButton;
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
            EnsureProvincePanelHierarchy();
            EnsureSiegeLogisticsPanelHierarchy();
            EnsureRoyalBattlePromptHierarchy();
            EnsureTopBarHierarchy();
            EnsureGrandStrategyHUDHierarchy();
            WireButtons();
        }

        private void OnEnable()
        {
            BindToGameManagerEvents();
            RefreshDateTrackerUI();
            UpdateTopBar();
            UpdateTopBarSpeedButtons();
            UpdateFactionProfileFromCurrentGame();
        }

        private void Start()
        {
            EnsureProvincePanelHierarchy();
            EnsureSiegeLogisticsPanelHierarchy();
            EnsureRoyalBattlePromptHierarchy();
            EnsureTopBarHierarchy();
            EnsureGrandStrategyHUDHierarchy();
            BindToGameManagerEvents();
            RefreshDateTrackerUI();
            UpdateTopBar();
            UpdateTopBarSpeedButtons();
            UpdateFactionProfileFromCurrentGame();

            if (_selectedProvinceId <= 0 && provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(false);
            }

            if (siegeLogisticsPanel != null)
            {
                siegeLogisticsPanel.SetActive(false);
            }

            if (royalBattlePromptPanel != null)
            {
                royalBattlePromptPanel.SetActive(false);
            }
        }

        private void Update()
        {
            HandleEmptyClickToClose();
            HandleDiplomacyHotkey();
        }

        private void HandleDiplomacyHotkey()
        {
            bool dPressed = false;
#if ENABLE_INPUT_SYSTEM
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    dPressed = UnityEngine.InputSystem.Keyboard.current.dKey.wasPressedThisFrame;
                }
            }
            catch { }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!dPressed)
            {
                try { dPressed = Input.GetKeyDown(KeyCode.D); } catch { }
            }
#endif
            if (dPressed)
            {
                ToggleDiplomacyLedger();
            }
        }

        private void HandleEmptyClickToClose()
        {
            if (!IsProvincePanelOpen) return;

            bool isLeftClick = false;
            Vector2 mousePos = Vector2.zero;

            // 1. Yeni Input System (UnityEngine.InputSystem) yoxlanışı
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    isLeftClick = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
                    mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }
#endif
            }
            catch (System.Exception) { }

            // 2. Əgər Yeni Input System ilə oxunmayıbsa, köhnə (Legacy) Input meneceri yoxlanışı
            if (!isLeftClick)
            {
                try
                {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                    isLeftClick = Input.GetMouseButtonDown(0);
                    mousePos = Input.mousePosition;
#endif
                }
                catch (System.Exception) { }
            }

            if (isLeftClick)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    Vector2 mouseWorld = cam.ScreenToWorldPoint(mousePos);
                    Collider2D hit = Physics2D.OverlapPoint(mouseWorld);
                    if (hit == null || hit.GetComponent<MapInteractionController>() == null)
                    {
                        HideProvincePanel();
                    }
                }
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

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideProvincePanel);
                closeButton.onClick.AddListener(HideProvincePanel);
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
                && demographicsSummaryText == null)
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

        /// <summary>
        /// Faza 9.7 & 9.8 - Xəritədə kliklənən real GIS əyalətinin premium məlumatlarını göstərir.
        /// </summary>
        public void ShowProvincePanel(MapInteractionController province)
        {
            if (province == null) return;

            _selectedProvinceId = province.ProvinceId;
            _selectedMapProvince = province;

            EnsureProvincePanelHierarchy();

            if (provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(true);
            }

            // WASTELAND / KEÇİLMƏZ ƏRAZİ XÜSUSİ İNTERFEYSİ
            if (province.IsWasteland)
            {
                if (ProvinceNameText != null)
                {
                    ProvinceNameText.text = $"{province.ProvinceName}";
                }

                if (ownerColorBadge != null)
                {
                    ownerColorBadge.color = new Color(0.824f, 0.706f, 0.549f, 1f); // #D2B48C Solğun qum rəngi
                }

                if (OwnerText != null)
                {
                    OwnerText.text = "Status: Keçilməz Ərazi / Wasteland";
                    OwnerText.color = new Color(0.824f, 0.706f, 0.549f, 1f);
                }

                if (rulerText != null)
                {
                    rulerText.text = "Hökmdar: Yoxdur (Boş Ərazi)";
                    rulerText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                if (PopulationText != null)
                {
                    PopulationText.text = "-";
                    PopulationText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                if (GoldText != null)
                {
                    GoldText.text = "-";
                    GoldText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                if (FoodText != null)
                {
                    FoodText.text = "-";
                    FoodText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                if (IronText != null)
                {
                    IronText.text = "-";
                    IronText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                }

                if (economyDetailsText != null)
                {
                    economyDetailsText.text = "Bu ərazidə heç bir dövlət və ya iqtisadi fəaliyyət yoxdur.";
                }

                if (actionStatusText != null)
                {
                    actionStatusText.text = "Keçilməz Ərazi (Wasteland). Hərbi və iqtisadi fəaliyyət qadağandır.";
                }

                if (draftArmyButton != null)
                {
                    draftArmyButton.interactable = false;
                }

                if (demographicsPanel != null)
                {
                    demographicsPanel.SetActive(false);
                }

                return;
            }

            if (draftArmyButton != null)
            {
                draftArmyButton.interactable = true;
            }

            // 1. Əyalət Adı
            if (ProvinceNameText != null)
            {
                ProvinceNameText.text = $"Əyalət: {province.ProvinceName}";
            }

            // 2. Dövlət və Hökmdar (1187 Ssenarisi)
            string owner = province.ProvinceOwner;
            if (string.IsNullOrEmpty(owner)) owner = province.AdminCountry;
            Color stateColor = province.DefaultFillColor;

            string ruler = !string.IsNullOrEmpty(province.RulerName) ? province.RulerName : string.Empty;
            if (string.IsNullOrEmpty(ruler) && HistoricalScenarioManager.Instance != null)
            {
                var faction = HistoricalScenarioManager.Instance.GetFactionByName(owner);
                if (faction != null)
                {
                    ruler = faction.Leader;
                    if (stateColor == Color.white || stateColor == Color.clear)
                    {
                        stateColor = faction.BaseColor;
                    }
                }
                else
                {
                    ruler = HistoricalScenarioManager.Instance.GetRulerForFaction(owner);
                }
            }
            if (string.IsNullOrEmpty(ruler)) ruler = "Məlum deyil";

            if (ownerColorBadge != null)
            {
                ownerColorBadge.color = stateColor;
            }

            if (OwnerText != null)
            {
                OwnerText.text = $"Hakim Dövlət: {owner}";
                OwnerText.color = Color.white;
            }

            if (rulerText != null)
            {
                rulerText.text = $"Hökmdar: {ruler}";
            }

            // 3. Faza 10.1: SQLite Bazasından Real İqtisadi Göstəricilər
            ProvinceEconomyData ecoData = null;
            if (province.EconomyData != null)
            {
                ecoData = province.EconomyData;
            }
            else
            {
                DatabaseManager db = DatabaseManager.Instance != null
                    ? DatabaseManager.Instance
                    : FindAnyObjectByType<DatabaseManager>();
                if (db != null)
                {
                    ecoData = db.GetProvinceEconomyData(province.ProvinceId, province.ProvinceName, province.AdminCountry);
                }
            }

            CurrentProvinceEconomyData = ecoData;

            int popCount = ecoData != null ? ecoData.TotalPopulation : 50000;
            float goldIncome = ecoData != null ? ecoData.GoldIncome : 30f;
            float foodProduction = ecoData != null ? ecoData.FoodProduction : 40f;
            float ironProduction = ecoData != null ? ecoData.IronProduction : 15f;

            if (PopulationText != null)
            {
                PopulationText.text = $"{popCount:N0} nəfər";
                PopulationText.color = new Color(0.682f, 0.839f, 0.945f, 1f); // #AED6F1
            }

            if (GoldText != null)
            {
                GoldText.text = $"+{goldIncome:F0} /ay";
                GoldText.color = new Color(1f, 0.843f, 0f, 1f); // #FFD700
            }

            if (FoodText != null)
            {
                FoodText.text = $"+{foodProduction:F0} /ay";
                FoodText.color = new Color(0.180f, 0.800f, 0.443f, 1f); // #2ECC71
            }

            if (IronText != null)
            {
                IronText.text = $"+{ironProduction:F0} /ay";
                IronText.color = new Color(0.741f, 0.765f, 0.780f, 1f); // #BDC3C7
            }

            if (economyDetailsText != null)
            {
                economyDetailsText.text = $"İqtisadiyyat: Qızıl +{goldIncome:F0} | Ərzaq +{foodProduction:F0} | Dəmir +{ironProduction:F0}";
            }

            if (actionStatusText != null)
            {
                actionStatusText.text = $"{province.ProvinceName} əyaləti aktivdir (SQLite Sinxronizasiya).";
            }

            Debug.Log($"[UIManager] Faza 10.1 SQLite Əyalət Paneli -> {province.ProvinceName} (Sahib: {owner}, Hökmdar: {ruler}, Əhali: {popCount:N0}, Qızıl: +{goldIncome:F0}/ay, Ərzaq: +{foodProduction:F0}/ay, Dəmir: +{ironProduction:F0}/ay | Baza: {ecoData?.IsFromDatabase})");

            // Faza 10.5: Demoqrafiya və İdarəetmə bölməsi
            EnsureDemographicsPanelHierarchy();
            RefreshDemographicsPanel(province);
        }

        public void ShowProvincePanel(int provinceId)
        {
            _selectedProvinceId = provinceId;

            MapInteractionController mapProv = null;
            if (GISMapLoader.Instance != null)
            {
                mapProv = GISMapLoader.Instance.GetProvinceById(provinceId);
            }
            if (mapProv == null)
            {
                var all = UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude);
                foreach (var p in all)
                {
                    if (p != null && p.ProvinceId == provinceId)
                    {
                        mapProv = p;
                        break;
                    }
                }
            }

            if (mapProv != null)
            {
                ShowProvincePanel(mapProv);
                return;
            }

            EnsureProvincePanelHierarchy();
            if (provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(true);
            }

            DatabaseManager dbMgr = DatabaseManager.Instance != null ? DatabaseManager.Instance : FindAnyObjectByType<DatabaseManager>();
            if (dbMgr != null)
            {
                var eco = dbMgr.GetProvinceEconomyData(provinceId);
                if (eco != null)
                {
                    CurrentProvinceEconomyData = eco;
                    if (ProvinceNameText != null) ProvinceNameText.text = $"Əyalət: {eco.ProvinceName}";
                    if (PopulationText != null)
                    {
                        PopulationText.text = $"{eco.TotalPopulation:N0} nəfər";
                        PopulationText.color = new Color(0.682f, 0.839f, 0.945f, 1f);
                    }
                    if (GoldText != null)
                    {
                        GoldText.text = $"+{eco.GoldIncome:F0} /ay";
                        GoldText.color = new Color(1f, 0.843f, 0f, 1f);
                    }
                    if (FoodText != null)
                    {
                        FoodText.text = $"+{eco.FoodProduction:F0} /ay";
                        FoodText.color = new Color(0.180f, 0.800f, 0.443f, 1f);
                    }
                    if (IronText != null)
                    {
                        IronText.text = $"+{eco.IronProduction:F0} /ay";
                        IronText.color = new Color(0.741f, 0.765f, 0.780f, 1f);
                    }
                }
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
            _selectedMapProvince = null;
            if (provinceInfoPanel != null)
            {
                provinceInfoPanel.SetActive(false);
            }
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.DeselectAllProvinces();
            }
        }

        /// <summary>
        /// Faza 9.7 & 9.8 - Əyalət məlumat panelinin premium dizayn iyerarxiyasını təmin edir və ya yenidən qurur.
        /// </summary>
        public void EnsureProvincePanelHierarchy(bool forceRebuild = false)
        {
            if (provinceInfoPanel == null)
            {
                var found = GameObject.Find("ProvinceInfoPanel");
                if (found != null)
                {
                    if (found.GetComponent<RectTransform>() == null)
                    {
                        if (Application.isPlaying) Destroy(found);
                        else DestroyImmediate(found);
                        provinceInfoPanel = null;
                    }
                    else
                    {
                        provinceInfoPanel = found;
                    }
                }
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();

            if (provinceInfoPanel == null && canvas != null)
            {
                var panelObj = new GameObject("ProvinceInfoPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObj.transform.SetParent(canvas.transform, false);
                provinceInfoPanel = panelObj;
            }

            if (provinceInfoPanel == null) return;

            // Panelin mövqe, ölçü və premium çərçivə tənzimləmələri
            var rt = provinceInfoPanel.GetComponent<RectTransform>() ?? provinceInfoPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-20f, 0f);
            rt.sizeDelta = new Vector2(360f, 460f);

            var img = provinceInfoPanel.GetComponent<Image>() ?? provinceInfoPanel.AddComponent<Image>();
            img.color = new Color(0.086f, 0.106f, 0.141f, 0.95f); // #161B24 Alpha 0.95 (Tünd zərif göy-boz)

            var outline = provinceInfoPanel.GetComponent<Outline>() ?? provinceInfoPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059 Qızılı / bürünc incə çərçivə
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var layout = provinceInfoPanel.GetComponent<VerticalLayoutGroup>() ?? provinceInfoPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            // Referansları axtarırıq (əgər səhnədən yenicə yüklənibsə)
            if (provinceNameText == null)
            {
                var t = provinceInfoPanel.transform.Find("HeaderRow/ProvinceNameText") ?? provinceInfoPanel.transform.Find("ProvinceNameText");
                if (t != null) provinceNameText = t.GetComponent<Text>();
            }
            if (closeButton == null)
            {
                var b = provinceInfoPanel.transform.Find("HeaderRow/CloseButton") ?? provinceInfoPanel.transform.Find("CloseButton");
                if (b != null) closeButton = b.GetComponent<Button>();
                else closeButton = provinceInfoPanel.GetComponentInChildren<Button>();
            }
            if (ownerBanner == null)
            {
                var ob = provinceInfoPanel.transform.Find("OwnerBanner");
                if (ob != null) ownerBanner = ob.gameObject;
            }
            if (ownerColorBadge == null && ownerBanner != null)
            {
                var badge = ownerBanner.transform.Find("OwnerRow/OwnerColorBadge") ?? ownerBanner.transform.Find("OwnerColorBadge");
                if (badge != null) ownerColorBadge = badge.GetComponent<Image>();
            }
            if (ownerText == null && ownerBanner != null)
            {
                var ot = ownerBanner.transform.Find("OwnerRow/OwnerText") ?? ownerBanner.transform.Find("OwnerText");
                if (ot != null) ownerText = ot.GetComponent<Text>();
            }
            if (rulerText == null && ownerBanner != null)
            {
                var rtTxt = ownerBanner.transform.Find("RulerText");
                if (rtTxt != null) rulerText = rtTxt.GetComponent<Text>();
            }
            if (populationText == null)
            {
                var pop = provinceInfoPanel.transform.Find("PopulationRow/Value");
                if (pop != null) populationText = pop.GetComponent<Text>();
            }
            if (goldText == null)
            {
                var gold = provinceInfoPanel.transform.Find("GoldRow/Value");
                if (gold != null) goldText = gold.GetComponent<Text>();
            }
            if (foodText == null)
            {
                var food = provinceInfoPanel.transform.Find("FoodRow/Value");
                if (food != null) foodText = food.GetComponent<Text>();
            }
            if (ironText == null)
            {
                var iron = provinceInfoPanel.transform.Find("IronRow/Value");
                if (iron != null) ironText = iron.GetComponent<Text>();
            }

            // Əgər struktur artıq mövcuddursa və forceRebuild deyilsə
            if (!forceRebuild &&
                ownerBanner != null &&
                ownerColorBadge != null &&
                rulerText != null &&
                provinceNameText != null &&
                ownerText != null &&
                populationText != null &&
                goldText != null &&
                foodText != null &&
                ironText != null &&
                closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideProvincePanel);
                closeButton.onClick.AddListener(HideProvincePanel);
                return;
            }

            // Köhnə uşaq obyektləri təmizlənir
            for (int i = provinceInfoPanel.transform.childCount - 1; i >= 0; i--)
            {
                var child = provinceInfoPanel.transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. BAŞLIQ SƏTRİ (ProvinceNameText və CloseButton "✕")
            var headerRow = CreateUIContainer("HeaderRow", provinceInfoPanel.transform);
            var headerHLayout = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHLayout.spacing = 8f;
            headerHLayout.childForceExpandWidth = false;
            headerHLayout.childForceExpandHeight = true;
            headerHLayout.childControlWidth = true;
            headerHLayout.childControlHeight = true;
            var headerLe = headerRow.AddComponent<LayoutElement>();
            headerLe.preferredHeight = 32f;

            var nameObj = new GameObject("ProvinceNameText");
            nameObj.transform.SetParent(headerRow.transform, false);
            provinceNameText = nameObj.AddComponent<Text>();
            provinceNameText.text = "Əyalət: -";
            provinceNameText.fontSize = 18;
            provinceNameText.fontStyle = FontStyle.Bold;
            provinceNameText.color = new Color(0.957f, 0.816f, 0.247f, 1f); // #F4D03F Qızılı
            provinceNameText.alignment = TextAnchor.MiddleLeft;
            if (defaultFont != null) provinceNameText.font = defaultFont;
            var nameLe = nameObj.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1f;

            var closeBtnObj = new GameObject("CloseButton");
            closeBtnObj.transform.SetParent(headerRow.transform, false);
            var closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.573f, 0.169f, 0.129f, 0.9f); // #922B21 Qırmızı-bordo
            closeButton = closeBtnObj.AddComponent<Button>();
            var closeLe = closeBtnObj.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 28f;
            closeLe.preferredHeight = 28f;

            var closeTxtObj = new GameObject("Text");
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTxt = closeTxtObj.AddComponent<Text>();
            closeTxt.text = "✕";
            closeTxt.fontSize = 16;
            closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            if (defaultFont != null) closeTxt.font = defaultFont;
            var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.sizeDelta = Vector2.zero;

            closeButton.onClick.RemoveListener(HideProvincePanel);
            closeButton.onClick.AddListener(HideProvincePanel);

            // 2. AYIRICI 1 (Divider 1)
            CreateDivider("Divider1", provinceInfoPanel.transform);

            // 3. DÖVLƏT VƏ HÖKMDAR BANNERİ (OwnerBanner)
            ownerBanner = CreateUIContainer("OwnerBanner", provinceInfoPanel.transform);
            var bannerImg = ownerBanner.AddComponent<Image>();
            bannerImg.color = new Color(0.106f, 0.133f, 0.180f, 0.92f); // #1B222E
            var bannerOutline = ownerBanner.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.35f);
            bannerOutline.effectDistance = new Vector2(1f, -1f);
            var bannerVLayout = ownerBanner.AddComponent<VerticalLayoutGroup>();
            bannerVLayout.padding = new RectOffset(10, 10, 8, 8);
            bannerVLayout.spacing = 4f;
            bannerVLayout.childForceExpandWidth = true;
            bannerVLayout.childForceExpandHeight = false;
            bannerVLayout.childControlWidth = true;
            bannerVLayout.childControlHeight = false;
            var bannerLe = ownerBanner.AddComponent<LayoutElement>();
            bannerLe.preferredHeight = 64f;

            // Dövlət adı və rəng indikatoru sətri
            var ownerRow = CreateUIContainer("OwnerRow", ownerBanner.transform);
            var ownerHLayout = ownerRow.AddComponent<HorizontalLayoutGroup>();
            ownerHLayout.spacing = 8f;
            ownerHLayout.childForceExpandWidth = false;
            ownerHLayout.childControlWidth = true;
            ownerHLayout.childForceExpandHeight = true;
            ownerHLayout.childControlHeight = true;
            var ownerRowLe = ownerRow.AddComponent<LayoutElement>();
            ownerRowLe.preferredHeight = 24f;

            var badgeObj = new GameObject("OwnerColorBadge");
            badgeObj.transform.SetParent(ownerRow.transform, false);
            ownerColorBadge = badgeObj.AddComponent<Image>();
            ownerColorBadge.color = new Color(0.122f, 0.380f, 0.553f, 1f); // #1F618D
            var badgeOutline = badgeObj.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.8f);
            badgeOutline.effectDistance = new Vector2(1f, -1f);
            var badgeLe = badgeObj.AddComponent<LayoutElement>();
            badgeLe.preferredWidth = 18f;
            badgeLe.preferredHeight = 18f;

            var ownerTxtObj = new GameObject("OwnerText");
            ownerTxtObj.transform.SetParent(ownerRow.transform, false);
            ownerText = ownerTxtObj.AddComponent<Text>();
            ownerText.text = "Hakim Dövlət: -";
            ownerText.fontSize = 15;
            ownerText.fontStyle = FontStyle.Bold;
            ownerText.color = Color.white;
            ownerText.alignment = TextAnchor.MiddleLeft;
            if (defaultFont != null) ownerText.font = defaultFont;
            var ownerTxtLe = ownerTxtObj.AddComponent<LayoutElement>();
            ownerTxtLe.flexibleWidth = 1f;

            // Hökmdar sətri
            var rulerObj = new GameObject("RulerText");
            rulerObj.transform.SetParent(ownerBanner.transform, false);
            rulerText = rulerObj.AddComponent<Text>();
            rulerText.text = "Hökmdar: -";
            rulerText.fontSize = 13;
            rulerText.fontStyle = FontStyle.Normal;
            rulerText.color = new Color(0.835f, 0.847f, 0.863f, 1f); // #D5D8DC
            rulerText.alignment = TextAnchor.MiddleLeft;
            if (defaultFont != null) rulerText.font = defaultFont;
            var rulerLe = rulerObj.AddComponent<LayoutElement>();
            rulerLe.preferredHeight = 20f;

            // 4. AYIRICI 2 (Divider 2)
            CreateDivider("Divider2", provinceInfoPanel.transform);

            // 5. İQTİSADİYYAT BAŞLIĞI
            var ecoHeaderObj = new GameObject("EconomyHeader");
            ecoHeaderObj.transform.SetParent(provinceInfoPanel.transform, false);
            var ecoHeaderTxt = ecoHeaderObj.AddComponent<Text>();
            ecoHeaderTxt.text = "İQTİSADİ GÖSTƏRİCİLƏR";
            ecoHeaderTxt.fontSize = 12;
            ecoHeaderTxt.fontStyle = FontStyle.Bold;
            ecoHeaderTxt.color = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059
            ecoHeaderTxt.alignment = TextAnchor.MiddleLeft;
            if (defaultFont != null) ecoHeaderTxt.font = defaultFont;
            var ecoHeaderLe = ecoHeaderObj.AddComponent<LayoutElement>();
            ecoHeaderLe.preferredHeight = 18f;

            // 6. İQTİSADİ GÖSTƏRİCİLƏR CƏDVƏLİ (4 SƏTİR)
            // Sətir 1: Əhali
            populationText = CreateStatRow(
                "PopulationRow",
                "Əhali",
                "- nəfər",
                new Color(0.682f, 0.839f, 0.945f, 1f), // #AED6F1 Ağ/Açıq mavi
                new Color(0.106f, 0.133f, 0.180f, 0.75f),
                provinceInfoPanel.transform,
                defaultFont);

            // Sətir 2: Qızıl Gəliri
            goldText = CreateStatRow(
                "GoldRow",
                "Qızıl Gəliri",
                "+0 /ay",
                new Color(1f, 0.843f, 0f, 1f), // #FFD700 Parlaq qızılı
                new Color(0.125f, 0.157f, 0.212f, 0.75f),
                provinceInfoPanel.transform,
                defaultFont);

            // Sətir 3: Ərzaq İstehsalı
            foodText = CreateStatRow(
                "FoodRow",
                "Ərzaq İstehsalı",
                "+0 /ay",
                new Color(0.180f, 0.800f, 0.443f, 1f), // #2ECC71 Yaşıl
                new Color(0.106f, 0.133f, 0.180f, 0.75f),
                provinceInfoPanel.transform,
                defaultFont);

            // Sətir 4: Dəmir İstehsalı
            ironText = CreateStatRow(
                "IronRow",
                "Dəmir İstehsalı",
                "+0 /ay",
                new Color(0.741f, 0.765f, 0.780f, 1f), // #BDC3C7 Gümüşü/Boz
                new Color(0.125f, 0.157f, 0.212f, 0.75f),
                provinceInfoPanel.transform,
                defaultFont);

            // 7. AYIRICI 3 (Divider 3)
            CreateDivider("Divider3", provinceInfoPanel.transform);

            // 8. STATUS BİLDİRİŞİ
            var footerObj = new GameObject("ActionFooter");
            footerObj.transform.SetParent(provinceInfoPanel.transform, false);
            actionStatusText = footerObj.AddComponent<Text>();
            actionStatusText.text = "Əyalət seçilib.";
            actionStatusText.fontSize = 12;
            actionStatusText.fontStyle = FontStyle.Normal;
            actionStatusText.color = new Color(0.584f, 0.647f, 0.651f, 1f); // #95A5A6
            actionStatusText.alignment = TextAnchor.MiddleLeft;
            if (defaultFont != null) actionStatusText.font = defaultFont;
            var footerLe = footerObj.AddComponent<LayoutElement>();
            footerLe.preferredHeight = 22f;
        }

        /// <summary>
        /// Faza 10.3 - Mühasirə və Real Logistika Panelini göstərir və məlumatları doldurur.
        /// </summary>
        public void ShowSiegeLogisticsPanel(SiegeLogisticsData data)
        {
            if (data == null) return;
            _activeSiegeData = data;

            EnsureSiegeLogisticsPanelHierarchy();

            if (siegeLogisticsPanel != null)
            {
                siegeLogisticsPanel.SetActive(true);
            }

            string pName = data.BesiegedProvince != null ? data.BesiegedProvince.ProvinceName : "Naməlum";
            string pOwner = data.BesiegedProvince != null ? data.BesiegedProvince.ProvinceOwner : "Naməlum";
            string cName = (data.BesiegingArmy != null && data.BesiegingArmy.ArmyData != null) ? data.BesiegingArmy.ArmyData.CommanderName : "Komandir";
            string tCount = (data.BesiegingArmy != null && data.BesiegingArmy.ArmyData != null) ? data.BesiegingArmy.ArmyData.FormattedTroopCount : "-";

            if (siegeProvinceText != null) siegeProvinceText.text = $"Mühasirədə olan Qala: {pName} (Hakim: {pOwner})";
            if (siegeArmyText != null) siegeArmyText.text = $"Mühasirəçi Komandir: {cName} ({tCount} əsgər)";
            if (siegeNeedText != null) siegeNeedText.text = $"Ordunun Aylıq Ərzaq İstehlakı: {data.ArmyConsumption:F1} /ay";
            if (siegeSupplyText != null) siegeSupplyText.text = $"Əyalətin Yerli İstehsalı: {data.LocalFoodProduction:F1} /ay";

            if (siegeResultText != null)
            {
                siegeResultText.text = data.SummaryText;
            }

            if (siegeResultBackground != null)
            {
                siegeResultBackground.color = data.IsSurplus
                    ? new Color(0.10f, 0.28f, 0.16f, 0.95f)
                    : new Color(0.35f, 0.12f, 0.12f, 0.95f);
            }
        }

        public void HideSiegeLogisticsPanel()
        {
            if (siegeLogisticsPanel != null)
            {
                siegeLogisticsPanel.SetActive(false);
            }
        }

        private void HandlePillageClick()
        {
            if (_activeSiegeData != null && SiegeManager.Instance != null)
            {
                SiegeManager.Instance.ExecutePillage(_activeSiegeData);
                if (siegeResultText != null)
                {
                    siegeResultText.text = $"⚔️ QALA YAĞMALANDI! +{_activeSiegeData.PlunderGold:F0} Qızıl, +{_activeSiegeData.PlunderFood:F0} Ərzaq qənimət götürüldü. Əyalətin inkişaf səviyyəsi çökdü!";
                }
                if (siegeResultBackground != null)
                {
                    siegeResultBackground.color = new Color(0.45f, 0.15f, 0.15f, 0.95f);
                }
            }
        }

        private void HandleExploitClick()
        {
            if (_activeSiegeData != null && SiegeManager.Instance != null)
            {
                SiegeManager.Instance.ExecuteExploit(_activeSiegeData);
                if (siegeResultText != null)
                {
                    if (_activeSiegeData.IsSurplus)
                    {
                        siegeResultText.text = $"🌾 İstismar Təsdiqləndi: Ordu yerli ərzaqla tam doyur (+{_activeSiegeData.SurplusAmount:F1} artıq). Mərkəzi dövlət xəzinəsindən 0 ərzaq xərcləndi.";
                    }
                    else
                    {
                        siegeResultText.text = $"🌾 İstismar Təsdiqləndi: Yerli {_activeSiegeData.TakenFromProvince:F1} ərzaq müsadirə edildi. Çatışmayan {_activeSiegeData.DeficitAmount:F1} ərzaq mərkəzi dövlət anbarı hesabına ödənildi.";
                    }
                }
                if (siegeResultBackground != null)
                {
                    siegeResultBackground.color = new Color(0.12f, 0.25f, 0.35f, 0.95f);
                }
            }
        }

        /// <summary>
        /// Faza 10.3 - Mühasirə və Real Logistika Panelinin iyerarxiyasını təmin edir.
        /// </summary>
        public void EnsureSiegeLogisticsPanelHierarchy(bool forceRebuild = false)
        {
            if (siegeLogisticsPanel == null)
            {
                var found = GameObject.Find("SiegeLogisticsPanel");
                if (found != null)
                {
                    if (found.GetComponent<RectTransform>() == null)
                    {
                        if (Application.isPlaying) Destroy(found);
                        else DestroyImmediate(found);
                        siegeLogisticsPanel = null;
                    }
                    else
                    {
                        siegeLogisticsPanel = found;
                    }
                }
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();

            if (siegeLogisticsPanel == null && canvas != null)
            {
                var panelObj = new GameObject("SiegeLogisticsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObj.transform.SetParent(canvas.transform, false);
                siegeLogisticsPanel = panelObj;
            }

            if (siegeLogisticsPanel == null) return;

            var rt = siegeLogisticsPanel.GetComponent<RectTransform>();
            if (rt == null)
            {
                rt = siegeLogisticsPanel.AddComponent<RectTransform>();
            }
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(460f, 380f);

            var img = siegeLogisticsPanel.GetComponent<Image>() ?? siegeLogisticsPanel.AddComponent<Image>();
            img.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);

            var outline = siegeLogisticsPanel.GetComponent<Outline>() ?? siegeLogisticsPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.20f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = siegeLogisticsPanel.GetComponent<VerticalLayoutGroup>() ?? siegeLogisticsPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            if (siegeProvinceText == null || forceRebuild)
            {
                for (int i = siegeLogisticsPanel.transform.childCount - 1; i >= 0; i--)
                {
                    var c = siegeLogisticsPanel.transform.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(c);
                    else DestroyImmediate(c);
                }

                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                // 1. Başlıq
                var titleObj = new GameObject("TitleText", typeof(RectTransform));
                titleObj.transform.SetParent(siegeLogisticsPanel.transform, false);
                siegeTitleText = titleObj.AddComponent<Text>();
                siegeTitleText.text = "🏰 MÜHASİRƏ VƏ TƏCHİZAT LOGİSTİKASI";
                siegeTitleText.fontSize = 18;
                siegeTitleText.fontStyle = FontStyle.Bold;
                siegeTitleText.color = new Color(0.95f, 0.82f, 0.20f, 1f);
                siegeTitleText.alignment = TextAnchor.MiddleCenter;
                if (font != null) siegeTitleText.font = font;

                CreateDivider("TitleDivider", siegeLogisticsPanel.transform);

                // 2. Əyalət və Komandir Məlumatları
                var provObj = new GameObject("ProvinceInfoText", typeof(RectTransform));
                provObj.transform.SetParent(siegeLogisticsPanel.transform, false);
                siegeProvinceText = provObj.AddComponent<Text>();
                siegeProvinceText.text = "Mühasirədə olan Əyalət: -";
                siegeProvinceText.fontSize = 14;
                siegeProvinceText.fontStyle = FontStyle.Bold;
                siegeProvinceText.color = Color.white;
                if (font != null) siegeProvinceText.font = font;

                var armyObj = new GameObject("ArmyInfoText", typeof(RectTransform));
                armyObj.transform.SetParent(siegeLogisticsPanel.transform, false);
                siegeArmyText = armyObj.AddComponent<Text>();
                siegeArmyText.text = "Mühasirəçi Ordu: -";
                siegeArmyText.fontSize = 13;
                siegeArmyText.color = new Color(0.85f, 0.90f, 0.95f, 1f);
                if (font != null) siegeArmyText.font = font;

                // 3. İstehlak və Yerli İstehsal Sətirləri
                var needObj = new GameObject("NeedText", typeof(RectTransform));
                needObj.transform.SetParent(siegeLogisticsPanel.transform, false);
                siegeNeedText = needObj.AddComponent<Text>();
                siegeNeedText.text = "Ordunun Aylıq İstehlakı: -";
                siegeNeedText.fontSize = 13;
                siegeNeedText.color = new Color(0.95f, 0.60f, 0.40f, 1f);
                if (font != null) siegeNeedText.font = font;

                var supplyObj = new GameObject("SupplyText", typeof(RectTransform));
                supplyObj.transform.SetParent(siegeLogisticsPanel.transform, false);
                siegeSupplyText = supplyObj.AddComponent<Text>();
                siegeSupplyText.text = "Əyalətin Yerli İstehsalı: -";
                siegeSupplyText.fontSize = 13;
                siegeSupplyText.color = new Color(0.50f, 0.90f, 0.50f, 1f);
                if (font != null) siegeSupplyText.font = font;

                CreateDivider("CalcDivider", siegeLogisticsPanel.transform);

                // 4. Nəticə Lövhəsi (Logistics Result Box)
                var resultBox = CreateUIContainer("ResultBox", siegeLogisticsPanel.transform);
                siegeResultBackground = resultBox.AddComponent<Image>();
                siegeResultBackground.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
                var resultVLayout = resultBox.AddComponent<VerticalLayoutGroup>();
                resultVLayout.padding = new RectOffset(10, 10, 8, 8);
                resultVLayout.childForceExpandWidth = true;
                resultVLayout.childForceExpandHeight = false;
                resultVLayout.childControlWidth = true;
                resultVLayout.childControlHeight = true;

                var resultTextObj = new GameObject("ResultText", typeof(RectTransform));
                resultTextObj.transform.SetParent(resultBox.transform, false);
                siegeResultText = resultTextObj.AddComponent<Text>();
                siegeResultText.text = "Status: -";
                siegeResultText.fontSize = 12;
                siegeResultText.color = Color.white;
                if (font != null) siegeResultText.font = font;

                // 5. Düymələr Qrupu (Pillage, Exploit, Close)
                var btnRow = CreateUIContainer("ButtonRow", siegeLogisticsPanel.transform);
                var btnHLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
                btnHLayout.spacing = 8f;
                btnHLayout.childForceExpandWidth = true;
                btnHLayout.childForceExpandHeight = true;
                btnHLayout.childControlWidth = true;
                btnHLayout.childControlHeight = true;
                var btnRowLe = btnRow.AddComponent<LayoutElement>();
                btnRowLe.preferredHeight = 36f;

                // Yağmala Düyməsi
                var pillageBtnObj = new GameObject("PillageButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                pillageBtnObj.transform.SetParent(btnRow.transform, false);
                var pillageImg = pillageBtnObj.GetComponent<Image>();
                pillageImg.color = new Color(0.75f, 0.22f, 0.17f, 1f);
                pillageButton = pillageBtnObj.GetComponent<Button>();
                var pillageTxtObj = new GameObject("Text", typeof(RectTransform));
                pillageTxtObj.transform.SetParent(pillageBtnObj.transform, false);
                var pillageTxt = pillageTxtObj.AddComponent<Text>();
                pillageTxt.text = "⚔️ Yağmala";
                pillageTxt.alignment = TextAnchor.MiddleCenter;
                pillageTxt.fontStyle = FontStyle.Bold;
                pillageTxt.fontSize = 13;
                pillageTxt.color = Color.white;
                if (font != null) pillageTxt.font = font;
                var pillageTxtRt = pillageTxtObj.GetComponent<RectTransform>();
                pillageTxtRt.anchorMin = Vector2.zero; pillageTxtRt.anchorMax = Vector2.one;
                pillageTxtRt.offsetMin = Vector2.zero; pillageTxtRt.offsetMax = Vector2.zero;

                // İstismar Et Düyməsi
                var exploitBtnObj = new GameObject("ExploitButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                exploitBtnObj.transform.SetParent(btnRow.transform, false);
                var exploitImg = exploitBtnObj.GetComponent<Image>();
                exploitImg.color = new Color(0.16f, 0.55f, 0.35f, 1f);
                exploitButton = exploitBtnObj.GetComponent<Button>();
                var exploitTxtObj = new GameObject("Text", typeof(RectTransform));
                exploitTxtObj.transform.SetParent(exploitBtnObj.transform, false);
                var exploitTxt = exploitTxtObj.AddComponent<Text>();
                exploitTxt.text = "🌾 İstismar Et";
                exploitTxt.alignment = TextAnchor.MiddleCenter;
                exploitTxt.fontStyle = FontStyle.Bold;
                exploitTxt.fontSize = 13;
                exploitTxt.color = Color.white;
                if (font != null) exploitTxt.font = font;
                var exploitTxtRt = exploitTxtObj.GetComponent<RectTransform>();
                exploitTxtRt.anchorMin = Vector2.zero; exploitTxtRt.anchorMax = Vector2.one;
                exploitTxtRt.offsetMin = Vector2.zero; exploitTxtRt.offsetMax = Vector2.zero;

                // Bağla Düyməsi
                var closeSiegeBtnObj = new GameObject("CloseSiegeButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                closeSiegeBtnObj.transform.SetParent(btnRow.transform, false);
                var closeSiegeImg = closeSiegeBtnObj.GetComponent<Image>();
                closeSiegeImg.color = new Color(0.35f, 0.38f, 0.42f, 1f);
                closeSiegeButton = closeSiegeBtnObj.GetComponent<Button>();
                var closeSiegeTxtObj = new GameObject("Text", typeof(RectTransform));
                closeSiegeTxtObj.transform.SetParent(closeSiegeBtnObj.transform, false);
                var closeSiegeTxt = closeSiegeTxtObj.AddComponent<Text>();
                closeSiegeTxt.text = "Bağla";
                closeSiegeTxt.alignment = TextAnchor.MiddleCenter;
                closeSiegeTxt.fontSize = 13;
                closeSiegeTxt.color = Color.white;
                if (font != null) closeSiegeTxt.font = font;
                var closeSiegeTxtRt = closeSiegeTxtObj.GetComponent<RectTransform>();
                closeSiegeTxtRt.anchorMin = Vector2.zero; closeSiegeTxtRt.anchorMax = Vector2.one;
                closeSiegeTxtRt.offsetMin = Vector2.zero; closeSiegeTxtRt.offsetMax = Vector2.zero;
            }

            if (closeSiegeButton != null)
            {
                closeSiegeButton.onClick.RemoveListener(HideSiegeLogisticsPanel);
                closeSiegeButton.onClick.AddListener(HideSiegeLogisticsPanel);
            }
            if (pillageButton != null)
            {
                pillageButton.onClick.RemoveListener(HandlePillageClick);
                pillageButton.onClick.AddListener(HandlePillageClick);
            }
            if (exploitButton != null)
            {
                exploitButton.onClick.RemoveListener(HandleExploitClick);
                exploitButton.onClick.AddListener(HandleExploitClick);
            }
        }

        private GameObject CreateUIContainer(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj;
        }

        private void CreateDivider(string name, Transform parent)
        {
            var div = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            div.transform.SetParent(parent, false);
            var divImg = div.GetComponent<Image>();
            divImg.color = new Color(0.773f, 0.627f, 0.349f, 0.45f); // #C5A059 alpha 0.45
            var le = div.AddComponent<LayoutElement>();
            le.preferredHeight = 1.5f;
            le.flexibleWidth = 1f;
        }

        private Text CreateStatRow(string rowName, string label, string defaultValue, Color valueColor, Color bgStripColor, Transform parent, Font font)
        {
            var rowObj = new GameObject(rowName);
            rowObj.transform.SetParent(parent, false);
            var bgImg = rowObj.AddComponent<Image>();
            bgImg.color = bgStripColor;

            var hLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.padding = new RectOffset(10, 10, 0, 0);
            hLayout.spacing = 8f;
            hLayout.childForceExpandWidth = false;
            hLayout.childControlWidth = true;
            hLayout.childForceExpandHeight = true;
            hLayout.childControlHeight = true;

            var le = rowObj.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;

            // Sol Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            var labelTxt = labelObj.AddComponent<Text>();
            labelTxt.text = label;
            labelTxt.fontSize = 13;
            labelTxt.fontStyle = FontStyle.Normal;
            labelTxt.color = new Color(0.741f, 0.765f, 0.780f, 1f); // #BDC3C7
            labelTxt.alignment = TextAnchor.MiddleLeft;
            if (font != null) labelTxt.font = font;
            var labelLe = labelObj.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 120f;

            // Sağ Dəyər
            var valObj = new GameObject("Value");
            valObj.transform.SetParent(rowObj.transform, false);
            var valTxt = valObj.AddComponent<Text>();
            valTxt.text = defaultValue;
            valTxt.fontSize = 14;
            valTxt.fontStyle = FontStyle.Bold;
            valTxt.color = valueColor;
            valTxt.alignment = TextAnchor.MiddleRight;
            if (font != null) valTxt.font = font;
            var valLe = valObj.AddComponent<LayoutElement>();
            valLe.flexibleWidth = 1f;

            return valTxt;
        }

        /// <summary>
        /// Seçilmiş əyalətin ən son məlumatlarını DatabaseManager-dən oxuyaraq UI mətnlərini yeniləyir.
        /// </summary>
        public void RefreshSelectedProvinceUI()
        {
            if (_selectedMapProvince != null && _selectedMapProvince.IsWasteland)
            {
                return;
            }

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

            RefreshGlobalEconomyAndDemographicsHUD();

            Debug.Log(
                $"[UIManager] Əyalət Paneli Yeniləndi -> {province.Name} (İqlim: {province.Climate}, İnkişaf: {province.DevelopmentLevel}, Məhsuldarlıq: {province.ProductivityMultiplier * 100f:F0}%) | " +
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

        #region Faza 10.5 - Demoqrafiya və İdarəetmə Paneli

        /// <summary>
        /// Faza 10.5 - ProvinceInfoPanel-in sol tərəfinə yapışan "Demoqrafiya və İdarəetmə" bölməsini qurur.
        /// ProvinceInfoPanel-in övladıdır (birlikdə açılıb bağlanır), lakin layout-dan kənardadır (ignoreLayout).
        /// </summary>
        public void EnsureDemographicsPanelHierarchy(bool forceRebuild = false)
        {
            EnsureProvincePanelHierarchy();
            if (provinceInfoPanel == null) return;

            if (demographicsPanel != null && !forceRebuild && demoPublicOrderText != null && executeSickButton != null)
            {
                WireDemographicsButtons();
                return;
            }

            if (demographicsPanel != null)
            {
                if (Application.isPlaying) Destroy(demographicsPanel);
                else DestroyImmediate(demographicsPanel);
            }

            var existing = provinceInfoPanel.transform.Find("DemographicsPanel");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            demographicsPanel = new GameObject("DemographicsPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            demographicsPanel.transform.SetParent(provinceInfoPanel.transform, false);

            var ignore = demographicsPanel.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            var rt = demographicsPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-10f, 0f);
            rt.sizeDelta = new Vector2(330f, 540f);

            demographicsPanel.GetComponent<Image>().color = new Color(0.086f, 0.106f, 0.141f, 0.95f); // #161B24
            var outline = demographicsPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var vlg = demographicsPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Başlıq
            var header = new GameObject("DemographicsHeader", typeof(RectTransform));
            header.transform.SetParent(demographicsPanel.transform, false);
            var headerTxt = header.AddComponent<Text>();
            headerTxt.text = "👥 DEMOQRAFİYA VƏ İDARƏETMƏ";
            headerTxt.fontSize = 15;
            headerTxt.fontStyle = FontStyle.Bold;
            headerTxt.alignment = TextAnchor.MiddleCenter;
            headerTxt.color = new Color(0.95f, 0.82f, 0.20f, 1f);
            if (font != null) headerTxt.font = font;
            header.AddComponent<LayoutElement>().preferredHeight = 26f;

            CreateDivider("DemoDivider1", demographicsPanel.transform);

            Color stripA = new Color(1f, 1f, 1f, 0.04f);
            Color stripB = new Color(1f, 1f, 1f, 0.08f);
            Color neutral = new Color(0.92f, 0.94f, 0.95f, 1f);

            demoInfantsText = CreateStatRow("InfantsRow", "👶 Körpələr", "-", new Color(0.68f, 0.84f, 0.95f, 1f), stripA, demographicsPanel.transform, font);
            demoYouthsText = CreateStatRow("YouthsRow", "🧒 Gənclər", "-", neutral, stripB, demographicsPanel.transform, font);
            demoAdultMenText = CreateStatRow("AdultMenRow", "👨 Yetişkin Kişilər", "-", new Color(0.36f, 0.68f, 0.89f, 1f), stripA, demographicsPanel.transform, font);
            demoAdultWomenText = CreateStatRow("AdultWomenRow", "👩 Yetişkin Qadınlar", "-", new Color(0.91f, 0.55f, 0.75f, 1f), stripB, demographicsPanel.transform, font);
            demoElderlyText = CreateStatRow("ElderlyRow", "👴 Yaşlılar", "-", new Color(0.74f, 0.76f, 0.78f, 1f), stripA, demographicsPanel.transform, font);
            demoSickText = CreateStatRow("SickRow", "🤒 Xəstələr", "-", new Color(0.95f, 0.61f, 0.07f, 1f), stripB, demographicsPanel.transform, font);
            demoWoundedText = CreateStatRow("WoundedRow", "🩹 Yaralılar", "-", new Color(0.91f, 0.30f, 0.24f, 1f), stripA, demographicsPanel.transform, font);
            demoPublicOrderText = CreateStatRow("PublicOrderRow", "⚖️ Xalqın Moralı", "-", new Color(0.18f, 0.80f, 0.44f, 1f), stripB, demographicsPanel.transform, font);

            CreateDivider("DemoDivider2", demographicsPanel.transform);

            conscriptMenButton = CreateDemographicsButton("ConscriptMenButton", "⚔️ Kişiləri Orduya Çağır (+1,000)", new Color(0.20f, 0.40f, 0.65f, 1f), font);
            trainEliteButton = CreateDemographicsButton("TrainEliteButton", "🛡️ Gənclərdən Elit Ordu (+500, -250 Qızıl)", new Color(0.55f, 0.45f, 0.15f, 1f), font);
            mobilizeWomenButton = CreateDemographicsButton("MobilizeWomenButton", "🏹 Qadınları Səfərbər Et (+1,000)", new Color(0.55f, 0.25f, 0.45f, 1f), font);
            executeSickButton = CreateDemographicsButton("ExecuteSickButton", "☠️ Xəstələri Edam Et / Resursu Kəs", new Color(0.45f, 0.10f, 0.10f, 1f), font);

            var statusGo = new GameObject("DemographicsStatusText", typeof(RectTransform));
            statusGo.transform.SetParent(demographicsPanel.transform, false);
            demoStatusText = statusGo.AddComponent<Text>();
            demoStatusText.text = "Strateji qərar seçin.";
            demoStatusText.fontSize = 11;
            demoStatusText.alignment = TextAnchor.UpperLeft;
            demoStatusText.color = new Color(0.80f, 0.84f, 0.88f, 1f);
            demoStatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            demoStatusText.verticalOverflow = VerticalWrapMode.Truncate;
            if (font != null) demoStatusText.font = font;
            statusGo.AddComponent<LayoutElement>().preferredHeight = 44f;

            WireDemographicsButtons();
        }

        private Button CreateDemographicsButton(string name, string label, Color bg, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(demographicsPanel.transform, false);
            go.GetComponent<Image>().color = bg;
            go.AddComponent<LayoutElement>().preferredHeight = 32f;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            if (font != null) txt.font = font;

            return go.GetComponent<Button>();
        }

        private void WireDemographicsButtons()
        {
            if (conscriptMenButton != null)
            {
                conscriptMenButton.onClick.RemoveListener(HandleConscriptMenClick);
                conscriptMenButton.onClick.AddListener(HandleConscriptMenClick);
            }
            if (trainEliteButton != null)
            {
                trainEliteButton.onClick.RemoveListener(HandleTrainEliteClick);
                trainEliteButton.onClick.AddListener(HandleTrainEliteClick);
            }
            if (mobilizeWomenButton != null)
            {
                mobilizeWomenButton.onClick.RemoveListener(HandleMobilizeWomenClick);
                mobilizeWomenButton.onClick.AddListener(HandleMobilizeWomenClick);
            }
            if (executeSickButton != null)
            {
                executeSickButton.onClick.RemoveListener(HandleExecuteSickClick);
                executeSickButton.onClick.AddListener(HandleExecuteSickClick);
            }
        }

        /// <summary>
        /// 7 əhali sinfini və Xalqın Moralını paneldə yeniləyir.
        /// </summary>
        public void RefreshDemographicsPanel(MapInteractionController province = null)
        {
            if (province == null) province = _selectedMapProvince;
            if (province == null) return;

            var d = province.Demographics;
            if (d == null) return;

            if (demoInfantsText != null) demoInfantsText.text = $"{d.Infants:N0}";
            if (demoYouthsText != null) demoYouthsText.text = $"{d.Youths:N0}";
            if (demoAdultMenText != null) demoAdultMenText.text = $"{d.AdultMen:N0}";
            if (demoAdultWomenText != null) demoAdultWomenText.text = $"{d.AdultWomen:N0}";
            if (demoElderlyText != null) demoElderlyText.text = $"{d.Elderly:N0}";
            if (demoSickText != null) demoSickText.text = $"{d.Sick:N0}";
            if (demoWoundedText != null) demoWoundedText.text = $"{d.Wounded:N0}";
            if (demoPublicOrderText != null)
            {
                demoPublicOrderText.text = $"{d.PublicOrder:F0}%";
                demoPublicOrderText.color = d.PublicOrder >= 70f
                    ? new Color(0.18f, 0.80f, 0.44f, 1f)   // yaşıl
                    : d.PublicOrder >= 40f
                        ? new Color(0.95f, 0.77f, 0.06f, 1f) // sarı
                        : new Color(0.91f, 0.30f, 0.24f, 1f); // qırmızı
            }
        }

        private void AfterDemographicAction(bool success, string message)
        {
            if (demoStatusText != null)
            {
                demoStatusText.text = message;
                demoStatusText.color = success ? new Color(0.80f, 0.84f, 0.88f, 1f) : new Color(0.91f, 0.30f, 0.24f, 1f);
            }

            if (_selectedMapProvince != null)
            {
                string keep = message;
                ShowProvincePanel(_selectedMapProvince);
                if (demoStatusText != null) demoStatusText.text = keep;
            }

            if (GameTimeManager.Instance != null) GameTimeManager.Instance.RecalculateMonthlyBalances();
            UpdateTopBar();
        }

        public void HandleConscriptMenClick()
        {
            bool ok = DemographicsSystem.ConscriptMen(_selectedMapProvince, out string msg, out _);
            AfterDemographicAction(ok, msg);
        }

        public void HandleTrainEliteClick()
        {
            bool ok = DemographicsSystem.TrainEliteYouths(_selectedMapProvince, out string msg, out _);
            AfterDemographicAction(ok, msg);
        }

        public void HandleMobilizeWomenClick()
        {
            bool ok = DemographicsSystem.MobilizeWomen(_selectedMapProvince, out string msg, out _);
            AfterDemographicAction(ok, msg);
        }

        public void HandleExecuteSickClick()
        {
            bool ok = DemographicsSystem.ExecuteSick(_selectedMapProvince, out string msg);
            AfterDemographicAction(ok, msg);
        }

        #endregion

        #region Faza 10.4 - Top Bar Panel

        /// <summary>
        /// Faza 10.4 - Paradox Interactive (EU4/CK3) üslubunda boydan-boya uzanan Top Bar UI panelini qurur.
        /// Ekranın üst hissəsi: (0, 1) - (1, 1), Hündürlük: 48px, #121720 arxa fon, #C5A059 qızılı alt xətt.
        /// </summary>
        public void EnsureTopBarHierarchy(bool forceRebuild = false)
        {
            if (topBarPanel == null)
            {
                var found = GameObject.Find("TopBarPanel");
                if (found != null)
                {
                    if (found.GetComponent<RectTransform>() == null)
                    {
                        if (Application.isPlaying) Destroy(found);
                        else DestroyImmediate(found);
                        topBarPanel = null;
                    }
                    else
                    {
                        topBarPanel = found;
                    }
                }
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();

            if (topBarPanel == null && canvas != null)
            {
                var panelObj = new GameObject("TopBarPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelObj.transform.SetParent(canvas.transform, false);
                topBarPanel = panelObj;
            }

            if (topBarPanel == null) return;

            // Köhnə DateTrackerPanel varsa, onu gizlədirik ki, yeni Top Bar ilə toqquşmasın
            var oldDatePanel = GameObject.Find("DateTrackerPanel");
            if (oldDatePanel != null)
            {
                oldDatePanel.SetActive(false);
            }

            var rt = topBarPanel.GetComponent<RectTransform>();
            if (rt == null) rt = topBarPanel.AddComponent<RectTransform>();

            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 48f);

            var img = topBarPanel.GetComponent<Image>() ?? topBarPanel.AddComponent<Image>();
            img.color = new Color(0.071f, 0.090f, 0.125f, 0.95f); // #121720 Alpha 0.95

            // Alt qızılı xətt (#C5A059)
            var stripeObj = topBarPanel.transform.Find("BottomGoldStripe");
            if (stripeObj == null)
            {
                var stripeGo = new GameObject("BottomGoldStripe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                stripeGo.transform.SetParent(topBarPanel.transform, false);
                var stripeRt = stripeGo.GetComponent<RectTransform>();
                stripeRt.anchorMin = new Vector2(0f, 0f);
                stripeRt.anchorMax = new Vector2(1f, 0f);
                stripeRt.pivot = new Vector2(0.5f, 0f);
                stripeRt.anchoredPosition = Vector2.zero;
                stripeRt.sizeDelta = new Vector2(0f, 2f);
                var stripeImg = stripeGo.GetComponent<Image>();
                stripeImg.color = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059
            }

            if (topBarGoldText == null || forceRebuild)
            {
                // Təmizləyirik (BottomGoldStripe xaric)
                for (int i = topBarPanel.transform.childCount - 1; i >= 0; i--)
                {
                    var child = topBarPanel.transform.GetChild(i);
                    if (child.name != "BottomGoldStripe")
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }

                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                // ==========================================
                // 1. SOL HİSSƏ: DÖVLƏT KİMLİYİ
                // ==========================================
                var leftContainer = new GameObject("LeftSection", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                leftContainer.transform.SetParent(topBarPanel.transform, false);
                var leftRt = leftContainer.GetComponent<RectTransform>();
                leftRt.anchorMin = new Vector2(0f, 0f);
                leftRt.anchorMax = new Vector2(0.28f, 1f);
                leftRt.pivot = new Vector2(0f, 0.5f);
                leftRt.offsetMin = new Vector2(16f, 0f);
                leftRt.offsetMax = new Vector2(0f, 0f);

                var leftHlg = leftContainer.GetComponent<HorizontalLayoutGroup>();
                leftHlg.spacing = 10f;
                leftHlg.childAlignment = TextAnchor.MiddleLeft;
                leftHlg.childForceExpandWidth = false;
                leftHlg.childForceExpandHeight = false;
                leftHlg.childControlWidth = false;
                leftHlg.childControlHeight = false;

                // Dövlət Rəng Nişanı (OwnerColorBadge)
                var badgeGo = new GameObject("OwnerColorBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGo.transform.SetParent(leftContainer.transform, false);
                var badgeRt = badgeGo.GetComponent<RectTransform>();
                badgeRt.sizeDelta = new Vector2(16f, 24f);
                topBarOwnerBadge = badgeGo.GetComponent<Image>();
                topBarOwnerBadge.color = new Color(0.122f, 0.380f, 0.553f, 1f); // #1F618D
                var badgeOutline = badgeGo.AddComponent<Outline>();
                badgeOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f);
                badgeOutline.effectDistance = new Vector2(1f, -1f);

                // Dövlət və Rəhbər Mətni
                var leaderGo = new GameObject("FactionLeaderText", typeof(RectTransform));
                leaderGo.transform.SetParent(leftContainer.transform, false);
                var leaderRt = leaderGo.GetComponent<RectTransform>();
                leaderRt.sizeDelta = new Vector2(250f, 30f);
                topBarFactionLeaderText = leaderGo.AddComponent<Text>();
                topBarFactionLeaderText.text = "Eldəgəzlər (Atabəylər) | Qızıl Arslan";
                topBarFactionLeaderText.fontStyle = FontStyle.Bold;
                topBarFactionLeaderText.fontSize = 14;
                topBarFactionLeaderText.color = new Color(0.96f, 0.96f, 0.98f, 1f);
                topBarFactionLeaderText.alignment = TextAnchor.MiddleLeft;
                if (font != null) topBarFactionLeaderText.font = font;

                // ==========================================
                // 2. ORTA HİSSƏ: XƏZİNƏ VƏ RESURSLAR
                // ==========================================
                var centerContainer = new GameObject("CenterSection", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                centerContainer.transform.SetParent(topBarPanel.transform, false);
                var centerRt = centerContainer.GetComponent<RectTransform>();
                centerRt.anchorMin = new Vector2(0.28f, 0f);
                centerRt.anchorMax = new Vector2(0.68f, 1f);
                centerRt.pivot = new Vector2(0.5f, 0.5f);
                centerRt.offsetMin = Vector2.zero;
                centerRt.offsetMax = Vector2.zero;

                var centerHlg = centerContainer.GetComponent<HorizontalLayoutGroup>();
                centerHlg.spacing = 16f;
                centerHlg.childAlignment = TextAnchor.MiddleCenter;
                centerHlg.childForceExpandWidth = false;
                centerHlg.childForceExpandHeight = false;
                centerHlg.childControlWidth = false;
                centerHlg.childControlHeight = false;

                // 💰 Qızıl Xəzinəsi
                var goldGo = new GameObject("GoldText", typeof(RectTransform));
                goldGo.transform.SetParent(centerContainer.transform, false);
                var goldRt = goldGo.GetComponent<RectTransform>();
                goldRt.sizeDelta = new Vector2(140f, 30f);
                topBarGoldText = goldGo.AddComponent<Text>();
                topBarGoldText.text = "💰 2,500 (+410/ay)";
                topBarGoldText.fontStyle = FontStyle.Bold;
                topBarGoldText.fontSize = 13;
                topBarGoldText.color = new Color(1f, 0.843f, 0f, 1f); // #FFD700
                topBarGoldText.alignment = TextAnchor.MiddleLeft;
                if (font != null) topBarGoldText.font = font;

                // 🌾 Ərzaq Anbarı
                var foodGo = new GameObject("FoodText", typeof(RectTransform));
                foodGo.transform.SetParent(centerContainer.transform, false);
                var foodRt = foodGo.GetComponent<RectTransform>();
                foodRt.sizeDelta = new Vector2(140f, 30f);
                topBarFoodText = foodGo.AddComponent<Text>();
                topBarFoodText.text = "🌾 1,800 (+125/ay)";
                topBarFoodText.fontStyle = FontStyle.Bold;
                topBarFoodText.fontSize = 13;
                topBarFoodText.color = new Color(0.18f, 0.80f, 0.44f, 1f); // #2ECC71
                topBarFoodText.alignment = TextAnchor.MiddleLeft;
                if (font != null) topBarFoodText.font = font;

                // ⚒️ Dəmir Ehtiyatı
                var ironGo = new GameObject("IronText", typeof(RectTransform));
                ironGo.transform.SetParent(centerContainer.transform, false);
                var ironRt = ironGo.GetComponent<RectTransform>();
                ironRt.sizeDelta = new Vector2(120f, 30f);
                topBarIronText = ironGo.AddComponent<Text>();
                topBarIronText.text = "⚒️ 650 (+85/ay)";
                topBarIronText.fontStyle = FontStyle.Bold;
                topBarIronText.fontSize = 13;
                topBarIronText.color = new Color(0.741f, 0.765f, 0.780f, 1f); // #BDC3C7
                topBarIronText.alignment = TextAnchor.MiddleLeft;
                if (font != null) topBarIronText.font = font;

                // ⚔️ Ümumi Ordu
                var troopGo = new GameObject("TroopsText", typeof(RectTransform));
                troopGo.transform.SetParent(centerContainer.transform, false);
                var troopRt = troopGo.GetComponent<RectTransform>();
                troopRt.sizeDelta = new Vector2(90f, 30f);
                topBarTroopsText = troopGo.AddComponent<Text>();
                topBarTroopsText.text = "⚔️ 35,000";
                topBarTroopsText.fontStyle = FontStyle.Bold;
                topBarTroopsText.fontSize = 13;
                topBarTroopsText.color = new Color(0.88f, 0.90f, 0.93f, 1f);
                topBarTroopsText.alignment = TextAnchor.MiddleLeft;
                if (font != null) topBarTroopsText.font = font;

                // ==========================================
                // 3. SAĞ HİSSƏ: TARİX VƏ ZAMAN İDARƏETMƏSİ
                // ==========================================
                var rightContainer = new GameObject("RightSection", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                rightContainer.transform.SetParent(topBarPanel.transform, false);
                var rightRt = rightContainer.GetComponent<RectTransform>();
                rightRt.anchorMin = new Vector2(0.68f, 0f);
                rightRt.anchorMax = new Vector2(1f, 1f);
                rightRt.pivot = new Vector2(1f, 0.5f);
                rightRt.offsetMin = Vector2.zero;
                rightRt.offsetMax = new Vector2(-16f, 0f);

                var rightHlg = rightContainer.GetComponent<HorizontalLayoutGroup>();
                rightHlg.spacing = 12f;
                rightHlg.childAlignment = TextAnchor.MiddleRight;
                rightHlg.childForceExpandWidth = false;
                rightHlg.childForceExpandHeight = false;
                rightHlg.childControlWidth = false;
                rightHlg.childControlHeight = false;

                // 📅 Cari Tarix
                var dateGo = new GameObject("DateText", typeof(RectTransform));
                dateGo.transform.SetParent(rightContainer.transform, false);
                var dateRt = dateGo.GetComponent<RectTransform>();
                dateRt.sizeDelta = new Vector2(210f, 30f);
                topBarDateText = dateGo.AddComponent<Text>();
                topBarDateText.text = "1 Yanvar 1187  <color=#E74C3C>⏸ PAUSED</color>";
                topBarDateText.fontStyle = FontStyle.Bold;
                topBarDateText.fontSize = 13;
                topBarDateText.color = new Color(0.95f, 0.82f, 0.20f, 1f);
                topBarDateText.alignment = TextAnchor.MiddleRight;
                if (font != null) topBarDateText.font = font;

                // Sürət Düymələri (⏸, 1x, 2x, 3x, 4x, 5x)
                var speedGroup = new GameObject("SpeedButtonGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                speedGroup.transform.SetParent(rightContainer.transform, false);
                var speedHlg = speedGroup.GetComponent<HorizontalLayoutGroup>();
                speedHlg.spacing = 4f;
                speedHlg.childAlignment = TextAnchor.MiddleRight;
                speedHlg.childForceExpandWidth = false;
                speedHlg.childForceExpandHeight = false;
                speedHlg.childControlWidth = false;
                speedHlg.childControlHeight = false;

                topBarPauseBtn = CreateSpeedButton("PauseBtn", "⏸", speedGroup.transform, font);
                topBarSpeed1Btn = CreateSpeedButton("Speed1Btn", "1x", speedGroup.transform, font);
                topBarSpeed2Btn = CreateSpeedButton("Speed2Btn", "2x", speedGroup.transform, font);
                topBarSpeed3Btn = CreateSpeedButton("Speed3Btn", "3x", speedGroup.transform, font);
                topBarSpeed4Btn = CreateSpeedButton("Speed4Btn", "4x", speedGroup.transform, font);
                topBarSpeed5Btn = CreateSpeedButton("Speed5Btn", "5x", speedGroup.transform, font);
            }

            WireTopBarButtons();
            UpdateTopBar();
            UpdateTopBarSpeedButtons();
        }

        private Button CreateSpeedButton(string name, string label, Transform parent, Font font)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(28f, 26f);

            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = new Color(0.12f, 0.15f, 0.19f, 1f); // #1E2530

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

            var txt = txtGo.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            txt.fontSize = 11;
            txt.color = new Color(0.92f, 0.94f, 0.95f, 1f);
            if (font != null) txt.font = font;

            return btnGo.GetComponent<Button>();
        }

        private void WireTopBarButtons()
        {
            if (topBarPauseBtn != null)
            {
                topBarPauseBtn.onClick.RemoveAllListeners();
                topBarPauseBtn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.Pause();
                });
            }
            if (topBarSpeed1Btn != null)
            {
                topBarSpeed1Btn.onClick.RemoveAllListeners();
                topBarSpeed1Btn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.SetSpeed(1);
                });
            }
            if (topBarSpeed2Btn != null)
            {
                topBarSpeed2Btn.onClick.RemoveAllListeners();
                topBarSpeed2Btn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.SetSpeed(2);
                });
            }
            if (topBarSpeed3Btn != null)
            {
                topBarSpeed3Btn.onClick.RemoveAllListeners();
                topBarSpeed3Btn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.SetSpeed(3);
                });
            }
            if (topBarSpeed4Btn != null)
            {
                topBarSpeed4Btn.onClick.RemoveAllListeners();
                topBarSpeed4Btn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.SetSpeed(4);
                });
            }
            if (topBarSpeed5Btn != null)
            {
                topBarSpeed5Btn.onClick.RemoveAllListeners();
                topBarSpeed5Btn.onClick.AddListener(() => {
                    if (GameTimeManager.Instance != null) GameTimeManager.Instance.SetSpeed(5);
                });
            }
        }

        /// <summary>
        /// Yuxarı Dövlət Panelindəki Xəzinə, Ərzaq, Dəmir və Ordu mətnlərini yeniləyir.
        /// </summary>
        public void UpdateTopBar()
        {
            var gtm = GameTimeManager.Instance;
            if (gtm == null) return;

            if (topBarFactionLeaderText != null)
            {
                topBarFactionLeaderText.text = $"{gtm.PlayerFactionName} | {gtm.PlayerLeaderName}";
            }
            if (topBarOwnerBadge != null)
            {
                topBarOwnerBadge.color = gtm.PlayerFactionColor;
            }

            if (topBarGoldText != null)
            {
                string goldSign = gtm.MonthlyGoldIncome >= 0 ? "+" : "";
                topBarGoldText.text = $"💰 {gtm.PlayerGold.ToString("N0", CultureInfo.InvariantCulture)} ({goldSign}{gtm.MonthlyGoldIncome.ToString("N0", CultureInfo.InvariantCulture)}/ay)";
            }

            if (topBarFoodText != null)
            {
                string foodSign = gtm.MonthlyFoodNet >= 0 ? "+" : "";
                topBarFoodText.text = $"🌾 {gtm.PlayerFood.ToString("N0", CultureInfo.InvariantCulture)} ({foodSign}{gtm.MonthlyFoodNet.ToString("N0", CultureInfo.InvariantCulture)}/ay)";
                topBarFoodText.color = gtm.MonthlyFoodNet >= 0
                    ? new Color(0.18f, 0.80f, 0.44f, 1f)  // #2ECC71 Yaşıl
                    : new Color(0.90f, 0.30f, 0.24f, 1f); // #E74C3C Qırmızı
            }

            if (topBarIronText != null)
            {
                string ironSign = gtm.MonthlyIronIncome >= 0 ? "+" : "";
                topBarIronText.text = $"⚒️ {gtm.PlayerIron.ToString("N0", CultureInfo.InvariantCulture)} ({ironSign}{gtm.MonthlyIronIncome.ToString("N0", CultureInfo.InvariantCulture)}/ay)";
            }

            if (topBarTroopsText != null)
            {
                topBarTroopsText.text = $"⚔️ {gtm.TotalPlayerTroops.ToString("N0", CultureInfo.InvariantCulture)}";
            }

            RefreshTopBarDate();
        }

        /// <summary>
        /// Yuxarı Dövlət Panelindəki Cari Tarixi və Pause vəziyyətini yeniləyir.
        /// </summary>
        public void RefreshTopBarDate()
        {
            var gtm = GameTimeManager.Instance;
            if (gtm == null) return;

            if (topBarDateText != null)
            {
                topBarDateText.text = gtm.DateWithPauseStatus;
            }
        }

        /// <summary>
        /// Sürət düymələrinin aktiv/passiv vizual stilini yeniləyir (Aktiv olan qızılı #F1C40F rəngdə olur).
        /// </summary>
        public void UpdateTopBarSpeedButtons()
        {
            var gtm = GameTimeManager.Instance;
            int speed = gtm != null ? gtm.CurrentSpeedMultiplier : 0;
            bool isPaused = gtm != null && gtm.IsPaused;

            SetSpeedButtonState(topBarPauseBtn, isPaused);
            SetSpeedButtonState(topBarSpeed1Btn, !isPaused && speed == 1);
            SetSpeedButtonState(topBarSpeed2Btn, !isPaused && speed == 2);
            SetSpeedButtonState(topBarSpeed3Btn, !isPaused && speed == 3);
            SetSpeedButtonState(topBarSpeed4Btn, !isPaused && speed == 4);
            SetSpeedButtonState(topBarSpeed5Btn, !isPaused && speed == 5);
        }

        private void SetSpeedButtonState(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            var txt = btn.GetComponentInChildren<Text>();

            if (isActive)
            {
                if (img != null) img.color = new Color(0.945f, 0.769f, 0.059f, 1f); // #F1C40F Qızılı
                if (txt != null) txt.color = new Color(0.071f, 0.090f, 0.125f, 1f); // Tünd mətn
            }
            else
            {
                if (img != null) img.color = new Color(0.12f, 0.15f, 0.19f, 1f); // #1E2530
                if (txt != null) txt.color = new Color(0.92f, 0.94f, 0.95f, 1f); // Açıq mətn
            }
        }

        #endregion

        #region Faza 10.8 - AAA Grand Strategy HUD Panelləri (Faction Profile, Event Log, Minimap)

        /// <summary>
        /// Faza 10.8: EU4 / CK3 standartlarında 3 əsas künc panelinin iyerarxiyasını təmin edir:
        /// 1. Sol Yuxarı: Dövlət və Hökmdar Paneli (FactionProfilePanel - Top-Left Anchor)
        /// 2. Sol Aşağı: Bildirişlər və Salnamə Paneli (EventLogPanel - Bottom-Left Anchor)
        /// 3. Sağ Aşağı: Mini-Xəritə və Xəritə Rejimləri (MinimapContainer - Bottom-Right Anchor)
        /// </summary>
        public void EnsureGrandStrategyHUDHierarchy(bool forceRebuild = false)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform canvasTr = canvas.transform;

            EnsureFactionProfilePanel(canvasTr, forceRebuild);
            EnsureEventLogPanel(canvasTr, forceRebuild);

            // Mini-xəritə ləğv edildiyi üçün əgər səhnədə köhnə MinimapContainer qalıbsa, onu tamamilə silirik:
            var oldMinimap = canvasTr.Find("MinimapContainer");
            if (oldMinimap != null)
            {
                if (Application.isPlaying) Destroy(oldMinimap.gameObject);
                else DestroyImmediate(oldMinimap.gameObject);
            }

            UpdateFactionProfileFromCurrentGame();
            EnsureThemeEngine();
            EnsureDiplomacyLedgerHierarchy(forceRebuild);
        }

        /// <summary>
        /// Faza 10.10: AAA Qlobal Diplomatiya Mərkəzi (DiplomacyLedgerPanel) iyerarxiyasını təmin edir.
        /// </summary>
        public void EnsureDiplomacyLedgerHierarchy(bool forceRebuild = false)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (diplomacyUIManager == null)
            {
                diplomacyUIManager = GetComponent<DiplomacyUIManager>() ?? GetComponentInChildren<DiplomacyUIManager>(true) ?? UnityEngine.Object.FindAnyObjectByType<DiplomacyUIManager>();
                if (diplomacyUIManager == null)
                {
                    diplomacyUIManager = gameObject.AddComponent<DiplomacyUIManager>();
                }
            }

            if (diplomacyUIManager != null)
            {
                diplomacyUIManager.EnsureHierarchy(canvas.transform, forceRebuild);
                if (themeManager != null && themeManager.CurrentTheme != null)
                {
                    diplomacyUIManager.ApplyTheme(themeManager.CurrentTheme);
                }
            }
        }

        public void OpenDiplomacyLedger()
        {
            if (diplomacyUIManager != null) diplomacyUIManager.OpenPanel();
        }

        public void CloseDiplomacyLedger()
        {
            if (diplomacyUIManager != null) diplomacyUIManager.ClosePanel();
        }

        public void ToggleDiplomacyLedger()
        {
            if (diplomacyUIManager != null) diplomacyUIManager.TogglePanel();
        }

        /// <summary>
        /// Faza 10.9: Modulyar UI Tema Mühərrikini təmin edir və aktiv temann panellərə tətbiq olunmasını tətikləyir.
        /// </summary>
        public void EnsureThemeEngine()
        {
            if (themeManager == null)
            {
                themeManager = GetComponent<UIThemeManager>() ?? GetComponentInParent<UIThemeManager>() ?? UnityEngine.Object.FindAnyObjectByType<UIThemeManager>();
                if (themeManager == null)
                {
                    themeManager = gameObject.AddComponent<UIThemeManager>();
                }
            }

            if (themeManager != null)
            {
                themeManager.EnsureDefaultThemeAssigned();
                themeManager.ApplyTheme();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Grand Strategy/Rebuild HUD (Phase 10.8)")]
        public void RebuildGrandStrategyHUDInEditor()
        {
            EnsureGrandStrategyHUDHierarchy(forceRebuild: true);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }
#endif

        private void EnsureFactionProfilePanel(Transform canvasTr, bool forceRebuild)
        {
            if (factionProfilePanel == null)
            {
                var found = canvasTr.Find("FactionProfilePanel");
                if (found != null) factionProfilePanel = found.gameObject;
            }

            if (forceRebuild && factionProfilePanel != null)
            {
                if (Application.isPlaying) Destroy(factionProfilePanel);
                else DestroyImmediate(factionProfilePanel);
                factionProfilePanel = null;
            }

            if (factionProfilePanel == null)
            {
                factionProfilePanel = new GameObject("FactionProfilePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                factionProfilePanel.transform.SetParent(canvasTr, false);
            }

            var rt = factionProfilePanel.GetComponent<RectTransform>() ?? factionProfilePanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -56f); // Top Bar-dan 8px aşağıda
            rt.sizeDelta = new Vector2(300f, 74f);

            var img = factionProfilePanel.GetComponent<Image>() ?? factionProfilePanel.AddComponent<Image>();
            img.color = new Color(0.067f, 0.067f, 0.067f, 0.88f); // #111111 ~88% Alpha

            var outline = factionProfilePanel.GetComponent<Outline>() ?? factionProfilePanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059 Qızılı kənar çərçivə
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Alt dekorativ zərif qızılı xətt
            var stripe = factionProfilePanel.transform.Find("GoldBottomStripe");
            if (stripe == null)
            {
                var stripeGo = new GameObject("GoldBottomStripe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                stripeGo.transform.SetParent(factionProfilePanel.transform, false);
                var sRt = stripeGo.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0f);
                sRt.anchorMax = new Vector2(1f, 0f);
                sRt.pivot = new Vector2(0.5f, 0f);
                sRt.anchoredPosition = Vector2.zero;
                sRt.sizeDelta = new Vector2(0f, 2f);
                var sImg = stripeGo.GetComponent<Image>();
                sImg.color = new Color(0.773f, 0.627f, 0.349f, 0.9f);
            }

            // Sol tərəf: Dairəvi formalı Dövlət Gerbi / Bayraq (FactionCrest)
            var crestTr = factionProfilePanel.transform.Find("FactionCrest");
            GameObject crestGo;
            if (crestTr == null)
            {
                crestGo = new GameObject("FactionCrest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                crestGo.transform.SetParent(factionProfilePanel.transform, false);
            }
            else
            {
                crestGo = crestTr.gameObject;
            }

            var crestRt = crestGo.GetComponent<RectTransform>();
            crestRt.anchorMin = new Vector2(0f, 0.5f);
            crestRt.anchorMax = new Vector2(0f, 0.5f);
            crestRt.pivot = new Vector2(0f, 0.5f);
            crestRt.anchoredPosition = new Vector2(12f, 0f);
            crestRt.sizeDelta = new Vector2(50f, 50f);

            factionCrestImage = crestGo.GetComponent<Image>();
            factionCrestImage.color = Color.white; // Placeholder təmiz ağ
            Sprite knobSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (knobSprite != null) factionCrestImage.sprite = knobSprite;

            var crestOutline = crestGo.GetComponent<Outline>() ?? crestGo.AddComponent<Outline>();
            crestOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059 Qızılı haşiyə
            crestOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Sağ tərəf: Mətn Konteyneri (Dövlət Adı + Hökmdar Adı)
            var textTr = factionProfilePanel.transform.Find("TextContainer");
            GameObject textGo;
            if (textTr == null)
            {
                textGo = new GameObject("TextContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
                textGo.transform.SetParent(factionProfilePanel.transform, false);
            }
            else
            {
                textGo = textTr.gameObject;
            }

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0f, 0.5f);
            textRt.offsetMin = new Vector2(70f, 6f);
            textRt.offsetMax = new Vector2(-10f, -6f);

            var vlg = textGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.childAlignment = TextAnchor.MiddleLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            TMP_FontAsset titleFont = (themeManager != null) ? themeManager.GetTitleFont() : TMP_Settings.defaultFontAsset;
            TMP_FontAsset mainFont = (themeManager != null) ? themeManager.GetMainFont() : TMP_Settings.defaultFontAsset;

            // 1. Üstdəki böyük şriftlə "Dövlət Adı" (TextMeshProUGUI)
            var fnTr = textGo.transform.Find("FactionNameText");
            GameObject fnGo = fnTr != null ? fnTr.gameObject : new GameObject("FactionNameText", typeof(RectTransform));
            if (fnTr == null) fnGo.transform.SetParent(textGo.transform, false);

            factionNameText = fnGo.GetComponent<TextMeshProUGUI>() ?? fnGo.AddComponent<TextMeshProUGUI>();
            if (titleFont != null && factionNameText.font == null) factionNameText.font = titleFont;
            factionNameText.text = "Eldəgəzlər (Atabəylər)";
            factionNameText.fontSize = 17f;
            factionNameText.fontStyle = FontStyles.Bold;
            factionNameText.color = Color.white;
            factionNameText.alignment = TextAlignmentOptions.MidlineLeft;
            factionNameText.raycastTarget = false;
            var fnLe = fnGo.GetComponent<LayoutElement>() ?? fnGo.AddComponent<LayoutElement>();
            fnLe.preferredHeight = 26f;

            // 2. Altındakı kiçik şriftlə, qızılı/sarı tonda "Hökmdar Adı" (TextMeshProUGUI)
            var flTr = textGo.transform.Find("FactionLeaderText");
            GameObject flGo = flTr != null ? flTr.gameObject : new GameObject("FactionLeaderText", typeof(RectTransform));
            if (flTr == null) flGo.transform.SetParent(textGo.transform, false);

            factionLeaderText = flGo.GetComponent<TextMeshProUGUI>() ?? flGo.AddComponent<TextMeshProUGUI>();
            if (mainFont != null && factionLeaderText.font == null) factionLeaderText.font = mainFont;
            factionLeaderText.text = "👑 Qızıl Arslan";
            factionLeaderText.fontSize = 13f;
            factionLeaderText.fontStyle = FontStyles.Normal;
            factionLeaderText.color = new Color(0.945f, 0.769f, 0.059f, 1f); // #F1C40F Qızılı
            factionLeaderText.alignment = TextAlignmentOptions.MidlineLeft;
            factionLeaderText.raycastTarget = false;
            var flLe = flGo.GetComponent<LayoutElement>() ?? flGo.AddComponent<LayoutElement>();
            flLe.preferredHeight = 22f;
        }

        private void EnsureEventLogPanel(Transform canvasTr, bool forceRebuild)
        {
            if (eventLogPanel == null)
            {
                var found = canvasTr.Find("EventLogPanel");
                if (found != null) eventLogPanel = found.gameObject;
            }

            if (forceRebuild && eventLogPanel != null)
            {
                if (Application.isPlaying) Destroy(eventLogPanel);
                else DestroyImmediate(eventLogPanel);
                eventLogPanel = null;
            }

            if (eventLogPanel == null)
            {
                eventLogPanel = new GameObject("EventLogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                eventLogPanel.transform.SetParent(canvasTr, false);
            }

            var rt = eventLogPanel.GetComponent<RectTransform>() ?? eventLogPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(350f, 200f);

            var img = eventLogPanel.GetComponent<Image>() ?? eventLogPanel.AddComponent<Image>();
            img.color = new Color(0.067f, 0.067f, 0.067f, 0.88f); // #111111 ~88% Alpha

            var outline = eventLogPanel.GetComponent<Outline>() ?? eventLogPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059 Qızılı
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            TMP_FontAsset titleFont = (themeManager != null) ? themeManager.GetTitleFont() : TMP_Settings.defaultFontAsset;
            TMP_FontAsset mainFont = (themeManager != null) ? themeManager.GetMainFont() : TMP_Settings.defaultFontAsset;

            // Başlıq Zolağı (HeaderBar): "📜 SALNAMƏ VƏ BİLDİRİŞLƏR"
            var headerTr = eventLogPanel.transform.Find("HeaderBar");
            GameObject headerGo = headerTr != null ? headerTr.gameObject : new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (headerTr == null) headerGo.transform.SetParent(eventLogPanel.transform, false);

            var hRt = headerGo.GetComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0f, 1f);
            hRt.anchorMax = new Vector2(1f, 1f);
            hRt.pivot = new Vector2(0.5f, 1f);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(0f, 26f);

            var hImg = headerGo.GetComponent<Image>();
            hImg.color = new Color(0.11f, 0.13f, 0.17f, 0.95f);

            // Başlıq mətni
            var htTr = headerGo.transform.Find("TitleText");
            GameObject htGo = htTr != null ? htTr.gameObject : new GameObject("TitleText", typeof(RectTransform));
            if (htTr == null) htGo.transform.SetParent(headerGo.transform, false);
            var htRt = htGo.GetComponent<RectTransform>();
            htRt.anchorMin = Vector2.zero;
            htRt.anchorMax = Vector2.one;
            htRt.offsetMin = new Vector2(10f, 0f);
            htRt.offsetMax = new Vector2(-10f, 0f);

            var hTmp = htGo.GetComponent<TextMeshProUGUI>() ?? htGo.AddComponent<TextMeshProUGUI>();
            if (titleFont != null && hTmp.font == null) hTmp.font = titleFont;
            hTmp.text = "📜  SALNAMƏ VƏ BİLDİRİŞLƏR";
            hTmp.fontSize = 11.5f;
            hTmp.fontStyle = FontStyles.Bold;
            hTmp.color = new Color(0.88f, 0.77f, 0.45f, 1f); // Klassik qızılı/parçament rəngi
            hTmp.alignment = TextAlignmentOptions.MidlineLeft;
            hTmp.raycastTarget = false;

            // Qızılı ayırıcı alt xətt
            var lineTr = headerGo.transform.Find("DividerLine");
            GameObject lineGo = lineTr != null ? lineTr.gameObject : new GameObject("DividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (lineTr == null) lineGo.transform.SetParent(headerGo.transform, false);
            var lRt = lineGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0f);
            lRt.anchorMax = new Vector2(1f, 0f);
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(0f, 1.5f);
            var lImg = lineGo.GetComponent<Image>();
            lImg.color = new Color(0.773f, 0.627f, 0.349f, 0.85f);

            // Scroll View (ScrollRect + Viewport + Content)
            var scrollTr = eventLogPanel.transform.Find("ScrollView");
            GameObject scrollGo = scrollTr != null ? scrollTr.gameObject : new GameObject("ScrollView", typeof(RectTransform));
            if (scrollTr == null) scrollGo.transform.SetParent(eventLogPanel.transform, false);

            var sRt = scrollGo.GetComponent<RectTransform>();
            sRt.anchorMin = Vector2.zero;
            sRt.anchorMax = Vector2.one;
            sRt.offsetMin = new Vector2(8f, 8f);
            sRt.offsetMax = new Vector2(-8f, -28f);

            eventLogScrollRect = scrollGo.GetComponent<ScrollRect>() ?? scrollGo.AddComponent<ScrollRect>();
            eventLogScrollRect.horizontal = false;
            eventLogScrollRect.vertical = true;
            eventLogScrollRect.movementType = ScrollRect.MovementType.Clamped;
            eventLogScrollRect.scrollSensitivity = 20f;

            // Viewport
            var vpTr = scrollGo.transform.Find("Viewport");
            GameObject vpGo = vpTr != null ? vpTr.gameObject : new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            if (vpTr == null) vpGo.transform.SetParent(scrollGo.transform, false);

            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            if (vpGo.GetComponent<RectMask2D>() == null) vpGo.AddComponent<RectMask2D>();

            eventLogScrollRect.viewport = vpRt;

            // Content
            var cntTr = vpGo.transform.Find("Content");
            GameObject cntGo = cntTr != null ? cntTr.gameObject : new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            if (cntTr == null) cntGo.transform.SetParent(vpGo.transform, false);

            eventLogContent = cntGo.GetComponent<RectTransform>();
            eventLogContent.anchorMin = new Vector2(0f, 1f);
            eventLogContent.anchorMax = new Vector2(1f, 1f);
            eventLogContent.pivot = new Vector2(0.5f, 1f);
            eventLogContent.anchoredPosition = Vector2.zero;
            eventLogContent.sizeDelta = new Vector2(0f, 0f);

            var cVlg = cntGo.GetComponent<VerticalLayoutGroup>();
            cVlg.spacing = 6f;
            cVlg.padding = new RectOffset(4, 4, 4, 4);
            cVlg.childAlignment = TextAnchor.UpperLeft;
            cVlg.childControlWidth = true;
            cVlg.childControlHeight = true;
            cVlg.childForceExpandWidth = true;
            cVlg.childForceExpandHeight = false;

            var cCsf = cntGo.GetComponent<ContentSizeFitter>();
            cCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            eventLogScrollRect.content = eventLogContent;

            // Əgər sətir yoxdursa, test üçün 3 ədəd nümunə salnamə mətni əlavə edirik:
            if (eventLogContent.childCount == 0)
            {
                CreateLogEntryRow(eventLogContent, "• <color=#F1C40F>1187:</color> Səlahəddin Qüdsə doğru hərəkətə keçdi.");
                CreateLogEntryRow(eventLogContent, "• <color=#2ECC71>Gəncə:</color> Bazarında ipək qiymətləri qalxdı.");
                CreateLogEntryRow(eventLogContent, "• <color=#3498DB>Təbriz:</color> Şirvanşahlar ilə sülh müqaviləsi imzalandı.");
            }
        }

        private GameObject CreateLogEntryRow(Transform parent, string message)
        {
            var rowGo = new GameObject("LogEntry", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);

            var rRt = rowGo.GetComponent<RectTransform>();
            rRt.sizeDelta = new Vector2(0f, 32f);

            var tmp = rowGo.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset mainFont = (themeManager != null) ? themeManager.GetMainFont() : TMP_Settings.defaultFontAsset;
            if (mainFont != null && tmp.font == null) tmp.font = mainFont;

            tmp.text = message;
            tmp.fontSize = 11.5f;
            tmp.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.raycastTarget = false;

            var csf = rowGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rowGo;
        }

        /// <summary>
        /// Salnamə və Bildirişlər panelinə yeni hadisə sətiri əlavə edir.
        /// </summary>
        public void AddEventLogMessage(string message)
        {
            if (eventLogContent == null) return;
            CreateLogEntryRow(eventLogContent, message);

            // Çox köhnə sətirləri məhdudlaşdırırıq (maksimum 50 sətir saxlanılır)
            while (eventLogContent.childCount > 50)
            {
                var oldest = eventLogContent.GetChild(0).gameObject;
                if (Application.isPlaying) Destroy(oldest);
                else DestroyImmediate(oldest);
            }

            if (eventLogScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                eventLogScrollRect.verticalNormalizedPosition = 0f; // Ən son sətirə sürüşdür
            }
        }

        /// <summary>
        /// GameTimeManager-dən cari dövlət və hökmdar adını oxuyub Sol Yuxarı FactionProfilePanel-i yeniləyir.
        /// </summary>
        public void UpdateFactionProfileFromCurrentGame()
        {
            var gtm = GameTimeManager.Instance;
            string fName = gtm != null ? gtm.PlayerFactionName : "Eldəgəzlər (Atabəylər)";
            string rName = gtm != null ? gtm.PlayerLeaderName : "Qızıl Arslan";
            Color? fColor = gtm != null ? (Color?)gtm.PlayerFactionColor : null;
            UpdateFactionProfile(fName, rName, fColor);
        }

        /// <summary>
        /// Sol Yuxarı Dövlət və Hökmdar profil panelini mətn və gerblə dinamik yeniləyir.
        /// </summary>
        public void UpdateFactionProfile(string factionName, string rulerName, Color? factionColor = null, Sprite crestSprite = null)
        {
            if (factionNameText != null)
            {
                factionNameText.text = factionName;
            }
            if (factionLeaderText != null)
            {
                factionLeaderText.text = $"👑 {rulerName}";
            }
            if (factionCrestImage != null)
            {
                if (crestSprite != null) factionCrestImage.sprite = crestSprite;
                if (factionColor.HasValue) factionCrestImage.color = factionColor.Value;
            }
        }

        #endregion

        #region Faza 10.12 - Hökmdar Döyüş Qərarı (RoyalBattlePrompt)

        public void EnsureRoyalBattlePromptHierarchy(bool forceRebuild = false)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();

            if (royalBattlePromptPanel == null)
            {
                var found = GameObject.Find("RoyalBattlePrompt");
                if (found == null && canvas != null)
                {
                    var tr = canvas.transform.Find("RoyalBattlePrompt");
                    if (tr != null) found = tr.gameObject;
                }

                if (found != null)
                {
                    royalBattlePromptPanel = found;
                }
            }

            if (royalBattlePromptPanel == null && canvas != null)
            {
                var panelGo = new GameObject("RoyalBattlePrompt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                panelGo.transform.SetParent(canvas.transform, false);
                royalBattlePromptPanel = panelGo;
            }

            if (royalBattlePromptPanel == null) return;

            var rt = royalBattlePromptPanel.GetComponent<RectTransform>() ?? royalBattlePromptPanel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 300f);

            var bgImg = royalBattlePromptPanel.GetComponent<Image>() ?? royalBattlePromptPanel.AddComponent<Image>();
            bgImg.color = new Color(0.07f, 0.09f, 0.14f, 0.97f);

            var outline = royalBattlePromptPanel.GetComponent<Outline>() ?? royalBattlePromptPanel.AddComponent<Outline>();
            outline.effectColor = new Color(0.90f, 0.72f, 0.22f, 1f);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            if (forceRebuild || royalBattleTitleText == null || royalBattleBodyText == null ||
                automateBattleButton == null || leadBattlePersonallyButton == null)
            {
                for (int i = royalBattlePromptPanel.transform.childCount - 1; i >= 0; i--)
                {
                    var child = royalBattlePromptPanel.transform.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }

                TMP_FontAsset mainFont = (themeManager != null) ? themeManager.GetMainFont() : TMP_Settings.defaultFontAsset;

                // Başlıq zolağı
                var headerGo = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
                headerGo.transform.SetParent(royalBattlePromptPanel.transform, false);
                var hRt = headerGo.GetComponent<RectTransform>();
                hRt.anchorMin = new Vector2(0f, 1f);
                hRt.anchorMax = new Vector2(1f, 1f);
                hRt.pivot = new Vector2(0.5f, 1f);
                hRt.anchoredPosition = Vector2.zero;
                hRt.sizeDelta = new Vector2(0f, 52f);
                headerGo.GetComponent<Image>().color = new Color(0.11f, 0.14f, 0.21f, 1f);

                var titleGo = new GameObject("TitleText", typeof(RectTransform));
                titleGo.transform.SetParent(headerGo.transform, false);
                var tRt = titleGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = new Vector2(16f, 0f);
                tRt.offsetMax = new Vector2(-16f, 0f);

                royalBattleTitleText = titleGo.AddComponent<TextMeshProUGUI>();
                if (mainFont != null) royalBattleTitleText.font = mainFont;
                royalBattleTitleText.text = "HÖKMDAR DÖYÜŞ QƏRARI";
                royalBattleTitleText.fontSize = 20f;
                royalBattleTitleText.fontStyle = FontStyles.Bold;
                royalBattleTitleText.color = new Color(0.95f, 0.79f, 0.22f, 1f);
                royalBattleTitleText.alignment = TextAlignmentOptions.Center;
                royalBattleTitleText.textWrappingMode = TextWrappingModes.NoWrap;

                // Mətn
                var bodyGo = new GameObject("BodyText", typeof(RectTransform));
                bodyGo.transform.SetParent(royalBattlePromptPanel.transform, false);
                var bRt = bodyGo.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0f, 0.35f);
                bRt.anchorMax = new Vector2(1f, 1f);
                bRt.offsetMin = new Vector2(28f, 10f);
                bRt.offsetMax = new Vector2(-28f, -66f);

                royalBattleBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
                if (mainFont != null) royalBattleBodyText.font = mainFont;
                royalBattleBodyText.text = "Hökmdar, ordularımız düşmənlə qarşılaşdı!";
                royalBattleBodyText.fontSize = 16.5f;
                royalBattleBodyText.color = new Color(0.93f, 0.95f, 0.97f, 1f);
                royalBattleBodyText.alignment = TextAlignmentOptions.Center;
                royalBattleBodyText.textWrappingMode = TextWrappingModes.Normal;
                royalBattleBodyText.richText = true;

                // Düymələr konteyneri
                var btnRowGo = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                btnRowGo.transform.SetParent(royalBattlePromptPanel.transform, false);
                var rowRt = btnRowGo.GetComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0f, 0f);
                rowRt.anchorMax = new Vector2(1f, 0f);
                rowRt.pivot = new Vector2(0.5f, 0f);
                rowRt.anchoredPosition = new Vector2(0f, 22f);
                rowRt.sizeDelta = new Vector2(-44f, 54f);

                var hlg = btnRowGo.GetComponent<HorizontalLayoutGroup>();
                hlg.spacing = 16f;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;

                automateBattleButton = CreateRoyalPromptButton(
                    btnRowGo.transform,
                    "AutomateBattleButton",
                    "Döyüşü Avtomatlaşdır",
                    new Color(0.18f, 0.28f, 0.42f, 1f),
                    new Color(0.42f, 0.58f, 0.78f, 1f),
                    Color.white,
                    mainFont);

                leadBattlePersonallyButton = CreateRoyalPromptButton(
                    btnRowGo.transform,
                    "LeadBattlePersonallyButton",
                    "Döyüşə Şəxsən Rəhbərlik Et",
                    new Color(0.76f, 0.56f, 0.12f, 1f),
                    new Color(0.98f, 0.84f, 0.28f, 1f),
                    new Color(0.08f, 0.08f, 0.10f, 1f),
                    mainFont);
            }

            if (automateBattleButton != null)
            {
                automateBattleButton.onClick.RemoveListener(OnAutomateBattleClicked);
                automateBattleButton.onClick.AddListener(OnAutomateBattleClicked);
            }

            if (leadBattlePersonallyButton != null)
            {
                leadBattlePersonallyButton.onClick.RemoveListener(OnLeadBattlePersonallyClicked);
                leadBattlePersonallyButton.onClick.AddListener(OnLeadBattlePersonallyClicked);
            }
        }

        private Button CreateRoyalPromptButton(
            Transform parent,
            string objName,
            string label,
            Color bgColor,
            Color borderColor,
            Color textColor,
            TMP_FontAsset font)
        {
            var go = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1.8f, -1.8f);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var tRt = txtGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(8f, 4f);
            tRt.offsetMax = new Vector2(-8f, -4f);

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Hökmdar döyüş meydanında düşmənlə qarşılaşdıqda seçimi (Avto-döyüş və ya 2D Şəxsi İdarə) göstərir.
        /// </summary>
        public void ShowRoyalBattlePrompt(CombatManager.ActiveBattleData battle)
        {
            if (battle == null) return;
            _activeRoyalBattle = battle;

            EnsureRoyalBattlePromptHierarchy();
            HideSiegeLogisticsPanel();

            if (royalBattlePromptPanel != null)
            {
                royalBattlePromptPanel.transform.SetAsLastSibling();
                royalBattlePromptPanel.SetActive(true);
            }

            string aName = battle.AttackerArmy != null ? battle.AttackerArmy.CommanderName : "Ordumuz";
            string aTroops = battle.AttackerArmy != null ? battle.AttackerArmy.FormattedTroopCount : "-";
            string dName = battle.DefenderArmy != null ? battle.DefenderArmy.CommanderName : "Düşmən";
            string dTroops = battle.DefenderArmy != null ? battle.DefenderArmy.FormattedTroopCount : "-";

            if (royalBattleBodyText != null)
            {
                royalBattleBodyText.text =
                    $"<b>Hökmdar, ordularımız düşmənlə qarşılaşdı!</b>\n\n" +
                    $"<color=#F1C40F>{battle.ProvinceName}</color> əyalətində böyük toqquşma:\n" +
                    $"<b>{aName}</b> ({aTroops})  <color=#E74C3C>VS</color>  <b>{dName}</b> ({dTroops})";
            }
        }

        public void HideRoyalBattlePrompt()
        {
            if (royalBattlePromptPanel != null)
            {
                royalBattlePromptPanel.SetActive(false);
            }
        }

        public void OnAutomateBattleClicked()
        {
            if (CombatManager.Instance != null && _activeRoyalBattle != null)
            {
                CombatManager.Instance.AutomateBattle(_activeRoyalBattle);
            }
            else
            {
                HideRoyalBattlePrompt();
            }
        }

        public void OnLeadBattlePersonallyClicked()
        {
            if (CombatManager.Instance != null && _activeRoyalBattle != null)
            {
                CombatManager.Instance.LeadBattlePersonally(_activeRoyalBattle);
            }
            else
            {
                HideRoyalBattlePrompt();
            }
        }

        #endregion
    }
}
