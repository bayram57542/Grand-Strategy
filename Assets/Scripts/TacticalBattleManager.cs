using System;
using System.Collections;
using System.Collections.Generic;
using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Tactical
{
    /// <summary>
    /// 2D Taktiki Döyüş Lövhəsi Meneceri (Epic Battles Online / Europa Universalis formatı):
    /// DatabaseManager-dən iki Army ID-sini oxuyur və ordudakı əsgər sayına görə
    /// UI_BattalionToken prefablarını Object Pool vasitəsilə TacticalBoardCanvas-ın
    /// sol (Ordu 1) və sağ (Ordu 2) kənarlarında RectTransform.anchoredPosition ilə düzür.
    /// </summary>
    public class TacticalBattleManager : MonoBehaviour
    {
        private static TacticalBattleManager _instance;
        public static TacticalBattleManager Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<TacticalBattleManager>());

        [Header("Döyüşən Orduların Baza ID-ləri")]
        [SerializeField] private int attackerArmyId = 1;
        [SerializeField] private int defenderArmyId = 2;
        [SerializeField] private float endBattleDelaySeconds = 3.0f;

        [Header("2D UI Token Prefabı, Lövhə və Object Pool")]
        [SerializeField] private GameObject battalionTokenPrefab;
        [SerializeField] private RectTransform tacticalBoardRect;
        [SerializeField] private RectTransform activeTokensContainer;
        [SerializeField] private RectTransform poolContainer;
        [SerializeField] private int initialPoolSize = 40;

        [Header("2D Lövhə Düzülüşü Parametrləri (Piksel ilə)")]
        [Tooltip("Hər 1 UI_BattalionToken-in təmsil etdiyi əsgər sayı")]
        [SerializeField] private int soldiersPerBattalion = 250;
        [SerializeField] private int maxTokensPerVerticalColumn = 6;
        [SerializeField] private float verticalSpacingY = 82f;
        [SerializeField] private float columnSpacingX = 76f;
        [SerializeField] private float sideDistanceFromCenterX = 520f;

        [Header("Faza 6B - Sərkərdə (Commander) Tokeni və Taxt Böhranı")]
        [SerializeField] private GameObject commanderTokenPrefab;
        [SerializeField] private UnityEngine.UI.Text throneCrisisText;
        public const string KingDeathMessage = "KRAL ÖLDÜ! TAXT BÖHRANI!";

        [Header("Faza 3.1 - Flanking və Döyüş Göstəriciləri")]
        private string _lastCombatIndicatorText = string.Empty;
        private float _lastCombatIndicatorMultiplier = 1.0f;
        private Vector2 _lastCombatIndicatorPosition;

        [Header("Faza 3.2 - Dinamik Mühit və Qala Qapısı")]
        [SerializeField] private BattleEnvironment currentEnvironment = BattleEnvironment.Plains;
        [SerializeField] private GameObject gateTokenPrefab;
        [SerializeField] private UnityEngine.UI.Text gateBreachedText;
        [SerializeField] private UnityEngine.UI.Text sandstormWarningText;
        public const string SandstormWarningMessage = "QUM FIRTINASI AKTİVDİR! Mənzil azaldı!";
        public const string GateBreachedMessage = "QALA QAPISI SINDIRILDI!";

        private UI_GateToken _gateToken;
        private bool _isSandstormActive;
        private string _lastGateBreachedMessage = string.Empty;

        private readonly Queue<UI_BattalionToken> _tokenPool = new Queue<UI_BattalionToken>();
        private readonly List<UI_BattalionToken> _activeBattalions = new List<UI_BattalionToken>();
        private readonly List<UI_BattalionToken> _attackerBattalions = new List<UI_BattalionToken>();
        private readonly List<UI_BattalionToken> _defenderBattalions = new List<UI_BattalionToken>();
        private readonly List<UI_CommanderToken> _commanderTokens = new List<UI_CommanderToken>();

        private bool _poolsInitialized;
        private bool _isBattleEnded;
        private int _winningArmyId = -1;
        private string _lastVictoryMessage = string.Empty;
        private int _totalDestroyedBattalions;
        private bool _isKingDead;
        private string _lastThroneCrisisMessage = string.Empty;

        public IReadOnlyList<UI_CommanderToken> CommanderTokens => _commanderTokens;
        public bool IsKingDead => _isKingDead;
        public string LastThroneCrisisMessage => _lastThroneCrisisMessage;
        public string CurrentThroneCrisisLabel => throneCrisisText != null && throneCrisisText.gameObject.activeSelf ? throneCrisisText.text : string.Empty;

        public IReadOnlyList<UI_BattalionToken> ActiveBattalions => _activeBattalions;
        public IReadOnlyList<UI_BattalionToken> AttackerBattalions => _attackerBattalions;
        public IReadOnlyList<UI_BattalionToken> DefenderBattalions => _defenderBattalions;
        public RectTransform TacticalBoardRect => tacticalBoardRect;
        public RectTransform ActiveTokensContainer => activeTokensContainer;
        public int AttackerArmyId => attackerArmyId;
        public int DefenderArmyId => defenderArmyId;
        public int PoolAvailableCount => _tokenPool.Count;
        public bool IsBattleEnded => _isBattleEnded;
        public int WinningArmyId => _winningArmyId;
        public string LastVictoryMessage => _lastVictoryMessage;
        public int TotalDestroyedBattalions => _totalDestroyedBattalions;
        public string LastCombatIndicatorText => _lastCombatIndicatorText;
        public float LastCombatIndicatorMultiplier => _lastCombatIndicatorMultiplier;
        public Vector2 LastCombatIndicatorPosition => _lastCombatIndicatorPosition;

        public BattleEnvironment CurrentEnvironment => currentEnvironment;
        public UI_GateToken GateToken => _gateToken;
        public bool IsGateIntact => _gateToken != null && !_gateToken.IsDead && _gateToken.gameObject.activeInHierarchy;
        public float GatePositionX => _gateToken != null ? _gateToken.AnchoredPosition.x : 0f;
        public bool IsSandstormActive => _isSandstormActive;
        public string LastGateBreachedMessage => _lastGateBreachedMessage;
        public string CurrentGateBreachedLabel => gateBreachedText != null && gateBreachedText.gameObject.activeSelf ? gateBreachedText.text : string.Empty;
        public string CurrentSandstormWarningLabel => sandstormWarningText != null && sandstormWarningText.gameObject.activeSelf ? sandstormWarningText.text : string.Empty;

        public void ConfigureBoardAndPrefab(
            GameObject tokenPrefab,
            RectTransform boardRect,
            RectTransform activeContainer,
            RectTransform pooledContainer)
        {
            battalionTokenPrefab = tokenPrefab;
            tacticalBoardRect = boardRect;
            activeTokensContainer = activeContainer;
            poolContainer = pooledContainer;
            _poolsInitialized = false;
        }

        /// <summary>
        /// Sərkərdə Tokeni prefabını və "KRAL ÖLDÜ! TAXT BÖHRANI!" mətn elementini təyin edir.
        /// </summary>
        public void ConfigureCommanderPrefabAndCrisisText(GameObject commanderPrefab, UnityEngine.UI.Text crisisText)
        {
            commanderTokenPrefab = commanderPrefab;
            throneCrisisText = crisisText;
            HideThroneCrisisText();
        }

        /// <summary>
        /// Faza 3.2: Qala Qapısı prefabını və mühit UI mətnlərini konfiqurasiya edir.
        /// </summary>
        public void ConfigureGatePrefabAndTexts(GameObject gatePrefab, UnityEngine.UI.Text gateText, UnityEngine.UI.Text sandText)
        {
            gateTokenPrefab = gatePrefab;
            gateBreachedText = gateText;
            sandstormWarningText = sandText;
            HideGateBreachedText();
            HideSandstormWarningText();
        }

        public void SetBattleEnvironment(BattleEnvironment environment)
        {
            currentEnvironment = environment;
        }

        /// <summary>
        /// Səhra mühitində Qum Fırtınasını açır və ya bağlayır:
        /// Aktiv olduqda bütün batalyonların hərəkət sürəti və görmə/hücum mənzili 50% azalır,
        /// UI-da "QUM FIRTINASI AKTİVDİR! Mənzil azaldı!" xəbərdarlığı görünür.
        /// </summary>
        public void SetSandstorm(bool active)
        {
            _isSandstormActive = active;
            for (int i = 0; i < _activeBattalions.Count; i++)
            {
                UI_BattalionToken token = _activeBattalions[i];
                if (token != null && !token.IsDead && token.gameObject.activeInHierarchy)
                {
                    token.SetSandstormActive(active);
                }
            }

            if (active)
            {
                ShowSandstormWarningText(SandstormWarningMessage);
            }
            else
            {
                HideSandstormWarningText();
            }

            Debug.Log($"[TacticalBattleManager] Qum Fırtınası Vəziyyəti: {active}");
        }

        public void HideGateBreachedText()
        {
            if (gateBreachedText != null)
            {
                gateBreachedText.text = string.Empty;
                gateBreachedText.gameObject.SetActive(false);
            }
        }

        public void HideSandstormWarningText()
        {
            if (sandstormWarningText != null)
            {
                sandstormWarningText.text = string.Empty;
                sandstormWarningText.gameObject.SetActive(false);
            }
        }

        public void ShowGateBreachedText(string message)
        {
            if (gateBreachedText == null)
            {
                EnsureContainers();
                Transform parent = tacticalBoardRect != null ? tacticalBoardRect : transform;
                var go = new GameObject("GateBreachedText", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1600f, 180f);
                rt.anchoredPosition = new Vector2(0f, 130f);
                gateBreachedText = go.GetComponent<UnityEngine.UI.Text>();
                gateBreachedText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                gateBreachedText.fontSize = 80;
                gateBreachedText.fontStyle = FontStyle.Bold;
                gateBreachedText.alignment = TextAnchor.MiddleCenter;
                gateBreachedText.color = new Color(1f, 0.22f, 0.15f, 1f);
                gateBreachedText.raycastTarget = false;

                var outline = go.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                outline.effectDistance = new Vector2(3f, -3f);
            }

            gateBreachedText.text = message.ToUpperInvariant();
            gateBreachedText.gameObject.SetActive(true);
            gateBreachedText.transform.SetAsLastSibling();
        }

        public void ShowSandstormWarningText(string message)
        {
            if (sandstormWarningText == null)
            {
                EnsureContainers();
                Transform parent = tacticalBoardRect != null ? tacticalBoardRect : transform;
                var go = new GameObject("SandstormWarningText", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1600f, 100f);
                rt.anchoredPosition = new Vector2(0f, 50f);
                sandstormWarningText = go.GetComponent<UnityEngine.UI.Text>();
                sandstormWarningText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                sandstormWarningText.fontSize = 46;
                sandstormWarningText.fontStyle = FontStyle.Bold;
                sandstormWarningText.alignment = TextAnchor.MiddleCenter;
                sandstormWarningText.color = new Color(0.96f, 0.65f, 0.15f, 1f);
                sandstormWarningText.raycastTarget = false;

                var outline = go.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0.25f, 0.12f, 0f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            sandstormWarningText.text = message.ToUpperInvariant();
            sandstormWarningText.gameObject.SetActive(true);
            sandstormWarningText.transform.SetAsLastSibling();
        }

        private void HideThroneCrisisText()
        {
            if (throneCrisisText != null)
            {
                throneCrisisText.text = string.Empty;
                throneCrisisText.gameObject.SetActive(false);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureContainers();
        }

        private void Start()
        {
            InitializeBattleFromGameManager();

            // Play Mode-da rəqib ordusunu (Sağ tərəf - Ordu 2) lövhənin mərkəzinə doğru hərəkətə başladırıq ki,
            // oyunçu tokenləri ilə yaxınlaşdıqda avtomatik toqquşub döyüşsünlər (Qala qapısı varsa, arxasında dayanırlar)
            for (int i = 0; i < _defenderBattalions.Count; i++)
            {
                UI_BattalionToken defender = _defenderBattalions[i];
                if (defender != null && !defender.IsDead && defender.gameObject.activeInHierarchy && !(defender is UI_GateToken))
                {
                    Vector2 pos = defender.AnchoredPosition;
                    float targetX = (currentEnvironment == BattleEnvironment.Siege && IsGateIntact)
                        ? Mathf.Max(60f, pos.x)
                        : 35f;
                    defender.MoveTo(new Vector2(targetX, pos.y));
                }
            }
        }

        /// <summary>
        /// Səhnə açıldıqda sərt (hardcoded) ID-lər əvəzinə GameManager.Instance.CurrentAttackerArmyId
        /// və CurrentDefenderArmyId dəyişənlərini oxuyaraq 2D tokenləri lövhədə yaradır.
        /// </summary>
        public void InitializeBattleFromGameManager()
        {
            GameManager gm = GameManager.Instance;
            int resolvedAttackerId = gm != null
                ? GameManager.ParseArmyId(gm.CurrentAttackerArmyId, attackerArmyId)
                : attackerArmyId;

            int resolvedDefenderId = gm != null
                ? GameManager.ParseArmyId(gm.CurrentDefenderArmyId, defenderArmyId)
                : defenderArmyId;

            InitializeAndSpawnBattle(resolvedAttackerId, resolvedDefenderId);
        }

        private void EnsureContainers()
        {
            if (tacticalBoardRect == null)
            {
                GameObject canvasGo = GameObject.Find("TacticalBoardCanvas");
                if (canvasGo != null)
                {
                    Transform boardTr = canvasGo.transform.Find("BattlefieldBoard");
                    tacticalBoardRect = boardTr != null
                        ? boardTr.GetComponent<RectTransform>()
                        : canvasGo.GetComponent<RectTransform>();
                }
            }

            Transform parentForContainers = tacticalBoardRect != null ? tacticalBoardRect : transform;

            if (poolContainer == null)
            {
                Transform foundPool = parentForContainers.Find("TokenObjectPool");
                if (foundPool != null)
                {
                    poolContainer = foundPool.GetComponent<RectTransform>();
                }
                else
                {
                    var go = new GameObject("TokenObjectPool", typeof(RectTransform));
                    go.transform.SetParent(parentForContainers, false);
                    poolContainer = go.GetComponent<RectTransform>();
                    poolContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    poolContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    poolContainer.anchoredPosition = Vector2.zero;
                }
            }

            if (activeTokensContainer == null)
            {
                Transform foundActive = parentForContainers.Find("ActiveTokensContainer");
                if (foundActive != null)
                {
                    activeTokensContainer = foundActive.GetComponent<RectTransform>();
                }
                else
                {
                    var go = new GameObject("ActiveTokensContainer", typeof(RectTransform));
                    go.transform.SetParent(parentForContainers, false);
                    activeTokensContainer = go.GetComponent<RectTransform>();
                    activeTokensContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    activeTokensContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    activeTokensContainer.sizeDelta = new Vector2(1920f, 1080f);
                    activeTokensContainer.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// 2D UI_BattalionToken Object Pool-unu əvvəlcədən hazırlayır (Prewarm).
        /// </summary>
        public void InitializeObjectPools()
        {
            EnsureContainers();

            if (_poolsInitialized && (_tokenPool.Count > 0 || _activeBattalions.Count > 0))
            {
                return;
            }

            ClearTransformChildren(poolContainer);
            ClearTransformChildren(activeTokensContainer);
            _tokenPool.Clear();
            _activeBattalions.Clear();
            _attackerBattalions.Clear();
            _defenderBattalions.Clear();
            _commanderTokens.Clear();

            for (int i = 0; i < initialPoolSize; i++)
            {
                if (battalionTokenPrefab != null)
                {
                    UI_BattalionToken tokenInstance = CreatePooledInstance(battalionTokenPrefab, $"UI_BattalionToken_Pooled_{i + 1}");
                    _tokenPool.Enqueue(tokenInstance);
                }
            }

            _poolsInitialized = true;
            Debug.Log($"[TacticalBattleManager] 2D Token Object Pool hazırlandı -> Pool Sayı: {_tokenPool.Count}");
        }

        private UI_BattalionToken CreatePooledInstance(GameObject prefab, string instanceName)
        {
            GameObject instanceGo = Instantiate(prefab, poolContainer);
            instanceGo.name = instanceName;
            UI_BattalionToken token = instanceGo.GetComponent<UI_BattalionToken>();
            if (token == null)
            {
                token = instanceGo.AddComponent<UI_BattalionToken>();
            }
            instanceGo.SetActive(false);
            return token;
        }

        private UI_BattalionToken GetFromPool()
        {
            UI_BattalionToken token = null;
            while (_tokenPool.Count > 0 && token == null)
            {
                token = _tokenPool.Dequeue();
            }

            if (token == null)
            {
                if (battalionTokenPrefab == null)
                {
                    throw new InvalidOperationException("[TacticalBattleManager] UI_BattalionToken prefabı təyin edilməyib!");
                }

                token = CreatePooledInstance(battalionTokenPrefab, "UI_BattalionToken_ExpandedPool");
            }

            token.transform.SetParent(activeTokensContainer, false);
            token.gameObject.SetActive(true);
            _activeBattalions.Add(token);
            return token;
        }

        /// <summary>
        /// Tək bir 2D tokeni (məsələn, öldükdə) aktiv siyahılardan çıxarır,
        /// gizlədir (SetActive(false)) və Object Pool-a qaytarır.
        /// </summary>
        public void ReturnToPool(UI_BattalionToken token)
        {
            if (token == null)
            {
                return;
            }

            EnsureContainers();

            _activeBattalions.Remove(token);
            _attackerBattalions.Remove(token);
            _defenderBattalions.Remove(token);

            token.SetSelected(false);
            token.gameObject.SetActive(false);
            token.transform.SetParent(poolContainer, false);

            if (!_tokenPool.Contains(token))
            {
                _tokenPool.Enqueue(token);
            }
        }

        /// <summary>
        /// Token öldükdə (CurrentHP <= 0) UI_BattalionToken tərəfindən çağırılır.
        /// Tokeni hovuza qaytarır və qələbə şərtlərini yoxlayır.
        /// </summary>
        public void NotifyBattalionDied(UI_BattalionToken deadToken)
        {
            if (deadToken == null)
            {
                return;
            }

            int deadArmyId = deadToken.ArmyId;
            string deadName = deadToken.name;
            _totalDestroyedBattalions++;

            UI_GateToken deadGate = deadToken as UI_GateToken;
            if (deadGate != null)
            {
                NotifyGateDestroyed(deadGate);
                CheckVictoryConditions();
                return;
            }

            UI_CommanderToken deadCommander = deadToken as UI_CommanderToken;
            if (deadCommander != null)
            {
                HandleCommanderDeath(deadCommander);
            }
            else
            {
                ReturnToPool(deadToken);
            }

            Debug.Log(
                $"[TacticalBattleManager] 2D Token məhv edildi və hovuza qaytarıldı: {deadName} (Ordu #{deadArmyId}) | " +
                $"Qalan Tokenlər -> Ordu #{attackerArmyId}: {_attackerBattalions.Count}, Ordu #{defenderArmyId}: {_defenderBattalions.Count}");

            CheckVictoryConditions();
        }

        /// <summary>
        /// Hər dəfə bir token öləndə orduların vəziyyətini yoxlayır.
        /// Əgər Ordu 1-in və ya Ordu 2-nin bütün tokenləri məhv olarsa,
        /// konsola "Döyüş Bitdi! Qalib: Ordu X" mesajını yazır və EndBattle keçidini başladır.
        /// Qeyd: Müdafiə tərəfindəki UI_GateToken ordu əsgəri sayılmır (baryerdir).
        /// </summary>
        public void CheckVictoryConditions()
        {
            if (_isBattleEnded)
            {
                return;
            }

            int activeAttackerCount = 0;
            for (int i = 0; i < _attackerBattalions.Count; i++)
            {
                UI_BattalionToken atk = _attackerBattalions[i];
                if (atk != null && !atk.IsDead && atk.gameObject.activeInHierarchy)
                {
                    activeAttackerCount++;
                }
            }

            int activeDefenderCount = 0;
            for (int i = 0; i < _defenderBattalions.Count; i++)
            {
                UI_BattalionToken def = _defenderBattalions[i];
                if (def != null && !def.IsDead && def.gameObject.activeInHierarchy && !(def is UI_GateToken))
                {
                    activeDefenderCount++;
                }
            }

            if (activeAttackerCount == 0 && activeDefenderCount > 0)
            {
                _isBattleEnded = true;
                _winningArmyId = defenderArmyId;
                _lastVictoryMessage = $"Döyüş Bitdi! Qalib: Ordu {_winningArmyId}";
                Debug.Log(_lastVictoryMessage);
                ScheduleEndBattleTransition();
            }
            else if (activeDefenderCount == 0 && activeAttackerCount > 0)
            {
                _isBattleEnded = true;
                _winningArmyId = attackerArmyId;
                _lastVictoryMessage = $"Döyüş Bitdi! Qalib: Ordu {_winningArmyId}";
                Debug.Log(_lastVictoryMessage);
                ScheduleEndBattleTransition();
            }
            else if (activeAttackerCount == 0 && activeDefenderCount == 0)
            {
                _isBattleEnded = true;
                _winningArmyId = attackerArmyId;
                _lastVictoryMessage = $"Döyüş Bitdi! Qalib: Ordu {_winningArmyId}";
                Debug.Log(_lastVictoryMessage);
                ScheduleEndBattleTransition();
            }
        }

        public string GetWinningArmyIdString()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (_winningArmyId == attackerArmyId && !string.IsNullOrWhiteSpace(gm.CurrentAttackerArmyId))
                {
                    return gm.CurrentAttackerArmyId;
                }

                if (_winningArmyId == defenderArmyId && !string.IsNullOrWhiteSpace(gm.CurrentDefenderArmyId))
                {
                    return gm.CurrentDefenderArmyId;
                }
            }

            return $"Army_{_winningArmyId}";
        }

        private void ScheduleEndBattleTransition()
        {
            if (Application.isPlaying)
            {
                StartCoroutine(EndBattleAfterDelayCoroutine(GetWinningArmyIdString(), endBattleDelaySeconds));
            }
        }

        private IEnumerator EndBattleAfterDelayCoroutine(string winnerArmyIdStr, float delaySeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, delaySeconds));
            CompleteBattleAndReturnToGlobalMap(winnerArmyIdStr);
        }

        /// <summary>
        /// Döyüş bitdikdə GameManager.Instance.EndBattle(winnerArmyId) funksiyasını çağıraraq
        /// məğlub ordunu bazadan silir və GlobalMapScene səhnəsinə qayıdır.
        /// </summary>
        public void CompleteBattleAndReturnToGlobalMap(string winnerArmyIdStr = null)
        {
            string resolvedWinner = string.IsNullOrWhiteSpace(winnerArmyIdStr)
                ? GetWinningArmyIdString()
                : winnerArmyIdStr;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                gm.EndBattle(resolvedWinner, _isKingDead);
            }
        }

        /// <summary>
        /// Sərkərdə Tokeni öldükdə çağırılır: tokeni siyahılardan çıxarır və gizlədir.
        /// Əgər ölən sərkərdə "Kral"dırsa, ekrana böyük hərflərlə "KRAL ÖLDÜ! TAXT BÖHRANI!" yazısı çıxır
        /// və Kralın ordusundakı sağ qalan bütün tokenlərə anında panika tətbiq edilir.
        /// </summary>
        private void HandleCommanderDeath(UI_CommanderToken commander)
        {
            _activeBattalions.Remove(commander);
            _attackerBattalions.Remove(commander);
            _defenderBattalions.Remove(commander);
            commander.SetSelected(false);
            commander.gameObject.SetActive(false);

            if (commander.IsKing)
            {
                _isKingDead = true;
                _lastThroneCrisisMessage = KingDeathMessage;
                ShowThroneCrisisText(KingDeathMessage);
                Debug.LogWarning($"[TacticalBattleManager] {KingDeathMessage} (Ordu #{commander.ArmyId}, Lider #{commander.LeaderId} '{commander.LeaderName}')");

                // Döyüşdə "Kral Öldü" Panikası (Faza 6B Təkmilləşdirmə):
                // Kralın ordusundakı sağ qalan BÜTÜN tokenlər üçün anında ApplyPanic(5.0f, 0.5f) funksiyasını çağırırıq
                ApplyPanicToArmy(commander.ArmyId, 5.0f, 0.5f);
            }
            else
            {
                Debug.Log($"[TacticalBattleManager] Sərkərdə həlak oldu: '{commander.LeaderName}' (Ordu #{commander.ArmyId})");
            }
        }

        /// <summary>
        /// Verilən ordudakı sağ qalan bütün tokenlərə panika tətbiq edir (sürət 50% azalır, zərər yarıbayarı düşür).
        /// </summary>
        public int ApplyPanicToArmy(int armyId, float duration, float damageReductionMultiplier)
        {
            int panickedCount = 0;
            for (int i = 0; i < _activeBattalions.Count; i++)
            {
                UI_BattalionToken token = _activeBattalions[i];
                if (token != null && !token.IsDead && token.gameObject.activeInHierarchy && token.ArmyId == armyId)
                {
                    token.ApplyPanic(duration, damageReductionMultiplier);
                    panickedCount++;
                }
            }

            Debug.Log($"[TacticalBattleManager] Kral Öldü Panikası -> Ordu #{armyId} daxilindəki {panickedCount} tokenə tətbiq edildi!");
            return panickedCount;
        }

        private void ShowThroneCrisisText(string message)
        {
            if (throneCrisisText == null)
            {
                EnsureContainers();
                Transform parent = tacticalBoardRect != null ? tacticalBoardRect : transform;
                var go = new GameObject("ThroneCrisisText", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1600f, 200f);
                rt.anchoredPosition = new Vector2(0f, 120f);
                throneCrisisText = go.GetComponent<UnityEngine.UI.Text>();
                throneCrisisText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                throneCrisisText.fontSize = 96;
                throneCrisisText.fontStyle = FontStyle.Bold;
                throneCrisisText.alignment = TextAnchor.MiddleCenter;
                throneCrisisText.color = new Color(1f, 0.18f, 0.12f, 1f);
                throneCrisisText.raycastTarget = false;
            }

            throneCrisisText.text = message.ToUpperInvariant();
            throneCrisisText.gameObject.SetActive(true);
            throneCrisisText.transform.SetAsLastSibling();
        }

        #region Faza 3.1 - Flanking və Döyüş Göstəriciləri

        /// <summary>
        /// Flanking (Yandan 1.5x) və ya Arxadan (2.0x) zərbə endirildikdə ekranda uçuşan mətn göstərir.
        /// </summary>
        public void TriggerFlankingCombatIndicator(Vector2 targetAnchoredPosition, CombatAttackDirection direction, float multiplier)
        {
            string label = direction == CombatAttackDirection.Rear
                ? "Arxadan Hücum! x2.0"
                : "Yandan Zərbə! x1.5";

            Color textColor = direction == CombatAttackDirection.Rear
                ? new Color(1.0f, 0.22f, 0.18f, 1f) // Parlaq qırmızı / kritik
                : new Color(1.0f, 0.85f, 0.20f, 1f); // Qızılı sarı / cinah

            _lastCombatIndicatorText = label;
            _lastCombatIndicatorMultiplier = multiplier;
            _lastCombatIndicatorPosition = targetAnchoredPosition;

            Debug.Log($"[TacticalBattleManager] FLANKING ZƏRBƏSİ! '{label}' @ {targetAnchoredPosition} (Multiplier: {multiplier:F1}x)");

            ShowFloatingCombatText(targetAnchoredPosition + new Vector2(0f, 34f), label, textColor);
        }

        public void ShowFloatingCombatText(Vector2 anchoredPosition, string message, Color textColor)
        {
            Transform parent = activeTokensContainer != null ? activeTokensContainer : transform;
            var textGo = new GameObject("FloatingCombatText", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            textGo.transform.SetParent(parent, false);

            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(260f, 36f);
            rt.anchoredPosition = anchoredPosition;

            var txt = textGo.GetComponent<UnityEngine.UI.Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = message;
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.color = textColor;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            var outline = textGo.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            if (Application.isPlaying && isActiveAndEnabled)
            {
                StartCoroutine(AnimateFloatingTextCoroutine(textGo, rt, txt));
            }
            else
            {
                // Test zamanı və ya dərhal icrada 1 frame sonra silirik
                DestroyImmediate(textGo);
            }
        }

        private IEnumerator AnimateFloatingTextCoroutine(GameObject textGo, RectTransform rt, UnityEngine.UI.Text txt)
        {
            float duration = 1.0f;
            float elapsed = 0f;
            Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 endPos = startPos + new Vector2(0f, 45f);
            Color startColor = txt != null ? txt.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                }
                if (txt != null)
                {
                    txt.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
                }
                yield return null;
            }

            if (textGo != null)
            {
                Destroy(textGo);
            }
        }

        #endregion

        /// <summary>
        /// Hər iki ordunun SQLite-dakı Leader (sərkərdə) qeydlərini yoxlayır və sərkərdəsi olan
        /// ordunun arxa-mərkəzində ulduz ikonlu UI_CommanderToken yaradır.
        /// </summary>
        private void SpawnCommandersForArmies(DatabaseManager db, Army attackerArmy, Army defenderArmy)
        {
            List<Leader> leaders = db.Connection.Table<Leader>().ToList();
            SpawnCommanderIfAssigned(leaders, attackerArmy, true, -sideDistanceFromCenterX, -1f, _attackerBattalions);
            SpawnCommanderIfAssigned(leaders, defenderArmy, false, sideDistanceFromCenterX, 1f, _defenderBattalions);
        }

        private void SpawnCommanderIfAssigned(
            List<Leader> leaders,
            Army army,
            bool isPlayerControlled,
            float frontLineX,
            float backColumnDirectionX,
            List<UI_BattalionToken> targetList)
        {
            if (army == null || leaders == null)
            {
                return;
            }

            Leader commanderLeader = leaders.Find(l => l.ParsedArmyId.HasValue && l.ParsedArmyId.Value == army.Id);
            if (commanderLeader == null)
            {
                return;
            }

            if (commanderTokenPrefab == null)
            {
                Debug.LogWarning($"[TacticalBattleManager] Ordu #{army.Id} üçün sərkərdə '{commanderLeader.Name}' tapıldı, lakin UI_CommanderToken prefabı təyin edilməyib!");
                return;
            }

            // Ordunun arxasında (son sütundan bir addım geridə), şaquli mərkəzdə yerləşdiririk
            int rowsPerColumn = Mathf.Max(2, maxTokensPerVerticalColumn);
            int unitTokens = 0;
            for (int i = 0; i < targetList.Count; i++)
            {
                if (!(targetList[i] is UI_CommanderToken))
                {
                    unitTokens++;
                }
            }

            int columns = Mathf.Max(1, Mathf.CeilToInt(unitTokens / (float)rowsPerColumn));
            float x = frontLineX + (columns * columnSpacingX * backColumnDirectionX);
            Vector2 spawnPos = new Vector2(x, 0f);

            GameObject go = Instantiate(commanderTokenPrefab, activeTokensContainer);
            go.name = $"Commander_Army{army.Id}_{commanderLeader.Name.Replace(' ', '_')}";
            go.SetActive(true);

            UI_CommanderToken commander = go.GetComponent<UI_CommanderToken>();
            if (commander == null)
            {
                commander = go.AddComponent<UI_CommanderToken>();
            }

            commander.InitializeCommander(
                army.Id,
                army.OwnerFactionId,
                commanderLeader.Id,
                commanderLeader.Name,
                commanderLeader.IsKing,
                commanderLeader.IsRebel,
                isPlayerControlled,
                spawnPos);

            _commanderTokens.Add(commander);
            _activeBattalions.Add(commander);
            targetList.Add(commander);

            Debug.Log($"[TacticalBattleManager] Sərkərdə Tokeni (Ulduz) yaradıldı: '{commanderLeader.Name}' -> Ordu #{army.Id} @ {spawnPos} (Kral: {commanderLeader.IsKing}, Üsyançı: {commanderLeader.IsRebel})");
        }

        private void DestroyCommanderTokens()
        {
            for (int i = _commanderTokens.Count - 1; i >= 0; i--)
            {
                UI_CommanderToken commander = _commanderTokens[i];
                if (commander == null)
                {
                    continue;
                }

                _activeBattalions.Remove(commander);
                _attackerBattalions.Remove(commander);
                _defenderBattalions.Remove(commander);

                if (Application.isPlaying)
                {
                    Destroy(commander.gameObject);
                }
                else
                {
                    DestroyImmediate(commander.gameObject);
                }
            }

            _commanderTokens.Clear();
        }

        /// <summary>
        /// Faza 3.2: Lövhənin mərkəzində (X = 0) 2000 HP cana malik UI_GateToken (Qala Qapısı) yaradır.
        /// </summary>
        public void SpawnGateToken(Army defenderArmy)
        {
            DestroyGateToken();

            int defArmyId = defenderArmy != null ? defenderArmy.Id : defenderArmyId;
            int defFactionId = defenderArmy != null ? defenderArmy.OwnerFactionId : 2;
            Vector2 spawnPos = new Vector2(0f, 0f);

            GameObject gateGo;
            if (gateTokenPrefab != null)
            {
                gateGo = Instantiate(gateTokenPrefab, activeTokensContainer);
            }
            else
            {
                EnsureContainers();
                Transform parent = activeTokensContainer != null ? activeTokensContainer : transform;
                gateGo = new GameObject("UI_GateToken", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                gateGo.transform.SetParent(parent, false);
                var img = gateGo.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0.38f, 0.24f, 0.15f, 1f);
                var outline = gateGo.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.95f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            gateGo.name = $"UI_GateToken_Army{defArmyId}";
            gateGo.SetActive(true);

            UI_GateToken gate = gateGo.GetComponent<UI_GateToken>();
            if (gate == null)
            {
                gate = gateGo.AddComponent<UI_GateToken>();
            }

            gate.InitializeGate(defArmyId, defFactionId, spawnPos, UI_GateToken.GateDefaultMaxHP);

            _gateToken = gate;
            _activeBattalions.Add(gate);
            _defenderBattalions.Add(gate);

            Debug.Log($"[TacticalBattleManager] Qala Qapısı (2000 HP) lövhənin mərkəzində yaradıldı @ {spawnPos} (Ordu #{defArmyId})");
        }

        public void DestroyGateToken()
        {
            if (_gateToken != null)
            {
                _activeBattalions.Remove(_gateToken);
                _defenderBattalions.Remove(_gateToken);

                if (Application.isPlaying)
                {
                    Destroy(_gateToken.gameObject);
                }
                else
                {
                    DestroyImmediate(_gateToken.gameObject);
                }

                _gateToken = null;
            }
        }

        public void NotifyGateDestroyed(UI_GateToken gate)
        {
            _lastGateBreachedMessage = GateBreachedMessage;
            ShowGateBreachedText(GateBreachedMessage);
            Debug.LogWarning($"[TacticalBattleManager] {GateBreachedMessage} (Qapı sındırıldı! Hücumçular üçün qala qapısı açıldı)");

            if (gate != null)
            {
                _activeBattalions.Remove(gate);
                _defenderBattalions.Remove(gate);
                gate.gameObject.SetActive(false);
            }

            _gateToken = null;

            for (int i = 0; i < _attackerBattalions.Count; i++)
            {
                UI_BattalionToken atk = _attackerBattalions[i];
                if (atk != null && !atk.IsDead && atk.gameObject.activeInHierarchy)
                {
                    atk.NotifyGateBreached();
                }
            }
        }

        /// <summary>
        /// Bütün aktiv tokenləri yenidən Object Pool-a qaytarır.
        /// </summary>
        public void ReturnAllToPool()
        {
            EnsureContainers();
            DestroyGateToken();
            DestroyCommanderTokens();

            for (int i = _activeBattalions.Count - 1; i >= 0; i--)
            {
                UI_BattalionToken token = _activeBattalions[i];
                if (token == null)
                {
                    continue;
                }

                token.SetSelected(false);
                token.gameObject.SetActive(false);
                token.transform.SetParent(poolContainer, false);

                if (!_tokenPool.Contains(token))
                {
                    _tokenPool.Enqueue(token);
                }
            }

            _activeBattalions.Clear();
            _attackerBattalions.Clear();
            _defenderBattalions.Clear();
        }

        public void InitializeAndSpawnBattle(int firstArmyId, int secondArmyId)
        {
            InitializeAndSpawnBattle(firstArmyId, secondArmyId, null);
        }

        /// <summary>
        /// DatabaseManager-dən verilən iki Army ID-sini oxuyur, döyüş mühitini (Plains, Desert, Siege)
        /// təyin edir və orduları Canvas-ın sol (Ordu 1) və sağ (Ordu 2) kənarlarında düzür.
        /// </summary>
        public void InitializeAndSpawnBattle(int firstArmyId, int secondArmyId, BattleEnvironment? overrideEnvironment)
        {
            attackerArmyId = firstArmyId;
            defenderArmyId = secondArmyId;
            _isBattleEnded = false;
            _winningArmyId = -1;
            _lastVictoryMessage = string.Empty;
            _totalDestroyedBattalions = 0;
            _isKingDead = false;
            _lastThroneCrisisMessage = string.Empty;
            _lastGateBreachedMessage = string.Empty;
            HideThroneCrisisText();
            HideGateBreachedText();
            HideSandstormWarningText();

            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db == null)
            {
                Debug.LogError("[TacticalBattleManager] Səhnədə DatabaseManager tapılmadı!");
                return;
            }

            if (!db.IsConnected)
            {
                db.InitializeDatabase();
            }

            EnsureTwoBattleArmiesExistInDatabase(db, ref attackerArmyId, ref defenderArmyId);

            Army attackerArmy = db.Connection.Table<Army>().FirstOrDefault(a => a.Id == attackerArmyId);
            Army defenderArmy = db.Connection.Table<Army>().FirstOrDefault(a => a.Id == defenderArmyId);

            if (attackerArmy == null || defenderArmy == null)
            {
                Debug.LogError($"[TacticalBattleManager] Baza cədvəlində Army #{attackerArmyId} və ya Army #{defenderArmyId} tapılmadı!");
                return;
            }

            // Faza 3.2: Döyüş Mühitinin (BattleEnvironment) təyini
            if (overrideEnvironment.HasValue)
            {
                currentEnvironment = overrideEnvironment.Value;
            }
            else
            {
                GameManager gm = GameManager.Instance;
                if (gm != null && gm.CurrentBattleEnvironment != BattleEnvironment.Plains)
                {
                    currentEnvironment = gm.CurrentBattleEnvironment;
                }
                else
                {
                    Province prov = null;
                    if (defenderArmy != null)
                    {
                        prov = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == defenderArmy.LocationProvinceId);
                    }
                    if (prov == null && attackerArmy != null)
                    {
                        prov = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == attackerArmy.LocationProvinceId);
                    }

                    if (prov != null && prov.IsBesieged)
                    {
                        currentEnvironment = BattleEnvironment.Siege;
                    }
                    else if (prov != null && !string.IsNullOrEmpty(prov.Climate) &&
                             (prov.Climate.IndexOf("Səhra", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              prov.Climate.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              prov.Climate.IndexOf("Quraq", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        currentEnvironment = BattleEnvironment.Desert;
                    }
                    else
                    {
                        currentEnvironment = BattleEnvironment.Plains;
                    }
                }
            }

            InitializeObjectPools();
            ReturnAllToPool();

            // 1. Ordu 1 (Hücum / Oyunçu tərəfi): Canvas-ın SOL kənarında (X = -sideDistanceFromCenterX, arxa sütunlar sola -X doğru)
            SpawnArmyOnCanvasSide(
                attackerArmy,
                isPlayerControlled: true,
                frontLineX: -sideDistanceFromCenterX,
                backColumnDirectionX: -1f,
                targetList: _attackerBattalions);

            // 2. Ordu 2 (Müdafiə / Rəqib tərəfi): Canvas-ın SAĞ kənarında (X = +sideDistanceFromCenterX, arxa sütunlar sağa +X doğru)
            SpawnArmyOnCanvasSide(
                defenderArmy,
                isPlayerControlled: false,
                frontLineX: sideDistanceFromCenterX,
                backColumnDirectionX: 1f,
                targetList: _defenderBattalions);

            // 3. Faza 6B: Orduların sərkərdəsi (Leader) varsa, ulduz ikonlu Sərkərdə Tokeni yaradırıq
            SpawnCommandersForArmies(db, attackerArmy, defenderArmy);

            // 4. Faza 3.2: Əgər mühit Siege (Qala Mühasirəsi) olarsa, lövhənin mərkəzində Qala Qapısı (2000 HP) yaradırıq
            if (currentEnvironment == BattleEnvironment.Siege)
            {
                SpawnGateToken(defenderArmy);
            }

            // 5. Faza 3.2: Əgər mühit Desert (Səhra) olarsa, Qum Fırtınası vəziyyəti işə düşür
            if (currentEnvironment == BattleEnvironment.Desert)
            {
                SetSandstorm(true);
            }
            else
            {
                SetSandstorm(false);
            }

            Debug.Log(
                $"[TacticalBattleManager] 2D Taktiki Lövhə Quruldu! Mühit: {currentEnvironment} | " +
                $"Sol Kənar -> Ordu #{attackerArmy.Id} (Faction {attackerArmy.OwnerFactionId}): {_attackerBattalions.Count} token ({attackerArmy.YouthSoldiers} Süvari, {attackerArmy.AdultSoldiers} Piyada) | " +
                $"Sağ Kənar -> Ordu #{defenderArmy.Id} (Faction {defenderArmy.OwnerFactionId}): {_defenderBattalions.Count} token ({defenderArmy.YouthSoldiers} Süvari, {defenderArmy.AdultSoldiers} Piyada).");
        }

        /// <summary>
        /// Ordudakı əsgər sayına əsasən Ağır Piyada (ön sütun) və Elit Süvari (arxa/cinah sütun)
        /// tokenlərini Canvas-ın sol və ya sağ kənarında şaquli cərgələrlə (RectTransform.anchoredPosition) yerləşdirir.
        /// </summary>
        private void SpawnArmyOnCanvasSide(
            Army army,
            bool isPlayerControlled,
            float frontLineX,
            float backColumnDirectionX,
            List<UI_BattalionToken> targetList)
        {
            int unitScale = Mathf.Max(50, soldiersPerBattalion);

            int adultTokenCount = army.AdultSoldiers > 0
                ? Mathf.Max(1, Mathf.RoundToInt(army.AdultSoldiers / (float)unitScale))
                : 0;

            int youthTokenCount = army.YouthSoldiers > 0
                ? Mathf.Max(1, Mathf.RoundToInt(army.YouthSoldiers / (float)unitScale))
                : 0;

            // Ön xətdə Ağır Piyadalar (AdultInfantry), arxa/cinah sütunlarda Elit Süvarilər (YouthCavalry)
            var typesToSpawn = new List<BattalionType>(adultTokenCount + youthTokenCount);
            for (int i = 0; i < adultTokenCount; i++)
            {
                typesToSpawn.Add(BattalionType.AdultInfantry);
            }
            for (int i = 0; i < youthTokenCount; i++)
            {
                typesToSpawn.Add(BattalionType.YouthCavalry);
            }

            int totalUnits = typesToSpawn.Count;
            if (totalUnits == 0)
            {
                return;
            }

            int rowsPerColumn = Mathf.Max(2, maxTokensPerVerticalColumn);

            for (int index = 0; index < totalUnits; index++)
            {
                int col = index / rowsPerColumn;
                int row = index % rowsPerColumn;
                int unitsInThisCol = Mathf.Min(rowsPerColumn, totalUnits - (col * rowsPerColumn));

                // Şahmat sürüşməsi: tək sütunlar yarım addım şaquli sürüşür
                float staggeredYOffset = (col % 2 == 1) ? (verticalSpacingY * 0.35f) : 0f;
                float colHeight = (unitsInThisCol - 1) * verticalSpacingY;
                float y = (colHeight * 0.5f) - (row * verticalSpacingY) + staggeredYOffset;
                float x = frontLineX + (col * columnSpacingX * backColumnDirectionX);

                Vector2 anchoredPos = new Vector2(x, y);
                BattalionType bType = typesToSpawn[index];

                UI_BattalionToken token = GetFromPool();
                string sidePrefix = isPlayerControlled ? "Player" : "Enemy";
                token.name = $"{sidePrefix}_Army{army.Id}_{bType}_{index + 1}_C{col}R{row}";
                token.InitializeToken(
                    bType,
                    army.Id,
                    army.OwnerFactionId,
                    unitScale,
                    isPlayerControlled,
                    anchoredPos);

                // Faza 8.1: Strateji Resurs Asılılıqları: Dəmir (Iron) və At (Horses) yoxlanışı
                if (DatabaseManager.Instance != null && DatabaseManager.Instance.IsConnected)
                {
                    Nation ownerNation = DatabaseManager.Instance.Connection.Table<Nation>().FirstOrDefault(n => n.Id == army.OwnerFactionId);
                    if (ownerNation != null)
                    {
                        token.HasIronEquipment = ownerNation.Iron > 0f;
                        token.HasHorsesMounted = ownerNation.Horses > 0f;
                    }
                }

                targetList.Add(token);
            }
        }

        private static void EnsureTwoBattleArmiesExistInDatabase(DatabaseManager db, ref int firstArmyId, ref int secondArmyId)
        {
            List<Army> allArmies = db.Connection.Table<Army>().ToList();
            if (allArmies.Count == 0)
            {
                if (db.Connection.Table<Province>().Count() == 0)
                {
                    db.SeedAndTestDummyData(forceReseed: true);
                }

                MilitaryAndDiplomacyController milController = MilitaryAndDiplomacyController.Instance != null
                    ? MilitaryAndDiplomacyController.Instance
                    : FindAnyObjectByType<MilitaryAndDiplomacyController>();

                List<Province> provinces = db.Connection.Table<Province>().ToList();
                if (milController != null && provinces.Count >= 2)
                {
                    milController.DraftArmy(db, provinces[0].Id);
                    milController.DraftArmy(db, provinces[1].Id);
                }
                else
                {
                    db.RunInTransaction(conn =>
                    {
                        conn.Insert(new Army
                        {
                            OwnerFactionId = 1,
                            LocationProvinceId = 1,
                            YouthSoldiers = 1250,
                            AdultSoldiers = 2040,
                            Morale = 1.0f
                        });
                        conn.Insert(new Army
                        {
                            OwnerFactionId = 2,
                            LocationProvinceId = 2,
                            YouthSoldiers = 600,
                            AdultSoldiers = 960,
                            Morale = 1.0f
                        });
                    });
                }

                allArmies = db.Connection.Table<Army>().ToList();
            }
            else if (allArmies.Count == 1)
            {
                List<Province> provinces = db.Connection.Table<Province>().ToList();
                int secondProvinceId = provinces.Count > 1 ? provinces[1].Id : 2;
                MilitaryAndDiplomacyController milController = MilitaryAndDiplomacyController.Instance != null
                    ? MilitaryAndDiplomacyController.Instance
                    : FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (milController != null)
                {
                    milController.DraftArmy(db, secondProvinceId);
                }
                else
                {
                    db.Connection.Insert(new Army
                    {
                        OwnerFactionId = 2,
                        LocationProvinceId = secondProvinceId,
                        YouthSoldiers = 600,
                        AdultSoldiers = 960,
                        Morale = 1.0f
                    });
                }

                allArmies = db.Connection.Table<Army>().ToList();
            }

            if (allArmies.Count >= 2)
            {
                int currentFirst = firstArmyId;
                int currentSecond = secondArmyId;
                bool hasFirst = allArmies.Exists(a => a.Id == currentFirst);
                bool hasSecond = allArmies.Exists(a => a.Id == currentSecond && a.Id != currentFirst);

                if (!hasFirst)
                {
                    firstArmyId = allArmies[0].Id;
                    currentFirst = firstArmyId;
                }

                if (!hasSecond)
                {
                    Army other = allArmies.Find(a => a.Id != currentFirst);
                    if (other != null)
                    {
                        secondArmyId = other.Id;
                    }
                }
            }
        }

        /// <summary>
        /// Hər iki ordunun tokenlərinə 2D lövhənin mərkəzinə (X = 0) doğru irəliləmək əmri verir
        /// ki, bir-birilərinə yaxınlaşdıqda (AggroRange daxilində) avtomatik toqquşub döyüşsünlər.
        /// Qala Mühasirəsi rejimində qapı sındırılana qədər müdafiəçilər qapının arxasında gözləyirlər.
        /// </summary>
        public void OrderBothArmiesToAdvanceTowardCenter()
        {
            for (int i = 0; i < _attackerBattalions.Count; i++)
            {
                UI_BattalionToken token = _attackerBattalions[i];
                if (token != null && !token.IsDead && token.gameObject.activeInHierarchy)
                {
                    Vector2 pos = token.AnchoredPosition;
                    token.MoveTo(new Vector2(-28f, pos.y));
                }
            }

            for (int i = 0; i < _defenderBattalions.Count; i++)
            {
                UI_BattalionToken token = _defenderBattalions[i];
                if (token != null && !token.IsDead && token.gameObject.activeInHierarchy && !(token is UI_GateToken))
                {
                    Vector2 pos = token.AnchoredPosition;
                    float targetX = (currentEnvironment == BattleEnvironment.Siege && IsGateIntact)
                        ? Mathf.Max(60f, pos.x)
                        : 28f;
                    token.MoveTo(new Vector2(targetX, pos.y));
                }
            }
        }

        /// <summary>
        /// İki ordunun 2D Canvas üzərində bir-birinə yaxınlaşmasını, Chasing/Attacking keçidlərini,
        /// zərbə/zərər (TakeDamage), ölüm (ReturnToPool) və qələbə şərtini ("Döyüş Bitdi! Qalib: Ordu X")
        /// addım-addım simulyasiya edir.
        /// </summary>
        public int SimulateBattleUntilResolution(float timeStep = 0.25f, int maxSteps = 500)
        {
            OrderBothArmiesToAdvanceTowardCenter();

            var snapshot = new List<UI_BattalionToken>();
            int stepsExecuted = 0;

            for (int step = 0; step < maxSteps && !_isBattleEnded; step++)
            {
                stepsExecuted++;
                snapshot.Clear();
                snapshot.AddRange(_activeBattalions);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    UI_BattalionToken token = snapshot[i];
                    if (token != null && !token.IsDead && token.gameObject.activeInHierarchy)
                    {
                        token.TickCombat(timeStep);
                    }
                }

                if (_isBattleEnded)
                {
                    break;
                }
            }

            return stepsExecuted;
        }

        private static void ClearTransformChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }
    }

    /// <summary>
    /// Faza 3.2: 2D Taktiki Döyüş Lövhəsində relyef və dinamik mühit tipləri:
    /// - Plains (Düzənlik): Standart açıq meydan döyüşü.
    /// - Desert (Səhra): Qum fırtınası mexanikası (mənzil və hərəkət sürəti 50% azalır).
    /// - Siege (Qala Mühasirəsi): Mərkəzdə 2000 HP Qala Qapısı hücum edənləri bloklayır.
    /// </summary>
    public enum BattleEnvironment
    {
        Plains = 0,
        Desert = 1,
        Siege = 2
    }
}
