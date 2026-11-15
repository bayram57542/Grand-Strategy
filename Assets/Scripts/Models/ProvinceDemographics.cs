using System;
using UnityEngine;

namespace GrandStrategy.Database.Models
{
    /// <summary>
    /// Faza 10.5 - 7 Sinifli Dərin Demoqrafiya Modeli.
    /// Əyalətin ümumi əhalisi 7 sinfə bölünür: Körpələr, Gənclər, Yetişkin Kişilər, Yetişkin Qadınlar,
    /// Yaşlılar, Xəstələr və Yaralılar. İstehsal birbaşa işləyən siniflərin sayından və
    /// Xalqın Moralından (PublicOrder) hesablanır.
    /// </summary>
    [Serializable]
    public class ProvinceDemographics
    {
        // Başlanğıc bölgü faizləri
        public const float InfantsShare = 0.10f;
        public const float YouthsShare = 0.18f;
        public const float AdultMenShare = 0.30f;
        public const float AdultWomenShare = 0.30f;
        public const float ElderlyShare = 0.08f;
        public const float SickShare = 0.02f;
        public const float WoundedShare = 0.02f;

        public const float BaselinePublicOrder = 85f;

        // Asılı siniflərin (Körpə, Xəstə, Yaralı) aylıq ərzaq/müalicə xərci (hər 1000 nəfərə)
        public const float FoodCostPerThousandDependents = 1.0f;

        [Header("7 Əhali Sinfi")]
        public int Infants;
        public int Youths;
        public int AdultMen;
        public int AdultWomen;
        public int Elderly;
        public int Sick;
        public int Wounded;

        [Header("Xalqın Moralı və Siyasətlər")]
        [Range(0f, 100f)] public float PublicOrder = BaselinePublicOrder;
        public float BirthRateModifier = 1.0f;
        public bool HealSickAndWounded = true;

        [Header("Başlanğıc (Kalibrasiya) Dəyərləri")]
        public bool IsInitialized;
        public int InitialInfants;
        public int InitialYouths;
        public int InitialAdultMen;
        public int InitialAdultWomen;
        public int InitialElderly;
        public int InitialSick;
        public int InitialWounded;

        [Header("Son Aylıq Hesabat")]
        public int LastBirths;
        public int LastDeaths;
        public int LastHealedSick;
        public int LastHealedWounded;

        public int TotalPopulation => Infants + Youths + AdultMen + AdultWomen + Elderly + Sick + Wounded;
        public int Dependents => Infants + Sick + Wounded;
        public int InitialDependents => InitialInfants + InitialSick + InitialWounded;

        /// <summary>
        /// Ümumi əhalini 7 sinfə bölür (yuvarlaqlaşdırma qalığı Yetişkin Kişilərə əlavə olunur).
        /// </summary>
        public void InitializeFromPopulation(int totalPopulation)
        {
            int total = Mathf.Max(0, totalPopulation);
            Infants = Mathf.RoundToInt(total * InfantsShare);
            Youths = Mathf.RoundToInt(total * YouthsShare);
            AdultWomen = Mathf.RoundToInt(total * AdultWomenShare);
            Elderly = Mathf.RoundToInt(total * ElderlyShare);
            Sick = Mathf.RoundToInt(total * SickShare);
            Wounded = Mathf.RoundToInt(total * WoundedShare);
            AdultMen = Mathf.Max(0, total - (Infants + Youths + AdultWomen + Elderly + Sick + Wounded));

            InitialInfants = Infants;
            InitialYouths = Youths;
            InitialAdultMen = AdultMen;
            InitialAdultWomen = AdultWomen;
            InitialElderly = Elderly;
            InitialSick = Sick;
            InitialWounded = Wounded;

            PublicOrder = BaselinePublicOrder;
            BirthRateModifier = 1.0f;
            HealSickAndWounded = true;
            IsInitialized = true;
        }

        /// <summary>
        /// Xalqın moralına görə istehsal səmərəliliyi (85% moral = 100% səmərəlilik).
        /// </summary>
        public float OrderEfficiency
        {
            get
            {
                float order = Mathf.Clamp(PublicOrder, 0f, 100f);
                if (Mathf.Approximately(order, BaselinePublicOrder)) return 1f;
                // 0% moral -> 25% səmərəlilik, 100% moral -> ~113% səmərəlilik
                return Mathf.Clamp(0.25f + 0.75f * (order / BaselinePublicOrder), 0.25f, 1.15f);
            }
        }

        private static float Ratio(float current, float initial)
        {
            if (initial <= 0f) return current > 0f ? 1f : 0f;
            if (Mathf.Approximately(current, initial)) return 1f;
            return Mathf.Max(0f, current / initial);
        }

        /// <summary>Dəmir: yalnız Yetişkin Kişilərin ağır əməyi.</summary>
        public float IronWorkforceRatio => Ratio(AdultMen, InitialAdultMen);

        /// <summary>Ərzaq: Kişilər (tam), Qadınlar (tam), Gənclər (yarım güc).</summary>
        public float FoodWorkforceRatio => Ratio(
            AdultMen + AdultWomen + Youths * 0.5f,
            InitialAdultMen + InitialAdultWomen + InitialYouths * 0.5f);

        /// <summary>Qızıl: Kişilər, Qadınlar, Gənclər (yüngül iş) və Yaşlılar (xalçaçılıq/sənətkarlıq).</summary>
        public float GoldWorkforceRatio => Ratio(
            AdultMen + AdultWomen * 0.8f + Youths * 0.4f + Elderly * 0.6f,
            InitialAdultMen + InitialAdultWomen * 0.8f + InitialYouths * 0.4f + InitialElderly * 0.6f);

        /// <summary>
        /// Bazadakı baza dəyərlərini (tam işçi qüvvəsi, 85% moral) cari demoqrafiyaya görə tənzimləyir.
        /// Ərzaqdan Körpə, Xəstə və Yaralıların qida/müalicə xərci çıxılır (baza dəyər bu xərclə kalibrlənib).
        /// </summary>
        public void ApplyToEconomy(ProvinceEconomyData eco)
        {
            if (eco == null || !IsInitialized) return;

            float eff = OrderEfficiency;
            float baseGold = eco.GoldIncome;
            float baseFood = eco.FoodProduction;
            float baseIron = eco.IronProduction;

            float dependentDelta = (InitialDependents - Dependents) / 1000f * FoodCostPerThousandDependents;

            eco.TotalPopulation = TotalPopulation;
            eco.IronProduction = baseIron * IronWorkforceRatio * eff;
            eco.GoldIncome = baseGold * GoldWorkforceRatio * eff;
            eco.FoodProduction = baseFood * FoodWorkforceRatio * eff + dependentDelta;
        }

        public override string ToString()
        {
            return $"Körpə={Infants:N0} | Gənc={Youths:N0} | Kişi={AdultMen:N0} | Qadın={AdultWomen:N0} | Yaşlı={Elderly:N0} | Xəstə={Sick:N0} | Yaralı={Wounded:N0} | Cəmi={TotalPopulation:N0} | Moral={PublicOrder:F0}%";
        }
    }
}
