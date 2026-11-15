using System;
using System.Collections.Generic;
using UnityEngine;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Military;
using GrandStrategy.UI;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Dövlətlər arası diplomatik münasibətləri, AI təkliflərinin qiymətləndirilməsini,
    /// müqavilələrin bağlanmasını və müharibə elanlarını SQLite üzərindən idarə edən əsas backend meneceri.
    /// </summary>
    [ExecuteAlways]
    public class DiplomacyManager : MonoBehaviour
    {
        public static DiplomacyManager Instance { get; private set; }

        [Header("Oyunçu Dövləti")]
        [SerializeField] private int playerFactionId = 1;

        public int PlayerFactionId => playerFactionId;

        /// <summary>
        /// Diplomatiya dəyişiklikləri olduqda UI-ın dərhal yenilənməsi üçün hadisə.
        /// </summary>
        public event Action OnDiplomacyDataChanged;

        private DatabaseManager _db;
        private bool _initialized;

        public static DiplomacyManager EnsureInstance()
        {
            if (Instance != null)
            {
                Instance.EnsureInitialized();
                return Instance;
            }

            var found = FindAnyObjectByType<DiplomacyManager>(FindObjectsInactive.Include);
            if (found != null)
            {
                Instance = found;
                Instance.EnsureInitialized();
                return Instance;
            }

            GameObject host = GameObject.Find("MapManager");
            if (host == null)
            {
                host = new GameObject("DiplomacyManager");
            }

            Instance = host.GetComponent<DiplomacyManager>();
            if (Instance == null)
            {
                Instance = host.AddComponent<DiplomacyManager>();
            }

            Instance.EnsureInitialized();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }

            Instance = this;
            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (_db == null)
            {
                _db = DatabaseManager.Instance != null
                    ? DatabaseManager.Instance
                    : FindAnyObjectByType<DatabaseManager>(FindObjectsInactive.Include);

                if (_db == null)
                {
                    GameObject host = GameObject.Find("MapManager");
                    if (host == null) host = gameObject;

                    _db = host.GetComponent<DatabaseManager>();
                    if (_db == null)
                    {
                        _db = host.AddComponent<DatabaseManager>();
                    }
                }
            }

            if (!_db.IsConnected)
            {
                _db.InitializeDatabase();
            }

            if (!_initialized && _db.IsConnected)
            {
                _db.EnsureDiplomacyTablesAndSeed(forceReseed: false);
                _initialized = true;
            }
        }

        public List<Faction> GetAllFactions()
        {
            EnsureInitialized();
            return _db.GetAllFactions();
        }

        public Faction GetFaction(int factionId)
        {
            EnsureInitialized();
            return _db.GetFactionById(factionId);
        }

        public Faction GetPlayerFaction()
        {
            return GetFaction(playerFactionId);
        }

        /// <summary>
        /// İki dövlət arasındakı münasibət xalını (-100 .. +100) SQLite-dan oxuyur.
        /// </summary>
        public int GetRelation(int faction1, int faction2)
        {
            if (faction1 == faction2) return 100;
            return GetRelationRecord(faction1, faction2).RelationScore;
        }

        /// <summary>
        /// İki dövlət arasındakı tam münasibət qeydini (RelationScore + Status) qaytarır.
        /// </summary>
        public FactionRelation GetRelationRecord(int faction1, int faction2)
        {
            EnsureInitialized();

            if (faction1 == faction2)
            {
                return new FactionRelation
                {
                    Faction1_ID = faction1,
                    Faction2_ID = faction2,
                    RelationScore = 100,
                    Status = FactionRelation.GetStatusForScore(100)
                };
            }

            var existing = _db.GetFactionRelation(faction1, faction2);
            if (existing != null)
            {
                return existing;
            }

            FactionRelation.NormalizePair(faction1, faction2, out int minId, out int maxId);
            var created = new FactionRelation
            {
                Faction1_ID = minId,
                Faction2_ID = maxId,
                RelationScore = 0,
                Status = FactionRelation.GetStatusForScore(0)
            };
            _db.UpsertFactionRelation(created);
            return created;
        }

        /// <summary>
        /// İki dövlət arasındakı münasibəti dəyişir, -100..+100 arasında sıxır, Status-u yeniləyir və SQLite-a yazır.
        /// </summary>
        public int ChangeRelation(int faction1, int faction2, int amount)
        {
            EnsureInitialized();
            if (faction1 == faction2) return 100;

            FactionRelation record = GetRelationRecord(faction1, faction2);
            int updatedScore = Mathf.Clamp(record.RelationScore + amount, -100, 100);
            record.RelationScore = updatedScore;
            record.Status = FactionRelation.GetStatusForScore(updatedScore);

            _db.UpsertFactionRelation(record);
            OnDiplomacyDataChanged?.Invoke();
            return updatedScore;
        }

        public List<DiplomaticAgreement> GetAgreementsBetween(int faction1, int faction2)
        {
            EnsureInitialized();
            return _db.GetAgreementsBetween(faction1, faction2);
        }

        public List<DiplomaticAgreement> GetAgreementsForFaction(int factionId)
        {
            EnsureInitialized();
            return _db.GetAgreementsForFaction(factionId);
        }

        /// <summary>
        /// Oyunçu (və ya göstərilən dövlət) ilə hazırda 'Düşmənçilik' statusunda (aktiv müharibədə) olan dövlətləri qaytarır.
        /// </summary>
        public List<Faction> GetActiveWarFactions(int sourceFactionId = -1)
        {
            EnsureInitialized();
            int factionId = sourceFactionId <= 0 ? playerFactionId : sourceFactionId;

            var warFactions = new List<Faction>();
            var allFactions = _db.GetAllFactions();
            for (int i = 0; i < allFactions.Count; i++)
            {
                Faction f = allFactions[i];
                if (f.ID == factionId) continue;

                FactionRelation rel = GetRelationRecord(factionId, f.ID);
                if (rel != null && (string.Equals(rel.Status, "Düşmənçilik", StringComparison.OrdinalIgnoreCase) || rel.RelationScore <= -50))
                {
                    warFactions.Add(f);
                }
            }

            return warFactions;
        }

        /// <summary>
        /// İki dövlət arasında göstərilən növdə aktiv müqavilə olub-olmadığını yoxlayır.
        /// </summary>
        public bool HasAgreementOfType(int faction1, int faction2, AgreementType type)
        {
            EnsureInitialized();
            string targetType = type.ToDbType();
            var agreements = _db.GetAgreementsBetween(faction1, faction2);
            for (int i = 0; i < agreements.Count; i++)
            {
                if (string.Equals(agreements[i].Type, targetType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// AI dövlətinin siyasi maraqları (PrimaryMotive) və münasibət xalı əsasında təklifi qiymətləndirir.
        /// RelationScore &gt; 60 və maraqlar uyğundursa təklifi qəbul edib SQLite-a müqavilə əlavə edir.
        /// </summary>
        public bool EvaluateProposal(int sourceId, int targetId, AgreementType type)
        {
            return EvaluateProposal(sourceId, targetId, type, out _);
        }

        /// <summary>
        /// AI təklifini qiymətləndirir və oyunçuya göstəriləcək ətraflı nəticə mesajını out parametri ilə qaytarır.
        /// </summary>
        public bool EvaluateProposal(int sourceId, int targetId, AgreementType type, out string feedbackMessage)
        {
            EnsureInitialized();

            if (sourceId == targetId)
            {
                feedbackMessage = "Dövlət özü ilə müqavilə imzalaya bilməz.";
                return false;
            }

            Faction source = _db.GetFactionById(sourceId);
            Faction target = _db.GetFactionById(targetId);
            if (source == null || target == null)
            {
                feedbackMessage = "Hədəf və ya mənbə dövlət verilənlər bazasında tapılmadı.";
                return false;
            }

            int score = GetRelation(sourceId, targetId);
            string typeLabel = type.ToDbType();

            if (score <= 60)
            {
                feedbackMessage = $"<color=#E74C3C>✕ Rədd Edildi:</color> <b>{target.Name}</b> '{typeLabel}' təklifini qəbul etmədi. Münasibət xalı (<b>{score:+0;-0;0}</b>) minimum +61 olmalıdır.";
                LogEvent($"• <color=#E67E22>Diplomatiya Rəddi:</color> {target.Name} ({typeLabel}) — münasibət kifayət deyil ({score}).");
                return false;
            }

            FactionMotive targetMotive = target.MotiveEnum;
            if (!IsMotiveCompatible(target, source, targetMotive, type, score, out string motiveReason))
            {
                feedbackMessage = $"<color=#E74C3C>✕ Rədd Edildi:</color> {motiveReason}";
                LogEvent($"• <color=#E67E22>Diplomatiya Rəddi:</color> {motiveReason}");
                return false;
            }

            var existingBetween = _db.GetAgreementsBetween(sourceId, targetId);
            DiplomaticAgreement existingSame = null;
            for (int i = 0; i < existingBetween.Count; i++)
            {
                if (string.Equals(existingBetween[i].Type, typeLabel, StringComparison.OrdinalIgnoreCase))
                {
                    existingSame = existingBetween[i];
                    break;
                }
            }

            if (existingSame != null)
            {
                existingSame.DurationMonths += 12;
                _db.UpdateDiplomaticAgreement(existingSame);
            }
            else
            {
                _db.InsertDiplomaticAgreement(new DiplomaticAgreement
                {
                    Type = typeLabel,
                    SourceFaction_ID = sourceId,
                    TargetFaction_ID = targetId,
                    DurationMonths = GetDefaultDurationMonths(type)
                });
            }

            int updatedScore = ChangeRelation(sourceId, targetId, 10);
            feedbackMessage = $"<color=#2ECC71>✓ Təsdiqləndi:</color> <b>{target.Name}</b> ({targetMotive.ToAzerbaijaniDisplay()}) '<b>{typeLabel}</b>' təklifini qəbul etdi! Münasibət: <b>{updatedScore:+0;-0;0}</b> (+10).";
            LogEvent($"• <color=#2ECC71>Müqavilə İmzaladı:</color> {source.Name} ↔ {target.Name} ({typeLabel}) [+10 Münasibət].");

            OnDiplomacyDataChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Dövlətin xəritədəki aktiv bütün ordularının ümumi əsgər sayını dinamik olaraq qaytarır.
        /// </summary>
        public int GetFactionMilitaryPower(int factionId)
        {
            return ArmyManager.Instance != null ? ArmyManager.Instance.GetTotalTroopsForFaction(factionId) : 0;
        }

        private bool IsMotiveCompatible(Faction target, Faction source, FactionMotive targetMotive, AgreementType type, int relationScore, out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (type == AgreementType.Vassalage || type == AgreementType.Tribute)
            {
                if (targetMotive == FactionMotive.Expansion)
                {
                    rejectionReason = $"<b>{target.Name}</b> ('Expansion' marağı) müstəqilliyindən və ya xəracdan imtina etdi.";
                    return false;
                }

                int sourcePower = GetFactionMilitaryPower(source.ID);
                int targetPower = GetFactionMilitaryPower(target.ID);
                if (relationScore < 75 || sourcePower <= targetPower * 1.25f)
                {
                    rejectionReason = $"<b>{target.Name}</b> tabelik/xərac üçün kifayət qədər hərbi üstünlük ({sourcePower:N0} vs {targetPower:N0}) və ya etimad (+75) görmür.";
                    return false;
                }
            }

            if (targetMotive == FactionMotive.Consolidation && type == AgreementType.Military && relationScore < 80)
            {
                rejectionReason = $"<b>{target.Name}</b> ('Consolidation' marağı) hücum yönümlü Hərbi İttifaq üçün +80 münasibət tələb edir (hazırda: {relationScore}).";
                return false;
            }

            return true;
        }

        private static int GetDefaultDurationMonths(AgreementType type)
        {
            switch (type)
            {
                case AgreementType.Trade:
                case AgreementType.Economic:
                    return 36;
                case AgreementType.Defensive:
                case AgreementType.NonAggression:
                    return 24;
                case AgreementType.Military:
                    return 48;
                case AgreementType.Vassalage:
                    return 120;
                default:
                    return 24;
            }
        }

        /// <summary>
        /// İki dövlət arasında rəsmi müharibə elan edir:
        /// Münasibət -100 ("Düşmənçilik") edilir, aralarındakı bütün müqavilələr SQLite-dan silinir
        /// və EventLogPanel-ə qırmızı bildiriş yazılır.
        /// </summary>
        public void DeclareWar(int attackerId, int defenderId)
        {
            EnsureInitialized();
            if (attackerId == defenderId) return;

            Faction attacker = _db.GetFactionById(attackerId);
            Faction defender = _db.GetFactionById(defenderId);
            string attackerName = attacker != null ? attacker.Name : $"Dövlət #{attackerId}";
            string defenderName = defender != null ? defender.Name : $"Dövlət #{defenderId}";

            FactionRelation record = GetRelationRecord(attackerId, defenderId);
            record.RelationScore = -100;
            record.Status = FactionRelation.GetStatusForScore(-100);
            _db.UpsertFactionRelation(record);

            int deletedCount = _db.DeleteAgreementsBetween(attackerId, defenderId);

            string logMsg = $"• <color=#E74C3C><b>MÜHARİBƏ:</b> {attackerName} → {defenderName} dövlətinə rəsmi müharibə elan etdi! (Münasibət: -100, {deletedCount} müqavilə ləğv edildi)</color>";
            LogEvent(logMsg);

            OnDiplomacyDataChanged?.Invoke();
        }

        public void ResetToHistorical1187()
        {
            EnsureInitialized();
            _db.ResetDiplomacyDataToHistorical1187();
            OnDiplomacyDataChanged?.Invoke();
        }

        private static void LogEvent(string richTextMessage)
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.AddEventLogMessage(richTextMessage);
            }
        }
    }
}
