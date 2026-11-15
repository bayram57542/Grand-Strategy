using System;
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
    }
}
