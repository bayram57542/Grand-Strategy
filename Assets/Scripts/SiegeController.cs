using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Yağmalama (Plunder) əməliyyatının nəticə strukturu.
    /// </summary>
    public struct PlunderResult
    {
        public bool Success;
        public int ProvinceId;
        public string ProvinceName;
        public float GoldGained;
        public float FoodGained;
        public float NewProductivityMultiplier;
        public float NewPublicOrder;
    }

    /// <summary>
    /// Faza 6C - Mühasirə Logistikası və Taktikaları (SiegeController):
    /// Əyalətlərin mühasirəyə alınmasını (SetBesieged), qlobal xəzinə ilə əlaqənin kəsilməsini,
    /// Yağmalama (Plunder) və aylıq İstismar (Exploit) siyasətlərini idarə edir.
    /// </summary>
    public class SiegeController : MonoBehaviour
    {
        private static SiegeController _instance;
        public static SiegeController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<SiegeController>());

        [Header("Yağmalama (Plunder) Parametrləri")]
        [Tooltip("Yağmalama zamanı əldə edilən anlıq Qızıl miqdarı (2000)")]
        [SerializeField, Min(0f)] private float plunderGoldReward = 2000f;

        [Tooltip("Yağmalama zamanı əldə edilən anlıq Ərzaq miqdarı (2000)")]
        [SerializeField, Min(0f)] private float plunderFoodReward = 2000f;

        [Tooltip("Yağmalamadan sonra kənd dağıldığı üçün düşən məhsuldarlıq səviyyəsi (0.2x = 20%)")]
        [SerializeField, Range(0.01f, 1f)] private float plunderProductivityMultiplier = 0.2f;

        [Header("İstismar (Exploit) Parametrləri")]
        [Tooltip("İstismar taktikası tətbiq edilən mühasirəli əyalətdə hər ay xalqın moralının düşmə tempi (5 bal)")]
        [SerializeField, Min(0f)] private float monthlyExploitOrderDecay = 5.0f;

        public float PlunderGoldReward => plunderGoldReward;
        public float PlunderFoodReward => plunderFoodReward;
        public float PlunderProductivityMultiplier => plunderProductivityMultiplier;
        public float MonthlyExploitOrderDecay => monthlyExploitOrderDecay;

        public event Action<int, bool> OnProvinceSiegeStateChanged;
        public event Action<PlunderResult> OnProvincePlundered;
        public event Action<int, string> OnProvinceExploited;

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

        private static int ParseProvinceId(string provinceIdStr)
        {
            if (string.IsNullOrWhiteSpace(provinceIdStr))
            {
                return -1;
            }

            if (int.TryParse(provinceIdStr, out int id))
            {
                return id;
            }

            string digits = System.Text.RegularExpressions.Regex.Match(provinceIdStr, @"\d+").Value;
            if (int.TryParse(digits, out id))
            {
                return id;
            }

            return -1;
        }

        /// <summary>
        /// Əyaləti mühasirəyə alır və ya mühasirəni qaldırır.
        /// Mühasirə aktiv olduqda əyalətdəki resurslar qlobal (Nation) xəzinəyə axmağı dayandırır.
        /// </summary>
        public bool SetBesieged(string provinceId, bool isBesieged, DatabaseManager dbManager = null)
        {
            int id = ParseProvinceId(provinceId);
            if (id <= 0)
            {
                Debug.LogWarning($"[SiegeController] SetBesieged uğursuz: '{provinceId}' ID-yə çevrilə bilmədi.");
                return false;
            }

            return SetBesieged(id, isBesieged, dbManager);
        }

        /// <summary>
        /// int ID ilə mühasirə vəziyyətini təyin edir.
        /// </summary>
        public bool SetBesieged(int provinceId, bool isBesieged, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                Debug.LogWarning("[SiegeController] SetBesieged uğursuz: DatabaseManager hazır deyil.");
                return false;
            }

            bool updated = false;
            db.RunInTransaction(conn =>
            {
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                if (prov == null)
                {
                    Debug.LogWarning($"[SiegeController] SetBesieged: #{provinceId} nömrəli əyalət tapılmadı.");
                    return;
                }

                prov.IsBesieged = isBesieged;
                if (!isBesieged)
                {
                    prov.IsExploited = false;
                }

                conn.Update(prov);
                updated = true;

                Debug.Log($"[SiegeController] Əyalət '{prov.Name}' (ID: {prov.Id}) Mühasirə Statusu: {(isBesieged ? "MÜHASİRƏDƏDİR (Qlobal xəzinə axını dayandı)" : "MÜHASİRƏDƏN ÇIXDI")}");
            });

            if (updated)
            {
                OnProvinceSiegeStateChanged?.Invoke(provinceId, isBesieged);
            }

            return updated;
        }

        /// <summary>
        /// Yağmalama (Plunder):
        /// Oyunçuya anında 2000 Qızıl və 2000 Ərzaq verir.
        /// Lakin kənd dağıldığı üçün ProductivityMultiplier 0.2f-ə düşür, PublicOrder isə 0 olur.
        /// </summary>
        public PlunderResult Plunder(string provinceId, DatabaseManager dbManager = null)
        {
            int id = ParseProvinceId(provinceId);
            if (id <= 0)
            {
                Debug.LogWarning($"[SiegeController] Plunder uğursuz: '{provinceId}' ID-yə çevrilə bilmədi.");
                return default;
            }

            return Plunder(id, dbManager);
        }

        /// <summary>
        /// int ID ilə Yağmalama (Plunder) əmrini icra edir.
        /// </summary>
        public PlunderResult Plunder(int provinceId, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                Debug.LogWarning("[SiegeController] Plunder uğursuz: DatabaseManager hazır deyil.");
                return default;
            }

            PlunderResult result = default;
            db.RunInTransaction(conn =>
            {
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                if (prov == null)
                {
                    Debug.LogWarning($"[SiegeController] Plunder: #{provinceId} nömrəli əyalət tapılmadı.");
                    return;
                }

                // Kənd dağıdılır: məhsuldarlıq 0.2f-ə düşür, xalqın moralı 0 olur
                prov.ProductivityMultiplier = plunderProductivityMultiplier;
                prov.PublicOrder = 0f;
                conn.Update(prov);

                // Oyunçunun/Dövlətin (Faction 1) xəzinəsinə anında 2000 Qızıl və 2000 Ərzaq əlavə edilir
                Nation playerNation = conn.Table<Nation>().FirstOrDefault(n => n.Id == 1)
                                      ?? conn.Table<Nation>().FirstOrDefault();

                if (playerNation != null)
                {
                    playerNation.Gold += plunderGoldReward;
                    playerNation.Food += plunderFoodReward;
                    conn.Update(playerNation);
                }

                result = new PlunderResult
                {
                    Success = true,
                    ProvinceId = prov.Id,
                    ProvinceName = prov.Name,
                    GoldGained = plunderGoldReward,
                    FoodGained = plunderFoodReward,
                    NewProductivityMultiplier = prov.ProductivityMultiplier,
                    NewPublicOrder = prov.PublicOrder
                };

                Debug.LogWarning(
                    $"[SiegeController] YAĞMALAMA (PLUNDER) İCRA EDİLDİ! -> Əyalət '{prov.Name}': " +
                    $"+{plunderGoldReward:F0} Qızıl və +{plunderFoodReward:F0} Ərzaq əldə edildi. " +
                    $"Məhsuldarlıq {prov.ProductivityMultiplier * 100f:F0}%-ə düşdü, Xalqın Moralı (PublicOrder) = 0.");
            });

            if (result.Success)
            {
                OnProvincePlundered?.Invoke(result);
            }

            return result;
        }

        /// <summary>
        /// İstismar (Exploit):
        /// Əyalətin hər aylıq (Tick) ərzaq istehsalı birbaşa mühasirə edən orduya/xəzinəyə yönləndirilir,
        /// lakin xalq əzildiyi üçün hər ay PublicOrder -= 5 düşür.
        /// </summary>
        public bool Exploit(string provinceId, DatabaseManager dbManager = null)
        {
            int id = ParseProvinceId(provinceId);
            if (id <= 0)
            {
                Debug.LogWarning($"[SiegeController] Exploit uğursuz: '{provinceId}' ID-yə çevrilə bilmədi.");
                return false;
            }

            return Exploit(id, dbManager);
        }

        /// <summary>
        /// int ID ilə İstismar (Exploit) siyasətini başladır.
        /// </summary>
        public bool Exploit(int provinceId, DatabaseManager dbManager = null)
        {
            DatabaseManager db = dbManager != null ? dbManager : DatabaseManager.Instance;
            if (db == null)
            {
                db = FindAnyObjectByType<DatabaseManager>();
            }

            if (db == null || !db.IsConnected)
            {
                Debug.LogWarning("[SiegeController] Exploit uğursuz: DatabaseManager hazır deyil.");
                return false;
            }

            bool updated = false;
            db.RunInTransaction(conn =>
            {
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                if (prov == null)
                {
                    Debug.LogWarning($"[SiegeController] Exploit: #{provinceId} nömrəli əyalət tapılmadı.");
                    return;
                }

                prov.IsBesieged = true;
                prov.IsExploited = true;
                conn.Update(prov);
                updated = true;

                Debug.LogWarning($"[SiegeController] İSTİSMAR (EXPLOIT) BAŞLADILDI! -> Əyalət '{prov.Name}': Hər ay ərzaq mühasirəçilərə axacaq, PublicOrder hər ay -{monthlyExploitOrderDecay} düşəcək.");
            });

            if (updated)
            {
                OnProvinceExploited?.Invoke(provinceId, "Exploit");
            }

            return updated;
        }

        /// <summary>
        /// Hər Aylıq Tick-də GameManager tərəfindən çağırılır:
        /// İstismar taktikası olan mühasirəli əyalətlərdə hər ay PublicOrder -= 5 düşür
        /// və əyalətin aylıq ərzaq istehsalı mühasirə edən dövlətin (Faction 1) xəzinəsinə köçürülür.
        /// </summary>
        public void ProcessMonthlySiege(DatabaseManager dbManager, int month, int year)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                return;
            }

            dbManager.RunInTransaction(conn =>
            {
                List<Province> provinces = conn.Table<Province>().ToList();
                List<Economy> economies = conn.Table<Economy>().ToList();
                Nation playerNation = conn.Table<Nation>().FirstOrDefault(n => n.Id == 1)
                                      ?? conn.Table<Nation>().FirstOrDefault();

                var ecoByProvince = new Dictionary<int, Economy>(economies.Count);
                foreach (var eco in economies)
                {
                    ecoByProvince[eco.ProvinceId] = eco;
                }

                for (int i = 0; i < provinces.Count; i++)
                {
                    Province prov = provinces[i];
                    if (prov.IsBesieged && prov.IsExploited)
                    {
                        // 1. Xalq əzildiyi üçün hər ay PublicOrder -= 5 düşür
                        prov.PublicOrder = Mathf.Clamp(prov.PublicOrder - monthlyExploitOrderDecay, 0f, 100f);

                        // 2. Əyalətin hər aylıq ərzaq istehsalı birbaşa mühasirə edən qüvvələrin xəzinəsinə yönləndirilir
                        if (ecoByProvince.TryGetValue(prov.Id, out Economy eco) && playerNation != null)
                        {
                            float monthlyFood = Mathf.Max(0f, eco.FoodProduction);
                            playerNation.Food += monthlyFood;
                        }

                        conn.Update(prov);
                        Debug.Log(
                            $"[SiegeController - Aylıq İstismar] Ay {month}/{year} - '{prov.Name}': " +
                            $"PublicOrder -{monthlyExploitOrderDecay} -> {prov.PublicOrder:F0}/100 | " +
                            $"Mühasirə ordusuna/xəzinəsinə ərzaq təminatı təmin edildi.");
                    }
                }

                if (playerNation != null)
                {
                    conn.Update(playerNation);
                }
            });
        }
    }
}
