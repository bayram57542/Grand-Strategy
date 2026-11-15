using System;
using System.Collections.Generic;
using GrandStrategy.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Faza 9.2 & 9.3 - QGIS-dən gələn coğrafi əyalət poliqonları üçün interaktiv nəzarətçi.
    /// Siçanın üzərinə gəlməsi (Hover / Highlight), klikləmə (Selection),
    /// multi-poliqon (islands/rings) dəstəyi və vizual sərhəd/rəng effektlərini idarə edir.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class MapInteractionController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Əyalət İdentifikatorları")]
        [SerializeField] private int provinceId;
        [SerializeField] private string provinceName;
        [SerializeField] private string adminCountry;
        [SerializeField] private string provinceOwner;
        [SerializeField] private string rulerName;
        [SerializeField] private int factionId;
        [SerializeField] private string climate = "Temperate";

        [Header("Vəziyyət")]
        [SerializeField] private bool isHighlighted;
        [SerializeField] private bool isSelected;
        [SerializeField] private bool isWasteland;

        [Header("Rəng Konfiqurasiyası (Age of History Political Style)")]
        [SerializeField] private Color defaultFillColor = new Color(0.78f, 0.22f, 0.22f, 0.95f);
        [SerializeField] private Color defaultBorderColor = new Color(0.172f, 0.243f, 0.314f, 1f); // #2C3E50 tünd boz/qara
        [SerializeField] private Color highlightBorderColor = new Color(0.957f, 0.816f, 0.247f, 1f); // #F4D03F qızılı işıqlanma
        [SerializeField] private Color selectedBorderColor = new Color(0.945f, 0.769f, 0.059f, 1f);  // #F1C40F parlaq qızılı sərhəd
        [SerializeField] private Color selectedFillColor = new Color(1f, 0.90f, 0.35f, 0.95f);

        [Header("Sərhəd Qalınlığı")]
        [SerializeField] private float defaultBorderWidth = 0.018f;
        [SerializeField] private float highlightBorderWidth = 0.035f;
        [SerializeField] private float selectedBorderWidth = 0.055f;

        [Header("Komponentlər")]
        [SerializeField] private PolygonCollider2D polygonCollider;
        [SerializeField] private LineRenderer borderLineRenderer;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private TextMesh nameLabel;
        [SerializeField] private List<LineRenderer> childBorders = new List<LineRenderer>();
        [SerializeField] private Vector2 visualCentroid = Vector2.zero;

        [Header("Faza 10.3 - Mühasirə və Logistika Statusu")]
        [SerializeField] private bool isBesieged;
        [SerializeField] private bool isExploited;
        [SerializeField] private bool isPlundered;

        public int ProvinceId => provinceId;
        public string ProvinceName => provinceName;
        public string AdminCountry => adminCountry;
        public string ProvinceOwner
        {
            get => !string.IsNullOrEmpty(provinceOwner) ? provinceOwner : adminCountry;
            set => provinceOwner = value;
        }
        public string RulerName
        {
            get
            {
                if (!string.IsNullOrEmpty(rulerName)) return rulerName;
                if (HistoricalScenarioManager.Instance != null)
                {
                    return HistoricalScenarioManager.Instance.GetRulerForFaction(ProvinceOwner);
                }
                return "Məlum deyil";
            }
            set => rulerName = value;
        }
        public int FactionId => factionId;
        public int OwnerFactionId
        {
            get => factionId;
            set => SetOwnerByFactionId(value);
        }
        public string Climate => climate;
        public bool IsHighlighted => isHighlighted;
        public bool IsSelected => isSelected;
        public bool IsWasteland => isWasteland;
        public bool IsBesieged { get => isBesieged; set => SetBesieged(value); }
        public bool IsExploited { get => isExploited; set => isExploited = value; }
        public bool IsPlundered { get => isPlundered; set => isPlundered = value; }

        public void SetBesieged(bool besieged)
        {
            isBesieged = besieged;
            if (besieged)
            {
                // Mühasirəyə alındıqda sərhəd qırmızı-bordo rənglə vurğulanır
                defaultBorderColor = new Color(0.85f, 0.15f, 0.15f, 1f);
                if (borderLineRenderer != null)
                {
                    borderLineRenderer.startColor = defaultBorderColor;
                    borderLineRenderer.endColor = defaultBorderColor;
                }
            }
            else
            {
                defaultBorderColor = new Color(0.172f, 0.243f, 0.314f, 1f);
                ApplyColors();
            }

            if (GrandStrategy.Core.SiegeController.Instance != null && provinceId > 0)
            {
                GrandStrategy.Core.SiegeController.Instance.SetBesieged(provinceId, besieged);
            }
        }
        public Color DefaultFillColor { get => defaultFillColor; set => defaultFillColor = value; }
        public Color DefaultBorderColor { get => defaultBorderColor; set => defaultBorderColor = value; }
        public PolygonCollider2D PolygonCollider => polygonCollider;
        public Vector2 VisualCentroid => visualCentroid;

        /// <summary>
        /// Faza 10.2 - Əyalətin mərkəz nöqtəsini (Centroid) dünya koordinatlarında qaytarır (Orduların yerləşməsi üçün).
        /// </summary>
        public Vector3 GetCentroidWorldPosition()
        {
            if (visualCentroid != Vector2.zero)
            {
                return transform.TransformPoint(visualCentroid);
            }

            Transform lbl = transform.Find("Label");
            if (lbl != null)
            {
                visualCentroid = new Vector2(lbl.localPosition.x, lbl.localPosition.y);
                return transform.TransformPoint(visualCentroid);
            }

            if (polygonCollider != null && polygonCollider.pathCount > 0)
            {
                Vector2[] path = polygonCollider.GetPath(0);
                if (path != null && path.Length > 0)
                {
                    visualCentroid = CalculateCentroid(path);
                    return transform.TransformPoint(visualCentroid);
                }
                return polygonCollider.bounds.center;
            }

            return transform.position;
        }

        /// <summary>
        /// Əyaləti keçilməz / neytral boşluq (Wasteland) kimi təyin edir və rəngləyir.
        /// </summary>
        public void SetWasteland(Color wastelandColor, Color wastelandBorderColor)
        {
            isWasteland = true;
            provinceOwner = "Keçilməz Ərazi / Wasteland";
            rulerName = "Yoxdur";
            factionId = 0;
            defaultFillColor = wastelandColor;
            defaultBorderColor = wastelandBorderColor;
            ApplyColors();
        }

        /// <summary>
        /// Faza 9.6 & 9.8 - 1187 Tarixi Ssenari mənimsədilməsi, hökmdar və vizual rənglənməsi.
        /// </summary>
        public void SetHistoricalOwner(string ownerName, int newFactionId, Color fillColor, string newRulerName = null, Color? borderColor = null)
        {
            isWasteland = (newFactionId <= 0 || string.Equals(ownerName, "Neytral", StringComparison.OrdinalIgnoreCase) || string.Equals(ownerName, "Wasteland", StringComparison.OrdinalIgnoreCase));
            provinceOwner = ownerName;
            factionId = newFactionId;
            defaultFillColor = fillColor;
            if (!string.IsNullOrEmpty(newRulerName))
            {
                rulerName = newRulerName;
            }
            else if (HistoricalScenarioManager.Instance != null)
            {
                rulerName = HistoricalScenarioManager.Instance.GetRulerForFaction(ownerName);
            }
            if (borderColor.HasValue)
            {
                defaultBorderColor = borderColor.Value;
            }
            else if (isWasteland)
            {
                defaultBorderColor = new Color(0.2f, 0.24f, 0.28f, 0.25f);
            }
            ApplyColors();
        }

        /// <summary>
        /// Faza 10.13 - Əyaləti dövlət ID-sinə görə yeni sahibin rənginə və məlumatlarına boyayır (ərazi işğalı).
        /// </summary>
        public void SetOwnerByFactionId(int newFactionId)
        {
            string newOwner = newFactionId == 1 ? "Eldəgəzlər (Atabəylər)" : $"Dövlət #{newFactionId}";
            string newRuler = newFactionId == 1 ? "Qızıl Arslan" : "Hakim";
            Color newColor = newFactionId == 1
                ? new Color(0.122f, 0.380f, 0.553f, 1f) // #1F618D Eldəgəzlər rəngi
                : new Color(0.25f, 0.50f, 0.70f, 1f);

            var dipMgr = GrandStrategy.Core.DiplomacyManager.Instance;
            if (dipMgr != null)
            {
                var f = dipMgr.GetFaction(newFactionId);
                if (f != null)
                {
                    if (!string.IsNullOrEmpty(f.Name)) newOwner = f.Name;
                    if (!string.IsNullOrEmpty(f.RulerName)) newRuler = f.RulerName;
                    if (!string.IsNullOrEmpty(f.ColorHex) && ColorUtility.TryParseHtmlString(f.ColorHex, out Color parsed))
                    {
                        newColor = parsed;
                    }
                }
            }
            else
            {
                var db = GrandStrategy.Database.DatabaseManager.Instance;
                if (db != null && db.IsConnected)
                {
                    var f = db.GetFactionById(newFactionId);
                    if (f != null)
                    {
                        if (!string.IsNullOrEmpty(f.Name)) newOwner = f.Name;
                        if (!string.IsNullOrEmpty(f.RulerName)) newRuler = f.RulerName;
                        if (!string.IsNullOrEmpty(f.ColorHex) && ColorUtility.TryParseHtmlString(f.ColorHex, out Color parsed))
                        {
                            newColor = parsed;
                        }
                    }
                }
            }

            SetHistoricalOwner(newOwner, newFactionId, newColor, newRuler);
            SetBesieged(false);
        }

        public event Action<MapInteractionController> OnProvinceClicked;
        public event Action<MapInteractionController, bool> OnProvinceHovered;

        public static Shader GetDefaultMapShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("UI/Default");
            return shader;
        }

        public static Material CreateDefaultMapMaterial(string name = "ProvinceMaterial")
        {
            Shader shader = GetDefaultMapShader();
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.name = name;
            return mat;
        }

        private void Awake()
        {
            EnsureComponentsAndVisuals();
        }

        private void Start()
        {
            EnsureComponentsAndVisuals();
            ApplyColors();
        }

        /// <summary>
        /// Obyektin bütün vizual komponentlərinin (Mesh, Material, LineRenderer, Collider)
        /// tam və xətasız mövcudluğunu təmin edir (Play mode və ya Səhnə yüklənməsində).
        /// </summary>
        public void EnsureComponentsAndVisuals()
        {
            if (polygonCollider == null)
            {
                polygonCollider = GetComponent<PolygonCollider2D>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (borderLineRenderer == null)
            {
                borderLineRenderer = GetComponent<LineRenderer>();
            }

            // Materialın boş (null və ya çəhrayı/görünməz) olmamasını təmin edirik
            if (meshRenderer != null && (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterial.shader == null))
            {
                meshRenderer.material = CreateDefaultMapMaterial($"{provinceName}_FillMat");
            }

            if (borderLineRenderer != null && (borderLineRenderer.sharedMaterial == null || borderLineRenderer.sharedMaterial.shader == null))
            {
                borderLineRenderer.material = CreateDefaultMapMaterial($"{provinceName}_BorderMat");
            }

            // Əgər səhnə saxlanıb yenidən açıldıqda procedural Mesh itibsə, PolygonCollider2D-dən dərhal bərpa edirik
            if (meshFilter != null && meshFilter.sharedMesh == null && polygonCollider != null && polygonCollider.pathCount > 0)
            {
                try
                {
                    Mesh colMesh = polygonCollider.CreateMesh(false, false);
                    if (colMesh != null && colMesh.vertexCount > 0)
                    {
                        colMesh.name = $"{provinceName}_RestoredMesh";
                        meshFilter.sharedMesh = colMesh;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MapInteractionController] Mesh bərpası xətası ({provinceName}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Tək poliqonlu əyalətlər üçün inisializasiya (Faza 9.2 geri uyğunluq).
        /// </summary>
        public void Initialize(int id, string name, Vector2[] localPoints, Color fillColor, Color borderColor, int facId = 0, string clim = "Temperate")
        {
            var singleList = new List<Vector2[]>();
            if (localPoints != null) singleList.Add(localPoints);
            Initialize(id, name, singleList, fillColor, borderColor, facId, clim, null, false);
        }

        /// <summary>
        /// MultiPoliqon (adalar və çoxsaylı halqalar) dəstəyi ilə inisializasiya (Faza 9.3).
        /// </summary>
        public void Initialize(int id, string name, List<Vector2[]> allRings, Color fillColor, Color borderColor, int facId = 0, string clim = "Temperate", string admin = null, bool createLabels = false)
        {
            provinceId = id;
            provinceName = name;
            adminCountry = admin;
            factionId = facId;
            climate = clim;
            gameObject.name = name;

            defaultFillColor = fillColor;
            defaultBorderColor = borderColor;

            if (polygonCollider == null)
            {
                polygonCollider = GetComponent<PolygonCollider2D>();
                if (polygonCollider == null)
                {
                    polygonCollider = gameObject.AddComponent<PolygonCollider2D>();
                }
            }

            // PolygonCollider2D-nin bütün yollarını (paths) qeyd edirik
            if (allRings != null && allRings.Count > 0)
            {
                polygonCollider.pathCount = allRings.Count;
                for (int p = 0; p < allRings.Count; p++)
                {
                    polygonCollider.SetPath(p, allRings[p]);
                }
            }

            SetupVisuals(allRings, createLabels);
            ApplyColors();
        }

        /// <summary>
        /// Poliqon üçün sərhəd xətləri (LineRenderer), şəffaf doldurma toru (Mesh) və mərkəz adı yaradır.
        /// </summary>
        private void SetupVisuals(List<Vector2[]> allRings, bool createLabels)
        {
            if (allRings == null || allRings.Count == 0) return;

            // Ən böyük halqanı (Mainland) tapırıq
            Vector2[] mainRing = allRings[0];
            int maxCount = mainRing.Length;
            for (int i = 1; i < allRings.Count; i++)
            {
                if (allRings[i].Length > maxCount)
                {
                    mainRing = allRings[i];
                    maxCount = mainRing.Length;
                }
            }

            // 1. Əsas Sərhəd Xətti (LineRenderer)
            if (mainRing.Length >= 3)
            {
                if (borderLineRenderer == null)
                {
                    borderLineRenderer = GetComponent<LineRenderer>();
                    if (borderLineRenderer == null)
                    {
                        borderLineRenderer = gameObject.AddComponent<LineRenderer>();
                    }
                }

                borderLineRenderer.useWorldSpace = false;
                borderLineRenderer.loop = true;
                borderLineRenderer.positionCount = mainRing.Length;
                borderLineRenderer.startWidth = defaultBorderWidth;
                borderLineRenderer.endWidth = defaultBorderWidth;

                // Unity-nin default Sprites/Default materialı mütləq təyin olunur
                borderLineRenderer.material = CreateDefaultMapMaterial($"{provinceName}_BorderMat");

                for (int i = 0; i < mainRing.Length; i++)
                {
                    borderLineRenderer.SetPosition(i, new Vector3(mainRing[i].x, mainRing[i].y, -0.02f));
                }
            }

            // 2. Translucent Mesh Doldurma
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
                if (meshRenderer == null)
                {
                    meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }
            }

            // Unity-nin default Sprites/Default materialı mütləq təyin olunur
            meshRenderer.material = CreateDefaultMapMaterial($"{provinceName}_FillMat");

            Mesh polyMesh = GeneratePolygonMesh(mainRing);
            if (polyMesh != null)
            {
                meshFilter.sharedMesh = polyMesh;
            }

            if (mainRing.Length >= 3)
            {
                visualCentroid = CalculateCentroid(mainRing);
            }

            // 3. Mərkəzi Mətn Etiketi (TextMesh)
            if (createLabels && mainRing.Length >= 3)
            {
                Vector2 centroid = visualCentroid;
                var labelObj = transform.Find("Label");
                if (labelObj == null)
                {
                    var newLabel = new GameObject("Label");
                    newLabel.transform.SetParent(transform, false);
                    newLabel.transform.localPosition = new Vector3(centroid.x, centroid.y, -0.05f);
                    nameLabel = newLabel.AddComponent<TextMesh>();
                    nameLabel.alignment = TextAlignment.Center;
                    nameLabel.anchor = TextAnchor.MiddleCenter;
                    nameLabel.characterSize = 0.05f;
                    nameLabel.fontSize = 28;
                    nameLabel.color = Color.white;
                    nameLabel.text = provinceName;
                }
            }
        }

        public static Vector2 CalculateCentroid(Vector2[] points)
        {
            if (points == null || points.Length == 0) return Vector2.zero;

            float accumulatedArea = 0f;
            float centerX = 0f;
            float centerY = 0f;

            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                float temp = points[j].x * points[i].y - points[i].x * points[j].y;
                accumulatedArea += temp;
                centerX += (points[j].x + points[i].x) * temp;
                centerY += (points[j].y + points[i].y) * temp;
            }

            if (Mathf.Abs(accumulatedArea) < 1E-5f)
            {
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < points.Length; i++) sum += points[i];
                return sum / points.Length;
            }

            accumulatedArea *= 3f;
            return new Vector2(centerX / accumulatedArea, centerY / accumulatedArea);
        }

        private Mesh GeneratePolygonMesh(Vector2[] vertices2D)
        {
            if (vertices2D == null || vertices2D.Length < 3) return null;

            if (polygonCollider != null)
            {
                try
                {
                    Mesh colMesh = polygonCollider.CreateMesh(false, false);
                    if (colMesh != null && colMesh.vertexCount > 0)
                    {
                        return colMesh;
                    }
                }
                catch
                {
                    // Fallback to fan triangulation below
                }
            }

            int n = vertices2D.Length;
            Vector3[] vertices = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(vertices2D[i].x, vertices2D[i].y, 0f);
            }

            int[] triangles = new int[(n - 2) * 3];
            int t = 0;
            for (int i = 1; i < n - 1; i++)
            {
                triangles[t++] = 0;
                triangles[t++] = i;
                triangles[t++] = i + 1;
            }

            Mesh mesh = new Mesh();
            mesh.name = $"{provinceName}_Mesh";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void ApplyColors()
        {
            Color targetBorder = defaultBorderColor;
            Color targetFill = defaultFillColor;
            float targetWidth = defaultBorderWidth;

            if (isSelected)
            {
                targetBorder = isWasteland ? new Color(0.65f, 0.70f, 0.72f, 0.75f) : selectedBorderColor;
                targetFill = isWasteland ? new Color(0.55f, 0.60f, 0.61f, 0.95f) : selectedFillColor;
                targetWidth = selectedBorderWidth;
            }
            else if (isHighlighted)
            {
                targetBorder = isWasteland ? new Color(0.60f, 0.65f, 0.68f, 0.60f) : highlightBorderColor;
                targetFill = isWasteland
                    ? new Color(
                        Mathf.Min(defaultFillColor.r + 0.08f, 1f),
                        Mathf.Min(defaultFillColor.g + 0.08f, 1f),
                        Mathf.Min(defaultFillColor.b + 0.08f, 1f),
                        0.95f)
                    : new Color(
                        Mathf.Min(defaultFillColor.r + 0.15f, 1f),
                        Mathf.Min(defaultFillColor.g + 0.15f, 1f),
                        Mathf.Min(defaultFillColor.b + 0.15f, 1f),
                        0.95f);
                targetWidth = highlightBorderWidth;
            }

            if (borderLineRenderer != null)
            {
                borderLineRenderer.startColor = targetBorder;
                borderLineRenderer.endColor = targetBorder;
                borderLineRenderer.startWidth = targetWidth;
                borderLineRenderer.endWidth = targetWidth;
                if (borderLineRenderer.sharedMaterial == null)
                {
                    borderLineRenderer.material = CreateDefaultMapMaterial($"{provinceName}_BorderMat");
                }
                if (borderLineRenderer.material != null)
                {
                    borderLineRenderer.material.color = targetBorder;
                }
            }

            if (meshRenderer != null)
            {
                if (meshRenderer.sharedMaterial == null)
                {
                    meshRenderer.material = CreateDefaultMapMaterial($"{provinceName}_FillMat");
                }
                if (meshRenderer.material != null)
                {
                    meshRenderer.material.color = targetFill;
                }
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyColors();
        }

        /// <summary>
        /// Faza 9.9 - Kamera yaxınlaşdıqda və ya uzaqlaşdıqda sərhəd qalınlığını dinamik tənzimləyir.
        /// </summary>
        public void SetDynamicBorderWidth(float defaultWidth, float highlightWidth, float selectedWidth)
        {
            defaultBorderWidth = defaultWidth;
            highlightBorderWidth = highlightWidth;
            selectedBorderWidth = selectedWidth;
            ApplyColors();
        }

        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            ApplyColors();
            OnProvinceHovered?.Invoke(this, highlight);
        }

        [Header("Faza 10.5 - 7 Sinifli Demoqrafiya")]
        [SerializeField] private GrandStrategy.Database.Models.ProvinceDemographics demographics = new GrandStrategy.Database.Models.ProvinceDemographics();

        /// <summary>
        /// Faza 10.5 - Əyalətin 7 sinifli demoqrafiyası (ilk müraciətdə bazadakı ümumi əhalidən bölünür).
        /// </summary>
        public GrandStrategy.Database.Models.ProvinceDemographics Demographics
        {
            get
            {
                if (demographics == null || !demographics.IsInitialized)
                {
                    var _ = EconomyData; // bazadan oxuyub demoqrafiyanı inisializasiya edir
                    if (demographics == null) demographics = new GrandStrategy.Database.Models.ProvinceDemographics();
                    if (!demographics.IsInitialized) demographics.InitializeFromPopulation(50000);
                }
                return demographics;
            }
        }

        /// <summary>
        /// Demoqrafiyanı bazadakı ümumi əhaliyə görə yenidən bölür (test və ssenari sıfırlaması üçün).
        /// </summary>
        public void ResetDemographics()
        {
            if (demographics == null) demographics = new GrandStrategy.Database.Models.ProvinceDemographics();
            demographics.IsInitialized = false;
            var _ = EconomyData;
        }

        private GrandStrategy.Database.Models.ProvinceEconomyData _cachedEconomyData;
        public GrandStrategy.Database.Models.ProvinceEconomyData EconomyData
        {
            get
            {
                if (isWasteland)
                {
                    if (_cachedEconomyData == null)
                    {
                        _cachedEconomyData = new GrandStrategy.Database.Models.ProvinceEconomyData
                        {
                            ProvinceId = provinceId,
                            ProvinceName = provinceName,
                            TotalPopulation = 0,
                            GoldIncome = 0f,
                            FoodProduction = 0f,
                            IronProduction = 0f,
                            DevelopmentLevel = 0
                        };
                    }
                    else
                    {
                        _cachedEconomyData.TotalPopulation = 0;
                        _cachedEconomyData.GoldIncome = 0f;
                        _cachedEconomyData.FoodProduction = 0f;
                        _cachedEconomyData.IronProduction = 0f;
                        _cachedEconomyData.DevelopmentLevel = 0;
                    }
                    return _cachedEconomyData;
                }

                var db = GrandStrategy.Database.DatabaseManager.Instance != null
                    ? GrandStrategy.Database.DatabaseManager.Instance
                    : UnityEngine.Object.FindAnyObjectByType<GrandStrategy.Database.DatabaseManager>();
                if (db != null)
                {
                    _cachedEconomyData = db.GetProvinceEconomyData(provinceId, provinceName, adminCountry);
                }

                // Faza 10.5: İstehsal 7 sinifli demoqrafiyadan və Xalqın Moralından dinamik hesablanır
                if (_cachedEconomyData != null)
                {
                    if (demographics == null) demographics = new GrandStrategy.Database.Models.ProvinceDemographics();
                    if (!demographics.IsInitialized)
                    {
                        demographics.InitializeFromPopulation(_cachedEconomyData.TotalPopulation);
                    }
                    demographics.ApplyToEconomy(_cachedEconomyData);
                }

                // Faza 10.3: Mühasirədə olan əyalətin mərkəz dövlətlə əlaqəsi kəsilir, aylıq qızıl gəliri 0 olur
                if (_cachedEconomyData != null)
                {
                    if (isBesieged)
                    {
                        _cachedEconomyData.GoldIncome = 0f;
                    }
                    if (isPlundered)
                    {
                        _cachedEconomyData.DevelopmentLevel = 1;
                        _cachedEconomyData.FoodProduction *= 0.2f;
                        _cachedEconomyData.GoldIncome = 0f;
                    }
                }

                return _cachedEconomyData;
            }
        }

        public void SelectProvince()
        {
            SetSelected(true);
            OnProvinceClicked?.Invoke(this);

            if (isWasteland)
            {
                Debug.Log($"[MapInteractionController] Keçilməz ərazi seçildi: #{provinceId} - {provinceName} (Wasteland)");
            }
            else
            {
                string ownerText = !string.IsNullOrEmpty(provinceOwner) ? provinceOwner : adminCountry;
                string adminInfo = !string.IsNullOrEmpty(ownerText) ? $" [{ownerText}]" : "";
                var eco = EconomyData;
                string ecoInfo = eco != null
                    ? $" (Əhali: {eco.TotalPopulation:N0}, Qızıl: +{eco.GoldIncome:F0}/ay, Ərzaq: +{eco.FoodProduction:F0}/ay, Dəmir: +{eco.IronProduction:F0}/ay)"
                    : "";

                Debug.Log($"[MapInteractionController] Əyalət seçildi: #{provinceId} - {provinceName}{adminInfo}{ecoInfo}");
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowProvincePanel(this);
            }
        }

        /// <summary>
        /// Faza 10.12 - Bu əyalətdə düşmən orduları arasında döyüş olub-olmadığını yoxlayır.
        /// </summary>
        public GrandStrategy.Core.CombatManager.ActiveBattleData CheckProvinceCombat(GrandStrategy.Military.ArmyVisualController arrivingArmy = null)
        {
            return GrandStrategy.Core.CombatManager.EnsureInstance().CheckForBattle(provinceId, arrivingArmy);
        }

        private void OnMouseEnter()
        {
            SetHighlight(true);
        }

        private void OnMouseExit()
        {
            SetHighlight(false);
        }

        private void OnMouseDown()
        {
            SelectProvince();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlight(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlight(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SelectProvince();
        }
    }
}
