using System;
using GrandStrategy.Core;
using GrandStrategy.Map;
using UnityEngine;

namespace GrandStrategy.Military
{
    /// <summary>
    /// Ordunun hazırkı hərəkət və döyüş vəziyyətləri.
    /// </summary>
    public enum ArmyState
    {
        Idle,       // Düşərgə salıb / Hərəkətsiz
        Marching,   // Marş vəziyyətində / Hərəkət edir
        Besieging   // Düşmən qalasını mühasirəyə alıb
    }

    /// <summary>
    /// Faza 10.3 - Sərbəst Hərəkət Sistemi (ArmyMovementController):
    /// Oyunçu ordunu seçib xəritənin istənilən nöqtəsinə sağ kliklədikdə, ordu həmin koordinata doğru
    /// Vector2.MoveTowards ilə irəliləyir (State = Marching).
    /// Düşmən əyalətinin mərkəzinə (Qalaya) yaxınlaşdıqda hərəkət dayanır və mühasirə (State = Besieging) başlayır.
    /// </summary>
    [RequireComponent(typeof(ArmyVisualController))]
    public class ArmyMovementController : MonoBehaviour
    {
        [Header("Ordu Vəziyyəti")]
        [SerializeField] private ArmyState state = ArmyState.Idle;
        [SerializeField] private Vector2 targetDestination;
        [SerializeField] private MapInteractionController targetProvince;
        [SerializeField] private bool isSelected;

        [Header("Hərəkət Parametrləri")]
        [Tooltip("Hərəkət sürəti (dünya vahidi / saniyə)")]
        [SerializeField] private float moveSpeed = 2.0f;

        [Tooltip("Qalaya/Mərkəzə bu qədər yaxınlaşdıqda mühasirə başlayır")]
        [SerializeField] private float siegeTriggerDistance = 0.50f;

        private ArmyVisualController _visualController;

        public ArmyState State => state;
        public bool IsSelected => isSelected;
        public Vector2 TargetDestination => targetDestination;
        public MapInteractionController TargetProvince => targetProvince;
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        public float SiegeTriggerDistance { get => siegeTriggerDistance; set => siegeTriggerDistance = value; }
        public ArmyVisualController VisualController => _visualController != null ? _visualController : (_visualController = GetComponent<ArmyVisualController>());

        public event Action<ArmyState> OnStateChanged;
        public event Action<Vector2> OnDestinationReached;
        public event Action<MapInteractionController> OnSiegeTriggered;

        private void Awake()
        {
            _visualController = GetComponent<ArmyVisualController>();
        }

        private void Start()
        {
            if (_visualController == null)
            {
                _visualController = GetComponent<ArmyVisualController>();
            }
        }

        private void Update()
        {
            // 1. Marş vəziyyətindədirsə və oyun dayandırılmayıbsa (Pause deyil), hərəkəti addımlayırıq
            if (state == ArmyState.Marching)
            {
                bool isPaused = GameTimeManager.Instance != null && GameTimeManager.Instance.IsPaused;
                if (!isPaused)
                {
                    float speedMultiplier = GameTimeManager.Instance != null && GameTimeManager.Instance.CurrentSpeedMultiplier > 0
                        ? GameTimeManager.Instance.CurrentSpeedMultiplier
                        : 1f;
                    StepMovement(Time.deltaTime * speedMultiplier);
                }
            }

            // 2. Ordu seçilibsə və istifadəçi sağ klikləyirsə, yeni hərəkət əmri alırıq
            // (Pause rejimində də oyunçu xəritədə ordulara sərbəst hədəf təyin edə bilər!)
            if (isSelected)
            {
                HandlePlayerRightClickOrder();
            }
        }

        /// <summary>
        /// Ordunun seçilmə vəziyyətini dəyişir.
        /// </summary>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (VisualController != null)
            {
                VisualController.SetSelected(selected);
            }
        }

