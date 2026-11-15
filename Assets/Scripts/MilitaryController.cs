using System;
using System.Collections.Generic;
using GrandStrategy.Database;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 6C - MilitaryController:
    /// MilitaryAndDiplomacyController üçün vahid giriş və əmr fasadı.
    /// CallEmergencyConscription əmrini qəbul edərək ordu sisteminə yönləndirir.
    /// </summary>
    public class MilitaryController : MonoBehaviour
    {
        private static MilitaryController _instance;
        public static MilitaryController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<MilitaryController>());

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

        private MilitaryAndDiplomacyController GetUnderlyingController()
        {
            return MilitaryAndDiplomacyController.Instance != null
                ? MilitaryAndDiplomacyController.Instance
                : FindAnyObjectByType<MilitaryAndDiplomacyController>();
        }

        /// <summary>
        /// Ordunu səfərbər edir və çağırış həyata keçirir.
        /// </summary>
        public DraftArmyResult DraftArmy(DatabaseManager dbManager = null, int armyId = 1)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            if (controller == null)
            {
                Debug.LogError("[MilitaryController] MilitaryAndDiplomacyController tapılmadı!");
                return default;
            }

            return controller.DraftArmy(dbManager, armyId);
        }

        /// <summary>
        /// Faza 6C: Əyalətdə Fövqəladə Vəziyyət elan edərək qadınları (yetkin əhalinin 20%-ni) orduya çağırır.
        /// Cəza olaraq həmin əyalətin BirthRateModifier dəyəri 0.2f-ə düşür.
        /// </summary>
        public EmergencyConscriptionResult CallEmergencyConscription(string provinceId, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            if (controller == null)
            {
                Debug.LogError("[MilitaryController] MilitaryAndDiplomacyController tapılmadı!");
                return default;
            }

            return controller.CallEmergencyConscription(provinceId, dbManager);
        }

        public EmergencyConscriptionResult CallEmergencyConscription(int provinceId, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            if (controller == null)
            {
                Debug.LogError("[MilitaryController] MilitaryAndDiplomacyController tapılmadı!");
                return default;
            }

            return controller.CallEmergencyConscription(provinceId, dbManager);
        }

        /// <summary>
        /// Faza 8.1: Xəzinədə kifayət qədər Dəmir (Iron) olub-olmadığını yoxlayır.
        /// </summary>
        public bool HasRequiredIron(int factionId, float requiredIron = 5f, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            return controller != null && controller.HasRequiredIron(factionId, requiredIron, dbManager);
        }

        /// <summary>
        /// Faza 8.1: Xəzinədə kifayət qədər At (Horses) olub-olmadığını yoxlayır.
        /// </summary>
        public bool HasRequiredHorses(int factionId, float requiredHorses = 5f, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            return controller != null && controller.HasRequiredHorses(factionId, requiredHorses, dbManager);
        }

        /// <summary>
        /// Faza 8.1: Dəmir Asılılığı ilə Ağır Piyada (Heavy Infantry) yığımı.
        /// Xəzinədə kifayət qədər Iron olmalıdır. Dəmir yoxdursa və ya çatmırsa, yığım uğursuz olur.
        /// </summary>
        public bool RecruitHeavyInfantry(int provinceId, int count, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            if (controller == null)
            {
                Debug.LogError("[MilitaryController] MilitaryAndDiplomacyController tapılmadı!");
                return false;
            }

            return controller.RecruitHeavyInfantry(provinceId, count, dbManager);
        }

        /// <summary>
        /// Faza 8.1: At Asılılığı ilə Süvari (Cavalry) yığımı.
        /// Xəzinədə kifayət qədər Horses olmalıdır. At yoxdursa, "Flanking" edəcək sürətli qoşun çıxarıla bilməz.
        /// </summary>
        public bool RecruitCavalry(int provinceId, int count, DatabaseManager dbManager = null)
        {
            MilitaryAndDiplomacyController controller = GetUnderlyingController();
            if (controller == null)
            {
                Debug.LogError("[MilitaryController] MilitaryAndDiplomacyController tapılmadı!");
                return false;
            }

            return controller.RecruitCavalry(provinceId, count, dbManager);
        }

        /// <summary>
        /// Faza 8.2: Ordu tərəfindən düşmən ticarət karvanını yağmalayır (RaidCaravan).
        /// Karvan məhv edilir, daşınan resurs ordunun mənsub olduğu dövlətin xəzinəsinə daxil olur.
        /// </summary>
        public RaidResult RaidCaravan(int caravanId, int armyId, DatabaseManager dbManager = null)
        {
            TradeController tradeCtrl = TradeController.Instance != null ? TradeController.Instance : FindAnyObjectByType<TradeController>();
            if (tradeCtrl == null)
            {
                Debug.LogError("[MilitaryController] TradeController tapılmadı!");
                return default;
            }

            return tradeCtrl.RaidCaravan(caravanId, armyId, dbManager);
        }

        /// <summary>
        /// Faza 8.2: Xəritədəki bütün orduların yaxınlığında düşmən karvanı olub-olmadığını yoxlayır və yağmalayır.
        /// </summary>
        public List<RaidResult> CheckCaravanRaids(DatabaseManager dbManager = null)
        {
            TradeController tradeCtrl = TradeController.Instance != null ? TradeController.Instance : FindAnyObjectByType<TradeController>();
            if (tradeCtrl == null)
            {
                return new List<RaidResult>();
            }

            return tradeCtrl.CheckCaravanRaids(dbManager);
        }
    }
}
