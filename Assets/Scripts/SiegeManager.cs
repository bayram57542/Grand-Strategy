using System;
using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Military;
using GrandStrategy.UI;
using UnityEngine;

namespace GrandStrategy.Military
{
    /// <summary>
    /// Faza 10.3 - Mühasirə və Real Logistika Məlumat Strukturu.
    /// Ordunun aylıq tələbatı (ArmyConsumption) ilə əyalətin yerli ərzaq istehsalı (LocalFood) müqayisə edilir.
    /// </summary>
    [Serializable]
    public class SiegeLogisticsData
    {
        public ArmyVisualController BesiegingArmy;
        public MapInteractionController BesiegedProvince;

        [Header("Logistika Göstəriciləri")]
        public float ArmyConsumption;       // Aylıq tələbat (hər 1000 əsgərə 1 ərzaq)
        public float LocalFoodProduction;   // Əyalətin yerli ərzaq istehsalı
        public bool IsSurplus;              // LocalFood >= ArmyConsumption
        public float SurplusAmount;         // Artıq miqdar (əyalətdə qalır)
        public float DeficitAmount;         // Kəsir miqdar (dövlət xəzinəsindən tələb olunur)
        public float TakenFromProvince;     // Əyalətdən götürülən ərzaq
        public float DrawnFromState;        // Dövlət anbarından götürülən ərzaq
        public bool HasStarvation;          // Dövlətin ərzağı çatmadıqda aclıq başlayır
        public int AttritionLosses;         // Aclıq itkiləri

        [Header("Qərarlar")]
        public bool IsExploited;            // "İstismar Et" siyasəti tətbiq edildi
        public bool IsPlundered;            // "Yağmala" siyasəti tətbiq edildi
        public float PlunderGold;           // Yağmalama qızıl qəniməti
        public float PlunderFood;           // Yağmalama ərzaq qəniməti

        public string SummaryText;
    }