        /// <summary>
        /// Ordunu təyin edilmiş dünya koordinatına doğru hərəkət etdirir.
        /// Əgər hədəf düşmən əyalətidirsə, qalaya çatdıqda mühasirə avtomatik başlayır.
        /// </summary>
        public void MoveTo(Vector2 destination, MapInteractionController destinationProvince = null)
        {
            if (destinationProvince != null && destinationProvince.IsWasteland)
            {
                Debug.LogWarning($"[ArmyMovementController] ⛔ MoveTo rədd edildi: '{destinationProvince.ProvinceName}' keçilməz ərazidir (Wasteland)!");
                return;
            }

            targetDestination = destination;
            targetProvince = destinationProvince;

            SetState(ArmyState.Marching);

            string provName = targetProvince != null ? targetProvince.ProvinceName : "Açıq Ərazi";
            Debug.Log($"[ArmyMovementController] '{gameObject.name}' marşa başladı -> Hədəf: ({destination.x:F2}, {destination.y:F2}) [{provName}]");
        }

        /// <summary>
        /// Hərəkəti dərhal dayandırır.
        /// </summary>
        public void StopMovement()
        {
            if (state == ArmyState.Marching)
            {
                SetState(ArmyState.Idle);
            }
        }

        /// <summary>
        /// Hərəkəti müəyyən zaman fasiləsi (deltaTime) qədər simulyasiya edir (həm Update-də, həm də testlərdə istifadə olunur).
        /// </summary>
        public void StepMovement(float deltaTime)
        {
            if (state != ArmyState.Marching) return;

            Vector2 current2D = new Vector2(transform.position.x, transform.position.y);
            float z = VisualController != null ? VisualController.ZOffset : -2.0f;

            // 1. Əgər hədəf düşmən əyalətidirsə, onun mərkəzinə qədər olan məsafəni yoxlayırıq
            if (targetProvince != null && IsEnemyProvince(targetProvince))
            {
                Vector2 castleCenter = targetProvince.GetCentroidWorldPosition();
                float distToCastle = Vector2.Distance(current2D, castleCenter);

                if (distToCastle <= siegeTriggerDistance)
                {
                    // Düşmən əyalətinə çatdıq! Əvvəlcə döyüşü yoxlayırıq, yoxdursa mühasirəyə keçirik
                    transform.position = new Vector3(castleCenter.x, castleCenter.y, z);
                    UpdateCurrentProvince(targetProvince);
                    TriggerSiege(targetProvince);
                    return;
                }
            }

            // 2. Vector2.MoveTowards ilə hədəf nöqtəyə doğru irəliləyirik
            Vector2 nextPos = Vector2.MoveTowards(current2D, targetDestination, moveSpeed * deltaTime);
            transform.position = new Vector3(nextPos.x, nextPos.y, z);

            // 3. Hədəf koordinata çatdıqmı?
            if (Vector2.Distance(nextPos, targetDestination) <= 0.02f)
            {
                transform.position = new Vector3(targetDestination.x, targetDestination.y, z);

                MapInteractionController arrivedProvince = targetProvince != null ? targetProvince : FindProvinceAtPoint(targetDestination);
                if (arrivedProvince != null)
                {
                    UpdateCurrentProvince(arrivedProvince);
                }

                // Əgər çatdığımız yer düşmən əyalətidirsə və hələ mühasirə/döyüş başlamayıbsa
                if (targetProvince != null && IsEnemyProvince(targetProvince))
                {
                    TriggerSiege(targetProvince);
                }
                else
                {
                    SetState(ArmyState.Idle);
                    if (arrivedProvince != null)
                    {
                        CombatManager.EnsureInstance().CheckForBattle(arrivedProvince.ProvinceId, VisualController);
                    }
                    Debug.Log($"[ArmyMovementController] '{gameObject.name}' təyinat nöqtəsinə çatdı və düşərgə saldı.");
                    OnDestinationReached?.Invoke(targetDestination);
                }
            }
        }

        private void UpdateCurrentProvince(MapInteractionController province)
        {
            if (province != null && VisualController != null && VisualController.ArmyData != null)
            {
                VisualController.ArmyData.CurrentProvinceId = province.ProvinceId;
                VisualController.ArmyData.CurrentProvinceName = province.ProvinceName;
            }
        }

