using System;
using GrandStrategy.Models;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GrandStrategy.Military
{
    /// <summary>
    /// Faza 10.2 & 10.3 - Xəritə üzərindəki ordu və komandir birliyinin 2D vizual nəzarətçisi.
    /// Komandir ikonu (SpriteRenderer), dövlət rəngi, World Space Canvas və
    /// yarı-şəffaf tünd fon üzərində ordunun qısa sayını ("35k", "15k", "50k") göstərən TextMeshPro elementlərini idarə edir.
    /// Faza 10.3: Seçilmə halqası (SelectionRing), hərəkət idarəedicisi (ArmyMovementController) və klik interaksiyası.
    /// </summary>
    public class ArmyVisualController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Ordu Modeli")]
        [SerializeField] private CommanderArmy armyData;

        [Header("Vizual Komponentlər")]
        [SerializeField] private SpriteRenderer commanderIconRenderer;
        [SerializeField] private SpriteRenderer selectionRingRenderer;
        [SerializeField] private Canvas countCanvas;
        [SerializeField] private Image badgeBackground;
        [SerializeField] private TextMeshProUGUI troopCountText;
        [SerializeField] private TextMesh troopCountTextMesh; // 3D TextMesh fallback
        [SerializeField] private Text troopCountLegacyText;    // UGUI Text fallback
        [SerializeField] private CircleCollider2D armyCollider;

        [Header("Tənzimləmələr")]
        [SerializeField] private float zOffset = -2.0f;
        [SerializeField] private bool isSelected;

        private ArmyMovementController _movementController;

        public CommanderArmy ArmyData => armyData;
        public SpriteRenderer CommanderIconRenderer => commanderIconRenderer;
        public SpriteRenderer SelectionRingRenderer => selectionRingRenderer;
        public Canvas CountCanvas => countCanvas;
        public Image BadgeBackground => badgeBackground;
        public TextMeshProUGUI TroopCountText => troopCountText;
        public float ZOffset => zOffset;
        public bool IsSelected => isSelected;
        public ArmyMovementController MovementController => _movementController != null ? _movementController : (_movementController = GetComponent<ArmyMovementController>());

        /// <summary>
        /// Mövcud mətndə göstərilən əsgər sayının qısa yazılışı ("35k", "15k" və s.).
        /// </summary>
        public string DisplayedTroopText
        {
            get
            {
                if (troopCountText != null && !string.IsNullOrEmpty(troopCountText.text))
                    return troopCountText.text;
                if (troopCountTextMesh != null && !string.IsNullOrEmpty(troopCountTextMesh.text))
                    return troopCountTextMesh.text;
                if (troopCountLegacyText != null && !string.IsNullOrEmpty(troopCountLegacyText.text))
                    return troopCountLegacyText.text;
                return armyData != null ? armyData.FormattedTroopCount : string.Empty;
            }
        }

        public void Initialize(CommanderArmy data, Vector3 worldPosition)
        {
            armyData = data;

            // Xəritə poliqonlarının həmişə üzərində görünməsi üçün Z = -2f təyin edilir
            transform.position = new Vector3(worldPosition.x, worldPosition.y, zOffset);

            ApplyData();
        }

        public void ApplyData()
        {
            if (armyData == null) return;

            gameObject.name = $"Army_{armyData.CommanderName.Replace(" ", "")}";

            // 1. Komandir İkonu və Fraksiya Rəngi
            if (commanderIconRenderer != null)
            {
                EnsureSpriteMaterial(commanderIconRenderer);
                if (commanderIconRenderer.sprite == null)
                {
                    commanderIconRenderer.sprite = LoadShieldSprite();
                }
                commanderIconRenderer.color = armyData.FactionColor;
                commanderIconRenderer.sortingOrder = 50; // Xəritə səthindən qat-qat öndə
            }

            // 2. Ordu Sayı Mətni ("35k", "15k", "50k")
            string formattedCount = armyData.FormattedTroopCount;
            if (troopCountText != null)
            {
                troopCountText.text = formattedCount;
            }
            if (troopCountTextMesh != null)
            {
                troopCountTextMesh.text = formattedCount;
            }
            if (troopCountLegacyText != null)
            {
                troopCountLegacyText.text = formattedCount;
            }

            // 3. Yarı-şəffaf qara fon (Badge)
            if (badgeBackground != null)
            {
                badgeBackground.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionRingRenderer != null)
            {
                selectionRingRenderer.gameObject.SetActive(selected);
            }

            // Seçildikdə ordunun miqyası vizual olaraq azca böyüyür
            transform.localScale = selected ? Vector3.one * 1.12f : Vector3.one;

            if (MovementController != null && MovementController.IsSelected != selected)
            {
                MovementController.SetSelected(selected);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (ArmyManager.Instance != null)
                {
                    ArmyManager.Instance.SelectArmy(this);
                }
            }
        }

        private void OnMouseDown()
        {
            if (ArmyManager.Instance != null)
            {
                ArmyManager.Instance.SelectArmy(this);
            }
        }

        public void SetTroopCount(int newCount)
        {
            if (armyData != null)
            {
                armyData.TroopCount = newCount;
                ApplyData();
            }
        }

        public void MoveToProvince(int newProvinceId, string newProvinceName, Vector3 targetWorldPos)
        {
            if (armyData != null)
            {
                armyData.CurrentProvinceId = newProvinceId;
                armyData.CurrentProvinceName = newProvinceName;
            }
            transform.position = new Vector3(targetWorldPos.x, targetWorldPos.y, zOffset);
        }

        /// <summary>
        /// Ordu vizual obyektini həm prefabdan, həm də sıfırdan procedural olaraq quran universal fabrik metodu.
        /// </summary>
        public static ArmyVisualController CreateArmyView(
            CommanderArmy data,
            Vector3 worldPosition,
            Transform parent = null,
            Sprite commanderSprite = null,
            GameObject prefab = null)
        {
            if (data == null) return null;

            GameObject rootObj;
            ArmyVisualController visual;

            if (prefab != null)
            {
                rootObj = UnityEngine.Object.Instantiate(prefab, parent);
                visual = rootObj.GetComponent<ArmyVisualController>();
                if (visual == null) visual = rootObj.AddComponent<ArmyVisualController>();
            }
            else
            {
                // Procedural Hierarchy Yaradılması:
                rootObj = new GameObject($"Army_{data.CommanderName.Replace(" ", "")}");
                if (parent != null) rootObj.transform.SetParent(parent, false);

                visual = rootObj.AddComponent<ArmyVisualController>();

                // 1. Seçim Halqası (SelectionRing)
                var selObj = new GameObject("SelectionRing");
                selObj.transform.SetParent(rootObj.transform, false);
                selObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                selObj.transform.localScale = new Vector3(0.50f, 0.50f, 1f);

                var selSr = selObj.AddComponent<SpriteRenderer>();
                EnsureSpriteMaterial(selSr);
                selSr.sprite = LoadSelectionSprite();
                selSr.color = new Color(0.95f, 0.82f, 0.15f, 0.95f); // Qızılı sarı
                selSr.sortingOrder = 49;
                selObj.SetActive(false);
                visual.selectionRingRenderer = selSr;

                // 2. Mərkəzi Komandir İkonu (SpriteRenderer)
                var iconObj = new GameObject("CommanderIcon");
                iconObj.transform.SetParent(rootObj.transform, false);
                iconObj.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                iconObj.transform.localScale = new Vector3(0.35f, 0.35f, 1f);

                var sr = iconObj.AddComponent<SpriteRenderer>();
                EnsureSpriteMaterial(sr);
                sr.sprite = commanderSprite != null ? commanderSprite : LoadShieldSprite();
                sr.color = data.FactionColor;
                sr.sortingOrder = 50;
                visual.commanderIconRenderer = sr;

                // 3. İkonun Düz Altında World Space Canvas
                var canvasObj = new GameObject("CountBadgeCanvas");
                canvasObj.transform.SetParent(rootObj.transform, false);
                canvasObj.transform.localPosition = new Vector3(0f, -0.16f, 0f);
                canvasObj.transform.localScale = new Vector3(0.0035f, 0.0035f, 1f);

                var canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 60;
                visual.countCanvas = canvas;

                var rectCanvas = canvasObj.GetComponent<RectTransform>();
                if (rectCanvas != null)
                {
                    rectCanvas.sizeDelta = new Vector2(90f, 32f);
                }

                // 4. Yarı-şəffaf Qara Fon (Badge Image)
                var badgeObj = new GameObject("BadgeBackground");
                badgeObj.transform.SetParent(canvasObj.transform, false);
                var badgeRect = badgeObj.AddComponent<RectTransform>();
                badgeRect.anchorMin = Vector2.zero;
                badgeRect.anchorMax = Vector2.one;
                badgeRect.offsetMin = Vector2.zero;
                badgeRect.offsetMax = Vector2.zero;

                var badgeImg = badgeObj.AddComponent<Image>();
                badgeImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);
                visual.badgeBackground = badgeImg;

                // 5. Əsgər Sayı Mətni (TextMeshProUGUI və ya Fallback)
                var textObj = new GameObject("TroopCountText");
                textObj.transform.SetParent(canvasObj.transform, false);
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                try
                {
                    var tmp = textObj.AddComponent<TextMeshProUGUI>();
                    tmp.text = data.FormattedTroopCount;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.fontStyle = FontStyles.Bold;
                    tmp.fontSize = 20;
                    tmp.color = Color.white;
                    tmp.raycastTarget = false;
                    visual.troopCountText = tmp;
                }
                catch
                {
                    var legacyText = textObj.AddComponent<Text>();
                    legacyText.text = data.FormattedTroopCount;
                    legacyText.alignment = TextAnchor.MiddleCenter;
                    legacyText.fontStyle = UnityEngine.FontStyle.Bold;
                    legacyText.fontSize = 18;
                    legacyText.color = Color.white;
                    visual.troopCountLegacyText = legacyText;
                }

                // 6. Klik üçün 2D Collider
                var col = rootObj.AddComponent<CircleCollider2D>();
                col.radius = 0.35f;
                col.offset = new Vector2(0f, 0.05f);
                visual.armyCollider = col;

                // 7. Sərbəst Hərəkət İdarəedicisi (ArmyMovementController)
                var movement = rootObj.AddComponent<ArmyMovementController>();
                visual._movementController = movement;
            }

            // Hər iki halda da ArmyMovementController-in mövcudluğunu təmin edirik
            if (visual.GetComponent<ArmyMovementController>() == null)
            {
                visual._movementController = visual.gameObject.AddComponent<ArmyMovementController>();
            }

            visual.Initialize(data, worldPosition);
            return visual;
        }

        private static Material _cachedSpriteMaterial;

        public static void EnsureSpriteMaterial(SpriteRenderer sr)
        {
            if (sr == null) return;

            if (sr.sharedMaterial != null && sr.sharedMaterial.shader != null)
            {
                string sName = sr.sharedMaterial.shader.name;
                if (sName.Contains("Sprite") || sName.Contains("Unlit"))
                {
                    return;
                }
            }

            if (_cachedSpriteMaterial == null)
            {
#if UNITY_EDITOR
                _cachedSpriteMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
#endif
                if (_cachedSpriteMaterial == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                    ?? Shader.Find("Sprites/Default")
                                    ?? Shader.Find("Unlit/Transparent");
                    if (shader != null)
                    {
                        _cachedSpriteMaterial = new Material(shader);
                    }
                }
            }

            if (_cachedSpriteMaterial != null)
            {
                sr.sharedMaterial = _cachedSpriteMaterial;
            }
        }

        public static Sprite LoadShieldSprite()
        {
#if UNITY_EDITOR
            var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI_Assets/ArmyShieldIcon.png");
            if (sp != null) return sp;
            sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI_Assets/ShieldIcon.png");
            if (sp != null) return sp;
            var knob = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (knob != null) return knob;
#endif
            return CreateProceduralShieldSprite();
        }

        private static Sprite LoadSelectionSprite()
        {
#if UNITY_EDITOR
            string ringPath = "Assets/UI_Assets/SelectionCircle.png";
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ringPath);
            if (sp != null) return sp;
#endif
            return CreateProceduralShieldSprite();
        }

        /// <summary>
        /// Layihədə xüsusi qalxan spriti tapılmadıqda işləyən yüksək dəqiqlikli qalxan spriti.
        /// </summary>
        public static Sprite CreateProceduralShieldSprite()
        {
            const int width = 64;
            const int height = 64;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color transparent = new Color(0, 0, 0, 0);
            Color solid = Color.white;

            for (int y = 0; y < height; y++)
            {
                float ny = (float)y / height; // 0 to 1
                for (int x = 0; x < width; x++)
                {
                    float nx = Mathf.Abs((x - width * 0.5f) / (width * 0.5f)); // 0 at center, 1 at edge
                    bool inside = false;

                    if (ny >= 0.45f)
                    {
                        // Üst düzbucaqlı hissə
                        inside = nx <= 0.82f;
                    }
                    else
                    {
                        // Aşağı sivri ucluq hissə (qalxan burnu)
                        float limit = 0.82f * (ny / 0.45f);
                        inside = nx <= limit;
                    }

                    tex.SetPixel(x, y, inside ? solid : transparent);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
