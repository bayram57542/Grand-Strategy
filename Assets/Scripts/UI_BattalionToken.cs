using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.Tactical
{
    /// <summary>
    /// 2D Taktiki Lövhə (UI Canvas) üzərindəki Batalyon Tokeni:
    /// NavMeshAgent əvəzinə RectTransform.anchoredPosition və Vector2.MoveTowards istifadə edərək
    /// Canvas üzərində 2D hərəkət edir, Vector2.Distance ilə AggroRange / AttackRange məsafələrini
    /// hesablayır, zərbə vurur (TakeDamage), HP çubuğunu və əsgər sayını yeniləyir, öldükdə isə Object Pool-a qayıdır.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UI_BattalionToken : MonoBehaviour
    {
        [Header("Batalyon Kimliyi")]
        [SerializeField] private BattalionType battalionType = BattalionType.AdultInfantry;
        [SerializeField] private int armyId;
        [SerializeField] private int ownerFactionId;
        [SerializeField] private int maxSoldiers = 250;
        [SerializeField] private int representedSoldiers = 250;
        [SerializeField] private bool isPlayerControlled = true;

        [Header("2D Hərəkət və Döyüş Statları (Piksel ilə)")]
        [SerializeField] private float moveSpeed = 110f;
        [SerializeField] private float maxHP = 100f;
        [SerializeField] private float currentHP = 100f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackRange = 58f;
        [SerializeField] private float attackCooldown = 1.5f;
        [SerializeField] private float aggroRange = 320f;
        [SerializeField] private float separationRadius = 46f;
        [SerializeField] private BattalionCombatState currentState = BattalionCombatState.Idle;

        [Header("2D UI Komponent Referansları")]
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image tokenImage;
        [SerializeField] private Image selectionRingImage;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private Text soldierCountText;

        [Header("Faza 3.1 - Kalkan Duvarı (Shield Wall)")]
        [SerializeField] private Image shieldWallIndicatorImage;
        private bool _isShieldWallActive;
        private float _preShieldWallSpeed;

        [Header("Faza 3.2 - Dinamik Mühit (Qum Fırtınası və Uzaq Mənzil)")]
        [SerializeField] private bool isRanged;
        private bool _isSandstormActive;
        private float _baseMoveSpeed = 100f;
        private float _baseAttackRange = 58f;

        public const float ShieldWallSpeedReductionPercent = 0.70f;
        public const float ShieldWallSpeedMultiplier = 0.30f; // Sürət 70% azalır (0.3x)
        public const float ShieldWallIncomingDamageMultiplier = 0.40f; // Qəbul edilən zərər 60% azalır (0.4x)
        public const float FlankDamageMultiplier = 1.5f; // Yandan zərbə 1.5x
        public const float RearDamageMultiplier = 2.0f; // Arxadan zərbə 2.0x (ikiqat)
        public const float SandstormReductionMultiplier = 0.50f; // Qum fırtınasında sürət və mənzil 50% azalır

        private bool _isSelected;
        private bool _isDead;
        private Vector2 _moveTargetAnchoredPos;
        private float _attackCooldownTimer;
        private int _attackTriggerCount;
        private UI_BattalionToken _currentTargetEnemy;

        // Faza 6B Təkmilləşdirmə: Kral Öldü Panikası
        private bool _isPanicked;
        private float _prePanicMoveSpeed;
        private float _panicSpeedTimer;
        private Coroutine _panicCoroutine;

        public BattalionType Type => battalionType;
        public int ArmyId => armyId;
        public int OwnerFactionId => ownerFactionId;
        public int MaxSoldiers => maxSoldiers;
        public int RepresentedSoldiers => representedSoldiers;
        public bool IsPlayerControlled => isPlayerControlled;
        public bool IsSelected => _isSelected;
        public bool IsDead => _isDead;
        public float MoveSpeed => moveSpeed;
        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float AggroRange => aggroRange;
        public BattalionCombatState CurrentState => currentState;
        public UI_BattalionToken CurrentTargetEnemy => _currentTargetEnemy;
        public Vector2 MoveTargetAnchoredPos => _moveTargetAnchoredPos;
        public int AttackTriggerCount => _attackTriggerCount;
        public bool IsPanicked => _isPanicked;
        public float CurrentAttackDamage => attackDamage;
        public float CurrentMoveSpeed => moveSpeed;
        public bool IsShieldWallActive => _isShieldWallActive;
        public float IncomingDamageMultiplier => _isShieldWallActive ? ShieldWallIncomingDamageMultiplier : 1.0f;
        public bool IsShieldWallIndicatorActive => shieldWallIndicatorImage != null && shieldWallIndicatorImage.gameObject.activeSelf;
        public bool IsSandstormActive => _isSandstormActive;
        public bool IsRanged => isRanged;
        public float BaseMoveSpeed => _baseMoveSpeed;
        public float BaseAttackRange => _baseAttackRange;

        private Vector2 _facingDirection = Vector2.zero;

        /// <summary>
        /// Tokenin 2D lövhədə baxdığı istiqaməti (Facing Direction) hesablayır:
        /// Əgər xüsusi istiqamət təyin edilibsə onu, hədəf düşmən varsa ona doğru,
        /// hərəkətdədirsə hərəkət hədəfinə doğru, əks halda ordu tərəfinə görə (Oyunçu sağa, Rəqib sola).
        /// </summary>
        public Vector2 FacingDirection
        {
            get
            {
                if (_facingDirection.sqrMagnitude > 0.001f)
                {
                    return _facingDirection.normalized;
                }

                if (_currentTargetEnemy != null && !_currentTargetEnemy.IsDead && _currentTargetEnemy.gameObject.activeInHierarchy)
                {
                    Vector2 toEnemy = _currentTargetEnemy.AnchoredPosition - AnchoredPosition;
                    if (toEnemy.sqrMagnitude > 0.001f)
                    {
                        return toEnemy.normalized;
                    }
                }

                if (currentState == BattalionCombatState.Moving)
                {
                    Vector2 toTarget = _moveTargetAnchoredPos - AnchoredPosition;
                    if (toTarget.sqrMagnitude > 0.001f)
                    {
                        return toTarget.normalized;
                    }
                }

                return isPlayerControlled ? Vector2.right : Vector2.left;
            }
            set => _facingDirection = value;
        }

        public void SetFacingDirection(Vector2 direction)
        {
            _facingDirection = direction;
        }

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }
                return rectTransform;
            }
        }

        public Vector2 AnchoredPosition
        {
            get => RectTransform.anchoredPosition;
            set => RectTransform.anchoredPosition = value;
        }

        public float CurrentHpFillAmount => hpFillImage != null ? hpFillImage.fillAmount : (maxHP > 0f ? currentHP / maxHP : 0f);
        public string CurrentSoldierCountLabel => soldierCountText != null ? soldierCountText.text : representedSoldiers.ToString();
        public bool IsSelectionRingActive => selectionRingImage != null && selectionRingImage.gameObject.activeSelf;

        private void Awake()
        {
            CacheReferences();
            SetSelected(false);
        }

        private void Update()
        {
            if (!Application.isPlaying || _isDead)
            {
                return;
            }

            TickCombat(Time.deltaTime);
        }

        private void CacheReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (tokenImage == null)
            {
                tokenImage = GetComponent<Image>();
            }

            if (selectionRingImage == null)
            {
                Transform ringTr = transform.Find("SelectionRing");
                if (ringTr != null)
                {
                    selectionRingImage = ringTr.GetComponent<Image>();
                }
            }

            if (hpFillImage == null)
            {
                Transform fillTr = transform.Find("HPBarBackground/HPBarFill");
                if (fillTr != null)
                {
                    hpFillImage = fillTr.GetComponent<Image>();
                }
            }

            if (soldierCountText == null)
            {
                Transform txtTr = transform.Find("SoldierCountText");
                if (txtTr != null)
                {
                    soldierCountText = txtTr.GetComponent<Text>();
                }
            }

            if (shieldWallIndicatorImage == null)
            {
                Transform shieldTr = transform.Find("ShieldWallIndicator");
                if (shieldTr != null)
                {
                    shieldWallIndicatorImage = shieldTr.GetComponent<Image>();
                }
            }
        }

        /// <summary>
        /// Object Pool-dan çıxarılan 2D UI tokenini ilkin döyüş vəziyyətinə gətirir.
        /// </summary>
        public void InitializeToken(
            BattalionType type,
            int owningArmyId,
            int factionId,
            int soldiersCount,
            bool playerControlled,
            Vector2 spawnAnchoredPosition)
        {
            CacheReferences();

            battalionType = type;
            armyId = owningArmyId;
            ownerFactionId = factionId;
            maxSoldiers = Mathf.Max(1, soldiersCount);
            representedSoldiers = maxSoldiers;
            isPlayerControlled = playerControlled;

            // Elit Süvarilər (YouthCavalry) daha sürətli və yüksək həmlə gücünə malikdir
            moveSpeed = type == BattalionType.YouthCavalry ? 165f : 100f;
            maxHP = 100f;
            currentHP = maxHP;
            attackDamage = type == BattalionType.YouthCavalry ? 15f : 12f;
            attackRange = 58f;
            attackCooldown = 1.5f;
            aggroRange = 320f;

            _baseMoveSpeed = moveSpeed;
            _baseAttackRange = attackRange;
            _isSandstormActive = false;
            isRanged = false;

            _isDead = false;
            _isPanicked = false;
            _panicSpeedTimer = 0f;
            _prePanicMoveSpeed = moveSpeed;
            if (_panicCoroutine != null)
            {
                StopCoroutine(_panicCoroutine);
                _panicCoroutine = null;
            }
            _attackCooldownTimer = 0f;
            _attackTriggerCount = 0;
            _currentTargetEnemy = null;
            currentState = BattalionCombatState.Idle;

            _isShieldWallActive = false;
            _preShieldWallSpeed = moveSpeed;
            _facingDirection = Vector2.zero;
            UpdateShieldWallVisual();

            RectTransform.localScale = Vector3.one;
            RectTransform.anchoredPosition = spawnAnchoredPosition;
            _moveTargetAnchoredPos = spawnAnchoredPosition;

            ApplyVisualTheme();
            RefreshHpAndCountUI();
            SetSelected(false);
        }

        private void ApplyVisualTheme()
        {
            ApplyTokenColorTheme();
        }

        /// <summary>
        /// Tokenin əsas gövdə şəklinə (Image) birbaşa çıxış (alt siniflər üçün, məs: UI_CommanderToken).
        /// </summary>
        protected Image TokenBodyImage => tokenImage;

        /// <summary>
        /// Sürət modifikatorlarını (Panika: 0.5x, Kalkan Duvarı: 0.3x, Qum Fırtınası: 0.5x) birləşdirərək
        /// tokenin cari hərəkət sürətini dəqiq və deterministik şəkildə yenidən hesablayır.
        /// </summary>
        public void RecalculateMoveSpeed()
        {
            float speed = _baseMoveSpeed;
            if (_isPanicked)
            {
                speed *= 0.5f;
            }
            if (_isShieldWallActive)
            {
                speed *= ShieldWallSpeedMultiplier;
            }
            if (_isSandstormActive)
            {
                speed *= SandstormReductionMultiplier;
            }
            moveSpeed = Mathf.Max(1f, speed);
        }

        /// <summary>
        /// Tokenin döyüş statlarını (MaxHP, Zərər, Sürət) InitializeToken-dən sonra dəyişdirir
        /// (məsələn, Sərkərdə Tokeni daha çox HP və daha yavaş hərəkətə malikdir).
        /// </summary>
        public void OverrideCombatStats(float newMaxHp, float newAttackDamage, float newMoveSpeed)
        {
            maxHP = Mathf.Max(1f, newMaxHp);
            currentHP = maxHP;
            attackDamage = Mathf.Max(0f, newAttackDamage);
            _baseMoveSpeed = Mathf.Max(0f, newMoveSpeed);
            RecalculateMoveSpeed();
            RefreshHpAndCountUI();
        }

        /// <summary>
        /// Faza 3.2: Səhra mühitində Qum Fırtınası effektini tətbiq edir və ya ləğv edir.
        /// Qum fırtınası aktiv olduqda hərəkət sürəti və görmə/hücum mənzili 50% azalır.
        /// Fırtına bitdikdə ilkin dəyərlər bərpa olunur.
        /// </summary>
        public void SetSandstormActive(bool active)
        {
            if (_isSandstormActive == active)
            {
                return;
            }

            _isSandstormActive = active;
            RecalculateMoveSpeed();
            attackRange = _isSandstormActive
                ? Mathf.Max(1f, _baseAttackRange * SandstormReductionMultiplier)
                : _baseAttackRange;

            Debug.Log($"[UI_BattalionToken] '{name}' Qum Fırtınası: {_isSandstormActive} | Sürət: {moveSpeed:F1}, Mənzil: {attackRange:F1}");
        }

        /// <summary>
        /// Tokeni uzaqmənzilli (oxçu) dəstə kimi konfiqurasiya edir (məs: qala qapısının arxasından hücum üçün).
        /// </summary>
        public void SetRanged(bool ranged, float customRange = 180f)
        {
            isRanged = ranged;
            _baseAttackRange = ranged ? Mathf.Max(60f, customRange) : 58f;
            attackRange = _isSandstormActive
                ? Mathf.Max(1f, _baseAttackRange * SandstormReductionMultiplier)
                : _baseAttackRange;
        }

        /// <summary>
        /// Qala qapısı sındırıldıqda cari qapı hədəfini təmizləyir ki, token içəri irəliləyə bilsin.
        /// </summary>
        public void NotifyGateBreached()
        {
            if (_currentTargetEnemy is UI_GateToken)
            {
                _currentTargetEnemy = null;
                currentState = BattalionCombatState.Idle;
            }
        }

        /// <summary>
        /// Kral döyüşdə öldükdə ordudakı əsgərlərə panika tətbiq edir:
        /// 1. Hərəkət sürətini verilən müddət ərzində (məs: 5 saniyə) 50% azaldır.
        /// 2. Vurduğu zərəri (Damage) döyüşün sonuna qədər yarıbayarı (damageReductionMultiplier = 0.5x) aşağı salır.
        /// </summary>
        public void ApplyPanic(float duration, float damageReductionMultiplier)
        {
            if (_isDead)
            {
                return;
            }

            // Vurduğu zərər (Damage) döyüşün sonuna qədər yarıbayarı (0.5x) aşağı düşür
            attackDamage = Mathf.Max(1f, attackDamage * damageReductionMultiplier);

            // Hərəkət sürəti verilən müddətdə 50% azalır
            _isPanicked = true;
            RecalculateMoveSpeed();
            _panicSpeedTimer = duration;

            if (gameObject.activeInHierarchy && Application.isPlaying)
            {
                if (_panicCoroutine != null)
                {
                    StopCoroutine(_panicCoroutine);
                }
                _panicCoroutine = StartCoroutine(PanicDurationCoroutine(duration));
            }

            Debug.Log($"[UI_BattalionToken] '{name}' panikaya düşdü! Sürət: {moveSpeed:F1} (50% azaldı, {duration:F1}s), Zərər: {attackDamage:F1} (x{damageReductionMultiplier}).");
        }

        private System.Collections.IEnumerator PanicDurationCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            RecoverFromPanicSpeed();
        }

        /// <summary>
        /// Panika müddəti bitdikdə hərəkət sürətini əvvəlki dəyərinə qaytarır (Zərər isə döyüşün sonuna qədər yarı qalır).
        /// </summary>
        public void RecoverFromPanicSpeed()
        {
            if (_isPanicked && !_isDead)
            {
                _isPanicked = false;
                _panicSpeedTimer = 0f;
                _panicCoroutine = null;
                RecalculateMoveSpeed();
                Debug.Log($"[UI_BattalionToken] '{name}' panikadan çıxdı! Sürət bərpa olundu: {moveSpeed:F1} (Zərər isə yarı olaraq qalır: {attackDamage:F1}).");
            }
        }

        #region Faza 3.1 - Kalkan Duvarı (Shield Wall) və Flanking Sistemi

        /// <summary>
        /// Kalkan Duvarı (Shield Wall) rejimini açır və ya bağlayır:
        /// - Aktiv olduqda: moveSpeed 70% azalır (0.3x), qəbul edilən zərər 60% azalır (0.4x qəbul edir).
        /// - Deaktiv olduqda: moveSpeed və qəbul edilən zərər ilkin vəziyyətinə qayıdır.
        /// </summary>
        public bool ToggleShieldWall()
        {
            SetShieldWall(!_isShieldWallActive);
            return _isShieldWallActive;
        }

        public void SetShieldWall(bool active)
        {
            if (_isShieldWallActive == active)
            {
                return;
            }

            _isShieldWallActive = active;
            RecalculateMoveSpeed();
            UpdateShieldWallVisual();
            Debug.Log($"[UI_BattalionToken] '{name}' Kalkan Duvarı: {_isShieldWallActive} | Sürət: {moveSpeed:F1} (70% azaldı), Alınan Zərər Çarpanı: {IncomingDamageMultiplier:F2}x");
        }

        public void UpdateShieldWallVisual()
        {
            if (shieldWallIndicatorImage != null)
            {
                shieldWallIndicatorImage.gameObject.SetActive(_isShieldWallActive);
            }
        }

        /// <summary>
        /// Hücum edən tokenin mövqeyini müdafiə olunan tokenin baxdığı istiqamətə (Facing Direction)
        /// görə müqayisə edərək hücum bucağını (Ön: 1.0x, Yan/Flank: 1.5x, Arxa/Rear: 2.0x) müəyyən edir.
        /// </summary>
        public static CombatAttackDirection DetermineAttackDirection(UI_BattalionToken attacker, UI_BattalionToken defender)
        {
            if (attacker == null || defender == null)
            {
                return CombatAttackDirection.Front;
            }

            Vector2 defenderFacing = defender.FacingDirection;
            Vector2 toAttacker = attacker.AnchoredPosition - defender.AnchoredPosition;

            if (toAttacker.sqrMagnitude < 0.001f)
            {
                return CombatAttackDirection.Front;
            }

            toAttacker.Normalize();
            float angle = Vector2.Angle(defenderFacing, toAttacker);

            // [0, 55] dərəcə: Birbaşa qarşıdan (Ön tərəf)
            // (55, 125] dərəcə: Cinahdan / Yandan (Flank)
            // (125, 180] dərəcə: Birbaşa arxadan (Rear / Back)
            if (angle <= 55f)
            {
                return CombatAttackDirection.Front;
            }
            else if (angle <= 125f)
            {
                return CombatAttackDirection.Flank;
            }
            else
            {
                return CombatAttackDirection.Rear;
            }
        }

        public static float GetDirectionalDamageMultiplier(CombatAttackDirection direction)
        {
            switch (direction)
            {
                case CombatAttackDirection.Rear:
                    return RearDamageMultiplier; // 2.0x (İkiqat kritik zərər)
                case CombatAttackDirection.Flank:
                    return FlankDamageMultiplier; // 1.5x (Bonus zərər)
                case CombatAttackDirection.Front:
                default:
                    return 1.0f; // Standart zərər
            }
        }

        #endregion

        /// <summary>
        /// Tərəfə (Oyunçu/Rəqib) və batalyon növünə görə token rəngini təyin edir.
        /// Alt siniflər (məs: UI_CommanderToken) bu metodu öz vizual stilləri üçün dəyişə bilər.
        /// </summary>
        protected virtual void ApplyTokenColorTheme()
        {
            if (tokenImage == null)
            {
                return;
            }

            if (isPlayerControlled)
            {
                // Ordu 1 (Sol / Oyunçu): Mavi çalarlar (Süvari açıq parlaq mavi, Piyada tünd göy)
                tokenImage.color = battalionType == BattalionType.YouthCavalry
                    ? new Color(0.18f, 0.56f, 0.96f, 1f)
                    : new Color(0.12f, 0.36f, 0.76f, 1f);
            }
            else
            {
                // Ordu 2 (Sağ / Rəqib): Qırmızı çalarlar (Süvari narıncı-qırmızı, Piyada tünd qırmızı)
                tokenImage.color = battalionType == BattalionType.YouthCavalry
                    ? new Color(0.95f, 0.30f, 0.22f, 1f)
                    : new Color(0.76f, 0.14f, 0.16f, 1f);
            }
        }

        /// <summary>
        /// Tokenin seçilib-seçilmədiyini təyin edir və dairəvi seçim halqasını açıb-bağlayır.
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selectionRingImage != null && selectionRingImage.gameObject.activeSelf != selected)
            {
                selectionRingImage.gameObject.SetActive(selected);
            }
        }

        /// <summary>
        /// Tokenə Canvas üzərində hədəf anchoredPosition nöqtəsinə doğru 2D hərəkət əmri verir.
        /// </summary>
        public bool MoveTo(Vector2 targetAnchoredPosition)
        {
            if (_isDead || !gameObject.activeInHierarchy)
            {
                return false;
            }

            _moveTargetAnchoredPos = targetAnchoredPosition;
            _currentTargetEnemy = null;
            currentState = BattalionCombatState.Moving;
            return true;
        }

        /// <summary>
        /// Hər kadrda (Update) və ya simulyasiya addımında 2D Vəziyyət Maşınını (Idle, Moving, Chasing, Attacking) icra edir.
        /// </summary>
        public void TickCombat(float deltaTime)
        {
            if (_isDead || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= deltaTime;
            }

            if (_isPanicked && _panicSpeedTimer > 0f)
            {
                _panicSpeedTimer -= deltaTime;
                if (_panicSpeedTimer <= 0f)
                {
                    RecoverFromPanicSpeed();
                }
            }

            // 1. Idle və ya Moving vəziyyətindədirsə, AggroRange daxilində ən yaxın düşmən tokeni axtarır
            if (currentState == BattalionCombatState.Idle || currentState == BattalionCombatState.Moving)
            {
                UI_BattalionToken nearestEnemy = FindNearestEnemyWithinRange(aggroRange);
                if (nearestEnemy != null)
                {
                    _currentTargetEnemy = nearestEnemy;
                    currentState = BattalionCombatState.Chasing;
                }
            }

            // 2. Vəziyyət Maşını (State Machine)
            switch (currentState)
            {
                case BattalionCombatState.Idle:
                    ApplySoftTokenSeparation(deltaTime * 0.5f);
                    break;

                case BattalionCombatState.Moving:
                {
                    Vector2 currentPos = RectTransform.anchoredPosition;
                    Vector2 nextPos = Vector2.MoveTowards(currentPos, _moveTargetAnchoredPos, moveSpeed * deltaTime);
                    ClampPositionAgainstGate(ref nextPos);
                    RectTransform.anchoredPosition = nextPos;
                    ApplySoftTokenSeparation(deltaTime);

                    if (Vector2.Distance(RectTransform.anchoredPosition, _moveTargetAnchoredPos) <= 2.0f)
                    {
                        RectTransform.anchoredPosition = _moveTargetAnchoredPos;
                        currentState = BattalionCombatState.Idle;
                    }
                    break;
                }

                case BattalionCombatState.Chasing:
                {
                    if (!IsValidEnemyTarget(_currentTargetEnemy))
                    {
                        _currentTargetEnemy = FindNearestEnemyWithinRange(aggroRange);
                        if (_currentTargetEnemy == null)
                        {
                            currentState = BattalionCombatState.Idle;
                            break;
                        }
                    }

                    float distToEnemy = Vector2.Distance(RectTransform.anchoredPosition, _currentTargetEnemy.RectTransform.anchoredPosition);
                    if (distToEnemy <= attackRange)
                    {
                        currentState = BattalionCombatState.Attacking;
                        TryPerformAttack(_currentTargetEnemy);
                    }
                    else
                    {
                        Vector2 nextPos = Vector2.MoveTowards(
                            RectTransform.anchoredPosition,
                            _currentTargetEnemy.RectTransform.anchoredPosition,
                            moveSpeed * deltaTime);
                        ClampPositionAgainstGate(ref nextPos);
                        RectTransform.anchoredPosition = nextPos;
                        ApplySoftTokenSeparation(deltaTime);
                    }
                    break;
                }

                case BattalionCombatState.Attacking:
                {
                    if (!IsValidEnemyTarget(_currentTargetEnemy))
                    {
                        _currentTargetEnemy = FindNearestEnemyWithinRange(aggroRange);
                        if (_currentTargetEnemy == null)
                        {
                            currentState = BattalionCombatState.Idle;
                            break;
                        }
                    }

                    float distToEnemy = Vector2.Distance(RectTransform.anchoredPosition, _currentTargetEnemy.RectTransform.anchoredPosition);
                    if (distToEnemy > attackRange * 1.15f)
                    {
                        currentState = BattalionCombatState.Chasing;
                        Vector2 nextPos = Vector2.MoveTowards(
                            RectTransform.anchoredPosition,
                            _currentTargetEnemy.RectTransform.anchoredPosition,
                            moveSpeed * deltaTime);
                        ClampPositionAgainstGate(ref nextPos);
                        RectTransform.anchoredPosition = nextPos;
                        break;
                    }

                    ApplySoftTokenSeparation(deltaTime * 0.4f);
                    TryPerformAttack(_currentTargetEnemy);
                    break;
                }
            }
        }

        public void ResetAttackCooldown()
        {
            _attackCooldownTimer = 0f;
        }

        public void TryPerformAttack(UI_BattalionToken enemy, bool ignoreCooldown = false)
        {
            if (!ignoreCooldown && _attackCooldownTimer > 0f)
            {
                return;
            }

            if (!IsValidEnemyTarget(enemy))
            {
                return;
            }

            _attackCooldownTimer = Mathf.Max(0.1f, attackCooldown);
            _attackTriggerCount++;

            // Faza 3.1: Flanking (Yandan 1.5x) və Arxadan (2.0x) Zərbə Hesablanması
            CombatAttackDirection direction = DetermineAttackDirection(this, enemy);
            float directionalMultiplier = GetDirectionalDamageMultiplier(direction);
            float finalDamage = attackDamage * directionalMultiplier;

            enemy.TakeDamage(finalDamage, direction, this);

            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            if (battleManager != null && direction != CombatAttackDirection.Front)
            {
                battleManager.TriggerFlankingCombatIndicator(enemy.AnchoredPosition, direction, directionalMultiplier);
            }

            if (enemy.IsDead)
            {
                _currentTargetEnemy = FindNearestEnemyWithinRange(aggroRange);
                currentState = _currentTargetEnemy != null ? BattalionCombatState.Chasing : BattalionCombatState.Idle;
            }
        }

        /// <summary>
        /// Tokenə zərər verir, HP çubuğunu və əsgər sayını yeniləyir.
        /// Kalkan Duvarı (Shield Wall) aktivdirsə, zərər 60% azalır (0.4x qəbul edir).
        /// </summary>
        public void TakeDamage(float rawDamage, CombatAttackDirection direction = CombatAttackDirection.Front, UI_BattalionToken attacker = null)
        {
            if (_isDead || rawDamage <= 0f)
            {
                return;
            }

            float effectiveDamage = rawDamage * IncomingDamageMultiplier;
            currentHP = Mathf.Max(0f, currentHP - effectiveDamage);
            RefreshHpAndCountUI();

            if (currentHP <= 0f)
            {
                Die();
            }
        }

        public void TakeDamage(float damage)
        {
            TakeDamage(damage, CombatAttackDirection.Front, null);
        }

        private void RefreshHpAndCountUI()
        {
            float ratio = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;

            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = ratio;
                hpFillImage.color = Color.Lerp(new Color(0.92f, 0.20f, 0.18f, 1f), new Color(0.20f, 0.88f, 0.30f, 1f), ratio);
            }

            representedSoldiers = currentHP <= 0f
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(maxSoldiers * ratio));

            if (soldierCountText != null)
            {
                soldierCountText.text = representedSoldiers.ToString();
            }
        }

        private void Die()
        {
            if (_isDead)
            {
                return;
            }

            _isDead = true;
            currentState = BattalionCombatState.Idle;
            _currentTargetEnemy = null;

            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            if (battleManager != null)
            {
                battleManager.NotifyBattalionDied(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Faza 3.2: Hücum edən tokenin mövqeyini yoxlayır; əgər Qala Qapısı hələ sındırılmayıbsa,
        /// token mərkəzi xətti (X = 0) keçə bilməz və qapının qarşısında bloklanır.
        /// </summary>
        private void ClampPositionAgainstGate(ref Vector2 pos)
        {
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            if (battleManager == null || !battleManager.IsGateIntact)
            {
                return;
            }

            // Hücum edən (sol tərəf) tokenlər qapı sındırılmadan mərkəzi xətti (X=0) keçə bilməz
            if (isPlayerControlled || (battleManager.AttackerArmyId > 0 && armyId == battleManager.AttackerArmyId))
            {
                float maxAllowedX = battleManager.GatePositionX - 25f;
                if (pos.x > maxAllowedX)
                {
                    pos.x = maxAllowedX;
                }
            }
        }

        /// <summary>
        /// Vector2.Distance(rect1.anchoredPosition, rect2.anchoredPosition) istifadə edərək
        /// verilmiş məsafə daxilindəki ən yaxın rəqib (əks ArmyId) tokenini tapır.
        /// </summary>
        public UI_BattalionToken FindNearestEnemyWithinRange(float maxSearchDistance)
        {
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;

            // Faza 3.2: Əgər Qala Mühasirəsindəyiksə və Qala Qapısı hələ bütövdürsə,
            // hücum edən (sol tərəf) qoşunların məcburi və ən birinci hədəfi Qapıdır!
            if (battleManager != null && battleManager.IsGateIntact && (isPlayerControlled || (battleManager.AttackerArmyId > 0 && armyId == battleManager.AttackerArmyId)))
            {
                if (battleManager.GateToken != null && !battleManager.GateToken.IsDead && battleManager.GateToken.gameObject.activeInHierarchy)
                {
                    return battleManager.GateToken;
                }
            }

            IReadOnlyList<UI_BattalionToken> candidates = battleManager != null
                ? battleManager.ActiveBattalions
                : FindObjectsByType<UI_BattalionToken>(FindObjectsSortMode.None);

            UI_BattalionToken closest = null;
            float minDist = maxSearchDistance;
            Vector2 myPos = RectTransform.anchoredPosition;

            for (int i = 0; i < candidates.Count; i++)
            {
                UI_BattalionToken other = candidates[i];
                if (!IsValidEnemyTarget(other))
                {
                    continue;
                }

                float dist = Vector2.Distance(myPos, other.RectTransform.anchoredPosition);
                if (dist <= minDist)
                {
                    minDist = dist;
                    closest = other;
                }
            }

            return closest;
        }

        private bool IsValidEnemyTarget(UI_BattalionToken other)
        {
            if (other == null ||
                other == this ||
                other._isDead ||
                !other.gameObject.activeInHierarchy ||
                other.armyId == armyId)
            {
                return false;
            }

            // Faza 3.2: Əgər qala qapısı bütövdürsə və token hücum tərəfindədirsə,
            // qapı sındırılmadan qapının arxasındakı tokenləri hədəf ala bilməz (qapı birinci hədəfdir)
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            if (battleManager != null && battleManager.IsGateIntact && (isPlayerControlled || (battleManager.AttackerArmyId > 0 && armyId == battleManager.AttackerArmyId)))
            {
                if (!(other is UI_GateToken))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 2D Toqquşma (Overlap Separation): Dost tokenlərin bir-birinin tam üstünə minməsinin qarşısını alır.
        /// </summary>
        private void ApplySoftTokenSeparation(float deltaTime)
        {
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            if (battleManager == null)
            {
                return;
            }

            IReadOnlyList<UI_BattalionToken> active = battleManager.ActiveBattalions;
            Vector2 myPos = RectTransform.anchoredPosition;
            Vector2 pushVector = Vector2.zero;
            int neighbors = 0;

            for (int i = 0; i < active.Count; i++)
            {
                UI_BattalionToken other = active[i];
                if (other == null || other == this || other._isDead || !other.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Yalnız eyni ordudakı tokenlər arasında yumşaq itələmə tətbiq edirik ki, rəqiblə təmas kəsilməsin
                if (other.armyId != armyId)
                {
                    continue;
                }

                Vector2 diff = myPos - other.RectTransform.anchoredPosition;
                float dist = diff.magnitude;
                if (dist < separationRadius)
                {
                    Vector2 dir = dist > 0.001f ? (diff / dist) : new Vector2(0.7f, 0.7f);
                    float strength = (separationRadius - dist) / separationRadius;
                    pushVector += dir * (strength * 65f);
                    neighbors++;
                }
            }

            if (neighbors > 0)
            {
                RectTransform.anchoredPosition = myPos + pushVector * deltaTime;
            }
        }
    }

    public enum BattalionType
    {
        YouthCavalry = 0,
        AdultInfantry = 1
    }

    public enum BattalionCombatState
    {
        Idle = 0,
        Moving = 1,
        Chasing = 2,
        Attacking = 3
    }

    /// <summary>
    /// Faza 3.1: Döyüş hücum bucağı (Ön, Yan, Arxa).
    /// </summary>
    public enum CombatAttackDirection
    {
        Front = 0,
        Flank = 1,
        Rear = 2
    }
}
