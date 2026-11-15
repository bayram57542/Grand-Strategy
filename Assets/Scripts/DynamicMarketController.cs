using System;
using System.Linq;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 8.1 - Qlobal Ticarət və Strateji Resurs Tipləri.
    /// </summary>
    public enum StrategicResourceType
    {
        Food = 0,
        Iron = 1,
        Horses = 2,
        Silk = 3
    }

    /// <summary>
    /// Faza 8.1 - Dinamik Bazar Mühərriki (DynamicMarketController):
    /// Baza qiymətlərinə və qlobal hadisələrə (Müharibə, Mühasirə, Sülh) əsasən
    /// tələb-təklif dalğalanmalarını idarə edir.
    /// Resurs alqı-satqısını (BuyResource, SellResource) təmin edir.
    /// </summary>
    public class DynamicMarketController : MonoBehaviour
    {
        private static DynamicMarketController _instance;
        public static DynamicMarketController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<DynamicMarketController>());

        public const float BasePriceFood = 1.0f;
        public const float BasePriceIron = 5.0f;
        public const float BasePriceHorses = 10.0f;
        public const float BasePriceSilk = 25.0f;

        [Header("Cari Bazar Qiymətləri (Current Market Prices)")]
        [SerializeField] private float currentPriceFood = BasePriceFood;
        [SerializeField] private float currentPriceIron = BasePriceIron;
        [SerializeField] private float currentPriceHorses = BasePriceHorses;
        [SerializeField] private float currentPriceSilk = BasePriceSilk;

        [Header("Dalğalanma Göstəriciləri")]
        [SerializeField] private int activeWarsCount;
        [SerializeField] private bool hasBesiegedProvinces;
        [SerializeField] private bool isPeaceEra = true;

        public float CurrentPriceFood => currentPriceFood;
        public float CurrentPriceIron => currentPriceIron;
        public float CurrentPriceHorses => currentPriceHorses;
        public float CurrentPriceSilk => currentPriceSilk;

        public int ActiveWarsCount => activeWarsCount;
        public bool HasBesiegedProvinces => hasBesiegedProvinces;
        public bool IsPeaceEra => isPeaceEra;

        public event Action<float, float, float, float> OnMarketPricesChanged;

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
        /// Resurs tipinə görə cari bazar qiymətini qaytarır.
        /// </summary>
        public float GetPrice(StrategicResourceType type)
        {
            switch (type)
            {
                case StrategicResourceType.Food:
                    return currentPriceFood;
                case StrategicResourceType.Iron:
                    return currentPriceIron;
                case StrategicResourceType.Horses:
                    return currentPriceHorses;
                case StrategicResourceType.Silk:
                    return currentPriceSilk;
                default:
                    return 1.0f;
            }
        }

        public float GetPrice(string resourceName)
        {
            if (Enum.TryParse(resourceName, true, out StrategicResourceType parsedType))
            {
                return GetPrice(parsedType);
            }

            return 1.0f;
        }

        /// <summary>
        /// TimeManager-in aylıq Tick döngüsündə çağırılır (ProcessMarketFluctuations):
        /// 1. Müharibə Təsiri: Aktiv müharibələrin sayı çoxdursa (Qlobal gərginlik), Iron və Horses qiymətləri 2.0x - 3.0x qalxır.
        /// 2. Mühasirə Təsiri: Əgər hər hansı əyalət mühasirədədirsə, Food qiyməti 5.0x (5 qat) artır.
        /// 3. Sülh Təsiri: Qlobal sülh dövründə (aktiv müharibə yoxdursa), Silk qiyməti tələbata görə (1.8x) artır.
        /// </summary>
        public void ProcessMarketFluctuations(DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                return;
            }

            try
            {
                var diplomacies = db.Connection.Table<Diplomacy>().ToList();
                var provinces = db.Connection.Table<Province>().ToList();

                // 1. Aktiv müharibələrin sayılması (TreatyType == "Müharibə" və ya RelationScore <= -60)
                activeWarsCount = diplomacies.Count(d =>
                    string.Equals(d.TreatyType, "Müharibə", StringComparison.OrdinalIgnoreCase) ||
                    d.RelationScore <= -60);

                isPeaceEra = (activeWarsCount == 0);

                // Müharibə Təsiri: Dəmir və At qiymətləri qalxır
                if (activeWarsCount > 0)
                {
                    // 1 müharibə: 2.0x, çox müharibə: 3.0x-ə qədər
                    float warMultiplier = Mathf.Clamp(2.0f + (activeWarsCount - 1) * 0.5f, 2.0f, 3.0f);
                    currentPriceIron = BasePriceIron * warMultiplier;
                    currentPriceHorses = BasePriceHorses * warMultiplier;
                }
                else
                {
                    currentPriceIron = BasePriceIron;
                    currentPriceHorses = BasePriceHorses;
                }

                // 2. Mühasirə Təsiri: Mühasirədə olan əyalət varsa Food 5.0x artır
                hasBesiegedProvinces = provinces.Any(p => p.IsBesieged);
                if (hasBesiegedProvinces)
                {
                    currentPriceFood = BasePriceFood * 5.0f;
                }
                else
                {
                    currentPriceFood = BasePriceFood;
                }

                // 3. Sülh Təsiri: Sülh dövründə lüks ticarət (Silk) 1.8x artır
                if (isPeaceEra)
                {
                    currentPriceSilk = BasePriceSilk * 1.8f;
                }
                else
                {
                    currentPriceSilk = BasePriceSilk;
                }

                OnMarketPricesChanged?.Invoke(currentPriceFood, currentPriceIron, currentPriceHorses, currentPriceSilk);

                Debug.Log(
                    $"[DynamicMarketController] Bazar Dalğalanması İcra Edildi: " +
                    $"Food={currentPriceFood:F1} (Mühasirə: {hasBesiegedProvinces}), " +
                    $"Iron={currentPriceIron:F1}, Horses={currentPriceHorses:F1} (Aktiv Müharibələr: {activeWarsCount}), " +
                    $"Silk={currentPriceSilk:F1} (Sülh Dövrü: {isPeaceEra})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DynamicMarketController] ProcessMarketFluctuations xətası: {ex.Message}");
            }
        }

        /// <summary>
        /// Dövlət üçün bazardan resurs satın alır (BuyResource):
        /// Xəzinədən qızıl çıxılır, alınan resurs dövlətin ehtiyatına əlavə edilir.
        /// </summary>
        public bool BuyResource(int nationId, StrategicResourceType type, int amount, DatabaseManager dbManager = null)
        {
            if (amount <= 0)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null || !db.IsConnected)
            {
                return false;
            }

            float price = GetPrice(type);
            float totalCost = price * amount;
            bool success = false;

            db.RunInTransaction(conn =>
            {
                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == nationId);
                if (nation == null || nation.Gold < totalCost)
                {
                    Debug.LogWarning($"[DynamicMarketController] Satınalma uğursuz: Qızıl çatışmır (Tələb: {totalCost:F1}, Mövcud: {nation?.Gold ?? 0:F1})");
                    return;
                }

                nation.Gold -= totalCost;
                switch (type)
                {
                    case StrategicResourceType.Food:
                        nation.Food += amount;
                        break;
                    case StrategicResourceType.Iron:
                        nation.Iron += amount;
                        break;
                    case StrategicResourceType.Horses:
                        nation.Horses += amount;
                        break;
                    case StrategicResourceType.Silk:
                        nation.Silk += amount;
                        break;
                }

                conn.Update(nation);

                // Əgər dövlətin paytaxt əyaləti varsa, onunla da sinxronlaşdırırıq
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.OwnerFactionId == nationId);
                if (prov != null)
                {
                    prov.Gold = nation.Gold;
                    prov.Food = nation.Food;
                    prov.Iron = nation.Iron;
                    prov.Horses = nation.Horses;
                    prov.Silk = nation.Silk;
                    conn.Update(prov);
                }

                success = true;
                Debug.Log($"[DynamicMarketController] Satınalma Uğurlu: Dövlət #{nationId} {amount} {type} aldı (-{totalCost:F1} Qızıl, Qalan: {nation.Gold:F1})");
            });

            return success;
        }

        public bool BuyResource(int nationId, string resourceName, int amount, DatabaseManager dbManager = null)
        {
            if (Enum.TryParse(resourceName, true, out StrategicResourceType parsedType))
            {
                return BuyResource(nationId, parsedType, amount, dbManager);
            }
            return false;
        }

        /// <summary>
        /// Dövlətin xəzinəsindəki resursu bazara satır (SellResource):
        /// Resurs ehtiyatından çıxılır, qazanc dövlətin Qızıl xəzinəsinə əlavə olunur.
        /// </summary>
        public bool SellResource(int nationId, StrategicResourceType type, int amount, DatabaseManager dbManager = null)
        {
            if (amount <= 0)
            {
                return false;
            }

            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null || !db.IsConnected)
            {
                return false;
            }

            float price = GetPrice(type);
            float totalRevenue = price * amount;
            bool success = false;

            db.RunInTransaction(conn =>
            {
                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == nationId);
                if (nation == null)
                {
                    return;
                }

                bool hasEnough = false;
                switch (type)
                {
                    case StrategicResourceType.Food:
                        hasEnough = nation.Food >= amount;
                        if (hasEnough) nation.Food -= amount;
                        break;
                    case StrategicResourceType.Iron:
                        hasEnough = nation.Iron >= amount;
                        if (hasEnough) nation.Iron -= amount;
                        break;
                    case StrategicResourceType.Horses:
                        hasEnough = nation.Horses >= amount;
                        if (hasEnough) nation.Horses -= amount;
                        break;
                    case StrategicResourceType.Silk:
                        hasEnough = nation.Silk >= amount;
                        if (hasEnough) nation.Silk -= amount;
                        break;
                }

                if (!hasEnough)
                {
                    Debug.LogWarning($"[DynamicMarketController] Satış uğursuz: Dövlət #{nationId} xəzinəsində kifayət qədər {type} yoxdur.");
                    return;
                }

                nation.Gold += totalRevenue;
                conn.Update(nation);

                Province prov = conn.Table<Province>().FirstOrDefault(p => p.OwnerFactionId == nationId);
                if (prov != null)
                {
                    prov.Gold = nation.Gold;
                    prov.Food = nation.Food;
                    prov.Iron = nation.Iron;
                    prov.Horses = nation.Horses;
                    prov.Silk = nation.Silk;
                    conn.Update(prov);
                }

                success = true;
                Debug.Log($"[DynamicMarketController] Satış Uğurlu: Dövlət #{nationId} {amount} {type} satdım (+{totalRevenue:F1} Qızıl, Ümumi: {nation.Gold:F1})");
            });

            return success;
        }

        public bool SellResource(int nationId, string resourceName, int amount, DatabaseManager dbManager = null)
        {
            if (Enum.TryParse(resourceName, true, out StrategicResourceType parsedType))
            {
                return SellResource(nationId, parsedType, amount, dbManager);
            }
            return false;
        }
    }
}