        /// <summary>
        /// Düşmən əyalətinə çatdıqda əvvəlcə Ordu-Ordu döyüşünü (CombatManager.CheckForBattle) yoxlayır.
        /// Düşmən qüvvəsi yoxdursa mühasirə məntiqinə keçir (köhnə SiegePanel açılmadan).
        /// </summary>
        private void TriggerSiege(MapInteractionController enemyProvince)
        {
            if (enemyProvince != null)
            {
                UpdateCurrentProvince(enemyProvince);

                // Köhnə SiegePanel-i hər zaman gizli saxlayırıq
                if (GrandStrategy.UI.UIManager.Instance != null)
                {
                    GrandStrategy.UI.UIManager.Instance.HideSiegeLogisticsPanel();
                }

                // Faza 10.12: Prioritet ordu-ordu döyüşünə verilir
                var battle = CombatManager.EnsureInstance().CheckForBattle(enemyProvince.ProvinceId, VisualController);
                if (battle != null)
                {
                    SetState(ArmyState.Idle);
                    return;
                }
            }

            SetState(ArmyState.Besieging);

            Debug.Log($"[ArmyMovementController] ⚔️ MÜHASİRƏ BAŞLADI! '{gameObject.name}' -> '{enemyProvince.ProvinceName}' qalasını mühasirəyə aldı!");

            // SiegeManager vasitəsilə əyalətin gəlir kəsilməsini tətbiq edirik (köhnə UI popup açılmadan)
            if (SiegeManager.Instance != null)
            {
                SiegeManager.Instance.StartSiege(VisualController, enemyProvince);
            }

            OnSiegeTriggered?.Invoke(enemyProvince);
        }

        private bool IsEnemyProvince(MapInteractionController province)
        {
            if (province == null || VisualController == null || VisualController.ArmyData == null) return false;
            // Eyni fraksiya deyilsə, düşmən qəbul edilir
            return province.FactionId != VisualController.ArmyData.FactionId;
        }

        private void SetState(ArmyState newState)
        {
            if (state == newState) return;
            state = newState;
            OnStateChanged?.Invoke(state);
        }

        /// <summary>
        /// Oyunçu xəritədə sağ kliklədikdə hədəf koordinatı və əyaləti tapıb hərəkət əmri verir.
        /// </summary>
        private void HandlePlayerRightClickOrder()
        {
            if (!GetSafeRightClickDown()) return;

            Vector2? mouseWorld = GetSafeMouseWorldPosition();
            if (!mouseWorld.HasValue) return;

            Vector2 target = mouseWorld.Value;
            MapInteractionController hitProvince = FindProvinceAtPoint(target);

            // MƏHDUDİYYƏT 1: Boş / Neytral (Wasteland) ərazilərə hərəkət qadağandır!
            if (hitProvince != null && hitProvince.IsWasteland)
            {
                Debug.LogWarning($"[ArmyMovementController] ⛔ HƏRƏKƏT RƏDD EDİLDİ: '{hitProvince.ProvinceName}' keçilməz boşluqdur (Wasteland)! Ordular bura daxil ola bilməz.");
                return;
            }

            // MƏHDUDİYYƏT 2: Açıq dənizə və ya xəritə xaricinə hərəkət qadağandır!
            if (hitProvince == null)
            {
                Debug.LogWarning($"[ArmyMovementController] ⛔ HƏRƏKƏT RƏDD EDİLDİ: Hədəf açıq dəniz və ya xəritədən kənar ərazidir!");
                return;
            }

            MoveTo(target, hitProvince);
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

                // Əgər birbaşa collider üstünə düşməyibsə, ən yaxın mərkəzə baxırıq (0.8f daxilində)
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

        private bool GetSafeRightClickDown()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
                {
                    return true;
                }
#endif
            }
            catch { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetMouseButtonDown(1))
                {
                    return true;
                }
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
                {
                    mouseScreen = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }
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
    }
}
