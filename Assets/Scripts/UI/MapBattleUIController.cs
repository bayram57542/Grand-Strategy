using System;
using GrandStrategy.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GrandStrategy.UI
{
    /// <summary>
    /// Faza 10.12 - Xəritə Üzərində Döyüş İndikatoru (MapBattleUI).
    /// Sol və Sağ dövlət bayraqları, ortada toqquşan qılınclar ikonu və altında 50/50 mərkəzdən başlayan
    /// döyüş tərəzisi (Battle Progress Slider) ilə əyalət üstündə döyüş gedişatını göstərir.
    /// </summary>
    public class MapBattleUIController : MonoBehaviour
    {
        public const string PrefabAssetPath = "Assets/Prefabs/UI/MapBattleUI.prefab";

        [Header("UI Elementləri")]
        [SerializeField] private Canvas battleCanvas;
        [SerializeField] private Image backgroundPanel;
        [SerializeField] private Image leftFlagImage;
        [SerializeField] private Image rightFlagImage;
        [SerializeField] private Image crossedSwordsImage;
        [SerializeField] private TextMeshProUGUI troopComparisonText;
        [SerializeField] private Slider battleProgressSlider;
        [SerializeField] private Image sliderBackgroundImage;
        [SerializeField] private Image sliderFillImage;

        private static Sprite _cachedSwordsSprite;

        public Canvas BattleCanvas => battleCanvas;
        public Image LeftFlagImage => leftFlagImage;
        public Image RightFlagImage => rightFlagImage;
        public Image CrossedSwordsImage => crossedSwordsImage;
        public TextMeshProUGUI TroopComparisonText => troopComparisonText;
        public Slider BattleProgressSlider => battleProgressSlider;
        public Image SliderBackgroundImage => sliderBackgroundImage;
        public Image SliderFillImage => sliderFillImage;

        /// <summary>
        /// Əyalətin üstündə (yaxud verilmiş koordinatda) MapBattleUI obyektini yaradır (əvvəlcə prefabdan, yoxdursa procedural).
        /// </summary>
        public static MapBattleUIController CreateForProvince(MapInteractionController province, Transform parent = null)
        {
            Vector3 worldPos = province != null ? province.GetCentroidWorldPosition() : Vector3.zero;
            worldPos = new Vector3(worldPos.x, worldPos.y + 0.36f, -2.6f);

            GameObject go = null;
#if UNITY_EDITOR
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            if (prefab != null)
            {
                go = Instantiate(prefab, worldPos, Quaternion.identity, parent);
                go.name = province != null ? $"MapBattleUI_{province.ProvinceName}" : "MapBattleUI_Instance";
            }
#endif
            if (go == null)
            {
                go = new GameObject(province != null ? $"MapBattleUI_{province.ProvinceName}" : "MapBattleUI_Instance");
                if (parent != null) go.transform.SetParent(parent, false);
                go.transform.position = worldPos;
            }

            var controller = go.GetComponent<MapBattleUIController>();
            if (controller == null) controller = go.AddComponent<MapBattleUIController>();
            controller.BuildHierarchyIfNeeded();
            return controller;
        }

        /// <summary>
        /// Bütün child UI obyektlərinin (Sol/Sağ bayraqlar, Toqquşan Qılınclar, Slider) mövcudluğunu təmin edir.
        /// Həm prefab yaradılmasında, həm də runtime fallback-də istifadə olunur.
        /// </summary>
        public void BuildHierarchyIfNeeded(bool forceRebuild = false)
        {
            var rootRt = GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(220f, 80f);
            transform.localScale = new Vector3(0.0042f, 0.0042f, 1f);

            if (battleCanvas == null) battleCanvas = GetComponent<Canvas>();
            if (battleCanvas == null) battleCanvas = gameObject.AddComponent<Canvas>();
            battleCanvas.renderMode = RenderMode.WorldSpace;
            battleCanvas.sortingOrder = 100;

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 10f;
            }

            if (forceRebuild)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
                backgroundPanel = null;
                leftFlagImage = null;
                rightFlagImage = null;
                crossedSwordsImage = null;
                troopComparisonText = null;
                battleProgressSlider = null;
                sliderBackgroundImage = null;
                sliderFillImage = null;
            }

            // 1. Tünd yarım-şəffaf fon paneli
            if (backgroundPanel == null)
            {
                var bgTr = transform.Find("BattlePanelBg");
                GameObject bgGo = bgTr != null ? bgTr.gameObject : new GameObject("BattlePanelBg", typeof(RectTransform));
                if (bgTr == null) bgGo.transform.SetParent(transform, false);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                backgroundPanel = bgGo.GetComponent<Image>() ?? bgGo.AddComponent<Image>();
                backgroundPanel.color = new Color(0.07f, 0.09f, 0.14f, 0.90f);

                var outline = bgGo.GetComponent<Outline>() ?? bgGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.85f, 0.65f, 0.18f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // 2. Sol Bayraq (Hücum edən tərəf)
            if (leftFlagImage == null)
            {
                var tr = transform.Find("LeftFlag");
                GameObject go = tr != null ? tr.gameObject : new GameObject("LeftFlag", typeof(RectTransform));
                if (tr == null) go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(12f, -10f);
                rt.sizeDelta = new Vector2(36f, 24f);

                leftFlagImage = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                leftFlagImage.color = new Color(0.18f, 0.52f, 0.80f, 1f);
                leftFlagImage.raycastTarget = false;

                var ol = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
                ol.effectColor = new Color(0.95f, 0.85f, 0.40f, 0.9f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 3. Sağ Bayraq (Müdafiə olunan tərəf)
            if (rightFlagImage == null)
            {
                var tr = transform.Find("RightFlag");
                GameObject go = tr != null ? tr.gameObject : new GameObject("RightFlag", typeof(RectTransform));
                if (tr == null) go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-12f, -10f);
                rt.sizeDelta = new Vector2(36f, 24f);

                rightFlagImage = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                rightFlagImage.color = new Color(0.80f, 0.22f, 0.20f, 1f);
                rightFlagImage.raycastTarget = false;

                var ol = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
                ol.effectColor = new Color(0.95f, 0.85f, 0.40f, 0.9f);
                ol.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 4. Ortada Toqquşan Qılınclar İkonu
            if (crossedSwordsImage == null)
            {
                var tr = transform.Find("CrossedSwordsIcon");
                GameObject go = tr != null ? tr.gameObject : new GameObject("CrossedSwordsIcon", typeof(RectTransform));
                if (tr == null) go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -6f);
                rt.sizeDelta = new Vector2(30f, 30f);

                crossedSwordsImage = go.GetComponent<Image>() ?? go.AddComponent<Image>();
                crossedSwordsImage.sprite = GetCrossedSwordsSprite();
                crossedSwordsImage.color = Color.white;
                crossedSwordsImage.preserveAspect = true;
                crossedSwordsImage.raycastTarget = false;
            }

            // 5. Ordu Sayı Müqayisəsi Mətni (Bayraqların altında kiçik oxunaqlı yazı)
            if (troopComparisonText == null)
            {
                var tr = transform.Find("TroopComparisonText");
                GameObject go = tr != null ? tr.gameObject : new GameObject("TroopComparisonText", typeof(RectTransform));
                if (tr == null) go.transform.SetParent(transform, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -4f);
                rt.sizeDelta = new Vector2(-16f, 20f);

                troopComparisonText = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
                if (TMP_Settings.defaultFontAsset != null && troopComparisonText.font == null)
                {
                    troopComparisonText.font = TMP_Settings.defaultFontAsset;
                }
                troopComparisonText.text = "0 vs 0";
                troopComparisonText.fontSize = 14f;
                troopComparisonText.fontStyle = FontStyles.Bold;
                troopComparisonText.color = Color.white;
                troopComparisonText.alignment = TextAlignmentOptions.Center;
                troopComparisonText.textWrappingMode = TextWrappingModes.NoWrap;
                troopComparisonText.raycastTarget = false;
            }

            // 6. Alt Hissə - Döyüş Tərəzisi (Battle Progress Slider, mərkəzdən 0.5 başlayır)
            if (battleProgressSlider == null)
            {
                var tr = transform.Find("BattleProgressSlider");
                GameObject sliderGo = tr != null ? tr.gameObject : new GameObject("BattleProgressSlider", typeof(RectTransform));
                if (tr == null) sliderGo.transform.SetParent(transform, false);

                var sRt = sliderGo.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0f);
                sRt.anchorMax = new Vector2(1f, 0f);
                sRt.pivot = new Vector2(0.5f, 0f);
                sRt.anchoredPosition = new Vector2(0f, 10f);
                sRt.sizeDelta = new Vector2(-24f, 16f);

                battleProgressSlider = sliderGo.GetComponent<Slider>() ?? sliderGo.AddComponent<Slider>();
                battleProgressSlider.interactable = false;
                battleProgressSlider.direction = Slider.Direction.LeftToRight;
                battleProgressSlider.minValue = 0f;
                battleProgressSlider.maxValue = 1f;

                // Background (Sağ tərəf - Qırmızı)
                var bgTr = sliderGo.transform.Find("Background");
                GameObject bgGo = bgTr != null ? bgTr.gameObject : new GameObject("Background", typeof(RectTransform));
                if (bgTr == null) bgGo.transform.SetParent(sliderGo.transform, false);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                sliderBackgroundImage = bgGo.GetComponent<Image>() ?? bgGo.AddComponent<Image>();
                sliderBackgroundImage.color = new Color(0.88f, 0.22f, 0.20f, 0.96f); // Qırmızı

                // Fill Area
                var fillAreaTr = sliderGo.transform.Find("Fill Area");
                GameObject fillAreaGo = fillAreaTr != null ? fillAreaTr.gameObject : new GameObject("Fill Area", typeof(RectTransform));
                if (fillAreaTr == null) fillAreaGo.transform.SetParent(sliderGo.transform, false);
                var faRt = fillAreaGo.GetComponent<RectTransform>();
                faRt.anchorMin = Vector2.zero;
                faRt.anchorMax = Vector2.one;
                faRt.offsetMin = Vector2.zero;
                faRt.offsetMax = Vector2.zero;

                // Fill (Sol tərəf - Yaşıl)
                var fillTr = fillAreaGo.transform.Find("Fill");
                GameObject fillGo = fillTr != null ? fillTr.gameObject : new GameObject("Fill", typeof(RectTransform));
                if (fillTr == null) fillGo.transform.SetParent(fillAreaGo.transform, false);
                var fRt = fillGo.GetComponent<RectTransform>();
                fRt.anchorMin = Vector2.zero;
                fRt.anchorMax = Vector2.one;
                fRt.offsetMin = Vector2.zero;
                fRt.offsetMax = Vector2.zero;

                sliderFillImage = fillGo.GetComponent<Image>() ?? fillGo.AddComponent<Image>();
                sliderFillImage.color = new Color(0.18f, 0.80f, 0.44f, 0.96f); // Yaşıl

                // Mərkəz xətti (50/50 işarəsi)
                var markerTr = sliderGo.transform.Find("CenterMarker");
                GameObject markerGo = markerTr != null ? markerTr.gameObject : new GameObject("CenterMarker", typeof(RectTransform));
                if (markerTr == null) markerGo.transform.SetParent(sliderGo.transform, false);
                var mRt = markerGo.GetComponent<RectTransform>();
                mRt.anchorMin = new Vector2(0.5f, 0f);
                mRt.anchorMax = new Vector2(0.5f, 1f);
                mRt.pivot = new Vector2(0.5f, 0.5f);
                mRt.anchoredPosition = Vector2.zero;
                mRt.sizeDelta = new Vector2(2f, 4f);
                var markerImg = markerGo.GetComponent<Image>() ?? markerGo.AddComponent<Image>();
                markerImg.color = Color.white;
                markerImg.raycastTarget = false;

                battleProgressSlider.fillRect = fRt;
                battleProgressSlider.targetGraphic = sliderFillImage;
                battleProgressSlider.value = 0.5f;
            }
        }

        public void Initialize(Color leftFactionColor, Color rightFactionColor, int leftTroops, int rightTroops, string provinceName = "")
        {
            BuildHierarchyIfNeeded();

            if (leftFlagImage != null) leftFlagImage.color = leftFactionColor;
            if (rightFlagImage != null) rightFlagImage.color = rightFactionColor;
            if (crossedSwordsImage != null && crossedSwordsImage.sprite == null)
            {
                crossedSwordsImage.sprite = GetCrossedSwordsSprite();
            }

            UpdateBattleProgress(0.5f, leftTroops, rightTroops);
        }

        /// <summary>
        /// Döyüş tərəzisini (0..1 arası, 0.5 mərkəz) və əsgər saylarını yeniləyir.
        /// Bir tərəf üstünlük qazandıqca onun rəngi (Yaşıl və ya Qırmızı) dominant olur.
        /// </summary>
        public void UpdateBattleProgress(float normalizedLeftRatio, int leftTroops, int rightTroops)
        {
            BuildHierarchyIfNeeded();

            float clamped = Mathf.Clamp01(normalizedLeftRatio);

            if (battleProgressSlider != null)
            {
                battleProgressSlider.value = clamped;
            }

            if (sliderFillImage != null && sliderBackgroundImage != null)
            {
                if (clamped >= 0.5f)
                {
                    // Sol/Oyunçu tərəfi üstünlük qazanır -> Yaşıl dominant
                    sliderFillImage.color = new Color(0.18f, 0.85f, 0.45f, 0.98f);
                    sliderBackgroundImage.color = new Color(0.70f, 0.18f, 0.18f, 0.92f);
                }
                else
                {
                    // Sağ/Düşmən tərəfi üstünlük qazanır -> Qırmızı dominant
                    sliderFillImage.color = new Color(0.16f, 0.65f, 0.35f, 0.90f);
                    sliderBackgroundImage.color = new Color(0.92f, 0.20f, 0.18f, 0.98f);
                }
            }

            if (troopComparisonText != null)
            {
                troopComparisonText.text = $"{FormatShort(leftTroops)}  vs  {FormatShort(rightTroops)}";
            }
        }

        private static string FormatShort(int count)
        {
            if (count <= 0) return "0";
            if (count >= 1000)
            {
                return (count / 1000f).ToString(count % 1000 == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + "k";
            }
            return count.ToString();
        }

        public static Sprite GetCrossedSwordsSprite()
        {
            if (_cachedSwordsSprite != null) return _cachedSwordsSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color bladeColor = new Color(0.92f, 0.95f, 0.98f, 1f);
            Color guardGold = new Color(0.95f, 0.78f, 0.20f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            // İki diaqonal qılınc (\ və /) çəkirik
            for (int y = 8; y < size - 8; y++)
            {
                for (int x = 8; x < size - 8; x++)
                {
                    // Qılınc 1: aşağı-soldan yuxarı-sağa (y == x)
                    float d1 = Mathf.Abs(x - y);
                    // Qılınc 2: aşağı-sağdan yuxarı-sola (x + y == size - 1)
                    float d2 = Mathf.Abs((x + y) - (size - 1));

                    bool onBlade1 = d1 <= 2.2f && x >= 10 && x <= 54 && y >= 10 && y <= 54;
                    bool onBlade2 = d2 <= 2.2f && x >= 10 && x <= 54 && y >= 10 && y <= 54;

                    // Qılıncın qoruyucu dəstəyi (Crossguard)
                    bool onGuard1 = Mathf.Abs((x + y) - 34) <= 1.8f && Mathf.Abs(x - 17) <= 7f;
                    bool onGuard2 = Mathf.Abs((y - x) - (-30)) <= 1.8f && Mathf.Abs(x - 47) <= 7f;

                    if (onGuard1 || onGuard2)
                    {
                        tex.SetPixel(x, y, guardGold);
                    }
                    else if (onBlade1 || onBlade2)
                    {
                        // Qulp hissəsi (aşağı uclar) qızılı, tiyə gümüşü
                        bool isHilt = y < 18;
                        tex.SetPixel(x, y, isHilt ? guardGold : bladeColor);
                    }
                }
            }

            tex.Apply();
            _cachedSwordsSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedSwordsSprite;
        }
    }
}
