using System;
using System.Collections.Generic;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Military;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 10.5 - Dərin Demoqrafiya Sistemi:
    /// Hərbi səfərbərlik qərarları (Kişilər, Elit Gənclər, Qadınlar), Xəstələrə resursun kəsilməsi
    /// və aylıq demoqrafik keçid (doğum, yaşlanma, ölüm, xəstə/yaralıların sağalması).
    /// </summary>
    public static class DemographicsSystem
    {
        public const int MenConscriptionAmount = 1000;
        public const int EliteYouthAmount = 500;
        public const float EliteYouthGoldCost = 250f;
        public const int WomenMobilizationAmount = 1000;

        // Aylıq demoqrafik əmsallar
        public const float MonthlyBirthRate = 0.004f;          // cüt başına aylıq doğum ehtimalı
        public const float InfantToYouthRate = 1f / 60f;       // ~5 il
        public const float YouthToAdultRate = 1f / 120f;       // ~10 il
        public const float AdultToElderlyRate = 1f / 480f;     // ~40 il
        public const float ElderlyDeathRate = 1f / 120f;       // ~10 il
        public const float InfantMortalityRate = 0.002f;
        public const float NewSicknessRate = 0.0004f;          // işləyən əhalidən aylıq xəstələnmə
        public const float SickNaturalRecoveryRate = 0.05f;
        public const float SickDeathRate = 0.02f;
        public const float SickHealRate = 0.40f;               // dövlət resurs ayırdıqda
        public const float WoundedHealRate = 0.50f;            // dövlət resurs ayırdıqda
        public const float WoundedNaturalRecoveryRate = 0.05f;
        public const float HealingGoldCostPerPerson = 0.02f;

        public static event Action<MapInteractionController, string> OnDemographicActionExecuted;

        #region Hərbi Səfərbərlik və Qərarlar

        /// <summary>
        /// "⚔️ Kişiləri Orduya Çağır (+1,000)": AdultMen -1000, ən yaxın komandirin ordusu +1000.
        /// </summary>
        public static bool ConscriptMen(MapInteractionController province, out string message, out ArmyVisualController army)
        {
            army = null;
            message = string.Empty;
            if (province == null) { message = "Əyalət seçilməyib."; return false; }

            var d = province.Demographics;
            if (d.AdultMen < MenConscriptionAmount)
            {
                message = $"Kifayət qədər Yetişkin Kişi yoxdur ({d.AdultMen:N0} < {MenConscriptionAmount:N0}).";
                return false;
            }

            army = FindNearestArmy(province);
            if (army == null) { message = "Yaxınlıqda komandir tapılmadı."; return false; }

            d.AdultMen -= MenConscriptionAmount;
            AddTroops(army, MenConscriptionAmount, 0f);

            message = $"⚔️ {province.ProvinceName}: {MenConscriptionAmount:N0} kişi '{army.ArmyData.CommanderName}' ordusuna qatıldı. Ağır işçi qüvvəsi azaldı.";
            Finish(province, message);
            return true;
        }

        /// <summary>
        /// "🛡️ Gənclərdən Elit Ordu Yetişdir (+500)": Youths -500, Qızıl -250, ordu +500 elit əsgər (moral artır).
        /// </summary>
        public static bool TrainEliteYouths(MapInteractionController province, out string message, out ArmyVisualController army)
        {
            army = null;
            message = string.Empty;
            if (province == null) { message = "Əyalət seçilməyib."; return false; }

            var d = province.Demographics;
            if (d.Youths < EliteYouthAmount)
            {
                message = $"Kifayət qədər Gənc yoxdur ({d.Youths:N0} < {EliteYouthAmount:N0}).";
                return false;
            }

            var gtm = GameTimeManager.Instance;
            if (gtm != null && gtm.PlayerGold < EliteYouthGoldCost)
            {
                message = $"Xəzinədə kifayət qədər Qızıl yoxdur ({gtm.PlayerGold:N0} < {EliteYouthGoldCost:N0}).";
                return false;
            }

            army = FindNearestArmy(province);
            if (army == null) { message = "Yaxınlıqda komandir tapılmadı."; return false; }

            d.Youths -= EliteYouthAmount;
            if (gtm != null) gtm.AddPlayerResources(-EliteYouthGoldCost, 0f, 0f);
            AddTroops(army, EliteYouthAmount, +0.05f);

            message = $"🛡️ {province.ProvinceName}: {EliteYouthAmount:N0} elit gənc əsgər yetişdirildi (-{EliteYouthGoldCost:N0} Qızıl) -> '{army.ArmyData.CommanderName}'.";
            Finish(province, message);
            return true;
        }

        /// <summary>
        /// "🏹 Qadınları Səfərbər Et (+1,000 - Fövqəladə)": AdultWomen -1000, ordu +1000 (zəif döyüş gücü),
        /// doğum səviyyəsi və PublicOrder (-10%) düşür.
        /// </summary>
        public static bool MobilizeWomen(MapInteractionController province, out string message, out ArmyVisualController army)
        {
            army = null;
            message = string.Empty;
            if (province == null) { message = "Əyalət seçilməyib."; return false; }

            var d = province.Demographics;
            if (d.AdultWomen < WomenMobilizationAmount)
            {
                message = $"Kifayət qədər Yetişkin Qadın yoxdur ({d.AdultWomen:N0} < {WomenMobilizationAmount:N0}).";
                return false;
            }

            army = FindNearestArmy(province);
            if (army == null) { message = "Yaxınlıqda komandir tapılmadı."; return false; }

            d.AdultWomen -= WomenMobilizationAmount;
            d.BirthRateModifier = Mathf.Max(0.2f, d.BirthRateModifier - 0.2f);
            d.PublicOrder = Mathf.Clamp(d.PublicOrder - 10f, 0f, 100f);
            AddTroops(army, WomenMobilizationAmount, -0.03f); // döyüş gücü kişilərə nisbətən zəifdir

            message = $"🏹 {province.ProvinceName}: {WomenMobilizationAmount:N0} qadın fövqəladə səfərbər edildi. Doğum səviyyəsi x{d.BirthRateModifier:F1}, Moral {d.PublicOrder:F0}%.";
            Finish(province, message);
            return true;
        }

        /// <summary>
        /// "☠️ Xəstələri Edam Et / Resursu Kəs": Sick = 0, müalicə xərci aradan qalxır,
        /// lakin xalq qəzəblənir: PublicOrder -35% və istehsal səmərəliliyi zəifləyir.
        /// </summary>
        public static bool ExecuteSick(MapInteractionController province, out string message)
        {
            message = string.Empty;
            if (province == null) { message = "Əyalət seçilməyib."; return false; }

            var d = province.Demographics;
            int removed = d.Sick;
            d.Sick = 0;
            d.PublicOrder = Mathf.Clamp(d.PublicOrder - 35f, 0f, 100f);

            message = $"☠️ {province.ProvinceName}: {removed:N0} xəstəyə resurs kəsildi. Xalq qəzəblidir! Moral {d.PublicOrder:F0}% (Səmərəlilik x{d.OrderEfficiency:F2}).";
            Finish(province, message);
            return true;
        }

        #endregion

        #region Aylıq Demoqrafik Keçid

        /// <summary>
        /// Bütün əyalətlər üçün aylıq demoqrafik keçidi icra edir (GameTimeManager.OnMonthlyTick-dən çağırılır).
        /// Oyunçunun əyalətlərindəki ümumi müalicə xərcini (Qızıl) qaytarır.
        /// </summary>
        public static int ProcessAllProvinces(out float totalHealingCost)
        {
            totalHealingCost = 0f;
            IReadOnlyList<MapInteractionController> provinces = GISMapLoader.Instance != null ? GISMapLoader.Instance.SpawnedProvinces : null;
            if (provinces == null || provinces.Count == 0)
            {
                provinces = UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude);
            }

            int processed = 0;
            foreach (var prov in provinces)
            {
                if (prov == null) continue;
                totalHealingCost += ProcessMonthlyDemographics(prov);
                processed++;
            }
            return processed;
        }

        /// <summary>
        /// Bir əyalət üçün aylıq doğum, yaşlanma (Körpə → Gənc → Yetişkin → Yaşlı), ölüm və sağalma döngüsü.
        /// Müalicə xərcini (Qızıl) qaytarır.
        /// </summary>
        public static float ProcessMonthlyDemographics(MapInteractionController province)
        {
            if (province == null) return 0f;
            var d = province.Demographics;

            // 1. Doğum (Yetişkin cütlərə və doğum əmsalına əsasən)
            int couples = Mathf.Min(d.AdultMen, d.AdultWomen);
            int births = Mathf.RoundToInt(couples * MonthlyBirthRate * d.BirthRateModifier);

            // 2. Keçidlər
            int infantsToYouths = Mathf.RoundToInt(d.Infants * InfantToYouthRate);
            int infantDeaths = Mathf.RoundToInt(d.Infants * InfantMortalityRate);
            int youthsToAdults = Mathf.RoundToInt(d.Youths * YouthToAdultRate);
            int menToElderly = Mathf.RoundToInt(d.AdultMen * AdultToElderlyRate);
            int womenToElderly = Mathf.RoundToInt(d.AdultWomen * AdultToElderlyRate);
            int elderlyDeaths = Mathf.RoundToInt(d.Elderly * ElderlyDeathRate);
            int newSick = Mathf.RoundToInt((d.AdultMen + d.AdultWomen + d.Youths) * NewSicknessRate);
            int sickDeaths = Mathf.RoundToInt(d.Sick * SickDeathRate);

            d.Infants += births - infantsToYouths - infantDeaths;
            d.Youths += infantsToYouths - youthsToAdults;
            int menFromYouth = youthsToAdults / 2;
            d.AdultMen += menFromYouth - menToElderly;
            d.AdultWomen += (youthsToAdults - menFromYouth) - womenToElderly;
            d.Elderly += menToElderly + womenToElderly - elderlyDeaths;

            // Yeni xəstələr işləyən əhalidən
            int sickFromMen = Mathf.Min(d.AdultMen, newSick / 2);
            int sickFromWomen = Mathf.Min(d.AdultWomen, newSick - sickFromMen);
            d.AdultMen -= sickFromMen;
            d.AdultWomen -= sickFromWomen;
            d.Sick += sickFromMen + sickFromWomen - sickDeaths;

            // 3. Sağalma (Xəstə → mülki əhali, Yaralı → aktiv ordu / Yetişkin Kişilər)
            float sickRate = d.HealSickAndWounded ? SickHealRate : SickNaturalRecoveryRate;
            float woundedRate = d.HealSickAndWounded ? WoundedHealRate : WoundedNaturalRecoveryRate;
            int healedSick = Mathf.RoundToInt(Mathf.Max(0, d.Sick) * sickRate);
            int healedWounded = Mathf.RoundToInt(Mathf.Max(0, d.Wounded) * woundedRate);

            d.Sick -= healedSick;
            int healedMen = healedSick / 2;
            d.AdultMen += healedMen;
            d.AdultWomen += healedSick - healedMen;

            d.Wounded -= healedWounded;
            if (healedWounded > 0)
            {
                var army = FindNearestArmy(province, sameFactionOnly: true);
                if (army != null)
                {
                    AddTroops(army, healedWounded, 0f);
                }
                else
                {
                    d.AdultMen += healedWounded;
                }
            }

            // Müalicə xərci (yalnız oyunçunun əyalətləri üçün dövlət xəzinəsindən çıxılır)
            float healingCost = 0f;
            if (d.HealSickAndWounded && (healedSick + healedWounded) > 0 && IsPlayerProvince(province))
            {
                healingCost = (healedSick + healedWounded) * HealingGoldCostPerPerson;
            }

            // 4. Moral və doğum əmsalının tədricən bərpası
            if (d.PublicOrder < ProvinceDemographics.BaselinePublicOrder)
                d.PublicOrder = Mathf.Min(ProvinceDemographics.BaselinePublicOrder, d.PublicOrder + 1f);
            if (d.BirthRateModifier < 1f)
                d.BirthRateModifier = Mathf.Min(1f, d.BirthRateModifier + 0.02f);

            ClampNonNegative(d);

            d.LastBirths = births;
            d.LastDeaths = infantDeaths + elderlyDeaths + sickDeaths;
            d.LastHealedSick = healedSick;
            d.LastHealedWounded = healedWounded;
            return healingCost;
        }

        #endregion

        #region Köməkçi Metodlar

        public static bool IsPlayerProvince(MapInteractionController province)
        {
            var gtm = GameTimeManager.Instance;
            if (province == null || gtm == null) return false;
            return string.Equals(province.ProvinceOwner, gtm.PlayerFactionName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Əyalətə ən yaxın komandiri tapır (əvvəlcə əyalətin sahibi olan dövlətin orduları).
        /// </summary>
        public static ArmyVisualController FindNearestArmy(MapInteractionController province, bool sameFactionOnly = false)
        {
            var mgr = ArmyManager.Instance;
            if (mgr == null || mgr.ActiveArmies == null || province == null) return null;

            Vector2 center = province.GetCentroidWorldPosition();
            ArmyVisualController bestSame = null, bestAny = null;
            float bestSameDist = float.MaxValue, bestAnyDist = float.MaxValue;

            foreach (var a in mgr.ActiveArmies)
            {
                if (a == null || a.ArmyData == null) continue;
                float dist = Vector2.Distance(center, a.transform.position);
                bool inProvince = a.ArmyData.CurrentProvinceId == province.ProvinceId;
                if (inProvince) dist = -1f; // həmin əyalətdəki komandir prioritetdir

                if (string.Equals(a.ArmyData.Faction, province.ProvinceOwner, StringComparison.OrdinalIgnoreCase))
                {
                    if (dist < bestSameDist) { bestSameDist = dist; bestSame = a; }
                }
                if (dist < bestAnyDist) { bestAnyDist = dist; bestAny = a; }
            }

            if (bestSame != null) return bestSame;
            return sameFactionOnly ? null : bestAny;
        }

        private static void AddTroops(ArmyVisualController army, int amount, float moraleDelta)
        {
            if (army == null || army.ArmyData == null) return;
            army.ArmyData.TroopCount += amount;
            if (!Mathf.Approximately(moraleDelta, 0f))
            {
                army.ArmyData.Morale = Mathf.Clamp(army.ArmyData.Morale + moraleDelta, 0.3f, 1.5f);
            }
            army.ApplyData();
        }

        private static void ClampNonNegative(ProvinceDemographics d)
        {
            d.Infants = Mathf.Max(0, d.Infants);
            d.Youths = Mathf.Max(0, d.Youths);
            d.AdultMen = Mathf.Max(0, d.AdultMen);
            d.AdultWomen = Mathf.Max(0, d.AdultWomen);
            d.Elderly = Mathf.Max(0, d.Elderly);
            d.Sick = Mathf.Max(0, d.Sick);
            d.Wounded = Mathf.Max(0, d.Wounded);
        }

        private static void Finish(MapInteractionController province, string message)
        {
            Debug.Log($"[DemographicsSystem] {message}");
            if (GameTimeManager.Instance != null) GameTimeManager.Instance.RecalculateMonthlyBalances();
            OnDemographicActionExecuted?.Invoke(province, message);
        }

        #endregion
    }
}
