using System;
using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Database.Models;
using GrandStrategy.Military;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Diplomatiya dövləti haqqında SQLite bazasından oxunan aktiv oyun məlumatları modeli.
    /// </summary>
    [Serializable]
    public class DiplomaticStateData
    {
        public int FactionId;
        public string StateName;
        public string RulerName;
        public Color StateColor;
        public int RelationScore; // -100 ilə +100 arası
        public string Status; // "Düşmənçilik", "Gərgin", "Neytral", "Dostluq", "Yaxın Müttəfiq"
        public int MilitaryPower;
        public float Treasury;
        public string GovernmentType;
        public string PrimaryMotive = "Consolidation";
        public int DiplomaticReputation = 50;
        public string ActiveAgreementsSummary = string.Empty;
        public Sprite FlagSprite;

        public DiplomaticStateData(
            int id,
            string stateName,
            string rulerName,
            Color color,
            int relationScore,
            string status,
            int military,
            float treasury,
            string govType = "Monarxiya",
            Sprite flag = null)
        {
            FactionId = id;
            StateName = stateName;
            RulerName = rulerName;
            StateColor = color;
            RelationScore = relationScore;
            Status = status;
            MilitaryPower = military;
            Treasury = treasury;
            GovernmentType = govType;
            FlagSprite = flag;
        }
    }

    /// <summary>
    /// Faza 10.10: AAA Qlobal Diplomatiya Mərkəzi (DiplomacyLedgerPanel).
    /// Europa Universalis standartında "Master/Detail" arxitekturası:
    /// - Header və Bizim Dövlət Profili (Top Profile Bar)
    /// - 6 Əsas Tab Naviqasiyası (Dövlətlər, Münasibətlər, İttifaqlar, Müqavilələr, Ticarət, Müharibə və Sülh)
    /// - View 1: StatesListView (Bütün Dünya Dövlətləri cədvəli)
    /// - View 2: StateDetailView (İkitərəfli Danışıqlar Masası və Fəaliyyətlər)
    /// - View 3: WarAndPeaceView (Müharibə Xalı və Sülh Şərtləri)
    /// UIThemeManager ilə tam sinxron işləyir.
    /// </summary>
    [ExecuteAlways]
    public class DiplomacyUIManager : MonoBehaviour
    {
        private static DiplomacyUIManager _instance;
        public static DiplomacyUIManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<DiplomacyUIManager>();
                }
                return _instance;
            }
        }

        [Header("Əsas Panel və Konteynerlər")]
        [SerializeField] private GameObject diplomacyLedgerPanel;
        [SerializeField] private GameObject headerBar;
        [SerializeField] private Button closeButton;

        [Header("Top Profile Bar (Bizim Dövlət)")]
        [SerializeField] private GameObject topProfileBar;
        [SerializeField] private Image playerFactionIcon;
        [SerializeField] private TextMeshProUGUI playerFactionNameText;
        [SerializeField] private TextMeshProUGUI playerRulerNameText;
        [SerializeField] private TextMeshProUGUI treasuryText;
        [SerializeField] private TextMeshProUGUI diplomaticInfluenceText;

        [Header("Main Tab Navigation (6 Düymə)")]
        [SerializeField] private GameObject tabNavigationGroup;
        [SerializeField] private Button[] tabButtons = new Button[6];
        [SerializeField] private TextMeshProUGUI[] tabButtonTexts = new TextMeshProUGUI[6];

        [Header("View 1: StatesListView")]
        [SerializeField] private GameObject statesListView;
        [SerializeField] private ScrollRect statesScrollRect;
        [SerializeField] private RectTransform statesContent;

        [Header("View 2: StateDetailView (İkitərəfli Danışıqlar Masası)")]
        [SerializeField] private GameObject stateDetailView;
        [SerializeField] private Button backToListButton;
        [SerializeField] private Image detailTargetIcon;
        [SerializeField] private TextMeshProUGUI detailTargetNameText;
        [SerializeField] private TextMeshProUGUI detailTargetRulerText;
        [SerializeField] private Image detailOurIcon;
        [SerializeField] private TextMeshProUGUI detailOurNameText;
        [SerializeField] private TextMeshProUGUI detailOurRulerText;
        [SerializeField] private TextMeshProUGUI detailRelationScoreText;
        [SerializeField] private TextMeshProUGUI detailRelationStatusText;
        [SerializeField] private Slider detailRelationSlider;

        [Header("Detail Diplomatik Fəaliyyət Düymələri")]
        [SerializeField] private Button btnDefensiveAlliance;
        [SerializeField] private Button btnMilitaryAlliance;
        [SerializeField] private Button btnEconomicAlliance;
        [SerializeField] private Button btnTradeTreaty;
        [SerializeField] private Button btnNonAggression;
        [SerializeField] private Button btnMilitaryAccess;
        [SerializeField] private Button btnDemandTribute;
        [SerializeField] private Button btnOfferVassalage;
        [SerializeField] private Button btnDeclareWar;
        [SerializeField] private TextMeshProUGUI actionFeedbackText;

        [Header("View 3: WarAndPeaceView")]
        [SerializeField] private GameObject warAndPeaceView;
        [SerializeField] private GameObject warNegotiationContent;
        [SerializeField] private TextMeshProUGUI noActiveWarsText;
        [SerializeField] private TextMeshProUGUI warScoreText;
        [SerializeField] private TextMeshProUGUI warTargetStateText;
        [SerializeField] private Toggle toggleTerritory;
        [SerializeField] private Toggle toggleReparations;
        [SerializeField] private Toggle toggleVassalage;
        [SerializeField] private Toggle toggleWithdrawal;
        [SerializeField] private Button btnProposePeace;
        [SerializeField] private Button btnSurrender;
        [SerializeField] private TextMeshProUGUI warFeedbackText;

        private List<DiplomaticStateData> _statesDataList = new List<DiplomaticStateData>();
        private DiplomaticStateData _selectedState;
        private int _currentTabIndex = 0;

        // Xarici çıxışlar (Getters)
        public GameObject DiplomacyLedgerPanel => diplomacyLedgerPanel;
        public GameObject StatesListView => statesListView;
        public GameObject StateDetailView => stateDetailView;
        public GameObject WarAndPeaceView => warAndPeaceView;
        public GameObject WarNegotiationContent => warNegotiationContent;
        public TextMeshProUGUI NoActiveWarsText => noActiveWarsText;
        public Button CloseButton => closeButton;
        public Button BackToListButton => backToListButton;
        public Button[] TabButtons => tabButtons;
        public RectTransform StatesContent => statesContent;
        public DiplomaticStateData SelectedState => _selectedState;
        public int CurrentTabIndex => _currentTabIndex;
        public bool IsOpen => diplomacyLedgerPanel != null && diplomacyLedgerPanel.activeSelf;
        public List<DiplomaticStateData> StatesDataList => _statesDataList;

        public event Action<DiplomaticStateData> OnStateSelected;
        public event Action<string, DiplomaticStateData> OnDiplomaticActionExecuted;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this && Application.isPlaying)
            {
                Destroy(this);
                return;
            }

            LoadRealDataFromDatabase();
            EnsureHierarchy(transform.parent != null ? transform.parent : transform, forceRebuild: false);
            WireEventHandlers();
            UpdatePlayerProfileUI();
        }

        private void Start()
        {
            if (_statesDataList.Count == 0)
            {
                LoadRealDataFromDatabase();
            }
            BindCombatEvents();
            UpdatePlayerProfileUI();
            RefreshStatesListUI();
            ApplyTheme();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            BindCombatEvents();
        }

        private void OnDisable()
        {
            UnbindCombatEvents();
        }

        public void BindCombatEvents()
        {
            if (CombatManager.Instance == null) return;
            CombatManager.Instance.OnBattleStarted -= HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleStarted += HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleUpdated -= HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleUpdated += HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleEnded -= HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleEnded += HandleCombatMilitaryChanged;
        }

        private void UnbindCombatEvents()
        {
            if (CombatManager.Instance == null) return;
            CombatManager.Instance.OnBattleStarted -= HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleUpdated -= HandleCombatMilitaryChanged;
            CombatManager.Instance.OnBattleEnded -= HandleCombatMilitaryChanged;
        }

        private void HandleCombatMilitaryChanged(CombatManager.ActiveBattleData battle)
        {
            RefreshDynamicMilitaryPowerUI();
        }

        /// <summary>
        /// Xəritədəki orduların real vaxt əsgər sayını (ArmyManager.GetTotalTroopsForFaction) oxuyub
        /// Diplomatiya panelindəki Hərbi Güc sütununu və Müharibə hesabını anında yeniləyir.
        /// </summary>
        public void RefreshDynamicMilitaryPowerUI()
        {
            var armyMgr = ArmyManager.Instance;
            if (armyMgr == null) return;

            for (int i = 0; i < _statesDataList.Count; i++)
            {
                var st = _statesDataList[i];
                if (st == null) continue;
                st.MilitaryPower = armyMgr.GetTotalTroopsForFaction(st.FactionId);

                if (statesContent != null)
                {
                    Transform rowTr = statesContent.Find($"StateRow_{st.StateName}");
                    if (rowTr != null)
                    {
                        var milTmp = rowTr.Find("MilitaryText")?.GetComponent<TextMeshProUGUI>();
                        if (milTmp != null)
                        {
                            milTmp.text = $"⚔️ {st.MilitaryPower:N0}";
                        }
                    }
                }
            }

            if (warAndPeaceView != null && warAndPeaceView.activeSelf && IsOpen)
            {
                UpdateWarAndPeaceViewUI();
            }
        }

        /// <summary>
        /// Köhnə sınaq metodu: artıq birbaşa SQLite bazasından real məlumatları yükləyir.
        /// </summary>
        public void InitializeMockData()
        {
            LoadRealDataFromDatabase();
        }

        /// <summary>
        /// SQLite verilənlər bazasından bütün dövlətləri (Factions), münasibətləri (FactionRelations)
        /// və qüvvədə olan müqavilələri (DiplomaticAgreements) yükləyir.
        /// </summary>
        public void LoadRealDataFromDatabase()
        {
            var dipMgr = DiplomacyManager.EnsureInstance();
            int playerId = dipMgr.PlayerFactionId;
            int selectedFactionId = _selectedState != null ? _selectedState.FactionId : -1;

            _statesDataList.Clear();

            List<Faction> factions = dipMgr.GetAllFactions();
            for (int i = 0; i < factions.Count; i++)
            {
                Faction f = factions[i];
                if (f.ID == playerId) continue;

                FactionRelation rel = dipMgr.GetRelationRecord(playerId, f.ID);
                List<DiplomaticAgreement> agreements = dipMgr.GetAgreementsBetween(playerId, f.ID);

                string agreementSummary = string.Empty;
                if (agreements != null && agreements.Count > 0)
                {
                    List<string> types = new List<string>(agreements.Count);
                    for (int a = 0; a < agreements.Count; a++)
                    {
                        types.Add(agreements[a].Type);
                    }
                    agreementSummary = string.Join(", ", types);
                }

                if (!ColorUtility.TryParseHtmlString(f.ColorHex, out Color stateColor))
                {
                    stateColor = new Color(0.25f, 0.45f, 0.65f, 1f);
                }

                // Faza 10.13: Statik Faction.MilitaryPower əvəzinə xəritədəki aktiv orduların real cəmini oxuyuruq
                int dynamicMilitaryPower = ArmyManager.Instance != null
                    ? ArmyManager.Instance.GetTotalTroopsForFaction(f.ID)
                    : 0;

                var data = new DiplomaticStateData(
                    f.ID,
                    f.Name,
                    f.RulerName,
                    stateColor,
                    rel != null ? rel.RelationScore : 0,
                    rel != null ? rel.Status : "Neytral",
                    dynamicMilitaryPower,
                    f.Treasury,
                    f.GovernmentType
                )
                {
                    PrimaryMotive = f.PrimaryMotive,
                    DiplomaticReputation = f.DiplomaticReputation,
                    ActiveAgreementsSummary = agreementSummary
                };

                _statesDataList.Add(data);

                if (selectedFactionId == f.ID)
                {
                    _selectedState = data;
                }
            }

            UpdatePlayerProfileUI();
        }

        private void UpdatePlayerProfileUI()
        {
            if (DiplomacyManager.Instance == null) return;

            Faction player = DiplomacyManager.Instance.GetPlayerFaction();
            if (player == null) return;

            if (playerFactionNameText != null) playerFactionNameText.text = $"<b>{player.Name}</b>";
            if (playerRulerNameText != null) playerRulerNameText.text = $"Hökmdar: {player.RulerName} ({player.PrimaryMotive})";
            if (treasuryText != null) treasuryText.text = $"💰 Xəzinə: <b>{player.Treasury:N0}</b> Qızıl";
            if (diplomaticInfluenceText != null) diplomaticInfluenceText.text = $"⚖️ Nüfuz: <b>{player.DiplomaticReputation}</b> / 100";

            if (ColorUtility.TryParseHtmlString(player.ColorHex, out Color pColor))
            {
                if (playerFactionIcon != null) playerFactionIcon.color = pColor;
                if (detailOurIcon != null) detailOurIcon.color = pColor;
            }

            if (detailOurNameText != null) detailOurNameText.text = $"<b>{player.Name}</b>";
            if (detailOurRulerText != null) detailOurRulerText.text = $"👑 Hökmdar: {player.RulerName} (Nüfuz: {player.DiplomaticReputation})";
        }

        #region Pəncərə İdarəetməsi (Açma / Bağlama / Naviqasiya)

        public void OpenPanel()
        {
            BindCombatEvents();
            if (diplomacyLedgerPanel != null)
            {
                diplomacyLedgerPanel.SetActive(true);
            }
            SelectTab(_currentTabIndex);
            ApplyTheme();
        }

        public void ClosePanel()
        {
            if (diplomacyLedgerPanel != null)
            {
                diplomacyLedgerPanel.SetActive(false);
            }
        }

        public void TogglePanel()
        {
            if (IsOpen) ClosePanel();
            else OpenPanel();
        }

        /// <summary>
        /// Tab düyməsi klikləndikdə çağırılır və seçilmiş taba uyğun filtri/görünüşü açır.
        /// </summary>
        public void OnTabClicked(int tabIndex)
        {
            SelectTab(tabIndex);
        }

        /// <summary>
        /// 6 Əsas Tab menyusundan birini seçir (0: Dövlətlər, 1: Münasibətlər, 2: İttifaqlar, 3: Müqavilələr, 4: Ticarət, 5: Müharibə və Sülh)
        /// </summary>
        public void SelectTab(int tabIndex)
        {
            _currentTabIndex = Mathf.Clamp(tabIndex, 0, 5);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                var img = tabButtons[i].GetComponent<Image>();
                var outline = tabButtons[i].GetComponent<Outline>();
                bool isActive = (i == _currentTabIndex);

                if (img != null)
                {
                    img.color = isActive
                        ? new Color(0.22f, 0.27f, 0.35f, 1f) // Aktiv tab fonu
                        : new Color(0.10f, 0.12f, 0.16f, 0.85f); // Passiv tab fonu
                }
                if (outline != null)
                {
                    outline.enabled = isActive;
                }
            }

            if (_currentTabIndex == 5)
            {
                ShowWarAndPeace();
            }
            else
            {
                ShowStatesList();
            }
        }

        public void ShowStatesList()
        {
            if (statesListView != null) statesListView.SetActive(true);
            if (stateDetailView != null) stateDetailView.SetActive(false);
            if (warAndPeaceView != null) warAndPeaceView.SetActive(false);

            RefreshStatesListUI();
        }

        public void ShowStateDetail(DiplomaticStateData state)
        {
            if (state == null && _statesDataList.Count > 0) state = _statesDataList[0];
            _selectedState = state;

            if (statesListView != null) statesListView.SetActive(false);
            if (warAndPeaceView != null) warAndPeaceView.SetActive(false);
            if (stateDetailView != null) stateDetailView.SetActive(true);

            UpdateDetailViewUI();
            OnStateSelected?.Invoke(_selectedState);
        }

        public void BackToList()
        {
            ShowStatesList();
        }

        public void ShowWarAndPeace()
        {
            if (statesListView != null) statesListView.SetActive(false);
            if (stateDetailView != null) stateDetailView.SetActive(false);
            if (warAndPeaceView != null) warAndPeaceView.SetActive(true);

            UpdateWarAndPeaceViewUI();
        }

        #endregion

        #region UI Doldurulması və Məlumatların Yenilənməsi

        /// <summary>
        /// Aktiv tab indeksinə görə StatesListView siyahısını filtrləyir və ya sıralayır.
        /// </summary>
        public List<DiplomaticStateData> GetFilteredStatesForTab(int tabIndex)
        {
            var result = new List<DiplomaticStateData>();
            if (_statesDataList == null || _statesDataList.Count == 0)
            {
                return result;
            }

            switch (tabIndex)
            {
                case 0: // Dövlətlər: Bütün siyahını göstərir
                    result.AddRange(_statesDataList);
                    break;

                case 1: // Münasibətlər: Münasibət xalına görə yuxarıdan aşağıya sıralayır
                    result.AddRange(_statesDataList);
                    result.Sort((a, b) => b.RelationScore.CompareTo(a.RelationScore));
                    break;

                case 2: // İttifaqlar: Yalnız Yaxın Müttəfiq və ya Dostluq olan dövlətləri göstərir
                    for (int i = 0; i < _statesDataList.Count; i++)
                    {
                        var s = _statesDataList[i];
                        if (string.Equals(s.Status, "Yaxın Müttəfiq", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.Status, "Dostluq", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(s);
                        }
                    }
                    break;

                case 3: // Müqavilələr: Aktiv müqaviləsi olan dövlətləri göstərir
                    for (int i = 0; i < _statesDataList.Count; i++)
                    {
                        var s = _statesDataList[i];
                        if (!string.IsNullOrEmpty(s.ActiveAgreementsSummary))
                        {
                            result.Add(s);
                        }
                    }
                    break;

                case 4: // Ticarət: Yalnız aramızda Ticarət müqaviləsi olan dövlətləri göstərir
                    for (int i = 0; i < _statesDataList.Count; i++)
                    {
                        var s = _statesDataList[i];
                        if (!string.IsNullOrEmpty(s.ActiveAgreementsSummary) &&
                            s.ActiveAgreementsSummary.IndexOf("Ticarət", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Add(s);
                        }
                    }
                    break;

                default:
                    result.AddRange(_statesDataList);
                    break;
            }

            return result;
        }

        /// <summary>
        /// StatesListView daxilindəki sətirləri aktiv tabın filtrinə uyğun real məlumatlarla yeniləyir.
        /// </summary>
        public void RefreshStatesListUI()
        {
            if (statesContent == null) return;

            // Köhnə sətirləri təmizləyirik
            for (int i = statesContent.childCount - 1; i >= 0; i--)
            {
                var child = statesContent.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            LoadRealDataFromDatabase();

            var filteredStates = GetFilteredStatesForTab(_currentTabIndex);
            if (filteredStates.Count == 0)
            {
                CreateEmptyListRow();
                return;
            }

            foreach (var state in filteredStates)
            {
                CreateStateRow(state);
            }
        }

        private void CreateEmptyListRow()
        {
            var rowGo = new GameObject("EmptyStateRow", typeof(RectTransform), typeof(TextMeshProUGUI));
            rowGo.transform.SetParent(statesContent, false);
            rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 80f);

            var tmp = rowGo.GetComponent<TextMeshProUGUI>();
            tmp.font = UIThemeManager.Instance != null ? UIThemeManager.Instance.GetMainFont() : TMP_Settings.defaultFontAsset;
            tmp.text = "Bu kateqoriyaya uyğun dövlət və ya aktiv müqavilə tapılmadı.";
            tmp.fontSize = 15f;
            tmp.color = new Color(0.75f, 0.78f, 0.82f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private void CreateStateRow(DiplomaticStateData state)
        {
            var rowGo = new GameObject($"StateRow_{state.StateName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            rowGo.transform.SetParent(statesContent, false);

            var rt = rowGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 52f);

            var img = rowGo.GetComponent<Image>();
            img.color = new Color(0.09f, 0.11f, 0.15f, 0.90f);

            var outline = rowGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.40f);
            outline.effectDistance = new Vector2(1f, -1f);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            var defaultFont = UIThemeManager.Instance != null ? UIThemeManager.Instance.GetMainFont() : TMP_Settings.defaultFontAsset;

            // 1. Bayraq / Rəng İkonu (36x36)
            var flagGo = new GameObject("FlagIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            flagGo.transform.SetParent(rowGo.transform, false);
            var flagRt = flagGo.GetComponent<RectTransform>();
            flagRt.sizeDelta = new Vector2(36f, 36f);
            var flagImg = flagGo.GetComponent<Image>();
            flagImg.color = state.StateColor;
            var flagOutline = flagGo.GetComponent<Outline>();
            flagOutline.effectColor = Color.white;
            flagOutline.effectDistance = new Vector2(1f, -1f);

            // 2. Dövlət Adı (Width: 260)
            var nameGo = new GameObject("StateName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(rowGo.transform, false);
            nameGo.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 40f);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.font = defaultFont;
            nameTmp.text = $"<b>{state.StateName}</b>\n<size=11><color=#BDC3C7>{state.RulerName} • {state.PrimaryMotive}</color></size>";
            nameTmp.fontSize = 14f;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 3. Münasibət Xalı (Width: 140)
            var relGo = new GameObject("RelationScore", typeof(RectTransform), typeof(TextMeshProUGUI));
            relGo.transform.SetParent(rowGo.transform, false);
            relGo.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 40f);
            var relTmp = relGo.GetComponent<TextMeshProUGUI>();
            relTmp.font = defaultFont;
            string relSign = state.RelationScore >= 0 ? $"+{state.RelationScore}" : state.RelationScore.ToString();
            string relColor = state.RelationScore >= 50 ? "#2ECC71" : (state.RelationScore >= 0 ? "#F1C40F" : "#E74C3C");
            relTmp.text = $"<color={relColor}><b>{relSign}</b></color> <size=11>(Fikr)</size>";
            relTmp.fontSize = 14f;
            relTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 4. Status (Width: 150)
            var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(rowGo.transform, false);
            statusGo.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 40f);
            var statusTmp = statusGo.GetComponent<TextMeshProUGUI>();
            statusTmp.font = defaultFont;
            statusTmp.text = string.IsNullOrEmpty(state.ActiveAgreementsSummary)
                ? $"🛡️ {state.Status}"
                : $"🛡️ {state.Status}\n<size=10><color=#85C1E9>{state.ActiveAgreementsSummary}</color></size>";
            statusTmp.fontSize = 13f;
            statusTmp.color = new Color(0.90f, 0.92f, 0.95f, 1f);
            statusTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 5. Hərbi Güc (Width: 150)
            var milGo = new GameObject("MilitaryText", typeof(RectTransform), typeof(TextMeshProUGUI));
            milGo.transform.SetParent(rowGo.transform, false);
            milGo.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 40f);
            var milTmp = milGo.GetComponent<TextMeshProUGUI>();
            milTmp.font = defaultFont;
            milTmp.text = $"⚔️ {state.MilitaryPower:N0}";
            milTmp.fontSize = 13.5f;
            milTmp.color = new Color(0.95f, 0.76f, 0.20f, 1f);
            milTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 6. İqtisadiyyat (Width: 140)
            var ecoGo = new GameObject("EconomyText", typeof(RectTransform), typeof(TextMeshProUGUI));
            ecoGo.transform.SetParent(rowGo.transform, false);
            ecoGo.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 40f);
            var ecoTmp = ecoGo.GetComponent<TextMeshProUGUI>();
            ecoTmp.font = defaultFont;
            ecoTmp.text = $"💰 {state.Treasury:N0}";
            ecoTmp.fontSize = 13.5f;
            ecoTmp.color = new Color(0.95f, 0.85f, 0.35f, 1f);
            ecoTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 7. Danışıqlar Masası Düyməsi (Detail Keçidi)
            var actionBtnGo = new GameObject("OpenDetailButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            actionBtnGo.transform.SetParent(rowGo.transform, false);
            var abRt = actionBtnGo.GetComponent<RectTransform>();
            abRt.sizeDelta = new Vector2(110f, 32f);

            var abImg = actionBtnGo.GetComponent<Image>();
            abImg.color = new Color(0.15f, 0.20f, 0.28f, 1f);

            var abOutline = actionBtnGo.GetComponent<Outline>();
            abOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.8f);

            var abTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            abTextGo.transform.SetParent(actionBtnGo.transform, false);
            var abtRt = abTextGo.GetComponent<RectTransform>();
            abtRt.anchorMin = Vector2.zero; abtRt.anchorMax = Vector2.one;
            abtRt.sizeDelta = Vector2.zero;

            var abTmp = abTextGo.GetComponent<TextMeshProUGUI>();
            abTmp.font = defaultFont;
            abTmp.text = "Masa ➔";
            abTmp.fontSize = 12f;
            abTmp.fontStyle = FontStyles.Bold;
            abTmp.color = Color.white;
            abTmp.alignment = TextAlignmentOptions.Center;

            // Həm sətir, həm düymə tıklandıqda StateDetailView açılır
            var btn = rowGo.GetComponent<Button>();
            btn.onClick.AddListener(() => ShowStateDetail(state));
            var abBtn = actionBtnGo.GetComponent<Button>();
            abBtn.onClick.AddListener(() => ShowStateDetail(state));
        }

        private void UpdateDetailViewUI(bool resetFeedback = true)
        {
            if (_selectedState == null) return;

            UpdatePlayerProfileUI();

            if (detailTargetNameText != null)
            {
                detailTargetNameText.text = $"<b>{_selectedState.StateName}</b>";
            }
            if (detailTargetRulerText != null)
            {
                string agreementsInfo = string.IsNullOrEmpty(_selectedState.ActiveAgreementsSummary)
                    ? "Aktiv müqavilə yoxdur"
                    : $"Müqavilələr: {_selectedState.ActiveAgreementsSummary}";
                detailTargetRulerText.text = $"👑 Hökmdar: {_selectedState.RulerName} ({_selectedState.GovernmentType})\n<size=12><color=#85C1E9>Maraq: {_selectedState.PrimaryMotive} | {agreementsInfo}</color></size>";
            }
            if (detailTargetIcon != null)
            {
                detailTargetIcon.color = _selectedState.StateColor;
            }

            if (detailRelationScoreText != null)
            {
                string sign = _selectedState.RelationScore >= 0 ? $"+{_selectedState.RelationScore}" : _selectedState.RelationScore.ToString();
                detailRelationScoreText.text = $"Münasibət Şkalası: <b>{sign} / 100</b>";
            }
            if (detailRelationStatusText != null)
            {
                detailRelationStatusText.text = $"Vəziyyət: <color=#F1C40F>{_selectedState.Status}</color>";
            }
            if (detailRelationSlider != null)
            {
                // -100..+100 aralığını 0..1 aralığına çeviririk
                detailRelationSlider.value = (_selectedState.RelationScore + 100f) / 200f;
            }

            if (resetFeedback && actionFeedbackText != null)
            {
                actionFeedbackText.text = $"Seçilmiş dövlət: <b>{_selectedState.StateName}</b> ({_selectedState.PrimaryMotive}). Diplomatik fəaliyyət üçün qərar verin.";
            }
        }

        private void UpdateWarAndPeaceViewUI()
        {
            LoadRealDataFromDatabase();
            EnsureWarAndPeaceDynamicContainers();
            EnsurePeaceToggleLayoutFixed();

            var dipMgr = DiplomacyManager.EnsureInstance();
            var activeWars = dipMgr.GetActiveWarFactions();

            DiplomaticStateData warState = null;
            if (_selectedState != null && string.Equals(_selectedState.Status, "Düşmənçilik", StringComparison.OrdinalIgnoreCase))
            {
                warState = _selectedState;
            }
            else if (activeWars.Count > 0)
            {
                int firstEnemyId = activeWars[0].ID;
                for (int i = 0; i < _statesDataList.Count; i++)
                {
                    if (_statesDataList[i].FactionId == firstEnemyId)
                    {
                        warState = _statesDataList[i];
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _statesDataList.Count; i++)
                {
                    if (string.Equals(_statesDataList[i].Status, "Düşmənçilik", StringComparison.OrdinalIgnoreCase))
                    {
                        warState = _statesDataList[i];
                        break;
                    }
                }
            }

            if (warState == null)
            {
                if (warNegotiationContent != null) warNegotiationContent.SetActive(false);
                if (noActiveWarsText != null)
                {
                    noActiveWarsText.gameObject.SetActive(true);
                    noActiveWarsText.text = "Hazırda heç bir dövlətlə müharibə vəziyyətində deyilsiniz.";
                }
                return;
            }

            if (noActiveWarsText != null) noActiveWarsText.gameObject.SetActive(false);
            if (warNegotiationContent != null) warNegotiationContent.SetActive(true);

            if (warTargetStateText != null)
            {
                string multiWarNote = activeWars.Count > 1 ? $"  <size=13><color=#F1C40F>[{activeWars.Count} Aktiv Cəbhə]</color></size>" : string.Empty;
                warTargetStateText.text = $"Müharibə Hədəfi: <b>{warState.StateName}</b> ({warState.RulerName}){multiWarNote}";
            }

            if (warScoreText != null)
            {
                var armyMgr = ArmyManager.Instance;
                int ourPower = armyMgr != null ? armyMgr.GetTotalTroopsForFaction(dipMgr.PlayerFactionId) : 0;
                int enemyPower = armyMgr != null ? armyMgr.GetTotalTroopsForFaction(warState.FactionId) : warState.MilitaryPower;
                warState.MilitaryPower = enemyPower;

                int denom = Mathf.Max(1, Mathf.Max(ourPower, enemyPower));
                int balancePct = Mathf.Clamp(Mathf.RoundToInt(((ourPower - enemyPower) / (float)denom) * 50f), -45, 45);
                string balanceColor = balancePct >= 0 ? "#2ECC71" : "#E74C3C";
                string balanceLabel = balancePct > 5 ? "HƏRBİ ÜSTÜNLÜK" : (balancePct < -5 ? "ÇƏTİN CƏBHƏ" : "TARAZLI CƏBHƏ");
                string signedPct = balancePct >= 0 ? $"+{balancePct}%" : $"{balancePct}%";
                warScoreText.text = $"Müharibə Vəziyyəti: <color={balanceColor}><b>{signedPct} {balanceLabel}</b></color>  <size=14>(Ordu: {ourPower:N0} vs {enemyPower:N0})</size>";
            }

            if (warFeedbackText != null)
            {
                warFeedbackText.text = $"Aktiv müharibə: <b>{warState.StateName}</b> (Münasibət: {warState.RelationScore}). Sülh şərtlərini seçin və tələbinizi qarşı tərəfə göndərin.";
            }
        }

        #endregion

        #region Diplomatik Fəaliyyətlər (Action Handlers)

        private void WireEventHandlers()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (backToListButton != null)
            {
                backToListButton.onClick.RemoveAllListeners();
                backToListButton.onClick.AddListener(BackToList);
            }

            // Tab düymələri
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                if (tabButtons[i] != null)
                {
                    tabButtons[i].onClick.RemoveAllListeners();
                    tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
                }
            }

            // Diplomatik Fəaliyyət Düymələri
            WireActionBtn(btnDefensiveAlliance, "Müdafiə İttifaqı");
            WireActionBtn(btnMilitaryAlliance, "Hərbi İttifaq");
            WireActionBtn(btnEconomicAlliance, "İqtisadi İttifaq");
            WireActionBtn(btnTradeTreaty, "Ticarət Müqaviləsi");
            WireActionBtn(btnNonAggression, "Hücum Etməmək Paktı");
            WireActionBtn(btnMilitaryAccess, "Hərbi Keçid");
            WireActionBtn(btnDemandTribute, "Xərac Tələbi");
            WireActionBtn(btnOfferVassalage, "Vassallıq Təklifi");

            if (btnDeclareWar != null)
            {
                btnDeclareWar.onClick.RemoveAllListeners();
                btnDeclareWar.onClick.AddListener(OnDeclareWarClicked);
            }

            if (btnProposePeace != null)
            {
                btnProposePeace.onClick.RemoveAllListeners();
                btnProposePeace.onClick.AddListener(OnPeaceProposeClicked);
            }

            if (btnSurrender != null)
            {
                btnSurrender.onClick.RemoveAllListeners();
                btnSurrender.onClick.AddListener(OnSurrenderClicked);
            }
        }

        private void WireActionBtn(Button btn, string actionName)
        {
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => ExecuteDiplomaticAction(actionName));
        }

        public void ExecuteDiplomaticAction(string actionName)
        {
            if (_selectedState == null) return;

            var dipMgr = DiplomacyManager.EnsureInstance();
            AgreementType agreementType = DiplomacyModelExtensions.ParseAgreementType(actionName);

            dipMgr.EvaluateProposal(dipMgr.PlayerFactionId, _selectedState.FactionId, agreementType, out string feedbackMsg);

            LoadRealDataFromDatabase();

            if (actionFeedbackText != null)
            {
                actionFeedbackText.text = feedbackMsg;
            }

            UpdateDetailViewUI(resetFeedback: false);
            OnDiplomaticActionExecuted?.Invoke(actionName, _selectedState);
        }

        public void OnDeclareWarClicked()
        {
            if (_selectedState == null) return;

            string targetName = _selectedState.StateName;
            var dipMgr = DiplomacyManager.EnsureInstance();
            dipMgr.DeclareWar(dipMgr.PlayerFactionId, _selectedState.FactionId);

            LoadRealDataFromDatabase();

            string warMsg = $"<color=red><b>⚔️ DİQQƏT: {targetName} dövlətinə rəsmi Müharibə Elan Edildi! (Münasibət: -100, Müqavilələr ləğv edildi)</b></color>";
            if (actionFeedbackText != null)
            {
                actionFeedbackText.text = warMsg;
            }

            UpdateDetailViewUI(resetFeedback: false);
            OnDiplomaticActionExecuted?.Invoke("Müharibə Elanı", _selectedState);
        }

        public void OnPeaceProposeClicked()
        {
            string terms = string.Empty;
            if (toggleTerritory != null && toggleTerritory.isOn) terms += "Ərazi Təhvili, ";
            if (toggleReparations != null && toggleReparations.isOn) terms += "Təzminat (Xərac), ";
            if (toggleVassalage != null && toggleVassalage.isOn) terms += "Vassallıq, ";
            if (toggleWithdrawal != null && toggleWithdrawal.isOn) terms += "Tərksilah, ";

            if (string.IsNullOrEmpty(terms)) terms = "Ağ Sülh (Status-kvo)";
            else terms = terms.TrimEnd(' ', ',');

            string pMsg = $"<color=#2ECC71>✓ Sülh Təklifi Göndərildi:</color> Şərtlər: {terms}.";
            if (warFeedbackText != null)
            {
                warFeedbackText.text = pMsg;
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddEventLogMessage($"• <color=#2ECC71>Sülh Təklifi:</color> {terms} şərtləri ilə qarşı tərəfə elçi göndərildi.");
            }
        }

        public void OnSurrenderClicked()
        {
            if (warFeedbackText != null)
            {
                warFeedbackText.text = "<color=red>Şərtsiz Təslimiyyət təklif edildi. Nüfuz itkisi: -50.</color>";
            }
        }

        #endregion

        #region UIThemeManager İnteqrasiyası

        /// <summary>
        /// Aktiv temanın rənglərini, spritelarını və şriftlərini diplomatiya pəncərəsinə tətbiq edir.
        /// </summary>
        public void ApplyTheme(UIThemeProfile theme = null)
        {
            if (theme == null && UIThemeManager.Instance != null)
            {
                theme = UIThemeManager.Instance.CurrentTheme;
            }

            if (theme == null || diplomacyLedgerPanel == null) return;

            // 1. Əsas Panel Arxa Fonu və Kənar Xətti
            var panelImg = diplomacyLedgerPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = theme.panelBackgroundColor;
                if (theme.panelBackground != null)
                {
                    panelImg.sprite = theme.panelBackground;
                    panelImg.type = Image.Type.Sliced;
                }
            }

            var panelOutline = diplomacyLedgerPanel.GetComponent<Outline>();
            if (panelOutline != null)
            {
                panelOutline.effectColor = theme.primaryBorderColor;
            }

            // 2. Başlıq Zolağı (HeaderBar)
            if (headerBar != null)
            {
                var hImg = headerBar.GetComponent<Image>();
                if (hImg != null) hImg.color = theme.secondaryHeaderColor;

                var hTitle = headerBar.GetComponentInChildren<TextMeshProUGUI>();
                if (hTitle != null)
                {
                    if (theme.titleFont != null) hTitle.font = theme.titleFont;
                    hTitle.color = theme.goldTextColor;
                }

                var divider = headerBar.transform.Find("DividerLine");
                if (divider != null)
                {
                    var dImg = divider.GetComponent<Image>();
                    if (dImg != null) dImg.color = theme.primaryBorderColor;
                }
            }

            // 3. Tab Düymələri
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                var bOutline = tabButtons[i].GetComponent<Outline>();
                if (bOutline != null) bOutline.effectColor = theme.primaryBorderColor;

                if (tabButtonTexts[i] != null && theme.mainFont != null)
                {
                    tabButtonTexts[i].font = theme.mainFont;
                }
            }

            // 4. Əsas Şriftlərin tətbiqi
            if (theme.mainFont != null)
            {
                var allTmps = diplomacyLedgerPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in allTmps)
                {
                    if (tmp != null && tmp.font == null)
                    {
                        tmp.font = theme.mainFont;
                    }
                }
            }
        }

        #endregion

        #region Avtomatik İyerarxiya Yaradıcısı (EnsureHierarchy)

        /// <summary>
        /// Səhnədə DiplomacyLedgerPanel yoxdursa və ya forceRebuild tələb edilirsə,
        /// bütün UI strukturu avtomatik, piksel-dəqiq şəkildə formalaşdırır.
        /// </summary>
        public void EnsureHierarchy(Transform parentCanvas, bool forceRebuild = false)
        {
            if (diplomacyLedgerPanel == null)
            {
                var found = parentCanvas.Find("DiplomacyLedgerPanel");
                if (found != null) diplomacyLedgerPanel = found.gameObject;
            }

            if (forceRebuild && diplomacyLedgerPanel != null)
            {
                if (Application.isPlaying) Destroy(diplomacyLedgerPanel);
                else DestroyImmediate(diplomacyLedgerPanel);
                diplomacyLedgerPanel = null;
            }

            if (diplomacyLedgerPanel == null)
            {
                diplomacyLedgerPanel = new GameObject("DiplomacyLedgerPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
                diplomacyLedgerPanel.transform.SetParent(parentCanvas, false);
            }

            // 1. ƏSAS PƏNCƏRƏ (Width: 1200, Height: 750, Mərkəzləşdirilmiş)
            var pRt = diplomacyLedgerPanel.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = Vector2.zero;
            pRt.sizeDelta = new Vector2(1200f, 750f);

            var pImg = diplomacyLedgerPanel.GetComponent<Image>();
            pImg.color = new Color(0.067f, 0.067f, 0.067f, 0.94f); // #111111 ~94% Alpha

            var pOutline = diplomacyLedgerPanel.GetComponent<Outline>();
            pOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059 Qızılı
            pOutline.effectDistance = new Vector2(2f, -2f);

            var defaultFont = UIThemeManager.Instance != null ? UIThemeManager.Instance.GetMainFont() : TMP_Settings.defaultFontAsset;
            var titleFont = UIThemeManager.Instance != null ? UIThemeManager.Instance.GetTitleFont() : defaultFont;

            // ==========================================
            // 2. HEADER BAR (Y: [710, 750], Height: 40)
            // ==========================================
            var hbTr = diplomacyLedgerPanel.transform.Find("HeaderBar");
            GameObject hbGo = hbTr != null ? hbTr.gameObject : new GameObject("HeaderBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (hbTr == null) hbGo.transform.SetParent(diplomacyLedgerPanel.transform, false);
            headerBar = hbGo;

            var hbRt = hbGo.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0f, 1f);
            hbRt.anchorMax = new Vector2(1f, 1f);
            hbRt.pivot = new Vector2(0.5f, 1f);
            hbRt.anchoredPosition = Vector2.zero;
            hbRt.sizeDelta = new Vector2(0f, 40f);

            var hbImg = hbGo.GetComponent<Image>();
            hbImg.color = new Color(0.11f, 0.13f, 0.17f, 0.98f);

            // Başlıq mətni: "📜 QÜDRƏT VƏ DİPLOMATİYA"
            var htTr = hbGo.transform.Find("TitleText");
            GameObject htGo = htTr != null ? htTr.gameObject : new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (htTr == null) htGo.transform.SetParent(hbGo.transform, false);
            var htRt = htGo.GetComponent<RectTransform>();
            htRt.anchorMin = new Vector2(0f, 0f);
            htRt.anchorMax = new Vector2(1f, 1f);
            htRt.offsetMin = new Vector2(20f, 0f);
            htRt.offsetMax = new Vector2(-60f, 0f);

            var htTmp = htGo.GetComponent<TextMeshProUGUI>();
            htTmp.font = titleFont;
            htTmp.text = "📜  QÜDRƏT VƏ DİPLOMATİYA (DIPLOMACY LEDGER)";
            htTmp.fontSize = 16f;
            htTmp.fontStyle = FontStyles.Bold;
            htTmp.color = new Color(0.957f, 0.816f, 0.247f, 1f); // #F4D03F Qızılı
            htTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Bağla (✕) Düyməsi
            var cbTr = hbGo.transform.Find("CloseButton");
            GameObject cbGo = cbTr != null ? cbTr.gameObject : new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            if (cbTr == null) cbGo.transform.SetParent(hbGo.transform, false);
            closeButton = cbGo.GetComponent<Button>();

            var cbRt = cbGo.GetComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(1f, 0.5f);
            cbRt.anchorMax = new Vector2(1f, 0.5f);
            cbRt.pivot = new Vector2(1f, 0.5f);
            cbRt.anchoredPosition = new Vector2(-10f, 0f);
            cbRt.sizeDelta = new Vector2(28f, 28f);

            var cbImg = cbGo.GetComponent<Image>();
            cbImg.color = new Color(0.60f, 0.15f, 0.12f, 0.95f);

            var cbTxtTr = cbGo.transform.Find("Text");
            GameObject cbTxtGo = cbTxtTr != null ? cbTxtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (cbTxtTr == null) cbTxtGo.transform.SetParent(cbGo.transform, false);
            var cbtRt = cbTxtGo.GetComponent<RectTransform>();
            cbtRt.anchorMin = Vector2.zero; cbtRt.anchorMax = Vector2.one; cbtRt.sizeDelta = Vector2.zero;
            var cbTmp = cbTxtGo.GetComponent<TextMeshProUGUI>();
            cbTmp.font = defaultFont;
            cbTmp.text = "✕";
            cbTmp.fontSize = 16f;
            cbTmp.fontStyle = FontStyles.Bold;
            cbTmp.color = Color.white;
            cbTmp.alignment = TextAlignmentOptions.Center;

            // Header alt qızılı xətti
            var hLineTr = hbGo.transform.Find("DividerLine");
            GameObject hLineGo = hLineTr != null ? hLineTr.gameObject : new GameObject("DividerLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (hLineTr == null) hLineGo.transform.SetParent(hbGo.transform, false);
            var hlRt = hLineGo.GetComponent<RectTransform>();
            hlRt.anchorMin = new Vector2(0f, 0f);
            hlRt.anchorMax = new Vector2(1f, 0f);
            hlRt.pivot = new Vector2(0.5f, 0f);
            hlRt.sizeDelta = new Vector2(0f, 2f);
            var hlImg = hLineGo.GetComponent<Image>();
            hlImg.color = new Color(0.773f, 0.627f, 0.349f, 1f);

            // ==========================================
            // 3. TOP PROFILE BAR (Height: 90)
            // ==========================================
            var pbTr = diplomacyLedgerPanel.transform.Find("TopProfileBar");
            GameObject pbGo = pbTr != null ? pbTr.gameObject : new GameObject("TopProfileBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (pbTr == null) pbGo.transform.SetParent(diplomacyLedgerPanel.transform, false);
            topProfileBar = pbGo;

            var pbRt = pbGo.GetComponent<RectTransform>();
            pbRt.anchorMin = new Vector2(0f, 1f);
            pbRt.anchorMax = new Vector2(1f, 1f);
            pbRt.pivot = new Vector2(0.5f, 1f);
            pbRt.anchoredPosition = new Vector2(0f, -42f);
            pbRt.sizeDelta = new Vector2(-24f, 90f);

            var pbImg = pbGo.GetComponent<Image>();
            pbImg.color = new Color(0.09f, 0.11f, 0.15f, 0.95f);

            var pbOutline = pbGo.GetComponent<Outline>();
            pbOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.35f);

            // Sol Tərəf: FactionIcon (70x70) + FactionName & Ruler
            var iconTr = pbGo.transform.Find("FactionIcon");
            GameObject iconGo = iconTr != null ? iconTr.gameObject : new GameObject("FactionIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (iconTr == null) iconGo.transform.SetParent(pbGo.transform, false);
            var iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(14f, 0f);
            iRt.sizeDelta = new Vector2(70f, 70f);
            playerFactionIcon = iconGo.GetComponent<Image>();
            playerFactionIcon.color = new Color(0.122f, 0.380f, 0.553f, 1f); // #1F618D
            var iOutline = iconGo.GetComponent<Outline>();
            iOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f);

            var nameContainerTr = pbGo.transform.Find("NameContainer");
            GameObject ncGo = nameContainerTr != null ? nameContainerTr.gameObject : new GameObject("NameContainer", typeof(RectTransform));
            if (nameContainerTr == null) ncGo.transform.SetParent(pbGo.transform, false);
            var ncRt = ncGo.GetComponent<RectTransform>();
            ncRt.anchorMin = new Vector2(0f, 0.5f);
            ncRt.anchorMax = new Vector2(0f, 0.5f);
            ncRt.pivot = new Vector2(0f, 0.5f);
            ncRt.anchoredPosition = new Vector2(96f, 0f);
            ncRt.sizeDelta = new Vector2(350f, 60f);

            var pfnTr = ncGo.transform.Find("FactionName");
            GameObject pfnGo = pfnTr != null ? pfnTr.gameObject : new GameObject("FactionName", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (pfnTr == null) pfnGo.transform.SetParent(ncGo.transform, false);
            playerFactionNameText = pfnGo.GetComponent<TextMeshProUGUI>();
            playerFactionNameText.font = titleFont;
            playerFactionNameText.text = "Eldəgəzlər (Atabəylər)";
            playerFactionNameText.fontSize = 20f;
            playerFactionNameText.fontStyle = FontStyles.Bold;
            playerFactionNameText.color = Color.white;
            var pfnRt = pfnGo.GetComponent<RectTransform>();
            pfnRt.anchorMin = new Vector2(0f, 0.5f); pfnRt.anchorMax = new Vector2(1f, 1f);
            pfnRt.offsetMin = Vector2.zero; pfnRt.offsetMax = Vector2.zero;

            var prnTr = ncGo.transform.Find("RulerName");
            GameObject prnGo = prnTr != null ? prnTr.gameObject : new GameObject("RulerName", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (prnTr == null) prnGo.transform.SetParent(ncGo.transform, false);
            playerRulerNameText = prnGo.GetComponent<TextMeshProUGUI>();
            playerRulerNameText.font = defaultFont;
            playerRulerNameText.text = "👑 Qızıl Arslan (Böyük Atabəy)";
            playerRulerNameText.fontSize = 13.5f;
            playerRulerNameText.color = new Color(0.945f, 0.769f, 0.059f, 1f); // #F1C40F
            var prnRt = prnGo.GetComponent<RectTransform>();
            prnRt.anchorMin = new Vector2(0f, 0f); prnRt.anchorMax = new Vector2(1f, 0.5f);
            prnRt.offsetMin = Vector2.zero; prnRt.offsetMax = Vector2.zero;

            // Sağ Tərəf: Xəzinə və Nüfuz
            var statContainerTr = pbGo.transform.Find("StatContainer");
            GameObject scGo = statContainerTr != null ? statContainerTr.gameObject : new GameObject("StatContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            if (statContainerTr == null) scGo.transform.SetParent(pbGo.transform, false);
            var scRt = scGo.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(1f, 0.5f);
            scRt.anchorMax = new Vector2(1f, 0.5f);
            scRt.pivot = new Vector2(1f, 0.5f);
            scRt.anchoredPosition = new Vector2(-20f, 0f);
            scRt.sizeDelta = new Vector2(500f, 60f);

            var scHlg = scGo.GetComponent<HorizontalLayoutGroup>();
            scHlg.spacing = 24f;
            scHlg.childAlignment = TextAnchor.MiddleRight;
            scHlg.childForceExpandWidth = false;
            scHlg.childForceExpandHeight = false;
            scHlg.childControlWidth = false;
            scHlg.childControlHeight = false;

            var tGo = EnsureStatBadge(scGo.transform, "TreasuryBadge", "💰 Xəzinə: 12 450", new Color(1f, 0.843f, 0f, 1f), defaultFont);
            treasuryText = tGo.GetComponentInChildren<TextMeshProUGUI>();

            var infGo = EnsureStatBadge(scGo.transform, "InfluenceBadge", "🕊️ Diplomatik Nüfuz: 78 / 100", new Color(0.35f, 0.75f, 1f, 1f), defaultFont);
            diplomaticInfluenceText = infGo.GetComponentInChildren<TextMeshProUGUI>();

            // ==========================================
            // 4. MAIN TAB NAVIGATION (Height: 38)
            // ==========================================
            var tabTr = diplomacyLedgerPanel.transform.Find("MainTabNavigation");
            GameObject tabGo = tabTr != null ? tabTr.gameObject : new GameObject("MainTabNavigation", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            if (tabTr == null) tabGo.transform.SetParent(diplomacyLedgerPanel.transform, false);
            tabNavigationGroup = tabGo;

            var tabRt = tabGo.GetComponent<RectTransform>();
            tabRt.anchorMin = new Vector2(0f, 1f);
            tabRt.anchorMax = new Vector2(1f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0f, -136f);
            tabRt.sizeDelta = new Vector2(-24f, 38f);

            var tabHlg = tabGo.GetComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 8f;
            tabHlg.childAlignment = TextAnchor.MiddleLeft;
            tabHlg.childForceExpandWidth = true;
            tabHlg.childForceExpandHeight = true;
            tabHlg.childControlWidth = true;
            tabHlg.childControlHeight = true;

            string[] tabNames = {
                "🏛️ Dövlətlər",
                "🤝 Münasibətlər",
                "🛡️ İttifaqlar",
                "📜 Müqavilələr",
                "⚖️ Ticarət",
                "⚔️ Müharibə və Sülh"
            };

            for (int i = 0; i < 6; i++)
            {
                var btn = EnsureTabButton(tabGo.transform, $"TabBtn_{i}", tabNames[i], defaultFont);
                tabButtons[i] = btn;
                tabButtonTexts[i] = btn.GetComponentInChildren<TextMeshProUGUI>();
            }

            // ==========================================
            // 5. VIEW 1: STATES LIST VIEW
            // ==========================================
            EnsureStatesListViewHierarchy(diplomacyLedgerPanel.transform, defaultFont);

            // ==========================================
            // 6. VIEW 2: STATE DETAIL VIEW
            // ==========================================
            EnsureStateDetailViewHierarchy(diplomacyLedgerPanel.transform, defaultFont, titleFont);

            // ==========================================
            // 7. VIEW 3: WAR AND PEACE VIEW
            // ==========================================
            EnsureWarAndPeaceViewHierarchy(diplomacyLedgerPanel.transform, defaultFont, titleFont);

            WireEventHandlers();
            SelectTab(0);
        }

        private GameObject EnsureStatBadge(Transform parent, string name, string text, Color textColor, TMP_FontAsset font)
        {
            var badgeTr = parent.Find(name);
            GameObject badgeGo = badgeTr != null ? badgeTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (badgeTr == null) badgeGo.transform.SetParent(parent, false);

            var rt = badgeGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(210f, 36f);

            var img = badgeGo.GetComponent<Image>();
            img.color = new Color(0.12f, 0.15f, 0.20f, 0.90f);

            var outline = badgeGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.3f);

            var txtTr = badgeGo.transform.Find("Text");
            GameObject txtGo = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTr == null) txtGo.transform.SetParent(badgeGo.transform, false);

            var tRt = txtGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;

            return badgeGo;
        }

        private Button EnsureTabButton(Transform parent, string name, string label, TMP_FontAsset font)
        {
            var btnTr = parent.Find(name);
            GameObject btnGo = btnTr != null ? btnTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            if (btnTr == null) btnGo.transform.SetParent(parent, false);

            var img = btnGo.GetComponent<Image>();
            img.color = new Color(0.10f, 0.12f, 0.16f, 0.85f);

            var outline = btnGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.enabled = false;

            var txtTr = btnGo.transform.Find("Text");
            GameObject txtGo = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTr == null) txtGo.transform.SetParent(btnGo.transform, false);

            var tRt = txtGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btnGo.GetComponent<Button>();
        }

        private void EnsureStatesListViewHierarchy(Transform parent, TMP_FontAsset font)
        {
            var vTr = parent.Find("StatesListView");
            GameObject vGo = vTr != null ? vTr.gameObject : new GameObject("StatesListView", typeof(RectTransform));
            if (vTr == null) vGo.transform.SetParent(parent, false);
            statesListView = vGo;

            var vRt = vGo.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0f, 0f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.offsetMin = new Vector2(12f, 14f);
            vRt.offsetMax = new Vector2(-12f, -178f);

            // Sütun Başlıqları (Column Headers Bar, Height: 32)
            var chTr = vGo.transform.Find("ColumnHeaders");
            GameObject chGo = chTr != null ? chTr.gameObject : new GameObject("ColumnHeaders", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
            if (chTr == null) chGo.transform.SetParent(vGo.transform, false);

            var chRt = chGo.GetComponent<RectTransform>();
            chRt.anchorMin = new Vector2(0f, 1f);
            chRt.anchorMax = new Vector2(1f, 1f);
            chRt.pivot = new Vector2(0.5f, 1f);
            chRt.anchoredPosition = Vector2.zero;
            chRt.sizeDelta = new Vector2(0f, 32f);

            var chImg = chGo.GetComponent<Image>();
            chImg.color = new Color(0.12f, 0.15f, 0.20f, 0.95f);

            var chHlg = chGo.GetComponent<HorizontalLayoutGroup>();
            chHlg.padding = new RectOffset(16, 16, 0, 0);
            chHlg.spacing = 16f;
            chHlg.childAlignment = TextAnchor.MiddleLeft;
            chHlg.childForceExpandWidth = false;
            chHlg.childForceExpandHeight = true;
            chHlg.childControlWidth = false;
            chHlg.childControlHeight = true;

            EnsureHeaderLabel(chGo.transform, "Col_Flag", "Bayraq", 36f, font);
            EnsureHeaderLabel(chGo.transform, "Col_State", "Dövlət Adı", 240f, font);
            EnsureHeaderLabel(chGo.transform, "Col_Rel", "Münasibət (Fikr)", 130f, font);
            EnsureHeaderLabel(chGo.transform, "Col_Status", "Status", 140f, font);
            EnsureHeaderLabel(chGo.transform, "Col_Military", "Hərbi Güc", 140f, font);
            EnsureHeaderLabel(chGo.transform, "Col_Economy", "İqtisadiyyat", 130f, font);
            EnsureHeaderLabel(chGo.transform, "Col_Action", "Fəaliyyət", 110f, font);

            // Scroll View (ScrollRect)
            var svTr = vGo.transform.Find("ScrollRect");
            GameObject svGo = svTr != null ? svTr.gameObject : new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect));
            if (svTr == null) svGo.transform.SetParent(vGo.transform, false);

            var svRt = svGo.GetComponent<RectTransform>();
            svRt.anchorMin = Vector2.zero;
            svRt.anchorMax = Vector2.one;
            svRt.offsetMin = Vector2.zero;
            svRt.offsetMax = new Vector2(0f, -34f);

            statesScrollRect = svGo.GetComponent<ScrollRect>();
            statesScrollRect.horizontal = false;
            statesScrollRect.vertical = true;
            statesScrollRect.movementType = ScrollRect.MovementType.Clamped;
            statesScrollRect.scrollSensitivity = 25f;

            // Viewport
            var vpTr = svGo.transform.Find("Viewport");
            GameObject vpGo = vpTr != null ? vpTr.gameObject : new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            if (vpTr == null) vpGo.transform.SetParent(svGo.transform, false);
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero; vpRt.offsetMax = Vector2.zero;
            statesScrollRect.viewport = vpRt;

            // Content
            var cntTr = vpGo.transform.Find("Content");
            GameObject cntGo = cntTr != null ? cntTr.gameObject : new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            if (cntTr == null) cntGo.transform.SetParent(vpGo.transform, false);

            statesContent = cntGo.GetComponent<RectTransform>();
            statesContent.anchorMin = new Vector2(0f, 1f);
            statesContent.anchorMax = new Vector2(1f, 1f);
            statesContent.pivot = new Vector2(0.5f, 1f);
            statesContent.anchoredPosition = Vector2.zero;
            statesContent.sizeDelta = Vector2.zero;

            var cVlg = cntGo.GetComponent<VerticalLayoutGroup>();
            cVlg.padding = new RectOffset(4, 4, 6, 6);
            cVlg.spacing = 6f;
            cVlg.childForceExpandWidth = true;
            cVlg.childForceExpandHeight = false;
            cVlg.childControlWidth = true;
            cVlg.childControlHeight = false;

            var cCsf = cntGo.GetComponent<ContentSizeFitter>();
            cCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            statesScrollRect.content = statesContent;
        }

        private void EnsureHeaderLabel(Transform parent, string name, string text, float width, TMP_FontAsset font)
        {
            var lblTr = parent.Find(name);
            GameObject lblGo = lblTr != null ? lblTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (lblTr == null) lblGo.transform.SetParent(parent, false);

            var rt = lblGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 30f);

            var tmp = lblGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 12f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private void EnsureStateDetailViewHierarchy(Transform parent, TMP_FontAsset font, TMP_FontAsset titleFont)
        {
            var vTr = parent.Find("StateDetailView");
            GameObject vGo = vTr != null ? vTr.gameObject : new GameObject("StateDetailView", typeof(RectTransform));
            if (vTr == null) vGo.transform.SetParent(parent, false);
            stateDetailView = vGo;

            var vRt = vGo.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0f, 0f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.offsetMin = new Vector2(12f, 14f);
            vRt.offsetMax = new Vector2(-12f, -178f);

            // 1. Qayıt Düyməsi (Yuxarı Sol)
            var backBtnTr = vGo.transform.Find("BackToListButton");
            GameObject bbGo = backBtnTr != null ? backBtnTr.gameObject : new GameObject("BackToListButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            if (backBtnTr == null) bbGo.transform.SetParent(vGo.transform, false);
            backToListButton = bbGo.GetComponent<Button>();

            var bbRt = bbGo.GetComponent<RectTransform>();
            bbRt.anchorMin = new Vector2(0f, 1f);
            bbRt.anchorMax = new Vector2(0f, 1f);
            bbRt.pivot = new Vector2(0f, 1f);
            bbRt.anchoredPosition = new Vector2(0f, 0f);
            bbRt.sizeDelta = new Vector2(160f, 34f);

            var bbImg = bbGo.GetComponent<Image>();
            bbImg.color = new Color(0.18f, 0.22f, 0.30f, 1f);

            var bbOutline = bbGo.GetComponent<Outline>();
            bbOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f);

            var bbTxtTr = bbGo.transform.Find("Text");
            GameObject bbTxtGo = bbTxtTr != null ? bbTxtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (bbTxtTr == null) bbTxtGo.transform.SetParent(bbGo.transform, false);
            var bbtRt = bbTxtGo.GetComponent<RectTransform>();
            bbtRt.anchorMin = Vector2.zero; bbtRt.anchorMax = Vector2.one; bbtRt.sizeDelta = Vector2.zero;
            var bbTmp = bbTxtGo.GetComponent<TextMeshProUGUI>();
            bbTmp.font = font;
            bbTmp.text = "⬅  Siyahıya Qayıt";
            bbTmp.fontSize = 13f;
            bbTmp.fontStyle = FontStyles.Bold;
            bbTmp.color = Color.white;
            bbTmp.alignment = TextAlignmentOptions.Center;

            // 2. Yuxarı Hissə: Qarşılaşdırma Paneli (Height: 140)
            var compTr = vGo.transform.Find("ComparisonSection");
            GameObject compGo = compTr != null ? compTr.gameObject : new GameObject("ComparisonSection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (compTr == null) compGo.transform.SetParent(vGo.transform, false);

            var compRt = compGo.GetComponent<RectTransform>();
            compRt.anchorMin = new Vector2(0f, 1f);
            compRt.anchorMax = new Vector2(1f, 1f);
            compRt.pivot = new Vector2(0.5f, 1f);
            compRt.anchoredPosition = new Vector2(0f, -40f);
            compRt.sizeDelta = new Vector2(0f, 140f);

            var compImg = compGo.GetComponent<Image>();
            compImg.color = new Color(0.08f, 0.10f, 0.14f, 0.95f);

            var compOutline = compGo.GetComponent<Outline>();
            compOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.5f);

            // Sol Tərəf: Hədəf Dövlət
            detailTargetIcon = EnsureDiplomacyProfileSide(compGo.transform, "TargetProfile", new Vector2(0f, 0.5f), new Vector2(30f, 0f), out detailTargetNameText, out detailTargetRulerText, titleFont, font, isLeft: true);

            // Sağ Tərəf: Bizim Dövlət
            detailOurIcon = EnsureDiplomacyProfileSide(compGo.transform, "OurProfile", new Vector2(1f, 0.5f), new Vector2(-30f, 0f), out detailOurNameText, out detailOurRulerText, titleFont, font, isLeft: false);
            if (detailOurNameText != null) detailOurNameText.text = "<b>Eldəgəzlər (Atabəylər)</b>";
            if (detailOurRulerText != null) detailOurRulerText.text = "👑 Qızıl Arslan";
            if (detailOurIcon != null) detailOurIcon.color = new Color(0.122f, 0.380f, 0.553f, 1f);

            // Orta Hissə: Münasibət Şkalası (-100..+100)
            var relCenterTr = compGo.transform.Find("RelationCenter");
            GameObject rcGo = relCenterTr != null ? relCenterTr.gameObject : new GameObject("RelationCenter", typeof(RectTransform));
            if (relCenterTr == null) rcGo.transform.SetParent(compGo.transform, false);
            var rcRt = rcGo.GetComponent<RectTransform>();
            rcRt.anchorMin = new Vector2(0.5f, 0.5f);
            rcRt.anchorMax = new Vector2(0.5f, 0.5f);
            rcRt.pivot = new Vector2(0.5f, 0.5f);
            rcRt.sizeDelta = new Vector2(380f, 120f);

            var scoreTr = rcGo.transform.Find("ScoreText");
            GameObject scoreGo = scoreTr != null ? scoreTr.gameObject : new GameObject("ScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (scoreTr == null) scoreGo.transform.SetParent(rcGo.transform, false);
            var scRt = scoreGo.GetComponent<RectTransform>();
            scRt.anchorMin = new Vector2(0f, 0.55f); scRt.anchorMax = new Vector2(1f, 1f);
            scRt.offsetMin = Vector2.zero; scRt.offsetMax = Vector2.zero;
            detailRelationScoreText = scoreGo.GetComponent<TextMeshProUGUI>();
            detailRelationScoreText.font = font;
            detailRelationScoreText.text = "Münasibət Şkalası: <color=#2ECC71><b>+75 / 100</b></color>";
            detailRelationScoreText.fontSize = 17f;
            detailRelationScoreText.fontStyle = FontStyles.Bold;
            detailRelationScoreText.alignment = TextAlignmentOptions.Center;

            var statusTr = rcGo.transform.Find("StatusText");
            GameObject statusGo = statusTr != null ? statusTr.gameObject : new GameObject("StatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (statusTr == null) statusGo.transform.SetParent(rcGo.transform, false);
            var stRt = statusGo.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0f); stRt.anchorMax = new Vector2(1f, 0.45f);
            stRt.offsetMin = Vector2.zero; stRt.offsetMax = Vector2.zero;
            detailRelationStatusText = statusGo.GetComponent<TextMeshProUGUI>();
            detailRelationStatusText.font = font;
            detailRelationStatusText.text = "Vəziyyət: <color=#F1C40F>Müttəfiqlik və Dostluq</color>";
            detailRelationStatusText.fontSize = 13.5f;
            detailRelationStatusText.alignment = TextAlignmentOptions.Center;

            // 3. Aşağı Hissə: Diplomatik Fəaliyyətlər Şəbəkəsi (Grid/Categories)
            var actTr = vGo.transform.Find("ActionSection");
            GameObject actGo = actTr != null ? actTr.gameObject : new GameObject("ActionSection", typeof(RectTransform));
            if (actTr == null) actGo.transform.SetParent(vGo.transform, false);

            var actRt = actGo.GetComponent<RectTransform>();
            actRt.anchorMin = Vector2.zero;
            actRt.anchorMax = Vector2.one;
            actRt.offsetMin = Vector2.zero;
            actRt.offsetMax = new Vector2(0f, -188f);

            var gridHlg = actGo.GetComponent<HorizontalLayoutGroup>() ?? actGo.AddComponent<HorizontalLayoutGroup>();
            gridHlg.spacing = 16f;
            gridHlg.childAlignment = TextAnchor.UpperCenter;
            gridHlg.childForceExpandWidth = true;
            gridHlg.childForceExpandHeight = true;
            gridHlg.childControlWidth = true;
            gridHlg.childControlHeight = true;

            // 4 Kateqoriya Panelləri
            var catAlliances = EnsureActionCategoryColumn(actGo.transform, "Cat_Alliances", "🛡️ İTTİFAQLAR", font);
            btnDefensiveAlliance = EnsureActionButton(catAlliances.transform, "Btn_Defensive", "🛡️ Müdafiə İttifaqı", font);
            btnMilitaryAlliance = EnsureActionButton(catAlliances.transform, "Btn_MilitaryAlliance", "⚔️ Hərbi İttifaq", font);
            btnEconomicAlliance = EnsureActionButton(catAlliances.transform, "Btn_EconomicAlliance", "💰 İqtisadi İttifaq", font);

            var catTreaties = EnsureActionCategoryColumn(actGo.transform, "Cat_Treaties", "📜 MÜQAVİLƏLƏR", font);
            btnTradeTreaty = EnsureActionButton(catTreaties.transform, "Btn_TradeTreaty", "⚖️ Ticarət Müqaviləsi", font);
            btnNonAggression = EnsureActionButton(catTreaties.transform, "Btn_NonAggression", "🕊️ Hücum Etməmək Paktı", font);
            btnMilitaryAccess = EnsureActionButton(catTreaties.transform, "Btn_MilitaryAccess", "🚩 Hərbi Keçid", font);

            var catDemands = EnsureActionCategoryColumn(actGo.transform, "Cat_Demands", "⚡ TƏZYİQ VƏ TƏLƏB", font);
            btnDemandTribute = EnsureActionButton(catDemands.transform, "Btn_DemandTribute", "💰 Xərac Tələb Et", font);
            btnOfferVassalage = EnsureActionButton(catDemands.transform, "Btn_OfferVassalage", "👑 Vassallıq Təklifi", font);

            var catWar = EnsureActionCategoryColumn(actGo.transform, "Cat_War", "⚔️ SAVAŞ VƏ SÜLH", font);
            btnDeclareWar = EnsureActionButton(catWar.transform, "Btn_DeclareWar", "⚔️ MÜHARİBƏ ELAN ET", font, isWar: true);

            // Alt Bildiriş / Status Zolağı
            var fbTr = vGo.transform.Find("ActionFeedbackText");
            GameObject fbGo = fbTr != null ? fbTr.gameObject : new GameObject("ActionFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (fbTr == null) fbGo.transform.SetParent(vGo.transform, false);
            var fbRt = fbGo.GetComponent<RectTransform>();
            fbRt.anchorMin = new Vector2(0f, 0f);
            fbRt.anchorMax = new Vector2(1f, 0f);
            fbRt.pivot = new Vector2(0.5f, 0f);
            fbRt.anchoredPosition = new Vector2(0f, -6f);
            fbRt.sizeDelta = new Vector2(0f, 26f);
            actionFeedbackText = fbGo.GetComponent<TextMeshProUGUI>();
            actionFeedbackText.font = font;
            actionFeedbackText.text = "Diplomatik hərəkət etmək üçün yuxarıdakı kateqoriyalardan birini seçin.";
            actionFeedbackText.fontSize = 12.5f;
            actionFeedbackText.color = new Color(0.85f, 0.90f, 0.95f, 1f);
            actionFeedbackText.alignment = TextAlignmentOptions.Center;

            stateDetailView.SetActive(false);
        }

        private Image EnsureDiplomacyProfileSide(Transform parent, string name, Vector2 anchor, Vector2 pos, out TextMeshProUGUI nameTmp, out TextMeshProUGUI rulerTmp, TMP_FontAsset titleFont, TMP_FontAsset font, bool isLeft)
        {
            var pTr = parent.Find(name);
            GameObject pGo = pTr != null ? pTr.gameObject : new GameObject(name, typeof(RectTransform));
            if (pTr == null) pGo.transform.SetParent(parent, false);

            var pRt = pGo.GetComponent<RectTransform>();
            pRt.anchorMin = anchor;
            pRt.anchorMax = anchor;
            pRt.pivot = anchor;
            pRt.anchoredPosition = pos;
            pRt.sizeDelta = new Vector2(320f, 100f);

            var iconTr = pGo.transform.Find("FlagIcon");
            GameObject iconGo = iconTr != null ? iconTr.gameObject : new GameObject("FlagIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (iconTr == null) iconGo.transform.SetParent(pGo.transform, false);

            var iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
            iRt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
            iRt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
            iRt.anchoredPosition = Vector2.zero;
            iRt.sizeDelta = new Vector2(60f, 60f);

            var img = iconGo.GetComponent<Image>();
            var outline = iconGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 1f);

            var nameTr = pGo.transform.Find("StateName");
            GameObject nGo = nameTr != null ? nameTr.gameObject : new GameObject("StateName", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (nameTr == null) nGo.transform.SetParent(pGo.transform, false);
            var nRt = nGo.GetComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0.5f);
            nRt.anchorMax = new Vector2(1f, 1f);
            nRt.offsetMin = new Vector2(isLeft ? 74f : 0f, 0f);
            nRt.offsetMax = new Vector2(isLeft ? 0f : -74f, 0f);

            nameTmp = nGo.GetComponent<TextMeshProUGUI>();
            nameTmp.font = titleFont;
            nameTmp.fontSize = 17f;
            nameTmp.color = Color.white;
            nameTmp.alignment = isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

            var rTr = pGo.transform.Find("RulerName");
            GameObject rGo = rTr != null ? rTr.gameObject : new GameObject("RulerName", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (rTr == null) rGo.transform.SetParent(pGo.transform, false);
            var rRt = rGo.GetComponent<RectTransform>();
            rRt.anchorMin = new Vector2(0f, 0f);
            rRt.anchorMax = new Vector2(1f, 0.5f);
            rRt.offsetMin = new Vector2(isLeft ? 74f : 0f, 0f);
            rRt.offsetMax = new Vector2(isLeft ? 0f : -74f, 0f);

            rulerTmp = rGo.GetComponent<TextMeshProUGUI>();
            rulerTmp.font = font;
            rulerTmp.fontSize = 12.5f;
            rulerTmp.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            rulerTmp.alignment = isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

            return img;
        }

        private GameObject EnsureActionCategoryColumn(Transform parent, string name, string title, TMP_FontAsset font)
        {
            var cTr = parent.Find(name);
            GameObject cGo = cTr != null ? cTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup));
            if (cTr == null) cGo.transform.SetParent(parent, false);

            var img = cGo.GetComponent<Image>();
            img.color = new Color(0.08f, 0.10f, 0.13f, 0.90f);

            var outline = cGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.40f);

            var vlg = cGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 10f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Başlıq mətni
            var tTr = cGo.transform.Find("Title");
            GameObject tGo = tTr != null ? tTr.gameObject : new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (tTr == null) tGo.transform.SetParent(cGo.transform, false);

            var tmp = tGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = title;
            tmp.fontSize = 13f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.957f, 0.816f, 0.247f, 1f); // #F4D03F
            tmp.alignment = TextAlignmentOptions.Center;

            return cGo;
        }

        private Button EnsureActionButton(Transform parent, string name, string label, TMP_FontAsset font, bool isWar = false)
        {
            var bTr = parent.Find(name);
            GameObject bGo = bTr != null ? bTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Button));
            if (bTr == null) bGo.transform.SetParent(parent, false);

            var rt = bGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 42f);

            var img = bGo.GetComponent<Image>();
            img.color = isWar
                ? new Color(0.75f, 0.18f, 0.15f, 0.95f) // Qırmızı Müharibə düyməsi
                : new Color(0.14f, 0.18f, 0.25f, 0.95f);

            var outline = bGo.GetComponent<Outline>();
            outline.effectColor = isWar
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : new Color(0.773f, 0.627f, 0.349f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);

            var txtTr = bGo.transform.Find("Text");
            GameObject txtGo = txtTr != null ? txtTr.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTr == null) txtGo.transform.SetParent(bGo.transform, false);

            var tRt = txtGo.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 12.5f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return bGo.GetComponent<Button>();
        }

        private void EnsureWarAndPeaceViewHierarchy(Transform parent, TMP_FontAsset font, TMP_FontAsset titleFont)
        {
            var vTr = parent.Find("WarAndPeaceView");
            GameObject vGo = vTr != null ? vTr.gameObject : new GameObject("WarAndPeaceView", typeof(RectTransform));
            if (vTr == null) vGo.transform.SetParent(parent, false);
            warAndPeaceView = vGo;

            var vRt = vGo.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(0f, 0f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.offsetMin = new Vector2(12f, 14f);
            vRt.offsetMax = new Vector2(-12f, -178f);

            // Mərkəzi Müharibə Paneli
            var centerPanelTr = vGo.transform.Find("WarCenterPanel");
            GameObject cpGo = centerPanelTr != null ? centerPanelTr.gameObject : new GameObject("WarCenterPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (centerPanelTr == null) cpGo.transform.SetParent(vGo.transform, false);

            var cpRt = cpGo.GetComponent<RectTransform>();
            cpRt.anchorMin = new Vector2(0.5f, 0.5f);
            cpRt.anchorMax = new Vector2(0.5f, 0.5f);
            cpRt.pivot = new Vector2(0.5f, 0.5f);
            cpRt.sizeDelta = new Vector2(800f, 440f);

            var cpImg = cpGo.GetComponent<Image>();
            cpImg.color = new Color(0.08f, 0.10f, 0.13f, 0.95f);

            var cpOutline = cpGo.GetComponent<Outline>();
            cpOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.6f);

            // Dinamik Müharibə Məzmunu Konteyneri (WarNegotiationContent)
            Transform contentParent = EnsureWarNegotiationContainer(cpGo.transform);

            // 1. Hədəf Dövlət Başlığı
            var wtTr = FindInCenterOrContent(cpGo.transform, contentParent, "TargetText");
            GameObject wtGo = wtTr != null ? wtTr.gameObject : new GameObject("TargetText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (wtGo.transform.parent != contentParent) wtGo.transform.SetParent(contentParent, false);
            var wtRt = wtGo.GetComponent<RectTransform>();
            wtRt.anchorMin = new Vector2(0f, 1f); wtRt.anchorMax = new Vector2(1f, 1f);
            wtRt.pivot = new Vector2(0.5f, 1f);
            wtRt.anchoredPosition = new Vector2(0f, -20f);
            wtRt.sizeDelta = new Vector2(0f, 32f);
            warTargetStateText = wtGo.GetComponent<TextMeshProUGUI>();
            warTargetStateText.font = titleFont;
            warTargetStateText.text = string.Empty;
            warTargetStateText.fontSize = 18f;
            warTargetStateText.fontStyle = FontStyles.Bold;
            warTargetStateText.color = Color.white;
            warTargetStateText.alignment = TextAlignmentOptions.Center;

            // 2. Müharibə Xalı (War Score Indikatoru)
            var wsTr = FindInCenterOrContent(cpGo.transform, contentParent, "WarScoreText");
            GameObject wsGo = wsTr != null ? wsTr.gameObject : new GameObject("WarScoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (wsGo.transform.parent != contentParent) wsGo.transform.SetParent(contentParent, false);
            var wsRt = wsGo.GetComponent<RectTransform>();
            wsRt.anchorMin = new Vector2(0f, 1f); wsRt.anchorMax = new Vector2(1f, 1f);
            wsRt.pivot = new Vector2(0.5f, 1f);
            wsRt.anchoredPosition = new Vector2(0f, -60f);
            wsRt.sizeDelta = new Vector2(0f, 36f);
            warScoreText = wsGo.GetComponent<TextMeshProUGUI>();
            warScoreText.font = font;
            warScoreText.text = string.Empty;
            warScoreText.fontSize = 18f;
            warScoreText.fontStyle = FontStyles.Bold;
            warScoreText.alignment = TextAlignmentOptions.Center;

            // 3. Sülh Şərtləri Bölməsi (4 Şərt Toggle / Checkbox)
            var termsGroupTr = FindInCenterOrContent(cpGo.transform, contentParent, "TermsGroup");
            GameObject tgGo = termsGroupTr != null ? termsGroupTr.gameObject : new GameObject("TermsGroup", typeof(RectTransform), typeof(VerticalLayoutGroup));
            if (tgGo.transform.parent != contentParent) tgGo.transform.SetParent(contentParent, false);
            var tgRt = tgGo.GetComponent<RectTransform>();
            tgRt.anchorMin = new Vector2(0.08f, 0.28f);
            tgRt.anchorMax = new Vector2(0.92f, 0.72f);
            tgRt.offsetMin = Vector2.zero; tgRt.offsetMax = Vector2.zero;

            var tgVlg = tgGo.GetComponent<VerticalLayoutGroup>();
            tgVlg.spacing = 10f;
            tgVlg.childControlWidth = true;
            tgVlg.childControlHeight = false;
            tgVlg.childForceExpandWidth = true;
            tgVlg.childForceExpandHeight = false;

            toggleTerritory = EnsurePeaceToggle(tgGo.transform, "Toggle_Territory", "🗺️  Ərazi Təhvili (Sərhəd qalaları və əyalətlər)  [-25% Xal]", font);
            toggleReparations = EnsurePeaceToggle(tgGo.transform, "Toggle_Reparations", "💰  Müharibə Təzminatı (10 illik qızıl xəracı)  [-15% Xal]", font);
            toggleVassalage = EnsurePeaceToggle(tgGo.transform, "Toggle_Vassalage", "👑  Vassallıq (Tabeliyin tam qəbul edilməsi)  [-50% Xal]", font);
            toggleWithdrawal = EnsurePeaceToggle(tgGo.transform, "Toggle_Withdrawal", "🏳️  Qoşunların Çıxarılması və Tərksilah  [-10% Xal]", font);

            // 4. Sülh Düymələri
            var btnGroupTr = FindInCenterOrContent(cpGo.transform, contentParent, "ButtonGroup");
            GameObject bgGo = btnGroupTr != null ? btnGroupTr.gameObject : new GameObject("ButtonGroup", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            if (bgGo.transform.parent != contentParent) bgGo.transform.SetParent(contentParent, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0f); bgRt.anchorMax = new Vector2(1f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = new Vector2(0f, 50f);
            bgRt.sizeDelta = new Vector2(-60f, 44f);

            var bgHlg = bgGo.GetComponent<HorizontalLayoutGroup>();
            bgHlg.spacing = 20f;
            bgHlg.childAlignment = TextAnchor.MiddleCenter;
            bgHlg.childForceExpandWidth = false;

            btnProposePeace = EnsureActionButton(bgGo.transform, "Btn_ProposePeace", "📜  Sülh Şərtlərini İmzala", font);
            btnProposePeace.GetComponent<RectTransform>().sizeDelta = new Vector2(240f, 40f);

            btnSurrender = EnsureActionButton(bgGo.transform, "Btn_Surrender", "🏳️  Şərtsiz Təslim Ol", font, isWar: true);
            btnSurrender.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 40f);

            // 5. Nəticə / Status Mətni
            var wfTr = FindInCenterOrContent(cpGo.transform, contentParent, "WarFeedbackText");
            GameObject wfGo = wfTr != null ? wfTr.gameObject : new GameObject("WarFeedbackText", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (wfGo.transform.parent != contentParent) wfGo.transform.SetParent(contentParent, false);
            var wfRt = wfGo.GetComponent<RectTransform>();
            wfRt.anchorMin = new Vector2(0f, 0f); wfRt.anchorMax = new Vector2(1f, 0f);
            wfRt.anchoredPosition = new Vector2(0f, 16f);
            wfRt.sizeDelta = new Vector2(0f, 26f);
            warFeedbackText = wfGo.GetComponent<TextMeshProUGUI>();
            warFeedbackText.font = font;
            warFeedbackText.text = "Sülh şərtlərini seçin və tələbinizi qarşı tərəfə göndərin.";
            warFeedbackText.fontSize = 12.5f;
            warFeedbackText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            warFeedbackText.alignment = TextAlignmentOptions.Center;

            // 6. Aktiv Müharibə Yoxdur Mesajı (Mərkəz)
            EnsureNoActiveWarsLabel(cpGo.transform, titleFont != null ? titleFont : font);
            EnsurePeaceToggleLayoutFixed();

            warAndPeaceView.SetActive(false);
        }

        private Transform EnsureWarNegotiationContainer(Transform centerPanel)
        {
            if (warNegotiationContent == null)
            {
                var found = centerPanel.Find("WarNegotiationContent");
                warNegotiationContent = found != null
                    ? found.gameObject
                    : new GameObject("WarNegotiationContent", typeof(RectTransform));
                if (found == null) warNegotiationContent.transform.SetParent(centerPanel, false);
            }

            var rt = warNegotiationContent.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return warNegotiationContent.transform;
        }

        private static Transform FindInCenterOrContent(Transform centerPanel, Transform contentParent, string childName)
        {
            var t = contentParent.Find(childName);
            if (t != null) return t;
            return centerPanel.Find(childName);
        }

        private void EnsureNoActiveWarsLabel(Transform centerPanel, TMP_FontAsset font)
        {
            if (noActiveWarsText == null)
            {
                var found = centerPanel.Find("NoActiveWarsText");
                GameObject go = found != null
                    ? found.gameObject
                    : new GameObject("NoActiveWarsText", typeof(RectTransform), typeof(TextMeshProUGUI));
                if (found == null) go.transform.SetParent(centerPanel, false);
                noActiveWarsText = go.GetComponent<TextMeshProUGUI>();
            }

            var rt = noActiveWarsText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.25f);
            rt.anchorMax = new Vector2(0.94f, 0.75f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (font != null) noActiveWarsText.font = font;
            noActiveWarsText.text = "Hazırda heç bir dövlətlə müharibə vəziyyətində deyilsiniz.";
            noActiveWarsText.fontSize = 21f;
            noActiveWarsText.fontStyle = FontStyles.Bold;
            noActiveWarsText.color = new Color(0.88f, 0.90f, 0.92f, 1f);
            noActiveWarsText.alignment = TextAlignmentOptions.Center;
            noActiveWarsText.gameObject.SetActive(false);
        }

        private void EnsureWarAndPeaceDynamicContainers()
        {
            if (warAndPeaceView == null) return;
            Transform cp = warAndPeaceView.transform.Find("WarCenterPanel");
            if (cp == null) return;

            Transform contentParent = EnsureWarNegotiationContainer(cp);
            string[] negotiationChildren = { "TargetText", "WarScoreText", "TermsGroup", "ButtonGroup", "WarFeedbackText" };
            for (int i = 0; i < negotiationChildren.Length; i++)
            {
                var child = cp.Find(negotiationChildren[i]);
                if (child != null && child.parent != contentParent)
                {
                    child.SetParent(contentParent, false);
                }
            }

            var font = UIThemeManager.Instance != null ? UIThemeManager.Instance.GetTitleFont() : TMP_Settings.defaultFontAsset;
            EnsureNoActiveWarsLabel(cp, font);
        }

        private void EnsurePeaceToggleLayoutFixed()
        {
            if (warNegotiationContent != null)
            {
                var termsTr = warNegotiationContent.transform.Find("TermsGroup");
                if (termsTr != null)
                {
                    var vlg = termsTr.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null)
                    {
                        vlg.childControlWidth = true;
                        vlg.childControlHeight = false;
                        vlg.childForceExpandWidth = true;
                        vlg.childForceExpandHeight = false;
                    }
                }
            }

            Toggle[] toggles = { toggleTerritory, toggleReparations, toggleVassalage, toggleWithdrawal };
            for (int i = 0; i < toggles.Length; i++)
            {
                ApplyHorizontalToggleLabelFix(toggles[i]);
            }
        }

        private static void ApplyHorizontalToggleLabelFix(Toggle toggle)
        {
            if (toggle == null) return;

            var rt = toggle.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(620f, 32f);
            }

            var labelTr = toggle.transform.Find("Label");
            if (labelTr == null) return;

            var txtRt = labelTr.GetComponent<RectTransform>();
            if (txtRt != null)
            {
                txtRt.anchorMin = new Vector2(0f, 0.5f);
                txtRt.anchorMax = new Vector2(0f, 0.5f);
                txtRt.pivot = new Vector2(0f, 0.5f);
                txtRt.anchoredPosition = new Vector2(36f, 0f);
                txtRt.sizeDelta = new Vector2(560f, 32f);
            }

            var tmp = labelTr.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private Toggle EnsurePeaceToggle(Transform parent, string name, string label, TMP_FontAsset font)
        {
            var tTr = parent.Find(name);
            GameObject tGo = tTr != null ? tTr.gameObject : new GameObject(name, typeof(RectTransform), typeof(Toggle));
            if (tTr == null) tGo.transform.SetParent(parent, false);

            var rt = tGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(620f, 32f);

            var toggle = tGo.GetComponent<Toggle>();

            var bgTr = tGo.transform.Find("Background");
            GameObject bgGo = bgTr != null ? bgTr.gameObject : new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            if (bgTr == null) bgGo.transform.SetParent(tGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f); bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(22f, 22f);
            bgRt.anchoredPosition = new Vector2(4f, 0f);

            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.14f, 0.18f, 0.24f, 1f);

            var bgOutline = bgGo.GetComponent<Outline>();
            bgOutline.effectColor = new Color(0.773f, 0.627f, 0.349f, 0.8f);

            var chkTr = bgGo.transform.Find("Checkmark");
            GameObject chkGo = chkTr != null ? chkTr.gameObject : new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (chkTr == null) chkGo.transform.SetParent(bgGo.transform, false);
            var chkRt = chkGo.GetComponent<RectTransform>();
            chkRt.anchorMin = new Vector2(0.2f, 0.2f); chkRt.anchorMax = new Vector2(0.8f, 0.8f);
            chkRt.offsetMin = Vector2.zero; chkRt.offsetMax = Vector2.zero;
            var chkImg = chkGo.GetComponent<Image>();
            chkImg.color = new Color(0.18f, 0.80f, 0.44f, 1f); // #2ECC71 Yaşıl quş
            toggle.graphic = chkImg;
            toggle.targetGraphic = bgImg;

            var txtTr = tGo.transform.Find("Label");
            GameObject txtGo = txtTr != null ? txtTr.gameObject : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            if (txtTr == null) txtGo.transform.SetParent(tGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0.5f); txtRt.anchorMax = new Vector2(0f, 0.5f);
            txtRt.pivot = new Vector2(0f, 0.5f);
            txtRt.anchoredPosition = new Vector2(36f, 0f);
            txtRt.sizeDelta = new Vector2(560f, 32f);

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 13.5f;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            return toggle;
        }

        #endregion
    }
}
