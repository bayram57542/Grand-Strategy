using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Faza 9.2 & 9.3 - QGIS və Unity İnteqrasiyası: Real GIS Xəritə və Avtomatik Poliqon Generatoru (GISMapLoader).
    /// QGIS-dən ixrac edilmiş real dünya koordinatlı (Yaxın Şərq, Anadolu, Balkanlar və Qafqaz)
    /// GeoJSON fayllarını (provinces_real.json) sürətli şəkildə təhlil edir,
    /// yüzlərlə əyalətin sərhədlərini PolygonCollider2D və MapInteractionController ilə formalaşdırır.
    /// </summary>
    public class GISMapLoader : MonoBehaviour
    {
        private static GISMapLoader _instance;
        public static GISMapLoader Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<GISMapLoader>());

        [Header("GeoJSON Mənbə Faylı")]
        [Tooltip("StreamingAssets daxilindəki GeoJSON faylının adı (Real xəritə üçün: provinces_real.json)")]
        [SerializeField] private string geoJsonFileName = "provinces_real.json";

        [Tooltip("Oyun başladıqda avtomatik yüklənsinmi?")]
        [SerializeField] private bool loadOnStart = true;

        [Header("Xəritə Miqyası və Mərkəzləşdirmə")]
        [Tooltip("Coğrafi dərəcələrin Unity 2D vahidlərinə çevrilmə miqyası (Bütün xəritəni ekrana sığışdırmaq üçün: 0.30 - 0.35)")]
        [SerializeField] private float coordinateScale = 0.35f;

        [Tooltip("Bütün poliqonların həndəsi mərkəzini (0,0) koordinatına gətirsin")]
        [SerializeField] private bool autoCenter = true;

        [Tooltip("autoCenter söndürüldükdə istifadə olunacaq xüsusi mərkəz dərəcələri (Lon, Lat)")]
        [SerializeField] private Vector2 customCenter = new Vector2(41.1f, 38.35f);

        [Tooltip("Xəritə yarandıqdan sonra kameranı avtomatik xəritənin mərkəzinə və ölçüsünə uyğunlaşdırsın")]
        [SerializeField] private bool autoFitCamera = true;

        [Header("Vizual Seçimlər")]
        [Tooltip("Hər əyalətin üzərində 3D mətn etiketi yaradılsınmı (Çox sayda əyalətdə performansı qorumaq üçün default false)")]
        [SerializeField] private bool createLabels = false;

        [Header("Age of History Vizual Tənzimləmələri")]
        [Tooltip("Klassik Age of History dəniz/okean canlı arxa fon rəngi (#2A6B9B)")]
        [SerializeField] private Color oceanBackgroundColor = new Color(0.165f, 0.420f, 0.608f, 1f); // #2A6B9B Canlı Okean Mavisi

        [Tooltip("İncə əyalət sərhəd rəngi (#2C3E50 tünd boz/qara)")]
        [SerializeField] private Color borderColor = new Color(0.172f, 0.243f, 0.314f, 1f); // #2C3E50

        [Tooltip("Siyasi xəritə doldurma rənglərinin şəffaflığı (Solid color: 0.95)")]
        [SerializeField] private float fillAlpha = 0.95f;

        [Header("Terra Incognita (Arxa Plan Materikləri)")]
        [Tooltip("Kəşf edilməmiş arxa plan torpaqlarının (Terra Incognita) unlit rəngi (#A69E8D)")]
        [SerializeField] private Color terraIncognitaColor = new Color(0.651f, 0.620f, 0.553f, 1f); // #A69E8D Köhnə kağız/qum

        [Tooltip("Arxa plan materiklərinin Z dərinlik koordinatı (Z = 0.5f, playable əsas əyalətlərin arxasında)")]
        [SerializeField] private float backgroundZOffset = 0.5f;

        [Tooltip("Arxa plan GeoJSON faylının standart yolu və ya adı")]
        [SerializeField] private string backgroundGeoJsonPath = "Assets/Database/world_background.json";

        [Tooltip("Xəritə yükləndikdə arxa plan materikləri avtomatik axtarılsın və yüklənsin")]
        [SerializeField] private bool autoLoadBackground = true;

        [Tooltip("Arxa plan materiklərinin toplanacağı konteyner Transform")]
        [SerializeField] private Transform backgroundContainer;

        [SerializeField] private List<GameObject> spawnedBackgroundLandmasses = new List<GameObject>();

        private Material _terraIncognitaMaterial;

        [Header("Konteyner və Aktiv Əyalətlər")]
        [Tooltip("Yaradılan əyalət poliqonlarının yerləşdiriləcəyi ana Transform")]
        [SerializeField] private Transform provincesContainer;

        [SerializeField] private List<MapInteractionController> spawnedProvinces = new List<MapInteractionController>();

        private MapInteractionController _currentSelectedProvince;
        private Vector2 _computedCenter = Vector2.zero;
        private Vector2 _geoBoundsMin = Vector2.zero;
        private Vector2 _geoBoundsMax = Vector2.zero;
        private Bounds _unityBounds = new Bounds();

        public IReadOnlyList<MapInteractionController> SpawnedProvinces => spawnedProvinces;
        public IReadOnlyList<GameObject> SpawnedBackgroundLandmasses => spawnedBackgroundLandmasses;
        public Color TerraIncognitaColor { get => terraIncognitaColor; set => terraIncognitaColor = value; }
        public float BackgroundZOffset { get => backgroundZOffset; set => backgroundZOffset = value; }
        public string BackgroundGeoJsonPath { get => backgroundGeoJsonPath; set => backgroundGeoJsonPath = value; }
        public bool AutoLoadBackground { get => autoLoadBackground; set => autoLoadBackground = value; }
        public Transform BackgroundContainer => backgroundContainer;
        public MapInteractionController CurrentSelectedProvince => _currentSelectedProvince;
        public string GeoJsonFileName { get => geoJsonFileName; set => geoJsonFileName = value; }
        public float CoordinateScale => coordinateScale;
        public Vector2 ComputedCenter => _computedCenter;
        public Vector2 GeoBoundsMin => _geoBoundsMin;
        public Vector2 GeoBoundsMax => _geoBoundsMax;
        public Bounds UnityBounds => _unityBounds;

        public event Action<List<MapInteractionController>> OnMapLoaded;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureCameraZ();
        }

        private void Start()
        {
            ApplyOceanBackgroundColor();
            EnsureCameraZ();

            if (loadOnStart)
            {
                Debug.Log($"[GISMapLoader] Start() çağırıldı. Xəritə birbaşa generasiya olunur ({geoJsonFileName})...");
                LoadMap();
            }

            if (autoLoadBackground)
            {
                LoadBackgroundLandmass();
            }
        }

        /// <summary>
        /// Xəritənin generasiya funksiyası. Həm Start()-da, həm də Context Menu vasitəsilə birbaşa çağırılır.
        /// </summary>
        [ContextMenu("Generate Map Now")]
        public List<MapInteractionController> LoadMap()
        {
            return LoadFromStreamingAssets(geoJsonFileName);
        }

        /// <summary>
        /// Inspector üzərindən sağ klikləyərək xəritəni anında yaratmaq üçün Editor metodu.
        /// </summary>
        [ContextMenu("Generate Map Now")]
        public void GenerateMapNow()
        {
            LoadMap();
        }

        /// <summary>
        /// Kameranın Z koordinatının mütləq -10f (xəritədən geridə) olmasını təmin edir.
        /// </summary>
        public void EnsureCameraZ()
        {
            Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 pos = cam.transform.position;
                if (pos.z >= 0f || Mathf.Abs(pos.z - (-10f)) > 0.001f)
                {
                    cam.transform.position = new Vector3(pos.x, pos.y, -10f);
                    Debug.Log($"[GISMapLoader] Kamera Z koordinatı -10f təyin edildi.");
                }
            }
        }

        /// <summary>
        /// StreamingAssets qovluğundakı GeoJSON faylını oxuyur və real əyalət poliqonlarını formalaşdırır.
        /// </summary>
        public List<MapInteractionController> LoadFromStreamingAssets(string fileName = null)
        {
            string targetFile;
            if (string.IsNullOrEmpty(fileName))
            {
                // Əgər parametr verilməyibsə və sahədə köhnə test faylı qalıbsa, real fayla yönləndiririk
                if (string.Equals(geoJsonFileName, "provinces_test.json", StringComparison.OrdinalIgnoreCase))
                {
                    string realCandidate = ResolveGeoJsonFilePath("provinces_real.json");
                    if (!string.IsNullOrEmpty(realCandidate) && File.Exists(realCandidate))
                    {
                        geoJsonFileName = "provinces_real.json";
                    }
                }
                targetFile = geoJsonFileName;
            }
            else
            {
                targetFile = fileName;
            }

            string fullPath = ResolveGeoJsonFilePath(targetFile);

            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                Debug.LogError($"[GISMapLoader] GeoJSON faylı heç bir yerdə tapılmadı: {targetFile}");
                return new List<MapInteractionController>();
            }

            Debug.Log($"[GISMapLoader] GeoJSON faylı oxunur: {fullPath}");
            string jsonContent = File.ReadAllText(fullPath);
            return LoadGeoJson(jsonContent);
        }

        private string ResolveGeoJsonFilePath(string fileName)
        {
            // 1. StreamingAssets daxilində tam ad
            string p1 = Path.Combine(Application.streamingAssetsPath, fileName);
            if (File.Exists(p1)) return p1;

            // 2. .geojson uzantısı ilə
            if (!fileName.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase))
            {
                string p2 = Path.Combine(Application.streamingAssetsPath, fileName + ".geojson");
                if (File.Exists(p2)) return p2;
            }

            // 3. Application.dataPath StreamingAssets fallback
            string p3 = Path.Combine(Application.dataPath, "StreamingAssets", fileName);
            if (File.Exists(p3)) return p3;

            if (!fileName.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase))
            {
                string p4 = Path.Combine(Application.dataPath, "StreamingAssets", fileName + ".geojson");
                if (File.Exists(p4)) return p4;
            }

            return null;
        }

        /// <summary>
        /// Verilmiş GeoJSON mətnini yüksək sürətlə təhlil edir və səhnədə yüzlərlə əyalət poliqonunu yaradır.
        /// </summary>
        public List<MapInteractionController> LoadGeoJson(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogError("[GISMapLoader] GeoJSON mətni boşdur!");
                return new List<MapInteractionController>();
            }

            ClearSpawnedProvinces();
            EnsureContainer();

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 1. GeoJSON Məlumatlarını Yüksək Sürətli Parser ilə Parsing edirik
            List<GeoFeatureData> features = ParseGeoJsonFast(jsonContent);
            if (features == null || features.Count == 0)
            {
                Debug.LogError("[GISMapLoader] GeoJSON faylında heç bir Feature tapılmadı!");
                return new List<MapInteractionController>();
            }

            // 2. Qlobal Bounding Box və Mərkəzin Hesablanması
            CalculateBoundingBox(features);
            Vector2 mapCenter = autoCenter ? _computedCenter : customCenter;

            Debug.Log($"[GISMapLoader] {features.Count} Feature oxundu. Xəritə Mərkəzi: Lon={mapCenter.x:F3}, Lat={mapCenter.y:F3} | Miqyas: {coordinateScale}");

            Bounds totalUnityBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool boundsInitialized = false;

            // 3. Hər bir Feature üçün GameObject, PolygonCollider2D və MapInteractionController yaradılır
            for (int f = 0; f < features.Count; f++)
            {
                var feat = features[f];
                if (feat.Rings == null || feat.Rings.Count == 0) continue;

                // Coğrafi dərəcələri Unity 2D koordinatlarına çeviririk
                var unityRings = new List<Vector2[]>(feat.Rings.Count);
                for (int r = 0; r < feat.Rings.Count; r++)
                {
                    var geoRing = feat.Rings[r];
                    if (geoRing.Count < 3) continue;

                    Vector2[] pts = new Vector2[geoRing.Count];
                    for (int i = 0; i < geoRing.Count; i++)
                    {
                        Vector2 g = geoRing[i];
                        float ux = (g.x - mapCenter.x) * coordinateScale;
                        float uy = (g.y - mapCenter.y) * coordinateScale;
                        pts[i] = new Vector2(ux, uy);

                        if (!boundsInitialized)
                        {
                            totalUnityBounds = new Bounds(new Vector3(ux, uy, 0f), Vector3.zero);
                            boundsInitialized = true;
                        }
                        else
                        {
                            totalUnityBounds.Encapsulate(new Vector3(ux, uy, 0f));
                        }
                    }
                    unityRings.Add(pts);
                }

                if (unityRings.Count == 0) continue;

                // Səhnədə unikal GameObject adı yaradırıq
                string goName = !string.IsNullOrEmpty(feat.Name) ? feat.Name : $"Province_{feat.ProvinceId}";
                GameObject provObj = new GameObject(goName);
                provObj.transform.SetParent(provincesContainer, false);
                provObj.transform.localPosition = Vector3.zero;

                // PolygonCollider2D komponenti
                PolygonCollider2D collider = provObj.AddComponent<PolygonCollider2D>();
                collider.pathCount = unityRings.Count;
                for (int p = 0; p < unityRings.Count; p++)
                {
                    collider.SetPath(p, unityRings[p]);
                }

                // Age of History: Dövlətlərə görə unikal siyasi rəngləmə (Solid color)
                Color fillColor = feat.CustomFillColor.HasValue
                    ? feat.CustomFillColor.Value
                    : GetCountryColor(feat.AdminCountry, feat.Name, feat.ProvinceId);

                Color borderC = feat.CustomBorderColor.HasValue
                    ? feat.CustomBorderColor.Value
                    : borderColor;

                // MapInteractionController əlavə edirik
                MapInteractionController interaction = provObj.AddComponent<MapInteractionController>();
                interaction.Initialize(
                    feat.ProvinceId,
                    feat.Name,
                    unityRings,
                    fillColor,
                    borderC,
                    feat.FactionId,
                    feat.Climate,
                    feat.AdminCountry,
                    createLabels
                );

                interaction.OnProvinceClicked += HandleProvinceClicked;
                spawnedProvinces.Add(interaction);
            }

            _unityBounds = totalUnityBounds;
            stopwatch.Stop();

            Debug.Log($"[GISMapLoader] Real GIS Xəritəsi Hazırdır: {spawnedProvinces.Count} əyalət {stopwatch.ElapsedMilliseconds} ms ərzində yaradıldı! " +
                      $"Xəritə Ölçüləri: En={_unityBounds.size.x:F2}, Hündürlük={_unityBounds.size.y:F2}, Mərkəz=({_unityBounds.center.x:F2}, {_unityBounds.center.y:F2})");

            // 4. Kameranı Xəritəyə Uyğunlaşdırma
            if (autoFitCamera)
            {
                FitCameraToMap();
            }

            // 5. Tarixi Ssenari İnteqrasiyası (Faza 9.6: 1187-ci il Ssenarisi)
            var scenarioMgr = HistoricalScenarioManager.Instance;
            if (scenarioMgr != null)
            {
                scenarioMgr.Apply1187Scenario(spawnedProvinces);
            }

            OnMapLoaded?.Invoke(spawnedProvinces);
            return spawnedProvinces;
        }

        /// <summary>
        /// Kameranı xəritənin mərkəzinə və ölçüsünə avtomatik fokuslayır.
        /// </summary>
        public void FitCameraToMap()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null || !cam.orthographic) return;

            cam.transform.position = new Vector3(_unityBounds.center.x, _unityBounds.center.y, -10f);

            float aspect = cam.aspect > 0.01f ? cam.aspect : (16f / 9f);
            float requiredHeightForWidth = (_unityBounds.size.x / aspect) * 0.5f;
            float requiredHeightForHeight = _unityBounds.size.y * 0.5f;

            float targetSize = Mathf.Max(requiredHeightForHeight, requiredHeightForWidth) + 0.8f;
            cam.orthographicSize = Mathf.Max(targetSize, 5f);
            ApplyOceanBackgroundColor();

            Debug.Log($"[GISMapLoader] Kamera fokuslandı: Mövqe=({cam.transform.position.x:F2}, {cam.transform.position.y:F2}), OrthographicSize={cam.orthographicSize:F2}");
        }

        /// <summary>
        /// Səhnədəki Main Camera-nın arxa fonunu klassik Age of History dəniz rənginə (#71A5C6) boyayır.
        /// </summary>
        public void ApplyOceanBackgroundColor()
        {
            Camera cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = oceanBackgroundColor;
                Debug.Log($"[GISMapLoader] Arxa fon dəniz rəngi tətbiq edildi: #{ColorUtility.ToHtmlStringRGB(oceanBackgroundColor)}");
            }
        }

        private void EnsureContainer()
        {
            if (provincesContainer == null)
            {
                var existing = GameObject.Find("GISProvincesContainer");
                if (existing != null)
                {
                    provincesContainer = existing.transform;
                }
                else
                {
                    var newContainer = new GameObject("GISProvincesContainer");
                    newContainer.transform.SetParent(transform, false);
                    provincesContainer = newContainer.transform;
                }
            }
        }

        private void HandleProvinceClicked(MapInteractionController clicked)
        {
            foreach (var prov in spawnedProvinces)
            {
                if (prov != null && prov != clicked)
                {
                    prov.SetSelected(false);
                }
            }

            _currentSelectedProvince = clicked;
        }

        /// <summary>
        /// Səhnədəki bütün əyalət obyektlərini və konteyner daxilindəki qalıqları tam təmizləyir.
        /// </summary>
        [ContextMenu("Clear Map")]
        public void ClearSpawnedProvinces()
        {
            foreach (var prov in spawnedProvinces)
            {
                if (prov != null && prov.gameObject != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(prov.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(prov.gameObject);
                    }
                }
            }

            spawnedProvinces.Clear();
            _currentSelectedProvince = null;

            if (provincesContainer != null)
            {
                for (int i = provincesContainer.childCount - 1; i >= 0; i--)
                {
                    var child = provincesContainer.GetChild(i);
                    if (child != null)
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// Inspector-da ContextMenu ilə xəritəni təmizləmək üçün metod.
        /// </summary>
        [ContextMenu("Clear Map")]
        public void ClearMap()
        {
            ClearSpawnedProvinces();
        }

        /// <summary>
        /// Xəritədəki bütün əyalətlərin seçim vurğusunu ləğv edir.
        /// </summary>
        public void DeselectAllProvinces()
        {
            foreach (var prov in spawnedProvinces)
            {
                if (prov != null)
                {
                    prov.SetSelected(false);
                }
            }
            _currentSelectedProvince = null;
        }

        public MapInteractionController GetProvinceByName(string name)
        {
            return spawnedProvinces.Find(p => string.Equals(p.ProvinceName, name, StringComparison.OrdinalIgnoreCase));
        }

        public MapInteractionController GetProvinceById(int id)
        {
            return spawnedProvinces.Find(p => p.ProvinceId == id);
        }

        // =========================================================================
        // TERRA INCOGNITA (ARXA PLAN MATERİKLƏRİ - İKİQATLI XƏRİTƏ SİSTEMİ)
        // =========================================================================

        /// <summary>
        /// Arxa plan materik GeoJSON faylının tam yolunu axtarır və tapır.
        /// Standart axtarış: Assets/Database/world_background.json
        /// </summary>
        public string ResolveBackgroundFilePath(string customPath = null)
        {
            string candidate = !string.IsNullOrEmpty(customPath) ? customPath : backgroundGeoJsonPath;

            if (string.IsNullOrEmpty(candidate)) return null;

            // 1. Birbaşa verilmiş tam və ya nisbi yol
            try
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch { }

            // 2. Application.dataPath daxilində (Assets/Database/...)
            string fileName = Path.GetFileName(candidate);
            try
            {
                string pDataDb = Path.Combine(Application.dataPath, "Database", fileName);
                if (File.Exists(pDataDb)) return Path.GetFullPath(pDataDb);

                string pDataDbGeo = Path.Combine(Application.dataPath, "Database", Path.GetFileNameWithoutExtension(fileName) + ".geojson");
                if (File.Exists(pDataDbGeo)) return Path.GetFullPath(pDataDbGeo);
            }
            catch { }

            // 3. Application.streamingAssetsPath daxilində
            try
            {
                string pStreaming = Path.Combine(Application.streamingAssetsPath, fileName);
                if (File.Exists(pStreaming)) return Path.GetFullPath(pStreaming);
            }
            catch { }

            // 4. Cari iş qovluğu ilə Assets/Database
            try
            {
                string pCurDb = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Database", fileName);
                if (File.Exists(pCurDb)) return Path.GetFullPath(pCurDb);
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Arxa plan qitə və materik poliqonlarını (Terra Incognita) GeoJSON faylından oxuyur və vizual olaraq səhnəyə yerləşdirir.
        /// Standart olaraq Assets/Database/world_background.json axtarılır.
        /// </summary>
        [ContextMenu("Load Background Landmass")]
        public int LoadBackgroundLandmass(string filePath = null)
        {
            string resolvedPath = ResolveBackgroundFilePath(filePath);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                string targetName = !string.IsNullOrEmpty(filePath) ? filePath : backgroundGeoJsonPath;
                Debug.Log($"[GISMapLoader] Arxa plan materik faylı ('{targetName}') tapılmadı. Terra Incognita arxa planı gözləmədədir (istifadəçi tərəfindən əlavə edildikdə avtomatik yüklənəcək).");
                return 0;
            }

            Debug.Log($"[GISMapLoader] Terra Incognita arxa plan faylı oxunur: {resolvedPath}");
            string jsonContent = File.ReadAllText(resolvedPath);
            return LoadBackgroundLandmassFromJson(jsonContent);
        }

        /// <summary>
        /// Verilmiş GeoJSON mətnindən arxa plan materiklərini (Z = 0.5f, yalnız MeshFilter və MeshRenderer, collidersiz) yaradır.
        /// </summary>
        public int LoadBackgroundLandmassFromJson(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogWarning("[GISMapLoader] Arxa plan GeoJSON mətni boşdur!");
                return 0;
            }

            ClearBackgroundLandmass();
            EnsureBackgroundContainer();

            var features = ParseGeoJsonFast(jsonContent);
            if (features == null || features.Count == 0)
            {
                Debug.LogWarning("[GISMapLoader] Arxa plan GeoJSON-da heç bir Feature tapılmadı!");
                return 0;
            }

            Vector2 mapCenter = (autoCenter && _computedCenter != Vector2.zero) ? _computedCenter : customCenter;

            // Unlit Terra Incognita ortaq materialı (Bütün arxa plan materikləri üçün tək material = yüksək performans)
            if (_terraIncognitaMaterial == null)
            {
                Shader shader = MapInteractionController.GetDefaultMapShader();
                _terraIncognitaMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
                _terraIncognitaMaterial.name = "TerraIncognita_Material";
            }
            _terraIncognitaMaterial.color = terraIncognitaColor;

            int loadedCount = 0;

            for (int f = 0; f < features.Count; f++)
            {
                var feat = features[f];
                if (feat.Rings == null || feat.Rings.Count == 0) continue;

                var unityRings = new List<Vector2[]>(feat.Rings.Count);
                for (int r = 0; r < feat.Rings.Count; r++)
                {
                    var geoRing = feat.Rings[r];
                    if (geoRing.Count < 3) continue;

                    Vector2[] pts = new Vector2[geoRing.Count];
                    for (int i = 0; i < geoRing.Count; i++)
                    {
                        Vector2 g = geoRing[i];
                        float ux = (g.x - mapCenter.x) * coordinateScale;
                        float uy = (g.y - mapCenter.y) * coordinateScale;
                        pts[i] = new Vector2(ux, uy);
                    }
                    unityRings.Add(pts);
                }

                if (unityRings.Count == 0) continue;

                string landName = !string.IsNullOrEmpty(feat.Name) ? feat.Name : $"BackgroundLandmass_{f + 1}";
                GameObject landObj = new GameObject(landName);
                landObj.transform.SetParent(backgroundContainer, false);
                landObj.transform.localPosition = Vector3.zero;

                MeshFilter filter = landObj.AddComponent<MeshFilter>();
                MeshRenderer renderer = landObj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _terraIncognitaMaterial;

                // Mesh generasiyası üçün müvəqqəti PolygonCollider2D istifadə edib dərhal silirik (Kritik: Heç bir Collider qalmır!)
                PolygonCollider2D tempCol = landObj.AddComponent<PolygonCollider2D>();
                tempCol.pathCount = unityRings.Count;
                for (int r = 0; r < unityRings.Count; r++)
                {
                    tempCol.SetPath(r, unityRings[r]);
                }
                Mesh polyMesh = tempCol.CreateMesh(false, false);
                if (Application.isPlaying) Destroy(tempCol);
                else DestroyImmediate(tempCol);

                if (polyMesh == null || polyMesh.vertexCount == 0)
                {
                    polyMesh = GenerateSimpleMesh(unityRings[0]);
                }

                if (polyMesh != null)
                {
                    polyMesh.name = $"{landName}_Mesh";
                    filter.sharedMesh = polyMesh;
                }

                spawnedBackgroundLandmasses.Add(landObj);
                loadedCount++;
            }

            Debug.Log($"[GISMapLoader] Terra Incognita: {loadedCount} arxa plan materiki (Z={backgroundZOffset:F2}, Rəng=#{ColorUtility.ToHtmlStringRGB(terraIncognitaColor)}) uğurla yükləndi.");
            return loadedCount;
        }

        private static Mesh GenerateSimpleMesh(Vector2[] vertices2D)
        {
            if (vertices2D == null || vertices2D.Length < 3) return null;
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
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void EnsureBackgroundContainer()
        {
            if (backgroundContainer != null)
            {
                backgroundContainer.localPosition = new Vector3(0f, 0f, backgroundZOffset);
                return;
            }

            var existing = GameObject.Find("BackgroundLandmassContainer");
            if (existing != null)
            {
                backgroundContainer = existing.transform;
                backgroundContainer.localPosition = new Vector3(0f, 0f, backgroundZOffset);
                return;
            }

            GameObject mapContainer = GameObject.Find("MapContainer");
            if (mapContainer == null)
            {
                mapContainer = new GameObject("MapContainer");
                mapContainer.transform.SetParent(transform, false);
                mapContainer.transform.localPosition = Vector3.zero;
            }

            GameObject bgGo = new GameObject("BackgroundLandmassContainer");
            bgGo.transform.SetParent(mapContainer.transform, false);
            bgGo.transform.localPosition = new Vector3(0f, 0f, backgroundZOffset);
            backgroundContainer = bgGo.transform;
        }

        /// <summary>
        /// Bütün arxa plan materik obyektlərini səhnədən təmizləyir.
        /// </summary>
        [ContextMenu("Clear Background Landmass")]
        public void ClearBackgroundLandmass()
        {
            if (backgroundContainer != null)
            {
                for (int i = backgroundContainer.childCount - 1; i >= 0; i--)
                {
                    var child = backgroundContainer.GetChild(i);
                    if (child != null)
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                    }
                }
            }

            spawnedBackgroundLandmasses.Clear();
        }

        // =========================================================================
        // YÜKSƏK SÜRƏTLİ GEOJSON TƏHLİL MÜHƏRRİKİ (FAST CHAR SCANNER)
        // =========================================================================

        public class GeoFeatureData
        {
            public int ProvinceId;
            public string Name;
            public string AdminCountry;
            public int FactionId;
            public string Climate = "Temperate";
            public Color? CustomFillColor;
            public Color? CustomBorderColor;
            public List<List<Vector2>> Rings = new List<List<Vector2>>();
        }

        private void CalculateBoundingBox(List<GeoFeatureData> features)
        {
            float minLon = float.MaxValue, maxLon = float.MinValue;
            float minLat = float.MaxValue, maxLat = float.MinValue;
            int count = 0;

            foreach (var f in features)
            {
                foreach (var r in f.Rings)
                {
                    foreach (var pt in r)
                    {
                        if (pt.x < minLon) minLon = pt.x;
                        if (pt.x > maxLon) maxLon = pt.x;
                        if (pt.y < minLat) minLat = pt.y;
                        if (pt.y > maxLat) maxLat = pt.y;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                _geoBoundsMin = new Vector2(minLon, minLat);
                _geoBoundsMax = new Vector2(maxLon, maxLat);
                _computedCenter = new Vector2((minLon + maxLon) * 0.5f, (minLat + maxLat) * 0.5f);
            }
            else
            {
                _computedCenter = customCenter;
            }
        }

        public static List<GeoFeatureData> ParseGeoJsonFast(string json)
        {
            var features = new List<GeoFeatureData>();
            int idx = 0;
            int len = json.Length;
            int autoId = 1;

            while (idx < len)
            {
                int featIdx = json.IndexOf("\"type\":\"Feature\"", idx, StringComparison.OrdinalIgnoreCase);
                if (featIdx == -1)
                {
                    featIdx = json.IndexOf("\"type\": \"Feature\"", idx, StringComparison.OrdinalIgnoreCase);
                }
                if (featIdx == -1) break;

                // Növbəti Feature-nin başlanğıcını tapırıq
                int nextFeat = json.IndexOf("\"type\":\"Feature\"", featIdx + 15, StringComparison.OrdinalIgnoreCase);
                if (nextFeat == -1)
                {
                    nextFeat = json.IndexOf("\"type\": \"Feature\"", featIdx + 15, StringComparison.OrdinalIgnoreCase);
                }
                int blockEnd = nextFeat != -1 ? nextFeat : len;

                string block = json.Substring(featIdx, blockEnd - featIdx);
                idx = blockEnd;

                var feat = new GeoFeatureData();
                feat.ProvinceId = autoId++;

                // 1. Properties
                feat.Name = ExtractStringField(block, "name");
                if (string.IsNullOrEmpty(feat.Name)) feat.Name = ExtractStringField(block, "name_en");
                if (string.IsNullOrEmpty(feat.Name)) feat.Name = ExtractStringField(block, "name_local");
                if (string.IsNullOrEmpty(feat.Name)) feat.Name = $"Province_{feat.ProvinceId}";

                feat.AdminCountry = ExtractStringField(block, "admin");
                if (string.IsNullOrEmpty(feat.AdminCountry)) feat.AdminCountry = ExtractStringField(block, "geonunit");

                int dissMe = ExtractIntField(block, "diss_me", -1);
                if (dissMe > 0) feat.ProvinceId = dissMe;
                else
                {
                    int pId = ExtractIntField(block, "provinceId", -1);
                    if (pId > 0) feat.ProvinceId = pId;
                }

                feat.FactionId = ExtractIntField(block, "factionId", 0);
                string clim = ExtractStringField(block, "climate");
                if (!string.IsNullOrEmpty(clim)) feat.Climate = clim;

                string fillHex = ExtractStringField(block, "fillColor");
                if (!string.IsNullOrEmpty(fillHex) && ColorUtility.TryParseHtmlString(fillHex, out Color fc))
                {
                    feat.CustomFillColor = new Color(fc.r, fc.g, fc.b, 0.45f);
                }

                string borderHex = ExtractStringField(block, "borderColor");
                if (!string.IsNullOrEmpty(borderHex) && ColorUtility.TryParseHtmlString(borderHex, out Color bc))
                {
                    feat.CustomBorderColor = bc;
                }

                // 2. Geometry Coordinates (Polygon & MultiPolygon)
                int coordIdx = block.IndexOf("\"coordinates\":", StringComparison.OrdinalIgnoreCase);
                if (coordIdx != -1)
                {
                    int cStart = block.IndexOf('[', coordIdx);
                    if (cStart != -1)
                    {
                        ParseCoordinatesFast(block, cStart, feat.Rings);
                    }
                }

                if (feat.Rings.Count > 0)
                {
                    features.Add(feat);
                }
            }

            return features;
        }

        private static void ParseCoordinatesFast(string text, int startIndex, List<List<Vector2>> destinationRings)
        {
            int pos = startIndex;
            int len = text.Length;
            var currentRing = new List<Vector2>();

            while (pos < len)
            {
                int pairStart = text.IndexOf('[', pos);
                if (pairStart == -1) break;

                // [lon, lat] cütlüyü tapırıq
                int comma = text.IndexOf(',', pairStart);
                int pairEnd = text.IndexOf(']', pairStart);

                if (comma != -1 && pairEnd != -1 && comma < pairEnd)
                {
                    string s1 = text.Substring(pairStart + 1, comma - pairStart - 1).Trim();
                    string s2 = text.Substring(comma + 1, pairEnd - comma - 1).Trim();

                    if (float.TryParse(s1, NumberStyles.Float, CultureInfo.InvariantCulture, out float lon) &&
                        float.TryParse(s2, NumberStyles.Float, CultureInfo.InvariantCulture, out float lat))
                    {
                        currentRing.Add(new Vector2(lon, lat));
                        pos = pairEnd + 1;

                        // Əgər növbəti xarakter ']' olarsa, deməli cari halqa/polygon bağlanır
                        int nextNonWs = pos;
                        while (nextNonWs < len && char.IsWhiteSpace(text[nextNonWs])) nextNonWs++;
                        if (nextNonWs < len && text[nextNonWs] == ']')
                        {
                            if (currentRing.Count >= 3)
                            {
                                destinationRings.Add(currentRing);
                            }
                            currentRing = new List<Vector2>();
                            pos = nextNonWs + 1;
                        }
                        continue;
                    }
                }

                pos = pairStart + 1;
            }

            if (currentRing.Count >= 3)
            {
                destinationRings.Add(currentRing);
            }
        }

        private static string ExtractStringField(string json, string fieldName)
        {
            string key = $"\"{fieldName}\":\"";
            int start = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start == -1)
            {
                key = $"\"{fieldName}\": \"";
                start = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            }
            if (start == -1) return null;

            int valStart = start + key.Length;
            int end = json.IndexOf('"', valStart);
            if (end == -1) return null;

            return json.Substring(valStart, end - valStart);
        }

        private static int ExtractIntField(string json, string fieldName, int defaultValue)
        {
            string key = $"\"{fieldName}\":";
            int start = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start == -1) return defaultValue;

            int valStart = start + key.Length;
            while (valStart < json.Length && (char.IsWhiteSpace(json[valStart]) || json[valStart] == '"')) valStart++;

            int valEnd = valStart;
            while (valEnd < json.Length && (char.IsDigit(json[valEnd]) || json[valEnd] == '-')) valEnd++;

            if (valEnd > valStart)
            {
                if (int.TryParse(json.Substring(valStart, valEnd - valStart), out int res))
                {
                    return res;
                }
            }

            return defaultValue;
        }

        // =========================================================================
        // "AGE OF HISTORY" DÖVLƏTLƏRƏ GÖRƏ SİYASİ RƏNG PALİTRASI
        // =========================================================================

        public static readonly Dictionary<string, Color> CountryColorMap = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            // Bizans və Yunanıstan (Bənövşəyi / Imperial Purple)
            { "Greece", new Color(0.48f, 0.22f, 0.62f) },
            { "Cyprus", new Color(0.28f, 0.48f, 0.72f) },
            { "Dhekelia Sovereign Base Area", new Color(0.35f, 0.45f, 0.65f) },
            { "Akrotiri Sovereign Base Area", new Color(0.35f, 0.45f, 0.65f) },

            // Anadolu və Türkiyə (Qırmızı / Imperial Red)
            { "Turkey", new Color(0.78f, 0.18f, 0.18f) },
            { "Northern Cyprus", new Color(0.85f, 0.32f, 0.32f) },

            // Qafqaz və Şirvan (Yaşıl / Emerald Green)
            { "Azerbaijan", new Color(0.14f, 0.58f, 0.32f) },
            { "Georgia", new Color(0.68f, 0.18f, 0.24f) },
            { "Armenia", new Color(0.72f, 0.42f, 0.18f) },

            // Fars və Mərkəzi Asiya (Qızılı Narıncı / Saffron Ochre)
            { "Iran", new Color(0.85f, 0.48f, 0.12f) },
            { "Turkmenistan", new Color(0.20f, 0.60f, 0.50f) },
            { "Uzbekistan", new Color(0.32f, 0.65f, 0.60f) },
            { "Kazakhstan", new Color(0.28f, 0.58f, 0.72f) },

            // Yaxın Şərq və Ərəbistan
            { "Iraq", new Color(0.70f, 0.38f, 0.15f) },
            { "Syria", new Color(0.65f, 0.58f, 0.20f) },
            { "Jordan", new Color(0.78f, 0.64f, 0.35f) },
            { "Lebanon", new Color(0.32f, 0.60f, 0.36f) },
            { "Saudi Arabia", new Color(0.18f, 0.46f, 0.24f) },
            { "Kuwait", new Color(0.42f, 0.55f, 0.35f) },
            { "Israel", new Color(0.22f, 0.45f, 0.70f) },
            { "West Bank", new Color(0.52f, 0.56f, 0.30f) },
            { "Gaza Strip", new Color(0.50f, 0.54f, 0.28f) },
            { "Egypt", new Color(0.85f, 0.66f, 0.16f) },

            // Balkanlar
            { "Bulgaria", new Color(0.26f, 0.52f, 0.38f) },
            { "Republic of Serbia", new Color(0.22f, 0.35f, 0.58f) },
            { "Romania", new Color(0.78f, 0.58f, 0.18f) },
            { "Albania", new Color(0.68f, 0.16f, 0.16f) },
            { "Macedonia", new Color(0.80f, 0.45f, 0.16f) },
            { "Kosovo", new Color(0.32f, 0.46f, 0.62f) },
            { "Montenegro", new Color(0.60f, 0.20f, 0.25f) },
            { "Bosnia and Herzegovina", new Color(0.20f, 0.40f, 0.65f) },
            { "Moldova", new Color(0.68f, 0.50f, 0.22f) },

            // Şimal (Rusiya, Ukrayna)
            { "Russia", new Color(0.25f, 0.38f, 0.48f) },
            { "Ukraine", new Color(0.24f, 0.48f, 0.72f) },
        };

        public Color GetCountryColor(string country, string provinceName, int provinceId)
        {
            Color baseColor;
            string key = !string.IsNullOrEmpty(country) ? country : (!string.IsNullOrEmpty(provinceName) ? provinceName : "Default");

            if (!CountryColorMap.TryGetValue(key, out baseColor))
            {
                int hash = Math.Abs(key.GetHashCode());
                baseColor = Color.HSVToRGB((hash % 360) / 360f, 0.60f, 0.68f);
            }

            // Hər bir əyalət üçün eyni ölkə daxilində çox incə (±2.5%) ton fərqi (Age of History stili)
            float jitter = ((provinceId * 17) % 11 - 5) * 0.012f;
            float r = Mathf.Clamp01(baseColor.r + jitter);
            float g = Mathf.Clamp01(baseColor.g + jitter);
            float b = Mathf.Clamp01(baseColor.b + jitter);

            return new Color(r, g, b, fillAlpha);
        }
    }
}
