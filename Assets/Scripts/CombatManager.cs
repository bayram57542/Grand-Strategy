using System;
using System.Collections.Generic;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Military;
using GrandStrategy.Models;
using GrandStrategy.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 10.12 - Hibrid Müharibə və 2D Taktiki Döyüş Sistemi İdarəedicisi (CombatManager).
    /// Eyni əyalətdə düşmən orduları qarşılaşdıqda avtomatik xəritə döyüşünü (MapBattleUI) başladır,
    /// hökmdar döyüşdədirsə 2D Taktiki Səhnə keçid pəncərəsini (RoyalBattlePrompt) açır və
    /// gündəlik Tick-lərdə itkiləri hesablayır.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public const string Tactical2DSceneName = "2DBattleScene";
        public const string SceneTransferLogMessage = "2D Savaş Mühitinə Keçid Edilir... Ordular transfer olundu";

        private static CombatManager _instance;
        public static CombatManager Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<CombatManager>(FindObjectsInactive.Include));

        [Serializable]
        public class ActiveBattleData
        {
            public int ProvinceId;
            public string ProvinceName;
            public MapInteractionController Province;
            public ArmyVisualController AttackerVisual;
            public ArmyVisualController DefenderVisual;
            public CommanderArmy AttackerArmy;
            public CommanderArmy DefenderArmy;
            public int InitialAttackerTroops;
            public int InitialDefenderTroops;
            public float BattleProgress = 0.5f; // 0.5 = 50/50 mərkəz
            public int DaysElapsed;
            public bool IsAutomated;
            public bool AwaitingRulerDecision;
            public bool IsResolved;
            public string WinnerFaction;
            public string WinnerCommander;
            public MapBattleUIController BattleUI;
        }

        [Header("Aktiv Döyüşlər")]
        [SerializeField] private List<ActiveBattleData> activeBattles = new List<ActiveBattleData>();

        [Header("2D Səhnə Transfer Məlumatları")]
        public CommanderArmy TransferredAttackerArmy { get; private set; }
        public CommanderArmy TransferredDefenderArmy { get; private set; }
        public int TransferredProvinceId { get; private set; }
        public string TransferredProvinceName { get; private set; }
        public string LastSceneTransferMessage { get; private set; }
        public string LastTransferredTargetScene { get; private set; }
        public bool DidRequest2DSceneLoad { get; private set; }

        public List<ActiveBattleData> ActiveBattles => activeBattles;
        public int ActiveBattleCount => activeBattles.FindAll(b => b != null && !b.IsResolved).Count;

        public event Action<ActiveBattleData> OnBattleStarted;
        public event Action<ActiveBattleData> OnBattleUpdated;
        public event Action<ActiveBattleData> OnBattleEnded;
        public event Action<ActiveBattleData, string> On2DBattleSceneTransferInitiated;

        private bool _boundToTimeEvents;

        public static CombatManager EnsureInstance()
        {
            if (_instance != null)
            {
                _instance.BindTickEvents();
                return _instance;
            }

            var found = FindAnyObjectByType<CombatManager>(FindObjectsInactive.Include);
            if (found != null)
            {
                _instance = found;
                _instance.BindTickEvents();
                return _instance;
            }

            GameObject host = GameObject.Find("MapManager");
            if (host == null)
            {
                host = new GameObject("CombatManager");
            }

            _instance = host.GetComponent<CombatManager>();
            if (_instance == null)
            {
                _instance = host.AddComponent<CombatManager>();
            }

            _instance.BindTickEvents();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                return;
            }
            _instance = this;
            BindTickEvents();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            BindTickEvents();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            BindTickEvents();
            ProcessCrossSceneBattleReturn();
        }

        private void OnDisable()
        {
            UnbindTickEvents();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.IsValid() && string.Equals(scene.name, "GlobalMapScene", StringComparison.OrdinalIgnoreCase))
            {
                BindTickEvents();
                ProcessCrossSceneBattleReturn();
            }
        }

        public void BindTickEvents()
        {
            if (_boundToTimeEvents) return;

            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.OnDailyTick -= HandleDailyTick;
                GameTimeManager.Instance.OnDailyTick += HandleDailyTick;
                _boundToTimeEvents = true;
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayPassed -= HandleDailyTick;
                GameManager.Instance.OnDayPassed += HandleDailyTick;
                _boundToTimeEvents = true;
            }
        }

        private void UnbindTickEvents()
        {
            if (GameTimeManager.Instance != null)
            {
                GameTimeManager.Instance.OnDailyTick -= HandleDailyTick;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnDayPassed -= HandleDailyTick;
            }
            _boundToTimeEvents = false;
        }

        private void HandleDailyTick(int day, int month, int year)
        {
            ProcessBattlesTick();
        }

        /// <summary>
        /// Eyni əyalətdə fərqli dövlətlərə (və RelationStatus == "Düşmənçilik" olan) aid orduların
        /// olub-olmadığını yoxlayır. Varsa dərhal döyüşü (StartBattle) başladır.
        /// </summary>
        public ActiveBattleData CheckForBattle(int provinceId)
        {
            return CheckForBattle(provinceId, null);
        }

        /// <summary>
        /// Əyalətə yeni ordu çatdıqda qarşıdurma olub-olmadığını yoxlayır və varsa StartBattle çağırır.
        /// </summary>
        public ActiveBattleData CheckForBattle(int provinceId, ArmyVisualController arrivingArmy)
        {
            if (provinceId <= 0) return null;

            // Bu əyalətdə artıq aktiv döyüş gedirsə, onu qaytarırıq
            ActiveBattleData existing = activeBattles.Find(b => b != null && !b.IsResolved && b.ProvinceId == provinceId);
            if (existing != null)
            {
                return existing;
            }

            MapInteractionController province = FindProvinceById(provinceId);
            if (province == null) return null;

            ArmyManager armyMgr = ArmyManager.Instance;
            if (armyMgr == null) return null;

            var armiesInProvince = armyMgr.GetArmiesInProvince(provinceId);
            if (arrivingArmy != null && arrivingArmy.ArmyData != null && arrivingArmy.ArmyData.TroopCount > 0 && !armiesInProvince.Contains(arrivingArmy))
            {
                armiesInProvince.Add(arrivingArmy);
            }

            var dipMgr = DiplomacyManager.EnsureInstance();

            // 1. Eyni əyalətdəki ordular arasında 'Düşmənçilik' (Müharibə) statusu olan cütlüyü tapırıq
            for (int i = 0; i < armiesInProvince.Count; i++)
            {
                var a = armiesInProvince[i];
                if (a == null || a.ArmyData == null || a.ArmyData.TroopCount <= 0) continue;

                for (int j = i + 1; j < armiesInProvince.Count; j++)
                {
                    var b = armiesInProvince[j];
                    if (b == null || b.ArmyData == null || b.ArmyData.TroopCount <= 0) continue;

                    if (a.ArmyData.FactionId == b.ArmyData.FactionId) continue;

                    if (AreFactionsHostile(dipMgr, a.ArmyData.FactionId, b.ArmyData.FactionId))
                    {
                        ArmyVisualController attacker = arrivingArmy == b ? b : a;
                        ArmyVisualController defender = attacker == a ? b : a;
                        return StartBattle(attacker, defender, province);
                    }
                }
            }

            // 2. Əgər gələn ordu ilə əyalət sahibi müharibədədir ('Düşmənçilik'), lakin əyalətdə hazırda
            // yerli ordu yoxdursa -> Faza 10.5 ProvinceDemographics AdultMen-in 15%-dən dinamik Milis Qarnizonu yaradırıq!
            if (arrivingArmy != null && arrivingArmy.ArmyData != null &&
                province.FactionId > 0 && arrivingArmy.ArmyData.FactionId != province.FactionId &&
                AreFactionsHostile(dipMgr, arrivingArmy.ArmyData.FactionId, province.FactionId))
            {
                var militia = armyMgr.SpawnDemographicMilitiaGarrison(province);
                if (militia != null)
                {
                    UIManager.Instance?.AddEventLogMessage(
                        $"• <color=#E67E22>Milis Səfərbərliyi ({province.ProvinceName}):</color> Yetişkin kişilərin 15%-i ({militia.ArmyData.FormattedTroopCount}) müdafiəyə qalxdı!");
                    return StartBattle(arrivingArmy, militia, province);
                }
            }

            return null;
        }

        private static bool AreFactionsHostile(DiplomacyManager dipMgr, int factionA, int factionB)
        {
            if (factionA <= 0 || factionB <= 0 || factionA == factionB) return false;
            if (dipMgr == null) return true;

            FactionRelation rel = dipMgr.GetRelationRecord(factionA, factionB);
            if (rel == null) return false;

            return string.Equals(rel.Status, "Düşmənçilik", StringComparison.OrdinalIgnoreCase) || rel.RelationScore <= -50;
        }

        /// <summary>
        /// İki CommanderArmy modeli arasında döyüşü başladır (Data-model uyğunluğu üçün overload).
        /// </summary>
        public ActiveBattleData StartBattle(CommanderArmy attackerData, CommanderArmy defenderData, MapInteractionController loc)
        {
            if (attackerData == null || defenderData == null) return null;

            ArmyVisualController attackerVis = FindVisualForArmy(attackerData);
            ArmyVisualController defenderVis = FindVisualForArmy(defenderData);

            if (attackerVis != null && defenderVis != null)
            {
                return StartBattle(attackerVis, defenderVis, loc);
            }

            return CreateAndRegisterBattle(attackerVis, defenderVis, attackerData, defenderData, loc);
        }

        /// <summary>
        /// İki ordu arasında dinamik xəritə döyüşünü (MapBattleUI) və lazım gələrsə Hökmdar 2D Səhnə Seçimini (RoyalBattlePrompt) başladır.
        /// </summary>
        public ActiveBattleData StartBattle(ArmyVisualController attacker, ArmyVisualController defender, MapInteractionController loc)
        {
            if (attacker == null || defender == null || attacker.ArmyData == null || defender.ArmyData == null)
            {
                return null;
            }

            return CreateAndRegisterBattle(attacker, defender, attacker.ArmyData, defender.ArmyData, loc);
        }

        private ActiveBattleData CreateAndRegisterBattle(
            ArmyVisualController attackerVisual,
            ArmyVisualController defenderVisual,
            CommanderArmy attackerData,
            CommanderArmy defenderData,
            MapInteractionController loc)
        {
            int provId = loc != null ? loc.ProvinceId : attackerData.CurrentProvinceId;
            string provName = loc != null ? loc.ProvinceName : attackerData.CurrentProvinceName;

            // Köhnə SiegePanel-i gizlədirik (prioritet ordu-ordu döyüşündədir)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideSiegeLogisticsPanel();
            }

            // Hər iki ordunun hərəkətini dayandırırıq
            attackerVisual?.MovementController?.StopMovement();
            defenderVisual?.MovementController?.StopMovement();

            var battle = new ActiveBattleData
            {
                ProvinceId = provId,
                ProvinceName = provName,
                Province = loc,
                AttackerVisual = attackerVisual,
                DefenderVisual = defenderVisual,
                AttackerArmy = attackerData,
                DefenderArmy = defenderData,
                InitialAttackerTroops = attackerData.TroopCount,
                InitialDefenderTroops = defenderData.TroopCount,
                BattleProgress = 0.5f, // 50/50 mərkəzdən başlayır
                DaysElapsed = 0,
                IsAutomated = false,
                AwaitingRulerDecision = false,
                IsResolved = false
            };

            // Xəritə üzərində MapBattleUI prefabını əyalətin düz üstünə qoyuruq
            var ui = MapBattleUIController.CreateForProvince(loc, transform);
            if (ui != null)
            {
                Color leftColor = attackerData.FactionColor;
                Color rightColor = defenderData.FactionColor;
                ui.Initialize(leftColor, rightColor, attackerData.TroopCount, defenderData.TroopCount, provName);
                battle.BattleUI = ui;
            }

            activeBattles.Add(battle);

            Debug.Log($"[CombatManager] ⚔️ DÖYÜŞ BAŞLADI ({provName}): {attackerData.CommanderName} ({attackerData.FormattedTroopCount}) vs {defenderData.CommanderName} ({defenderData.FormattedTroopCount})");
            UIManager.Instance?.AddEventLogMessage(
                $"• <color=#E74C3C><b>DÖYÜŞ ({provName}):</b></color> {attackerData.CommanderName} ({attackerData.FormattedTroopCount}) ⚔️ {defenderData.CommanderName} ({defenderData.FormattedTroopCount})");

            // Əgər oyunçunun ordusunun komandiri Hökmdardırsa (IsFactionRuler == true) -> RoyalBattlePrompt açılır!
            if (IsPlayerRulerParticipating(battle))
            {
                battle.AwaitingRulerDecision = true;
                if (GameTimeManager.Instance != null && !GameTimeManager.Instance.IsPaused)
                {
                    GameTimeManager.Instance.Pause();
                }

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowRoyalBattlePrompt(battle);
                }
            }
            else
            {
                battle.IsAutomated = true;
            }

            OnBattleStarted?.Invoke(battle);
            return battle;
        }

        private static bool IsPlayerRulerParticipating(ActiveBattleData battle)
        {
            if (battle == null) return false;

            int playerFactionId = DiplomacyManager.Instance != null ? DiplomacyManager.Instance.PlayerFactionId : 1;

            bool attackerIsPlayerRuler = battle.AttackerArmy != null &&
                                         battle.AttackerArmy.FactionId == playerFactionId &&
                                         battle.AttackerArmy.IsFactionRuler;

            bool defenderIsPlayerRuler = battle.DefenderArmy != null &&
                                         battle.DefenderArmy.FactionId == playerFactionId &&
                                         battle.DefenderArmy.IsFactionRuler;

            return attackerIsPlayerRuler || defenderIsPlayerRuler;
        }

        /// <summary>
        /// Oyunçunun seçimi 1: "Döyüşü Avtomatlaşdır" — Hökmdar iştirak etsə belə döyüş arxa planda MapBattleUI ilə davam edir.
        /// </summary>
        public void AutomateBattle(ActiveBattleData battle)
        {
            if (battle == null) return;

            battle.AwaitingRulerDecision = false;
            battle.IsAutomated = true;

            UIManager.Instance?.HideRoyalBattlePrompt();

            if (GameTimeManager.Instance != null && GameTimeManager.Instance.IsPaused)
            {
                GameTimeManager.Instance.Unpause();
            }

            Debug.Log($"[CombatManager] ⚙️ Hökmdar qərarı: '{battle.ProvinceName}' döyüşü xəritə üzərində avtomatlaşdırıldı.");
            UIManager.Instance?.AddEventLogMessage($"• <color=#3498DB>Avto-Döyüş:</color> {battle.ProvinceName} döyüşü xəritədə davam edir.");
        }

        /// <summary>
        /// Oyunçunun seçimi 2: "Döyüşə Şəxsən Rəhbərlik Et" — CrossSceneBattleData-nı doldurur, orduları transfer edir və 2DBattleScene mühitinə keçid edir.
        /// </summary>
        public void LeadBattlePersonally(ActiveBattleData battle)
        {
            if (battle == null) return;

            TransferredAttackerArmy = battle.AttackerArmy;
            TransferredDefenderArmy = battle.DefenderArmy;
            TransferredProvinceId = battle.ProvinceId;
            TransferredProvinceName = battle.ProvinceName;

            int playerFactionId = DiplomacyManager.Instance != null ? DiplomacyManager.Instance.PlayerFactionId : 1;
            int attackerId = battle.AttackerArmy != null ? battle.AttackerArmy.Id : 0;
            int defenderId = battle.DefenderArmy != null ? battle.DefenderArmy.Id : 0;
            int enemyArmyId = (battle.AttackerArmy != null && battle.AttackerArmy.FactionId == playerFactionId) ? defenderId : attackerId;
            CrossSceneBattleData.SetPendingBattle(battle.ProvinceId, attackerId, defenderId, playerFactionId, enemyArmyId);

            if (GameManager.Instance != null && battle.AttackerArmy != null && battle.DefenderArmy != null)
            {
                GameManager.Instance.CurrentAttackerArmyId = $"Army_{attackerId}";
                GameManager.Instance.CurrentDefenderArmyId = $"Army_{defenderId}";
            }

            UIManager.Instance?.HideRoyalBattlePrompt();

            LastSceneTransferMessage = SceneTransferLogMessage;
            LastTransferredTargetScene = Tactical2DSceneName;
            DidRequest2DSceneLoad = true;

            Debug.Log(SceneTransferLogMessage);
            UIManager.Instance?.AddEventLogMessage($"• <color=#F1C40F><b>👑 {SceneTransferLogMessage}</b></color>");

            On2DBattleSceneTransferInitiated?.Invoke(battle, Tactical2DSceneName);

            if (Application.isPlaying)
            {
                if (Application.CanStreamedLevelBeLoaded(Tactical2DSceneName))
                {
                    SceneManager.LoadScene(Tactical2DSceneName);
                }
                else if (Application.CanStreamedLevelBeLoaded("TacticalBattleScene"))
                {
                    Debug.LogWarning($"[CombatManager] '{Tactical2DSceneName}' Build Settings-də tapılmadı, 'TacticalBattleScene' fallback kimi yüklənir.");
                    SceneManager.LoadScene("TacticalBattleScene");
                }
            }
        }

        /// <summary>
        /// 2D Taktiki Döyüşdən Qlobal xəritəyə qayıtdıqda CrossSceneBattleData nəticəsini yoxlayır və icra edir.
        /// PlayerWon == true isə düşmən ordusunu silir, əyaləti oyunçunun rənginə boyayır (işğal) və MapBattleUI indikatorunu silir.
        /// </summary>
        public bool ProcessCrossSceneBattleReturn()
        {
            if (CrossSceneBattleData.ActiveProvinceId <= 0) return false;
            if (!CrossSceneBattleData.PlayerWon && !CrossSceneBattleData.HasPendingResult) return false;

            int provId = CrossSceneBattleData.ActiveProvinceId;
            int playerFactionId = CrossSceneBattleData.PlayerFactionId > 0
                ? CrossSceneBattleData.PlayerFactionId
                : (DiplomacyManager.Instance != null ? DiplomacyManager.Instance.PlayerFactionId : 1);
            bool playerWon = CrossSceneBattleData.PlayerWon;

            MapInteractionController province = FindProvinceById(provId);
            ActiveBattleData battle = activeBattles.Find(b => b != null && !b.IsResolved && b.ProvinceId == provId);

            ArmyManager armyMgr = ArmyManager.Instance;
            ArmyVisualController attackerVis = battle != null ? battle.AttackerVisual : armyMgr?.GetArmyById(CrossSceneBattleData.AttackerArmyId);
            ArmyVisualController defenderVis = battle != null ? battle.DefenderVisual : armyMgr?.GetArmyById(CrossSceneBattleData.DefenderArmyId);

            bool attackerIsPlayer = (attackerVis?.ArmyData != null && attackerVis.ArmyData.FactionId == playerFactionId) ||
                                    (battle?.AttackerArmy != null && battle.AttackerArmy.FactionId == playerFactionId);

            ArmyVisualController enemyVis = attackerIsPlayer ? defenderVis : attackerVis;
            ArmyVisualController playerVis = attackerIsPlayer ? attackerVis : defenderVis;

            if (playerWon)
            {
                // 1. Düşmən ordusunu xəritədən silirik
                if (enemyVis != null)
                {
                    if (enemyVis.ArmyData != null) enemyVis.ArmyData.TroopCount = 0;
                    if (armyMgr != null) armyMgr.RemoveArmy(enemyVis);
                    else if (Application.isPlaying) Destroy(enemyVis.gameObject);
                    else DestroyImmediate(enemyVis.gameObject);
                }
                else if (CrossSceneBattleData.EnemyArmyId > 0 && armyMgr != null)
                {
                    var fallbackEnemy = armyMgr.GetArmyById(CrossSceneBattleData.EnemyArmyId);
                    if (fallbackEnemy != null)
                    {
                        if (fallbackEnemy.ArmyData != null) fallbackEnemy.ArmyData.TroopCount = 0;
                        armyMgr.RemoveArmy(fallbackEnemy);
                    }
                }

                // 2. Əyaləti oyunçunun sahibliyinə və rənginə keçiririk
                if (province != null)
                {
                    province.OwnerFactionId = playerFactionId;
                }

                // 3. MapBattleUI indikatorunu silirik və ResolveBattle məntiqini tamamlayırıq
                if (battle != null)
                {
                    battle.IsResolved = true;
                    battle.AwaitingRulerDecision = false;
                    battle.BattleProgress = attackerIsPlayer ? 1.0f : 0.0f;
                    battle.WinnerFaction = playerVis?.ArmyData != null ? playerVis.ArmyData.Faction : "Eldəgəzlər (Atabəylər)";
                    battle.WinnerCommander = playerVis?.ArmyData != null ? playerVis.ArmyData.CommanderName : "Qızıl Arslan";

                    if (battle.BattleUI != null)
                    {
                        if (Application.isPlaying) Destroy(battle.BattleUI.gameObject);
                        else DestroyImmediate(battle.BattleUI.gameObject);
                        battle.BattleUI = null;
                    }

                    OnBattleEnded?.Invoke(battle);
                }

                CleanupLooseMapBattleUIs(province);

                string provName = province != null ? province.ProvinceName : $"Əyalət #{provId}";
                Debug.Log($"[CombatManager] 🏳️ 2D DÖYÜŞ ZƏFƏRİ: '{provName}' işğal edildi (OwnerFactionId={playerFactionId}), düşmən ordusu silindi!");
                UIManager.Instance?.AddEventLogMessage($"• <color=#2ECC71><b>👑 QƏLƏBƏ VƏ İŞĞAL ({provName}):</b></color> 2D döyüş qazanıldı! Əyalət dövlətimizə birləşdirildi.");
            }
            else
            {
                // Oyunçu məğlub olubsa oyunçu ordusunu silirik
                if (playerVis != null)
                {
                    if (playerVis.ArmyData != null) playerVis.ArmyData.TroopCount = 0;
                    if (armyMgr != null) armyMgr.RemoveArmy(playerVis);
                    else if (Application.isPlaying) Destroy(playerVis.gameObject);
                    else DestroyImmediate(playerVis.gameObject);
                }

                if (battle != null)
                {
                    battle.IsResolved = true;
                    battle.AwaitingRulerDecision = false;
                    battle.WinnerFaction = enemyVis?.ArmyData != null ? enemyVis.ArmyData.Faction : "Düşmən";
                    battle.WinnerCommander = enemyVis?.ArmyData != null ? enemyVis.ArmyData.CommanderName : "Düşmən Komandiri";

                    if (battle.BattleUI != null)
                    {
                        if (Application.isPlaying) Destroy(battle.BattleUI.gameObject);
                        else DestroyImmediate(battle.BattleUI.gameObject);
                        battle.BattleUI = null;
                    }

                    OnBattleEnded?.Invoke(battle);
                }

                CleanupLooseMapBattleUIs(province);
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideRoyalBattlePrompt();
            }

            if (DiplomacyUIManager.Instance != null)
            {
                DiplomacyUIManager.Instance.RefreshDynamicMilitaryPowerUI();
            }

            if (GameTimeManager.Instance != null && GameTimeManager.Instance.IsPaused)
            {
                GameTimeManager.Instance.RecalculateMonthlyBalances();
            }

            CrossSceneBattleData.Clear();
            return true;
        }

        /// <summary>
        /// 2D səhnə hələ ayrıca Build Settings-də olmadıqda və ya test zamanı 2D döyüşdən qayıdışı dərhal simulyasiya edir.
        /// </summary>
        public bool SimulateReturnFrom2DBattle(bool playerWon = true)
        {
            CrossSceneBattleData.RecordResult(playerWon);
            return ProcessCrossSceneBattleReturn();
        }

        private static void CleanupLooseMapBattleUIs(MapInteractionController province)
        {
            var remainingUis = FindObjectsByType<MapBattleUIController>(FindObjectsInactive.Include);
            for (int i = 0; i < remainingUis.Length; i++)
            {
                var ui = remainingUis[i];
                if (ui == null) continue;
                if (province == null || ui.gameObject.name.Contains(province.ProvinceName))
                {
                    if (Application.isPlaying) Destroy(ui.gameObject);
                    else DestroyImmediate(ui.gameObject);
                }
            }
        }

        /// <summary>
        /// Hər zaman tick-ində (OnDailyTick) aktiv döyüşləri hesablayır:
        /// ordu sayı, komandir bacarığı və moral əsasında itkiləri çıxır, tərəzini (Slider) yeniləyir.
        /// </summary>
        public void ProcessBattlesTick()
        {
            for (int i = activeBattles.Count - 1; i >= 0; i--)
            {
                ActiveBattleData battle = activeBattles[i];
                if (battle == null || battle.IsResolved) continue;
                if (battle.AwaitingRulerDecision) continue;

                SimulateSingleBattleDay(battle);
            }
        }

        public void SimulateSingleBattleDay(ActiveBattleData battle)
        {
            if (battle == null || battle.IsResolved || battle.AttackerArmy == null || battle.DefenderArmy == null)
            {
                return;
            }

            battle.DaysElapsed++;

            int aTroops = Mathf.Max(0, battle.AttackerArmy.TroopCount);
            int dTroops = Mathf.Max(0, battle.DefenderArmy.TroopCount);

            float aSkill = Mathf.Max(0.5f, battle.AttackerArmy.CommanderSkill);
            float dSkill = Mathf.Max(0.5f, battle.DefenderArmy.CommanderSkill);

            float aMorale = Mathf.Clamp(battle.AttackerArmy.Morale, 0.35f, 1.5f);
            float dMorale = Mathf.Clamp(battle.DefenderArmy.Morale, 0.35f, 1.5f);

            float aPower = Mathf.Max(1f, aTroops) * aSkill * aMorale;
            float dPower = Mathf.Max(1f, dTroops) * dSkill * dMorale;

            float powerRatio = Mathf.Clamp(aPower / dPower, 0.35f, 2.8f);

            // Günlük itkilər: qarşı tərəfin gücü, komandir bacarığı və morala əsaslanır
            int attackerCasualties = Mathf.Max(150, Mathf.RoundToInt((dTroops * 0.020f + 200f) * dSkill / Mathf.Max(0.75f, aSkill) / Mathf.Sqrt(powerRatio)));
            int defenderCasualties = Mathf.Max(150, Mathf.RoundToInt((aTroops * 0.020f + 200f) * aSkill / Mathf.Max(0.75f, dSkill) * Mathf.Sqrt(powerRatio)));

            int newAttackerTroops = Mathf.Max(0, aTroops - attackerCasualties);
            int newDefenderTroops = Mathf.Max(0, dTroops - defenderCasualties);

            battle.AttackerArmy.TroopCount = newAttackerTroops;
            battle.DefenderArmy.TroopCount = newDefenderTroops;

            // Moral dinamikası (itki verən tərəfin moralları azca düşür)
            battle.AttackerArmy.Morale = Mathf.Clamp(battle.AttackerArmy.Morale - 0.008f / Mathf.Sqrt(powerRatio), 0.25f, 1.5f);
            battle.DefenderArmy.Morale = Mathf.Clamp(battle.DefenderArmy.Morale - 0.008f * Mathf.Sqrt(powerRatio), 0.25f, 1.5f);

            if (battle.AttackerVisual != null) battle.AttackerVisual.SetTroopCount(newAttackerTroops);
            if (battle.DefenderVisual != null) battle.DefenderVisual.SetTroopCount(newDefenderTroops);

            // Tərəzi (0.5 mərkəz, >0.5 hücumçu üstünlüyü / Yaşıl, <0.5 müdafiəçi üstünlüyü / Qırmızı)
            if (newDefenderTroops <= 0 && newAttackerTroops > 0)
            {
                battle.BattleProgress = 1.0f;
            }
            else if (newAttackerTroops <= 0 && newDefenderTroops > 0)
            {
                battle.BattleProgress = 0.0f;
            }
            else
            {
                float remAPower = newAttackerTroops * aSkill * Mathf.Clamp(battle.AttackerArmy.Morale, 0.35f, 1.5f);
                float remDPower = newDefenderTroops * dSkill * Mathf.Clamp(battle.DefenderArmy.Morale, 0.35f, 1.5f);
                float liveShare = remAPower / Mathf.Max(1f, remAPower + remDPower);
                float targetBar = Mathf.Clamp01(0.5f + (liveShare - 0.5f) * 1.45f);
                battle.BattleProgress = Mathf.Lerp(battle.BattleProgress, targetBar, 0.40f);
            }

            battle.BattleUI?.UpdateBattleProgress(battle.BattleProgress, newAttackerTroops, newDefenderTroops);
            OnBattleUpdated?.Invoke(battle);

            if (newAttackerTroops <= 0 || newDefenderTroops <= 0)
            {
                ResolveBattle(battle);
            }
        }

        private void ResolveBattle(ActiveBattleData battle)
        {
            if (battle == null || battle.IsResolved) return;
            battle.IsResolved = true;

            bool attackerWon = battle.AttackerArmy.TroopCount > battle.DefenderArmy.TroopCount;
            CommanderArmy winner = attackerWon ? battle.AttackerArmy : battle.DefenderArmy;
            CommanderArmy loser = attackerWon ? battle.DefenderArmy : battle.AttackerArmy;
            ArmyVisualController loserVisual = attackerWon ? battle.DefenderVisual : battle.AttackerVisual;

            battle.WinnerFaction = winner != null ? winner.Faction : "Naməlum";
            battle.WinnerCommander = winner != null ? winner.CommanderName : "Komandir";

            Debug.Log($"[CombatManager] 🏆 DÖYÜŞ BİTDİ ({battle.ProvinceName})! Qalib: {battle.WinnerCommander} ({battle.WinnerFaction}) | Məğlub: {loser?.CommanderName}");
            UIManager.Instance?.AddEventLogMessage(
                $"• <color=#2ECC71><b>QƏLƏBƏ ({battle.ProvinceName}):</b></color> {battle.WinnerCommander} ({battle.WinnerFaction}) düşmən ordusunu darmadağın etdi!");

            if (loserVisual != null && ArmyManager.Instance != null)
            {
                ArmyManager.Instance.RemoveArmy(loserVisual);
            }

            // Hücum edən ordu qalib gəlibsə əyaləti ələ keçirir
            if (attackerWon && battle.Province != null && battle.AttackerArmy != null)
            {
                battle.Province.OwnerFactionId = battle.AttackerArmy.FactionId;
            }

            if (battle.BattleUI != null)
            {
                if (Application.isPlaying) Destroy(battle.BattleUI.gameObject);
                else DestroyImmediate(battle.BattleUI.gameObject);
                battle.BattleUI = null;
            }

            if (UIManager.Instance != null && UIManager.Instance.IsRoyalBattlePromptOpen)
            {
                UIManager.Instance.HideRoyalBattlePrompt();
            }

            OnBattleEnded?.Invoke(battle);
        }

        /// <summary>
        /// Səhnədə və testlərdə bütün aktiv döyüş indikatorlarını və siyahını təmizləyir.
        /// </summary>
        public void ClearAllBattles()
        {
            for (int i = activeBattles.Count - 1; i >= 0; i--)
            {
                var b = activeBattles[i];
                if (b != null && b.BattleUI != null)
                {
                    if (Application.isPlaying) Destroy(b.BattleUI.gameObject);
                    else DestroyImmediate(b.BattleUI.gameObject);
                }
            }
            activeBattles.Clear();
            CrossSceneBattleData.Clear();
            DidRequest2DSceneLoad = false;
            LastSceneTransferMessage = string.Empty;
            LastTransferredTargetScene = string.Empty;
        }

        private static MapInteractionController FindProvinceById(int provinceId)
        {
            if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
            {
                var spawned = GISMapLoader.Instance.SpawnedProvinces;
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null && spawned[i].ProvinceId == provinceId)
                    {
                        return spawned[i];
                    }
                }
            }

            var all = FindObjectsByType<MapInteractionController>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ProvinceId == provinceId)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static ArmyVisualController FindVisualForArmy(CommanderArmy data)
        {
            if (data == null || ArmyManager.Instance == null) return null;
            return ArmyManager.Instance.ActiveArmies.Find(a => a != null && a.ArmyData == data);
        }
    }
}
