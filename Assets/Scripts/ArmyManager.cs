using System;
using System.Collections.Generic;
using GrandStrategy.Map;
using GrandStrategy.Military;
using GrandStrategy.Models;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GrandStrategy.Military
{
    /// <summary>
    /// Faza 10.2 - Xəritədə Komandir və Ordu Sisteminin Baş İdarəedicisi (ArmyManager).
    /// 1187-ci il ssenarisinə uyğun olaraq tarixi komandirləri (Qızıl Arslan, I Axsitan, Səlahəddin, II Qılınc Arslan)
    /// və onların ordularını ("35k", "15k", "50k", "30k") müvafiq əyalətlərin mərkəz nöqtələrində yaradır və idarə edir.
    /// </summary>
    public class ArmyManager : MonoBehaviour
    {
        private static ArmyManager _instance;
        public static ArmyManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = UnityEngine.Object.FindAnyObjectByType<ArmyManager>();
#else
                    _instance = UnityEngine.Object.FindObjectOfType<ArmyManager>();
#endif
                }
                return _instance;
            }
        }

        [Header("Tənzimləmələr")]
        [Tooltip("Səhnə və ya xəritə yükləndikdə 1187 tarixi orduları avtomatik yaradılsın")]
        [SerializeField] private bool autoSpawnOnStart = true;

        [Tooltip("Ordu prefab-ı (null olarsa, procedurally avtomatik yaradılır)")]
        [SerializeField] private GameObject armyPrefab;

        [Tooltip("Komandir ikonu üçün xüsusi sprit (null olarsa, ShieldIcon və ya procedural qalxan)")]
        [SerializeField] private Sprite commanderShieldSprite;

        [Header("Aktiv Ordular")]
        [SerializeField] private List<ArmyVisualController> activeArmies = new List<ArmyVisualController>();
        [SerializeField] private ArmyVisualController selectedArmy;
        [SerializeField] private Transform armiesContainer;

        public List<ArmyVisualController> ActiveArmies => activeArmies;
        public ArmyVisualController SelectedArmy => selectedArmy;
        public GameObject ArmyPrefab { get => armyPrefab; set => armyPrefab = value; }
        public Sprite CommanderShieldSprite { get => commanderShieldSprite; set => commanderShieldSprite = value; }

        public event Action<List<ArmyVisualController>> OnArmiesSpawned;
        public event Action<ArmyVisualController> OnArmySelected;

        /// <summary>
        /// Xəritədə müəyyən bir ordunu seçir və digərlərini deselect edir.
        /// </summary>
        public void SelectArmy(ArmyVisualController army)
        {
            if (selectedArmy != null && selectedArmy != army)
            {
                selectedArmy.SetSelected(false);
            }

            selectedArmy = army;

            if (selectedArmy != null)
            {
                selectedArmy.SetSelected(true);
                Debug.Log($"[ArmyManager] Ordu seçildi: {selectedArmy.ArmyData.CommanderName} ({selectedArmy.ArmyData.FormattedTroopCount})");
            }

            OnArmySelected?.Invoke(selectedArmy);
        }

        public void DeselectAll()
        {
            if (selectedArmy != null)
            {
                selectedArmy.SetSelected(false);
                selectedArmy = null;
                OnArmySelected?.Invoke(null);
            }
        }

        private void Update()
        {
            // Oyunçu siçan kliklərini RTS stilində idarə edir
            HandleMouseSelectionAndOrders();
        }

        private void HandleMouseSelectionAndOrders()
        {
            // 1. Sol klik - Ordu seçilməsi
            if (GetSafeLeftClickDown())
            {
                Vector2? worldPos = GetSafeMouseWorldPosition();
                if (worldPos.HasValue)
                {
                    ArmyVisualController clickedArmy = FindArmyNearPoint(worldPos.Value, 0.45f);
                    if (clickedArmy != null)
                    {
                        SelectArmy(clickedArmy);
                        return;
                    }
                }
            }

            // 2. Sağ klik - Seçilmiş ordunun hərəkət və hücum əmri
            if (GetSafeRightClickDown() && selectedArmy != null)
            {
                Vector2? worldPos = GetSafeMouseWorldPosition();
                if (worldPos.HasValue)
                {
                    Vector2 target = worldPos.Value;
                    MapInteractionController hitProvince = FindProvinceAtPoint(target);

                    if (selectedArmy.MovementController != null)
                    {
                        selectedArmy.MovementController.MoveTo(target, hitProvince);
                    }
                }
            }
        }

        private ArmyVisualController FindArmyNearPoint(Vector2 point, float radius)
        {
            for (int i = 0; i < activeArmies.Count; i++)
            {
                var a = activeArmies[i];
                if (a != null)
                {
                    Vector2 pos = a.transform.position;
                    if (Vector2.Distance(point, pos) <= radius)
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        private MapInteractionController FindProvinceAtPoint(Vector2 point)
        {
            if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
            {
                var list = GISMapLoader.Instance.SpawnedProvinces;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p != null && p.PolygonCollider != null && p.PolygonCollider.OverlapPoint(point))
                    {
                        return p;
                    }
                }

                float closestDist = 0.8f;
                MapInteractionController closest = null;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p != null)
                    {
                        float d = Vector2.Distance(point, p.GetCentroidWorldPosition());
                        if (d < closestDist)
                        {
                            closestDist = d;
                            closest = p;
                        }
                    }
                }
                return closest;
            }
            return null;
        }

        private bool GetSafeLeftClickDown()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                    return true;
