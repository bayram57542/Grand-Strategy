using System;
using System.Collections.Generic;
using System.Linq;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.UI;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 8.2 - Yağmalama (Raid) əməliyyatının nəticə strukturu.
    /// </summary>
    public struct RaidResult
    {
        public bool Success;
        public int CaravanId;
        public int RaidingArmyId;
        public int RaidingFactionId;
        public int VictimFactionId;
        public string ResourceType;
        public float LootedAmount;
        public string Message;
    }

    /// <summary>
    /// Faza 8.2 - Karvan Logikası, Hərəkəti və Yağmalama Meneceri (TradeController).
    /// Ticarət paktı olan dövlətlər arasında fiziki karvanlar yaradır,
    /// xəritədə mənbə və hədəf arasında addım-addım hərəkət etdirir,
    /// düşmən orduları yaxınlaşdıqda avtomatik Yağmalama (Raid) icra edir
    /// və hədəfə çatdıqda resursu xəzinəyə təhvil verir.
    /// </summary>
    public class TradeController : MonoBehaviour
    {
        private static TradeController _instance;
        public static TradeController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<TradeController>());

        [Header("Karvan Hərəkət Parametrləri")]
        [Tooltip("Aylıq hərəkət sürəti (TravelProgress faiz artımı)")]
        [SerializeField] private float monthlyProgressSpeed = 35.0f;

        [Tooltip("Yağmalama (Raid) məsafə həddi (2D dünya koordinatları ilə)")]
        [SerializeField] private float raidDistanceThreshold = 2.5f;

        [Header("Konteyner və Prefab")]
        [SerializeField] private Transform caravansContainer;

        private readonly Dictionary<int, UI_CaravanToken> _activeTokens = new Dictionary<int, UI_CaravanToken>();

        public float MonthlyProgressSpeed { get => monthlyProgressSpeed; set => monthlyProgressSpeed = value; }
        public float RaidDistanceThreshold { get => raidDistanceThreshold; set => raidDistanceThreshold = value; }
        public string LastRaidAlertMessage { get; private set; } = string.Empty;
        public IReadOnlyDictionary<int, UI_CaravanToken> ActiveTokens => _activeTokens;

        public event Action<Caravan> OnCaravanSpawned;
        public event Action<Caravan, int> OnCaravanDelivered;
        public event Action<RaidResult> OnCaravanRaided;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// Əyalətin 2D xəritədəki mərkəz koordinatını qaytarır.
        /// MapController mövcuddursa oradakı ProvinceNode-u, əks halda deterministik offseti istifadə edir.
        /// </summary>
        public Vector3 GetProvincePosition(int provinceId)
        {
            if (MapController.Instance != null)
            {
                var node = MapController.Instance.GetNodeByProvinceId(provinceId);
                if (node != null)
                {
                    return node.transform.position;
                }
            }

            // Fallback: Anadolu (1) = (-2.6, 0.2), Şirvan (2) = (2.6, 0.2), Bizans (3) = (-5.2, 0.2)
            float x = -2.6f + ((provinceId - 1) * 5.2f);
            return new Vector3(x, 0.2f, 0f);
        }

        /// <summary>
        /// Karvanın cari TravelProgress-inə uyğun olaraq xəritədəki cari 2D koordinatını hesablayır.
        /// </summary>
        public Vector3 GetCaravanCurrentPosition(Caravan caravan)
        {
            Vector3 src = GetProvincePosition(caravan.SourceProvinceId);
            Vector3 dst = GetProvincePosition(caravan.TargetProvinceId);
            float t = Mathf.Clamp01(caravan.TravelProgress / 100f);
            return Vector3.Lerp(src, dst, t);
        }

        /// <summary>
        /// Yeni ticarət karvanı yaradır, bazaya yazır və xəritədə 2D tokenini formalaşdırır.
        /// </summary>
        public Caravan SpawnCaravan(int ownerFactionId, int sourceProvinceId, int targetProvinceId, string resourceType, float amount, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                Debug.LogError("[TradeController] SpawnCaravan xətası: DatabaseManager bağlıdır.");
                return null;
            }

            Caravan caravan = new Caravan
            {
                OwnerFactionId = ownerFactionId,
                SourceProvinceId = sourceProvinceId,
                TargetProvinceId = targetProvinceId,
                ResourceType = resourceType,
                ResourceAmount = amount,
                TravelProgress = 0f,
                IsRaided = false,
                IsCompleted = false
            };

            db.Connection.Insert(caravan);

            CreateOrUpdateToken(caravan);
            OnCaravanSpawned?.Invoke(caravan);

            Debug.Log($"[TradeController] Yeni Karvan #{caravan.Id} yaradıldı: Fraksiya #{ownerFactionId} (#{sourceProvinceId} -> #{targetProvinceId}) | {amount} {resourceType}");
            return caravan;
        }

        /// <summary>
        /// Karvanın 2D UI/World tokenini yaradır və ya yeniləyir.
        /// </summary>
        public UI_CaravanToken CreateOrUpdateToken(Caravan caravan)
        {
            if (caravan == null) return null;

            if (_activeTokens.TryGetValue(caravan.Id, out var existingToken) && existingToken != null)
            {
                existingToken.UpdatePosition(caravan.TravelProgress);
                return existingToken;
            }

            if (caravansContainer == null)
            {
                var containerObj = GameObject.Find("CaravansContainer") ?? new GameObject("CaravansContainer");
                caravansContainer = containerObj.transform;
            }

            var tokenObj = new GameObject($"CaravanToken_{caravan.Id}_{caravan.ResourceType}");
            tokenObj.transform.SetParent(caravansContainer, false);

            var token = tokenObj.AddComponent<UI_CaravanToken>();
            Vector3 src = GetProvincePosition(caravan.SourceProvinceId);
            Vector3 dst = GetProvincePosition(caravan.TargetProvinceId);
            token.Initialize(caravan, src, dst);

            _activeTokens[caravan.Id] = token;
            return token;
        }

        /// <summary>
        /// TimeManager-in aylıq Tick döngüsündə çağırılır:
        /// 1. Ticarət paktı olan dövlətlər arasında avtomatik karvanlar spawn olunur.
        /// 2. Bütün aktiv karvanlar xəritədə irəliləyir.
        /// 3. Düşmən orduları ilə məsafə yoxlanılır və Yağmalama (Raid) baş verir.
        /// 4. 100%-ə çatan karvanlar hədəf xəzinəyə təhvil verilir.
        /// </summary>
        public void ProcessMonthlyTrade(DatabaseManager dbManager, int month, int year)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                return;
            }

            try
            {
                dbManager.RunInTransaction(conn =>
                {
                    // 1. Ticarət Paktları Əsasında Karvanların Yaranması
                    AutoSpawnCaravansFromTradeTreaties(conn);

                    // 2. Karvanların Hərəkəti və Çatdırılması
                    List<Caravan> activeCaravans = conn.Table<Caravan>()
                        .ToList()
                        .Where(c => !c.IsRaided && !c.IsCompleted)
                        .ToList();

                    foreach (var caravan in activeCaravans)
                    {
                        caravan.TravelProgress += monthlyProgressSpeed;

                        if (caravan.TravelProgress >= 100f)
                        {
                            DeliverCaravanInternal(caravan, conn);
                        }
                        else
                        {
                            conn.Update(caravan);
                            CreateOrUpdateToken(caravan);
                        }
                    }

                    // 3. Düşmən Orduları ilə Yağmalama (Raiding) Yoxlanışı
                    CheckCaravanRaidsInternal(conn);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TradeController] ProcessMonthlyTrade xətası: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Ticarət Müqaviləsi ("Ticarət") olan dövlətlər arasında avtomatik karvanlar yaradır.
        /// </summary>
        private void AutoSpawnCaravansFromTradeTreaties(SQLite.SQLiteConnection conn)
        {
            var tradeTreaties = conn.Table<Diplomacy>()
                .ToList()
                .Where(d => d.TreatyType == "Ticarət" || d.IsAlliance)
                .ToList();

            if (tradeTreaties.Count == 0) return;

            var provinces = conn.Table<Province>().ToList();

            foreach (var treaty in tradeTreaties)
            {
                int fA = treaty.FactionA_Id;
                int fB = treaty.FactionB_Id;

                int activeCaravanCount = conn.Table<Caravan>()
                    .ToList()
                    .Count(c =>
                    !c.IsRaided && !c.IsCompleted &&
                    ((c.OwnerFactionId == fA) || (c.OwnerFactionId == fB)));

                // Hər ticarət marşrutu üzrə maksimum 2 aktiv karvan ola bilər
                if (activeCaravanCount < 2)
                {
                    var provA = provinces.FirstOrDefault(p => p.OwnerFactionId == fA);
                    var provB = provinces.FirstOrDefault(p => p.OwnerFactionId == fB);

                    if (provA != null && provB != null)
                    {
                        // Mənbə əyalətin strateji resursuna görə yük təyin edilir (Default: Silk)
                        string resType = !string.IsNullOrEmpty(provA.StrategicResourceType) && provA.StrategicResourceType != "None"
                            ? provA.StrategicResourceType
                            : "Silk";

                        float amount = (resType == "Silk") ? 50f : 40f;

                        var newCaravan = new Caravan
                        {
                            OwnerFactionId = fA,
                            SourceProvinceId = provA.Id,
                            TargetProvinceId = provB.Id,
                            ResourceType = resType,
                            ResourceAmount = amount,
                            TravelProgress = 0f,
                            IsRaided = false,
                            IsCompleted = false
                        };

                        conn.Insert(newCaravan);
                        CreateOrUpdateToken(newCaravan);
                        OnCaravanSpawned?.Invoke(newCaravan);
                        Debug.Log($"[TradeController] Avtomatik Ticarət Karvanı yola çıxdı: #{newCaravan.Id} ({amount} {resType})");
                    }
                }
            }
        }

        /// <summary>
        /// Xəritədəki aktiv karvanların düşmən orduları tərəfindən yağmalanmasını yoxlayır.
        /// </summary>
        public List<RaidResult> CheckCaravanRaids(DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null) db = FindAnyObjectByType<DatabaseManager>();
            if (db == null || !db.IsConnected) return new List<RaidResult>();

            var results = new List<RaidResult>();
            db.RunInTransaction(conn =>
            {
                results = CheckCaravanRaidsInternal(conn);
            });

            return results;
        }

        private List<RaidResult> CheckCaravanRaidsInternal(SQLite.SQLiteConnection conn)
        {
            var results = new List<RaidResult>();
            var activeCaravans = conn.Table<Caravan>().ToList().Where(c => !c.IsRaided && !c.IsCompleted).ToList();
            if (activeCaravans.Count == 0) return results;

            var armies = conn.Table<Army>().ToList();
            if (armies.Count == 0) return results;

            var diplomacies = conn.Table<Diplomacy>().ToList();

            foreach (var caravan in activeCaravans)
            {
                Vector3 caravanPos = GetCaravanCurrentPosition(caravan);

                foreach (var army in armies)
                {
                    if (army.OwnerFactionId == caravan.OwnerFactionId)
                    {
                        continue;
                    }

                    // Düşmənlik/Müharibə yoxlanışı (TreatyType == 'Müharibə' və ya RelationScore <= -60)
                    bool isHostile = diplomacies.Any(d =>
                        ((d.FactionA_Id == army.OwnerFactionId && d.FactionB_Id == caravan.OwnerFactionId) ||
                         (d.FactionA_Id == caravan.OwnerFactionId && d.FactionB_Id == army.OwnerFactionId)) &&
                        (d.TreatyType == "Müharibə" || d.RelationScore <= -60));

                    if (!isHostile)
                    {
                        continue;
                    }

                    Vector3 armyPos = GetProvincePosition(army.LocationProvinceId);
                    float distance = Vector2.Distance(new Vector2(caravanPos.x, caravanPos.y), new Vector2(armyPos.x, armyPos.y));

                    // Əgər ordu karvanın yanındadırsa (radius daxilində və ya eyni əyalətdə)
                    bool inRaidRange = distance <= raidDistanceThreshold ||
                                       (army.LocationProvinceId == caravan.SourceProvinceId && caravan.TravelProgress < 25f) ||
                                       (army.LocationProvinceId == caravan.TargetProvinceId && caravan.TravelProgress > 75f);

                    if (inRaidRange)
                    {
                        RaidResult result = ExecuteRaidInternal(caravan, army, conn);
                        results.Add(result);
                        break; // Karvan yağmalandı, növbəti karvana keçirik
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Karvanı birbaşa verilmiş ordu tərəfindən yağmalayır (RaidCaravan).
        /// </summary>
        public RaidResult RaidCaravan(int caravanId, int armyId, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null) db = FindAnyObjectByType<DatabaseManager>();
            if (db == null || !db.IsConnected) return default;

            RaidResult result = default;
            db.RunInTransaction(conn =>
            {
                var caravan = conn.Table<Caravan>().FirstOrDefault(c => c.Id == caravanId);
                var army = conn.Table<Army>().FirstOrDefault(a => a.Id == armyId);
                if (caravan != null && army != null && !caravan.IsRaided && !caravan.IsCompleted)
                {
                    result = ExecuteRaidInternal(caravan, army, conn);
                }
            });

            return result;
        }

        private RaidResult ExecuteRaidInternal(Caravan caravan, Army army, SQLite.SQLiteConnection conn)
        {
            caravan.IsRaided = true;
            conn.Update(caravan);

            // Resursu dərhal Yağmalayan Ordunun mənsub olduğu dövlətin xəzinəsinə əlavə edirik
            var raidingNation = conn.Table<Nation>().FirstOrDefault(n => n.Id == army.OwnerFactionId);
            if (raidingNation != null)
            {
                AddResourceToNation(raidingNation, caravan.ResourceType, caravan.ResourceAmount);
                conn.Update(raidingNation);

                // Əyalət xəzinəsi ilə də sinxronlaşdırırıq
                var raidingProv = conn.Table<Province>().FirstOrDefault(p => p.Id == army.LocationProvinceId);
                if (raidingProv != null)
                {
                    AddResourceToProvince(raidingProv, caravan.ResourceType, caravan.ResourceAmount);
                    conn.Update(raidingProv);
                }
            }

            // UI Token effektini işə salırıq
            if (_activeTokens.TryGetValue(caravan.Id, out var token) && token != null)
            {
                token.PlayRaidEffect();
                _activeTokens.Remove(caravan.Id);
            }

            // Qırmızı hərflərlə konsol və UI bildirişi
            string raidAlert = $"KARVAN YAĞMALANDI! Fraksiya {army.OwnerFactionId} Fraksiya {caravan.OwnerFactionId}-nin {caravan.ResourceAmount:F0} {caravan.ResourceType} karvanını ələ keçirdi!";
            LastRaidAlertMessage = raidAlert;
            Debug.LogWarning($"<color=red><b>{raidAlert}</b></color>");

            var result = new RaidResult
            {
                Success = true,
                CaravanId = caravan.Id,
                RaidingArmyId = army.Id,
                RaidingFactionId = army.OwnerFactionId,
                VictimFactionId = caravan.OwnerFactionId,
                ResourceType = caravan.ResourceType,
                LootedAmount = caravan.ResourceAmount,
                Message = raidAlert
            };

            OnCaravanRaided?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Karvan hədəf nöqtəsinə çatdıqda resursu hədəf dövlətin xəzinəsinə təhvil verir.
        /// </summary>
        public void DeliverCaravan(int caravanId, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null) db = FindAnyObjectByType<DatabaseManager>();
            if (db == null || !db.IsConnected) return;

            db.RunInTransaction(conn =>
            {
                var caravan = conn.Table<Caravan>().FirstOrDefault(c => c.Id == caravanId);
                if (caravan != null && !caravan.IsRaided && !caravan.IsCompleted)
                {
                    DeliverCaravanInternal(caravan, conn);
                }
            });
        }

        private void DeliverCaravanInternal(Caravan caravan, SQLite.SQLiteConnection conn)
        {
            caravan.TravelProgress = 100f;
            caravan.IsCompleted = true;
            conn.Update(caravan);

            var targetProvince = conn.Table<Province>().FirstOrDefault(p => p.Id == caravan.TargetProvinceId);
            int targetFactionId = targetProvince != null ? targetProvince.OwnerFactionId : caravan.OwnerFactionId;

            var targetNation = conn.Table<Nation>().FirstOrDefault(n => n.Id == targetFactionId);
            if (targetNation != null)
            {
                AddResourceToNation(targetNation, caravan.ResourceType, caravan.ResourceAmount);
                conn.Update(targetNation);

                if (targetProvince != null)
                {
                    AddResourceToProvince(targetProvince, caravan.ResourceType, caravan.ResourceAmount);
                    conn.Update(targetProvince);
                }
            }

            if (_activeTokens.TryGetValue(caravan.Id, out var token) && token != null)
            {
                token.PlayArrivalEffect();
                _activeTokens.Remove(caravan.Id);
            }

            Debug.Log($"[TradeController] Karvan #{caravan.Id} təyinat nöqtəsinə çatdı! +{caravan.ResourceAmount:F0} {caravan.ResourceType} Dövlət #{targetFactionId} xəzinəsinə təhvil verildi.");
            OnCaravanDelivered?.Invoke(caravan, targetFactionId);
        }

        private static void AddResourceToNation(Nation nation, string resourceType, float amount)
        {
            if (nation == null) return;
            switch (resourceType)
            {
                case "Iron": nation.Iron += amount; break;
                case "Horses": nation.Horses += amount; break;
                case "Silk": nation.Silk += amount; break;
                case "Gold": nation.Gold += amount; break;
                case "Food":
                default: nation.Food += amount; break;
            }
        }

        private static void AddResourceToProvince(Province province, string resourceType, float amount)
        {
            if (province == null) return;
            switch (resourceType)
            {
                case "Iron": province.Iron += amount; break;
                case "Horses": province.Horses += amount; break;
                case "Silk": province.Silk += amount; break;
                case "Gold": province.Gold += amount; break;
                case "Food":
                default: province.Food += amount; break;
            }
        }
    }
}