    /// <summary>
    /// Faza 10.3 - Mühasirə və Logistika Baş Meneceri (SiegeManager).
    /// Düşmən əyalətinin mühasirəyə alınmasını, əyalətin qlobal xəzinə ilə əlaqəsinin kəsilməsini (IsBesieged = true),
    /// "Yağmala" (Pillage) və "İstismar Et" (Exploit) real logistika hesablama mexanizmlərini idarə edir.
    /// </summary>
    public class SiegeManager : MonoBehaviour
    {
        private static SiegeManager _instance;
        public static SiegeManager Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_2023_1_OR_NEWER
                    _instance = UnityEngine.Object.FindAnyObjectByType<SiegeManager>();
#else
                    _instance = UnityEngine.Object.FindObjectOfType<SiegeManager>();
#endif
                }
                return _instance;
            }
        }

        [Header("Logistika Tənzimləmələri")]
        [Tooltip("Hər 1000 əsgər üçün aylıq ərzaq istehlakı (Varsayılan: 1.0f)")]
        [SerializeField] private float foodPerThousandTroops = 1.0f;

        [Tooltip("Faza 10.12: Köhnə SiegePanel-in avtomatik açılmasını deaktiv edir (prioritet CombatManager-dədir)")]
        [SerializeField] private bool suppressLegacySiegePopup = true;

        [Header("Aktiv Mühasirə")]
        [SerializeField] private SiegeLogisticsData currentSiege;

        public SiegeLogisticsData CurrentSiege => currentSiege;
        public float FoodPerThousandTroops { get => foodPerThousandTroops; set => foodPerThousandTroops = value; }
        public bool SuppressLegacySiegePopup { get => suppressLegacySiegePopup; set => suppressLegacySiegePopup = value; }

        public event Action<SiegeLogisticsData> OnSiegeStarted;
        public event Action<SiegeLogisticsData> OnSiegeExploited;
        public event Action<SiegeLogisticsData> OnSiegePlundered;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>
        /// Ordu düşmən qalasının mərkəzinə yaxınlaşdıqda mühasirəni başladır.
        /// Əyalətin mərkəz dövlətlə əlaqəsi kəsilir (IsBesieged = true), aylıq qızıl gəliri sahibinə getmir.
        /// </summary>
        public SiegeLogisticsData StartSiege(ArmyVisualController army, MapInteractionController province)
        {
            if (province == null) return null;

            // 1. Əyalət mühasirəyə alınır və mərkəz dövlətlə əlaqəsi kəsilir
            province.SetBesieged(true);

            // 2. Real Logistika və ərzaq hesablaması aparılır
            currentSiege = CalculateLogistics(army, province);

            Debug.Log($"[SiegeManager] 🏰 MÜHASİRƏ LOGİSTİKASI: Əyalət: '{province.ProvinceName}', Ordu: '{army.ArmyData.CommanderName}' ({army.ArmyData.FormattedTroopCount}) | " +
                      $"İstehlak: {currentSiege.ArmyConsumption:F1} /ay | Yerli Ərzaq: {currentSiege.LocalFoodProduction:F1} /ay | Status: {(currentSiege.IsSurplus ? $"ARTIQ (+{currentSiege.SurplusAmount:F1})" : $"KƏSİR (-{currentSiege.DeficitAmount:F1})")}");

            // 3. Köhnə SiegePanel avtomatik açılmır (CombatManager öncəliklidir)
            if (UIManager.Instance != null)
            {
                if (!suppressLegacySiegePopup)
                {
                    UIManager.Instance.ShowSiegeLogisticsPanel(currentSiege);
                }
                else
                {
                    UIManager.Instance.HideSiegeLogisticsPanel();
                }
            }

            OnSiegeStarted?.Invoke(currentSiege);
            return currentSiege;
        }

        /// <summary>
        /// Ordunun aylıq ərzaq istehlakını (ArmyConsumption) və əyalətin yerli ərzaq istehsalını (LocalFood) müqayisə edir.
        /// </summary>
        public SiegeLogisticsData CalculateLogistics(ArmyVisualController army, MapInteractionController province)
        {
            var data = new SiegeLogisticsData
            {
                BesiegingArmy = army,
                BesiegedProvince = province
            };

            int troops = (army != null && army.ArmyData != null) ? army.ArmyData.TroopCount : 25000;
            // Düstur: hər 1000 əsgər = 1 Ərzaq/ay
            data.ArmyConsumption = (troops / 1000.0f) * foodPerThousandTroops;

            var eco = province.EconomyData;
            data.LocalFoodProduction = eco != null ? eco.FoodProduction : 50.0f;

            if (data.LocalFoodProduction >= data.ArmyConsumption)
            {
                // HAL 1: LocalFood >= ArmyConsumption (ARTIQ)
                // Ordu tam doyur, dövlətdən heç bir ərzaq çəkilmir. Artıq qalan ərzaq əyalətdə qalır.
                data.IsSurplus = true;
                data.SurplusAmount = data.LocalFoodProduction - data.ArmyConsumption;
                data.DeficitAmount = 0.0f;
                data.TakenFromProvince = data.ArmyConsumption;
                data.DrawnFromState = 0.0f;
                data.HasStarvation = false;
                data.AttritionLosses = 0;

                data.SummaryText = $"🟢 ARTIQ: +{data.SurplusAmount:F1} /ay\n" +
                    $"Ordu yerli resurslarla tam doyur ({data.ArmyConsumption:F1} /ay). Dövlət xəzinəsindən 0 ərzaq xərclənir. +{data.SurplusAmount:F1} ərzaq əyalətdə qalır.";
            }
            else
            {
                // HAL 2: LocalFood < ArmyConsumption (KƏSİR)
                // Ordu əyalətdəki bütün ərzağı alır. Çatışmayan fərq dövlətin mərkəzi anbarı hesabına ödənməlidir.
                data.IsSurplus = false;
                data.SurplusAmount = 0.0f;
                data.DeficitAmount = data.ArmyConsumption - data.LocalFoodProduction;
                data.TakenFromProvince = data.LocalFoodProduction;
                data.DrawnFromState = data.DeficitAmount;

                data.SummaryText = $"🔴 KƏSİR: -{data.DeficitAmount:F1} /ay\n" +
                    $"Əyalətin yerli ərzağı çatmır ({data.LocalFoodProduction:F1} /ay). Ordu bütün yerli ərzağı alır, çatışmayan {data.DeficitAmount:F1} ərzaq dövlətin mərkəzi anbarı hesabına təmin edilməlidir!";
            }

            return data;
        }

        /// <summary>
        /// "İstismar Et" (Exploit) seçimini icra edir:
        /// Yerli ərzaq ordunun tələbatına yönləndirilir. Kəsir varsa dövlət anbarından ödənir, çatışmazsa aclıq başlayır.
        /// </summary>
        public bool ExecuteExploit(SiegeLogisticsData data = null)
        {
            if (data == null) data = currentSiege;
            if (data == null || data.BesiegedProvince == null) return false;

            data.IsExploited = true;
            data.BesiegedProvince.IsExploited = true;

            // Əgər kəsir varsa və dövlət anbarından ərzaq çəkilməlidirsə
            if (!data.IsSurplus && data.DeficitAmount > 0)
            {
                var db = DatabaseManager.Instance;
                if (db != null && db.IsConnected)
                {
                    int factionId = data.BesiegingArmy != null && data.BesiegingArmy.ArmyData != null ? data.BesiegingArmy.ArmyData.FactionId : 1;
                    var nation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == factionId)
                                 ?? db.Connection.Table<Nation>().FirstOrDefault();

                    if (nation != null)
                    {
                        if (nation.Food >= data.DeficitAmount)
                        {
                            nation.Food -= data.DeficitAmount;
                            db.Connection.Update(nation);
                            data.HasStarvation = false;
                            Debug.Log($"[SiegeManager] Dövlət Anbarından -{data.DeficitAmount:F1} ərzaq təchiz edildi. Qalan dövlət ərzağı: {nation.Food:F1}");
                        }
                        else
                        {
                            // Dövlətin ərzağı çatmadı! Aclıq və itkilər (Attrition) başlayır
                            float missing = data.DeficitAmount - nation.Food;
                            nation.Food = 0f;
                            db.Connection.Update(nation);

                            data.HasStarvation = true;
                            data.AttritionLosses = Mathf.CeilToInt(missing * 25.0f);

                            if (data.BesiegingArmy != null && data.BesiegingArmy.ArmyData != null)
                            {
                                data.BesiegingArmy.ArmyData.TroopCount = Mathf.Max(100, data.BesiegingArmy.ArmyData.TroopCount - data.AttritionLosses);
                                data.BesiegingArmy.ArmyData.Morale = Mathf.Max(0.4f, data.BesiegingArmy.ArmyData.Morale - 0.25f);
                                data.BesiegingArmy.ApplyData();
                            }

                            Debug.LogWarning($"[SiegeManager] ⚠️ ACLIQ VƏ TƏCHİZAT BÖHRANI! Dövlətin ərzağı bitdi! İtkilər: -{data.AttritionLosses} əsgər.");
                        }
                    }
                }
            }

            if (SiegeController.Instance != null && data.BesiegedProvince.ProvinceId > 0)
            {
                SiegeController.Instance.Exploit(data.BesiegedProvince.ProvinceId);
            }

            Debug.Log($"[SiegeManager] 'İstismar Et' təsdiqləndi: '{data.BesiegedProvince.ProvinceName}'");
            OnSiegeExploited?.Invoke(data);
            return true;
        }

        /// <summary>
        /// "Yağmala" (Pillage) seçimini icra edir:
        /// Dərhal birdəfəlik Qızıl/Ərzaq qəniməti verir, lakin əyalətin inkişaf səviyyəsini (DevelopmentLevel) və istehsalını çökdürür.
        /// </summary>
        public bool ExecutePillage(SiegeLogisticsData data = null)
        {
            if (data == null) data = currentSiege;
            if (data == null || data.BesiegedProvince == null) return false;

            data.IsPlundered = true;
            data.BesiegedProvince.IsPlundered = true;
            data.PlunderGold = 2000.0f;
            data.PlunderFood = 1500.0f;

            // Əyalətin inkişaf səviyyəsi və istehsalı çökdürülür
            if (data.BesiegedProvince.EconomyData != null)
            {
                data.BesiegedProvince.EconomyData.DevelopmentLevel = 1;
                data.BesiegedProvince.EconomyData.FoodProduction *= 0.2f;
                data.BesiegedProvince.EconomyData.GoldIncome = 0f;
            }

            // Oyunçunun dövlət xəzinəsinə qənimət əlavə edilir
            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.AddPlayerResources(data.PlunderGold, data.PlunderFood, 0f);
            }

            var db = DatabaseManager.Instance;
            if (db != null && db.IsConnected)
            {
                int factionId = data.BesiegingArmy != null && data.BesiegingArmy.ArmyData != null ? data.BesiegingArmy.ArmyData.FactionId : 1;
                var nation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == factionId)
                             ?? db.Connection.Table<Nation>().FirstOrDefault();
                if (nation != null)
                {
                    nation.Gold += data.PlunderGold;
                    nation.Food += data.PlunderFood;
                    db.Connection.Update(nation);
                }
            }

            if (SiegeController.Instance != null && data.BesiegedProvince.ProvinceId > 0)
            {
                SiegeController.Instance.Plunder(data.BesiegedProvince.ProvinceId);
            }

            Debug.LogWarning($"[SiegeManager] ⚔️ ƏYALƏT YAĞMALANDI! '{data.BesiegedProvince.ProvinceName}': +{data.PlunderGold:F0} Qızıl, +{data.PlunderFood:F0} Ərzaq qazanıldı. İnkişaf səviyyəsi çökdü!");
            OnSiegePlundered?.Invoke(data);
            return true;
        }

        /// <summary>
        /// Əyalətdəki mühasirəni qaldırır və ordunun vəziyyətini normala qaytarır.
        /// </summary>
        public void EndSiege(MapInteractionController province)
        {
            if (province == null) return;

            province.SetBesieged(false);

            if (currentSiege != null && currentSiege.BesiegedProvince == province)
            {
                if (currentSiege.BesiegingArmy != null && currentSiege.BesiegingArmy.MovementController != null)
                {
                    currentSiege.BesiegingArmy.MovementController.StopMovement();
                }
                currentSiege = null;
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideSiegeLogisticsPanel();
            }
        }
    }
}