#endif
            }
            catch { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetMouseButtonDown(0))
                    return true;
#endif
            }
            catch { }

            return false;
        }

        private bool GetSafeRightClickDown()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
                    return true;
#endif
            }
            catch { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetMouseButtonDown(1))
                    return true;
#endif
            }
            catch { }

            return false;
        }

        private Vector2? GetSafeMouseWorldPosition()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null) return null;

            Vector3 mouseScreen = Vector3.zero;
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                    mouseScreen = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#endif
            }
            catch { }

            if (mouseScreen == Vector3.zero)
            {
                try
                {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                    mouseScreen = Input.mousePosition;
#endif
                }
                catch { }
            }

            mouseScreen.z = -cam.transform.position.z;
            Vector3 worldPos = cam.ScreenToWorldPoint(mouseScreen);
            return new Vector2(worldPos.x, worldPos.y);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            EnsureContainer();
            EnsureSprites();
        }

        private void OnEnable()
        {
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.OnMapLoaded += HandleMapLoaded;
            }

            if (HistoricalScenarioManager.Instance != null)
            {
                HistoricalScenarioManager.Instance.OnScenarioApplied += HandleScenarioApplied;
            }
        }

        private void OnDisable()
        {
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.OnMapLoaded -= HandleMapLoaded;
            }

            if (HistoricalScenarioManager.Instance != null)
            {
                HistoricalScenarioManager.Instance.OnScenarioApplied -= HandleScenarioApplied;
            }
        }

        private void Start()
        {
            EnsureContainer();
            EnsureSprites();

            if (autoSpawnOnStart)
            {
                // Əgər xəritə poliqonları artıq mövcuddursa, dərhal orduları yaradırıq
                if (HasProvincesInScene())
                {
                    Spawn1187Armies();
                }
            }
        }

        private void HandleMapLoaded(List<MapInteractionController> provinces)
        {
            if (autoSpawnOnStart)
            {
                Spawn1187Armies();
            }
        }

        private void HandleScenarioApplied(int count)
        {
            if (autoSpawnOnStart)
            {
                Spawn1187Armies();
            }
        }

        private void EnsureContainer()
        {
            if (armiesContainer == null)
            {
                var found = transform.Find("ArmiesContainer");
                if (found != null)
                {
                    armiesContainer = found;
                }
                else
                {
                    var containerObj = new GameObject("ArmiesContainer");
                    containerObj.transform.SetParent(transform, false);
                    armiesContainer = containerObj.transform;
                }
            }
        }

        private void EnsureSprites()
        {
            if (commanderShieldSprite == null)
            {
#if UNITY_EDITOR
                string shieldPath = "Assets/UI_Assets/ArmyShieldIcon.png";
                commanderShieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(shieldPath);
                if (commanderShieldSprite == null)
                {
                    commanderShieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI_Assets/ShieldIcon.png");
                }
#endif
            }
        }

        private bool HasProvincesInScene()
        {
            if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
                return true;

#if UNITY_2023_1_OR_NEWER
            var found = UnityEngine.Object.FindAnyObjectByType<MapInteractionController>();
#else
            var found = UnityEngine.Object.FindObjectOfType<MapInteractionController>();
#endif
            return found != null;
        }

        /// <summary>
        /// 1187 ssenarisinin əsas tarixi komandirlərini və ordularını xəritəyə yerləşdirir.
        /// </summary>
        [ContextMenu("Spawn 1187 Armies")]
        public List<ArmyVisualController> Spawn1187Armies()
        {
            EnsureContainer();
            EnsureSprites();

            ClearArmies();

            Debug.Log("[ArmyManager] 1187-ci il ssenarisi üzrə tarixi komandir və ordular formalaşdırılır...");

            // 1. Eldəgəzlər (Azərbaycan Atabəyləri) - Qızıl Arslan (Gəncə, 35,000) [Hökmdar]
            SpawnArmyInternal(
                id: 1,
                commanderName: "Qızıl Arslan",
                factionName: "Eldəgəzlər (Atabəylər)",
                factionId: 1,
                targetProvinceKeyword: "Gəncə",
                fallbackAliases: new string[] { "Ganja", "Gəncə", "Gence" },
                troops: 35000,
                factionColor: new Color(0.122f, 0.380f, 0.553f, 1f), // #1F618D
                isFactionRuler: true,
                commanderSkill: 1.45f
            );

            // 2. Şirvanşahlar Dövləti - I Axsitan (Bakı, 15,000) [Hökmdar]
            SpawnArmyInternal(
                id: 2,
                commanderName: "I Axsitan",
                factionName: "Şirvanşahlar Dövləti",
                factionId: 2,
                targetProvinceKeyword: "Bakı",
                fallbackAliases: new string[] { "Bakı", "Baku", "Baki", "Abşeron" },
                troops: 15000,
                factionColor: new Color(0.180f, 0.800f, 0.443f, 1f), // #2ECC71
                isFactionRuler: true,
                commanderSkill: 1.25f
            );

            // 3. Əyyubilər Dövləti - Səlahəddin (Dəməşq / Damascus, 50,000) [Hökmdar]
            SpawnArmyInternal(
                id: 3,
                commanderName: "Səlahəddin",
                factionName: "Əyyubilər Dövləti",
                factionId: 6,
                targetProvinceKeyword: "Damascus",
                fallbackAliases: new string[] { "Damascus", "Dimashq", "Dəməşq", "Rif Dimashq", "Jerusalem", "Syria" },
                troops: 50000,
                factionColor: new Color(0.945f, 0.769f, 0.059f, 1f), // #F1C40F
                isFactionRuler: true,
                commanderSkill: 1.50f
            );

            // 4. Rum Sultanlığı - II Qılınc Arslan (Konya, 30,000) [Hökmdar]
            SpawnArmyInternal(
                id: 4,
                commanderName: "II Qılınc Arslan",
                factionName: "Rum Sultanlığı",
                factionId: 5,
                targetProvinceKeyword: "Konya",
                fallbackAliases: new string[] { "Konya", "Anatolia", "Iconium" },
                troops: 30000,
                factionColor: new Color(0.753f, 0.224f, 0.169f, 1f), // #C0392B
                isFactionRuler: true,
                commanderSkill: 1.30f
            );

            Debug.Log($"[ArmyManager] 1187 Ssenarisi üzrə {activeArmies.Count} komandir və ordu xəritədə uğurla yerləşdirildi!");
            OnArmiesSpawned?.Invoke(activeArmies);

            return activeArmies;
        }

        private ArmyVisualController SpawnArmyInternal(
            int id,
            string commanderName,
            string factionName,
            int factionId,
            string targetProvinceKeyword,
            string[] fallbackAliases,
            int troops,
            Color factionColor,
            bool isFactionRuler = false,
            float commanderSkill = 1.0f)
        {
            MapInteractionController province = FindProvince(targetProvinceKeyword, fallbackAliases);
            Vector3 spawnPos = Vector3.zero;
            int provId = id;
            string provName = targetProvinceKeyword;

            if (province != null)
            {
                provId = province.ProvinceId;
                provName = province.ProvinceName;
                spawnPos = province.GetCentroidWorldPosition();
            }
            else
            {
                Debug.LogWarning($"[ArmyManager] '{commanderName}' üçün '{targetProvinceKeyword}' əyaləti tapılmadı! Varsayılan mövqeyə yerləşdirilir.");
            }

            var armyData = new CommanderArmy(
                id: id,
                commanderName: commanderName,
                faction: factionName,
                factionId: factionId,
                provinceId: provId,
                provinceName: provName,
                troopCount: troops,
                factionColor: factionColor,
                isFactionRuler: isFactionRuler,
                commanderSkill: commanderSkill
            );

            var visual = ArmyVisualController.CreateArmyView(
                data: armyData,
                worldPosition: spawnPos,
                parent: armiesContainer,
                commanderSprite: commanderShieldSprite,
                prefab: armyPrefab
            );

            if (visual != null)
            {
                activeArmies.Add(visual);
                Debug.Log($"[ArmyManager] Ordu yaradıldı: {armyData} | Hökmdar: {isFactionRuler} | Mövqe: ({spawnPos.x:F2}, {spawnPos.y:F2}, {visual.ZOffset})");
            }

            return visual;
        }

        /// <summary>
        /// Xəritədəki əyalətlər arasından adı və ya açar sözləri uyğun gələn əyaləti tapır.
        /// </summary>
        public MapInteractionController FindProvince(string primaryName, string[] aliases = null)
        {
            List<MapInteractionController> provinces = null;

            if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
            {
                provinces = new List<MapInteractionController>(GISMapLoader.Instance.SpawnedProvinces);
            }
            else
            {
#if UNITY_2023_1_OR_NEWER
                provinces = new List<MapInteractionController>(UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude));
#else
                provinces = new List<MapInteractionController>(UnityEngine.Object.FindObjectsOfType<MapInteractionController>());
#endif
            }

            if (provinces == null || provinces.Count == 0) return null;

            // 1. Tam bərabərlik (Exact match)
            var found = provinces.Find(p => string.Equals(p.ProvinceName, primaryName, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;

            // 2. Açar sözün əyalət adında keçməsi
            found = provinces.Find(p => p.ProvinceName != null && p.ProvinceName.IndexOf(primaryName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null) return found;

            // 3. Alternativ adlar (Aliases) yoxlanışı
            if (aliases != null)
            {
                foreach (string alias in aliases)
                {
                    found = provinces.Find(p => string.Equals(p.ProvinceName, alias, StringComparison.OrdinalIgnoreCase));
                    if (found != null) return found;

                    found = provinces.Find(p => p.ProvinceName != null && p.ProvinceName.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Komandir adına görə ordunu tapır.
        /// </summary>
        public ArmyVisualController FindArmyByCommander(string commanderName)
        {
            if (string.IsNullOrEmpty(commanderName)) return null;

            return activeArmies.Find(a => a != null && a.ArmyData != null &&
                string.Equals(a.ArmyData.CommanderName, commanderName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Əyalət ID-sinə görə orada dayanan ordunu tapır.
        /// </summary>
        public ArmyVisualController GetArmyByProvince(int provinceId)
        {
            return activeArmies.Find(a => a != null && a.ArmyData != null && a.ArmyData.TroopCount > 0 && a.ArmyData.CurrentProvinceId == provinceId);
        }

        /// <summary>
        /// Göstərilən əyalətdə hazırda mövcud olan bütün döyüş qabiliyyətli orduları qaytarır.
        /// </summary>
        public List<ArmyVisualController> GetArmiesInProvince(int provinceId)
        {
            var result = new List<ArmyVisualController>();
            for (int i = activeArmies.Count - 1; i >= 0; i--)
            {
                var army = activeArmies[i];
                if (army == null)
                {
                    activeArmies.RemoveAt(i);
                    continue;
                }

                if (army.ArmyData != null && army.ArmyData.TroopCount > 0 && army.ArmyData.CurrentProvinceId == provinceId)
                {
                    result.Add(army);
                }
            }
            return result;
        }

        /// <summary>
        /// Göstərilən dövlətin (factionId) xəritədəki bütün aktiv ordularının (CommanderArmy) əsgər saylarının cəmini qaytarır.
        /// </summary>
        public int GetTotalTroopsForFaction(int factionId)
        {
            if (factionId <= 0) return 0;

            int total = 0;
            for (int i = activeArmies.Count - 1; i >= 0; i--)
            {
                var army = activeArmies[i];
                if (army == null)
                {
                    activeArmies.RemoveAt(i);
                    continue;
                }

                if (army.ArmyData != null && army.ArmyData.FactionId == factionId && army.ArmyData.TroopCount > 0)
                {
                    total += army.ArmyData.TroopCount;
                }
            }
            return total;
        }

        /// <summary>
        /// Ordu ID-sinə görə aktiv vizual ordu obyektini tapır.
        /// </summary>
        public ArmyVisualController GetArmyById(int armyId)
        {
            if (armyId <= 0) return null;
            return activeArmies.Find(a => a != null && a.ArmyData != null && a.ArmyData.Id == armyId);
        }

        /// <summary>
        /// Düşmən əyalətində hazır ordu yoxdursa, Faza 10.5 ProvinceDemographics sistemindən Yetişkin Kişilərin (AdultMen) 15%-ni
        /// səfərbər edərək dinamik 'Milis Qarnizonu' yaradır və əyalətin AdultMen sayını real olaraq azaldır.
        /// </summary>
        public ArmyVisualController SpawnDemographicMilitiaGarrison(MapInteractionController province)
        {
            if (province == null) return null;

            var demo = province.Demographics;
            if (demo == null || demo.AdultMen <= 0)
            {
                Debug.Log($"[ArmyManager] '{province.ProvinceName}' əyalətində milis çıxarmaq üçün Yetişkin Kişi (AdultMen) qalmayıb.");
                return null;
            }

            int beforeAdultMen = demo.AdultMen;
            int militiaTroops = Mathf.RoundToInt(beforeAdultMen * 0.15f);
            if (militiaTroops <= 0 && beforeAdultMen > 0) militiaTroops = beforeAdultMen;
            militiaTroops = Mathf.Clamp(militiaTroops, 0, beforeAdultMen);
            if (militiaTroops <= 0) return null;

            demo.AdultMen -= militiaTroops;
            var _ = province.EconomyData; // İqtisadi işçi nisbətini dərhal yeniləyir

            if (GrandStrategy.UI.UIManager.Instance != null &&
                GrandStrategy.UI.UIManager.Instance.SelectedMapProvince == province)
            {
                GrandStrategy.UI.UIManager.Instance.RefreshDemographicsPanel(province);
            }

            EnsureContainer();
            EnsureSprites();

            int nextId = 100 + activeArmies.Count + 1;
            Color factionColor = province.DefaultFillColor;
            if (factionColor.a < 0.2f) factionColor = new Color(0.75f, 0.22f, 0.17f, 1f);

            var data = new CommanderArmy(
                id: nextId,
                commanderName: $"{province.ProvinceName} Milis Qarnizonu",
                faction: province.ProvinceOwner,
                factionId: province.FactionId,
                provinceId: province.ProvinceId,
                provinceName: province.ProvinceName,
                troopCount: militiaTroops,
                factionColor: factionColor,
                isFactionRuler: false,
                commanderSkill: 0.85f
            );

            Vector3 spawnPos = province.GetCentroidWorldPosition() + new Vector3(0.18f, 0f, 0f);
            var visual = ArmyVisualController.CreateArmyView(
                data: data,
                worldPosition: spawnPos,
                parent: armiesContainer,
                commanderSprite: commanderShieldSprite,
                prefab: armyPrefab
            );

            if (visual != null)
            {
                activeArmies.Add(visual);
                Debug.Log($"[ArmyManager] 🛡️ MİLİS SƏFƏRBƏRLİYİ ({province.ProvinceName}): Yetişkin Kişilərin 15%-i ({militiaTroops:N0} əsgər) Milis Qarnizonuna çağırıldı! Qalan AdultMen: {beforeAdultMen:N0} -> {demo.AdultMen:N0}");
            }

            return visual;
        }

        /// <summary>
        /// Məğlub olmuş və ya ləğv edilmiş ordunu aktiv siyahıdan və səhnədən çıxarır.
        /// </summary>
        public void RemoveArmy(ArmyVisualController army)
        {
            if (army == null) return;
            if (selectedArmy == army) selectedArmy = null;
            activeArmies.Remove(army);

            if (Application.isPlaying) Destroy(army.gameObject);
            else DestroyImmediate(army.gameObject);
        }

        /// <summary>
        /// Səhnədəki bütün orduları təmizləyir.
        /// </summary>
        [ContextMenu("Clear All Armies")]
        public void ClearArmies()
        {
            for (int i = activeArmies.Count - 1; i >= 0; i--)
            {
                if (activeArmies[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(activeArmies[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(activeArmies[i].gameObject);
                    }
                }
            }
            activeArmies.Clear();

            // Əgər ArmiesContainer daxilində tək qalmış uşaq obyektlər varsa, onları da təmizləyirik
            if (armiesContainer != null)
            {
                for (int i = armiesContainer.childCount - 1; i >= 0; i--)
                {
                    var child = armiesContainer.GetChild(i);
                    if (Application.isPlaying)
                    {
                        Destroy(child.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }
            }
        }
    }
}
