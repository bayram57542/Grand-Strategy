using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.UI
{
    /// <summary>
    /// UIManager ilə sinxron işləyən, AI tərəfindən generasiya edilmiş qrafik assetləri
    /// və vizual üslubları mərkəzləşdirilmiş şəkildə bütün UI panellərinə tətbiq edən Tema Meneceri.
    /// </summary>
    [ExecuteAlways]
    public class UIThemeManager : MonoBehaviour
    {
        private static UIThemeManager _instance;
        public static UIThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<UIThemeManager>();
                }
                return _instance;
            }
        }

        [Header("Cari Tema Profili (ScriptableObject)")]
        [Tooltip("Bütün panellərə tətbiq ediləcək qrafika, şrift və rəng profili")]
        [SerializeField] private UIThemeProfile currentTheme;

        [Header("Avtomatik Tətbiq")]
        [Tooltip("Oyun və ya səhnə başlayarkən temann dərhal tətbiq olunması")]
        [SerializeField] private bool applyOnStart = true;

        public UIThemeProfile CurrentTheme
        {
            get => currentTheme;
            set => currentTheme = value;
        }

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

            EnsureDefaultThemeAssigned();
        }

        private void Start()
        {
            if (applyOnStart)
            {
                ApplyTheme();
            }
        }

        private void OnEnable()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        /// <summary>
        /// Mövcud profil boşdursa, standart temayı yükləyir və ya yaradır.
        /// </summary>
        public void EnsureDefaultThemeAssigned()
        {
            if (currentTheme != null) return;

#if UNITY_EDITOR
            currentTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<UIThemeProfile>("Assets/Database/DefaultTheme.asset");
#endif

            if (currentTheme == null)
            {
                currentTheme = Resources.Load<UIThemeProfile>("DefaultTheme");
            }

            if (currentTheme == null)
            {
                // Fallback runtime profili yaradırıq
                currentTheme = ScriptableObject.CreateInstance<UIThemeProfile>();
                currentTheme.name = "RuntimeFallbackTheme";
                currentTheme.primaryBorderColor = new Color(0.773f, 0.627f, 0.349f, 1f); // #C5A059
                currentTheme.panelBackgroundColor = new Color(0.067f, 0.067f, 0.067f, 0.88f); // #111111
                currentTheme.mainFont = TMP_Settings.defaultFontAsset;
                currentTheme.titleFont = TMP_Settings.defaultFontAsset;
            }
        }

        /// <summary>
        /// Yeni bir tema təyin edir və dərhal bütün UI panellərinə tətbiq edir.
        /// </summary>
        public void SetTheme(UIThemeProfile newTheme, bool applyImmediately = true)
        {
            currentTheme = newTheme;
            if (applyImmediately)
            {
                ApplyTheme();
            }
        }

        /// <summary>
        /// Verilən (və ya cari) temann spritelarını, rənglərini və şriftlərini
        /// UIManager altındakı bütün əsas panellərə tətbiq edir.
        /// </summary>
        public void ApplyTheme(UIThemeProfile themeToApply = null)
        {
            if (themeToApply != null)
            {
                currentTheme = themeToApply;
            }

            EnsureDefaultThemeAssigned();
            if (currentTheme == null) return;

            var uiMgr = UIManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<UIManager>();
            if (uiMgr == null) return;

            // 1. SOL YUXARI: Dövlət və Hökmdar Paneli (FactionProfilePanel)
            ApplyToFactionProfilePanel(uiMgr);

            // 2. SOL AŞAĞI: Bildirişlər və Salnamə Paneli (EventLogPanel)
            ApplyToEventLogPanel(uiMgr);

            // 3. YUXARI ZOLAĞI: Top Bar Paneli (TopBarPanel)
            ApplyToTopBarPanel(uiMgr);

            // 4. SAĞ TƏRƏF: Əyalət Məlumat Paneli (ProvinceInfoPanel)
            ApplyToProvinceInfoPanel(uiMgr);

            // 5. DEMOQRAFİYA VƏ MÜHASİRƏ PANELLƏRİ
            ApplyToAuxiliaryPanels(uiMgr);

            // 6. QLOBAL DİPLOMATİYA MƏRKƏZİ (DiplomacyLedgerPanel)
            var dipMgr = DiplomacyUIManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<DiplomacyUIManager>();
            if (dipMgr != null)
            {
                dipMgr.ApplyTheme(currentTheme);
            }
        }

        private void ApplyToFactionProfilePanel(UIManager uiMgr)
        {
            var panel = uiMgr.FactionProfilePanel;
            if (panel == null) return;

            // Arxa fon (Image)
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = currentTheme.panelBackgroundColor;
                if (currentTheme.panelBackground != null)
                {
                    bgImg.sprite = currentTheme.panelBackground;
                    bgImg.type = Image.Type.Sliced;
                }
            }

            // Kənar xətt (Outline)
            var outline = panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = currentTheme.primaryBorderColor;
            }

            // Alt dekorativ zolaq
            var stripe = panel.transform.Find("GoldBottomStripe");
            if (stripe != null)
            {
                var sImg = stripe.GetComponent<Image>();
                if (sImg != null) sImg.color = currentTheme.primaryBorderColor;
            }

            // Gerb haşiyəsi
            var crest = panel.transform.Find("FactionCrest");
            if (crest != null)
            {
                var cOutline = crest.GetComponent<Outline>();
                if (cOutline != null) cOutline.effectColor = currentTheme.primaryBorderColor;
            }

            // Mətn şriftləri
            if (uiMgr.FactionNameText != null)
            {
                var font = currentTheme.titleFont != null ? currentTheme.titleFont : currentTheme.mainFont;
                if (font != null) uiMgr.FactionNameText.font = font;
            }

            if (uiMgr.FactionLeaderText != null)
            {
                var font = currentTheme.mainFont != null ? currentTheme.mainFont : currentTheme.titleFont;
                if (font != null) uiMgr.FactionLeaderText.font = font;
            }
        }

        private void ApplyToEventLogPanel(UIManager uiMgr)
        {
            var panel = uiMgr.EventLogPanel;
            if (panel == null) return;

            // Arxa fon (Image)
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = currentTheme.panelBackgroundColor;
                if (currentTheme.panelBackground != null)
                {
                    bgImg.sprite = currentTheme.panelBackground;
                    bgImg.type = Image.Type.Sliced;
                }
            }

            // Kənar xətt (Outline)
            var outline = panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = currentTheme.primaryBorderColor;
            }

            // Başlıq zolağı (HeaderBar)
            var header = panel.transform.Find("HeaderBar");
            if (header != null)
            {
                var hImg = header.GetComponent<Image>();
                if (hImg != null) hImg.color = currentTheme.secondaryHeaderColor;

                var titleTr = header.Find("TitleText");
                if (titleTr != null)
                {
                    var titleTmp = titleTr.GetComponent<TextMeshProUGUI>();
                    if (titleTmp != null)
                    {
                        var font = currentTheme.titleFont != null ? currentTheme.titleFont : currentTheme.mainFont;
                        if (font != null) titleTmp.font = font;
                    }
                }

                var divider = header.Find("DividerLine");
                if (divider != null)
                {
                    var dImg = divider.GetComponent<Image>();
                    if (dImg != null) dImg.color = currentTheme.primaryBorderColor;
                }
            }

            // Salnamə sətirləri (Content daxilindəki TextMeshProUGUI)
            if (uiMgr.EventLogContent != null && currentTheme.mainFont != null)
            {
                int childCount = uiMgr.EventLogContent.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    var child = uiMgr.EventLogContent.GetChild(i);
                    var rowTmp = child.GetComponent<TextMeshProUGUI>();
                    if (rowTmp != null)
                    {
                        rowTmp.font = currentTheme.mainFont;
                    }
                }
            }
        }

        private void ApplyToTopBarPanel(UIManager uiMgr)
        {
            var panel = uiMgr.TopBarPanel;
            if (panel == null) return;

            // Arxa fon
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = currentTheme.panelBackgroundColor;
                if (currentTheme.panelBackground != null)
                {
                    bgImg.sprite = currentTheme.panelBackground;
                    bgImg.type = Image.Type.Sliced;
                }
            }

            // Alt haşiyə zolağı (BottomGoldStripe)
            var stripe = panel.transform.Find("BottomGoldStripe");
            if (stripe != null)
            {
                var sImg = stripe.GetComponent<Image>();
                if (sImg != null) sImg.color = currentTheme.primaryBorderColor;
            }

            // Dövlət Rəng Nişanı haşiyəsi (OwnerColorBadge)
            if (uiMgr.TopBarOwnerBadge != null)
            {
                var badgeOutline = uiMgr.TopBarOwnerBadge.GetComponent<Outline>();
                if (badgeOutline != null) badgeOutline.effectColor = currentTheme.primaryBorderColor;
            }

            // Sürət düymələri (Speed buttons)
            if (currentTheme.buttonNormal != null)
            {
                var buttons = panel.GetComponentsInChildren<Button>(true);
                foreach (var btn in buttons)
                {
                    var bImg = btn.GetComponent<Image>();
                    if (bImg != null)
                    {
                        bImg.sprite = currentTheme.buttonNormal;
                    }
                }
            }
        }

        private void ApplyToProvinceInfoPanel(UIManager uiMgr)
        {
            var panel = uiMgr.ProvinceInfoPanel;
            if (panel == null) return;

            // Arxa fon
            var bgImg = panel.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = currentTheme.panelBackgroundColor;
                if (currentTheme.panelBackground != null)
                {
                    bgImg.sprite = currentTheme.panelBackground;
                    bgImg.type = Image.Type.Sliced;
                }
            }

            // Kənar xətt
            var outline = panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = currentTheme.primaryBorderColor;
            }

            // Ayırıcı zolaqlar (Dividers)
            string[] dividerNames = { "Divider1", "Divider2", "Divider3" };
            foreach (var dName in dividerNames)
            {
                var divTr = panel.transform.Find(dName);
                if (divTr != null)
                {
                    var dImg = divTr.GetComponent<Image>();
                    if (dImg != null) dImg.color = currentTheme.primaryBorderColor;
                }
            }

            // Bağla və digər düymələr
            if (currentTheme.buttonNormal != null)
            {
                if (uiMgr.CloseButton != null)
                {
                    var cImg = uiMgr.CloseButton.GetComponent<Image>();
                    if (cImg != null) cImg.sprite = currentTheme.buttonNormal;
                }
            }
        }

        private void ApplyToAuxiliaryPanels(UIManager uiMgr)
        {
            // Demoqrafiya Paneli
            if (uiMgr.DemographicsPanel != null)
            {
                var dOutline = uiMgr.DemographicsPanel.GetComponent<Outline>();
                if (dOutline != null) dOutline.effectColor = currentTheme.primaryBorderColor;

                var dBg = uiMgr.DemographicsPanel.GetComponent<Image>();
                if (dBg != null)
                {
                    dBg.color = currentTheme.panelBackgroundColor;
                    if (currentTheme.panelBackground != null) dBg.sprite = currentTheme.panelBackground;
                }
            }

            // Mühasirə Paneli
            if (uiMgr.SiegeLogisticsPanel != null)
            {
                var sOutline = uiMgr.SiegeLogisticsPanel.GetComponent<Outline>();
                if (sOutline != null) sOutline.effectColor = currentTheme.primaryBorderColor;

                var sBg = uiMgr.SiegeLogisticsPanel.GetComponent<Image>();
                if (sBg != null)
                {
                    sBg.color = currentTheme.panelBackgroundColor;
                    if (currentTheme.panelBackground != null) sBg.sprite = currentTheme.panelBackground;
                }
            }
        }

        /// <summary>
        /// Yeni yaradılan mətnlər üçün cari əsas şrifti qaytarır.
        /// </summary>
        public TMP_FontAsset GetMainFont()
        {
            return currentTheme != null && currentTheme.mainFont != null
                ? currentTheme.mainFont
                : TMP_Settings.defaultFontAsset;
        }

        /// <summary>
        /// Yeni yaradılan başlıqlar üçün cari başlıq şriftini qaytarır.
        /// </summary>
        public TMP_FontAsset GetTitleFont()
        {
            return currentTheme != null && currentTheme.titleFont != null
                ? currentTheme.titleFont
                : GetMainFont();
        }
    }
}
