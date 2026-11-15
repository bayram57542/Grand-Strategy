using System;
using System.Collections.Generic;
using System.IO;
using GrandStrategy.Database.Models;
using SQLite;
using UnityEngine;

namespace GrandStrategy.Database
{
    /// <summary>
    /// Oyunun iqtisadiyyatını, demoqrafiyasını və diplomatiyasını lokal SQLite bazasında
    /// idarə edən mərkəzi verilənlər bazası meneceri (Singleton).
    /// </summary>
    public class DatabaseManager : MonoBehaviour
    {
        public static DatabaseManager Instance { get; private set; }

        [Header("Verilənlər Bazası Konfiqurasiyası")]
        [Tooltip("Yaradılacaq lokal SQLite baza faylının adı")]
        [SerializeField] private string databaseFileName = "GrandStrategy.db";

        [Tooltip("Editor rejimində bazanı birbaşa Assets/Database qovluğunda saxla")]
        [SerializeField] private bool useAssetsDatabaseFolderInEditor = true;

        [Tooltip("Baza ilk dəfə yaradıldıqda sınaq (Dummy) məlumatlarını daxil et və konsola yazdır")]
        [SerializeField] private bool seedDummyDataOnInit = true;

        private SQLiteConnection _connection;

        /// <summary>
        /// Aktiv SQLite bağlantısı.
        /// </summary>
        public SQLiteConnection Connection => _connection;

        /// <summary>
        /// Lokal verilənlər bazası faylının tam yolu.
        /// </summary>
        public string DatabasePath { get; private set; }

        /// <summary>
        /// Verilənlər bazası bağlantısının aktiv və işlək olub-olmadığını göstərir.
        /// </summary>
        public bool IsConnected => _connection != null;

        /// <summary>
        /// Hər Tick zamanı baza əməliyyatları icra olunarkən digər alt-sistemlərin (İqtisadiyyat,
        /// Demoqrafiya, Diplomatiya) qoşula biləcəyi tranzaksiya hadisəsi.
        /// </summary>
        public event Action<SQLiteConnection, long> OnTickDatabaseTransaction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDatabase();
        }

        /// <summary>
        /// Verilənlər bazası qovluğunu və lokal SQLite baza faylını yaradır,
        /// ORM cədvəllərini (CreateTables) qurur, bağlantını açır və test edir.
        /// </summary>
        public bool InitializeDatabase()
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                string directoryPath = GetDatabaseDirectory();
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                DatabasePath = Path.Combine(directoryPath, databaseFileName);

                var openFlags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex;
                _connection = new SQLiteConnection(DatabasePath, openFlags);

                // Xarici açar (Foreign Key) dəstəyini aktivləşdiririk
                _connection.Execute("PRAGMA foreign_keys = ON;");

                // 4 əsas ORM cədvəlini yaradırıq
                CreateTables();

                bool verified = VerifyConnection();
                if (verified)
                {
                    Debug.Log($"[DatabaseManager] SQLite bazası və cədvəllər uğurla hazırlandı: {DatabasePath}");

                    if (seedDummyDataOnInit)
                    {
                        SeedAndTestDummyData(forceReseed: false);
                    }

                    EnsureDiplomacyTablesAndSeed(forceReseed: false);
                }

                return verified;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] SQLite bazasının yaradılması və ya açılması zamanı xəta: {ex.Message}\n{ex.StackTrace}");
                CloseConnection();
                return false;
            }
        }

        /// <summary>
        /// Oyun arxa planını idarə edən 6 əsas cədvəli (Nation, Province, Population, Economy, Diplomacy, Army)
        /// ORM vasitəsilə lokal bazada avtomatik formalaşdırır.
        /// </summary>
        public void CreateTables()
        {
            if (_connection == null)
            {
                throw new InvalidOperationException("Cədvəlləri yaratmaq üçün aktiv SQLite bağlantısı tələb olunur.");
            }

            _connection.CreateTable<Nation>();
            _connection.CreateTable<Province>();
            _connection.CreateTable<Population>();
            _connection.CreateTable<Economy>();
            _connection.CreateTable<Diplomacy>();
            _connection.CreateTable<Army>();
            _connection.CreateTable<Leader>();
            _connection.CreateTable<Caravan>();
            _connection.CreateTable<Faction>();
            _connection.CreateTable<FactionRelation>();
            _connection.CreateTable<DiplomaticAgreement>();

            // Mövcud cədvəllər üçün geriyə uyğunluq sütunları
            TryAddColumnIfMissing("Nations", "PrimaryMotive", "TEXT DEFAULT 'Consolidation'");
            TryAddColumnIfMissing("Factions", "Treasury", "REAL DEFAULT 1000");
            TryAddColumnIfMissing("Factions", "DiplomaticReputation", "INTEGER DEFAULT 50");
            TryAddColumnIfMissing("Factions", "PrimaryMotive", "TEXT DEFAULT 'Consolidation'");

            Debug.Log("[DatabaseManager] ORM Cədvəlləri yaradıldı/təsdiqləndi: Nations, Provinces, Populations, Economies, Diplomacies, Armies, Leaders, Caravans, Factions, FactionRelations, DiplomaticAgreements.");
        }

        private class SqliteColumnInfo
        {
            public string name { get; set; }
        }

        private void TryAddColumnIfMissing(string tableName, string columnName, string columnDefSql)
        {
            if (_connection == null) return;

            try
            {
                var columns = _connection.Query<SqliteColumnInfo>($"PRAGMA table_info({tableName});");
                bool exists = false;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (string.Equals(columns[i].name, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    _connection.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefSql};");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DatabaseManager] {tableName}.{columnName} sütunu əlavə edilərkən xəbərdarlıq: {ex.Message}");
            }
        }

        /// <summary>
        /// Diplomatiya cədvəllərinin (Factions, FactionRelations, DiplomaticAgreements) 1187 tarixi məlumatları ilə hazır olmasını təmin edir.
        /// </summary>
        public void EnsureDiplomacyTablesAndSeed(bool forceReseed = false)
        {
            if (_connection == null)
            {
                if (!InitializeDatabase()) return;
            }

            _connection.CreateTable<Faction>();
            _connection.CreateTable<FactionRelation>();
            _connection.CreateTable<DiplomaticAgreement>();

            int factionCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Factions;");
            if (!forceReseed && factionCount > 0)
            {
                int relCount = _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM FactionRelations;");
                if (relCount > 0)
                {
                    return;
                }
            }

            _connection.RunInTransaction(() =>
            {
                if (forceReseed)
                {
                    _connection.Execute("DELETE FROM DiplomaticAgreements;");
                    _connection.Execute("DELETE FROM FactionRelations;");
                    _connection.Execute("DELETE FROM Factions;");
                }

                if (forceReseed || _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Factions;") == 0)
                {
                    var initialFactions = new[]
                    {
                        new Faction { ID = 1, Name = "Eldəgəzlər (Atabəylər)", RulerName = "Qızıl Arslan", GovernmentType = "Atabəylik / Sultanlıq", Treasury = 4250f, DiplomaticReputation = 72, PrimaryMotive = "Consolidation", MilitaryPower = 28500, ColorHex = "#1F618D" },
                        new Faction { ID = 2, Name = "Şirvanşahlar Dövləti", RulerName = "I Axsitan", GovernmentType = "Şahlıq (Monarxiya)", Treasury = 2800f, DiplomaticReputation = 80, PrimaryMotive = "Trade", MilitaryPower = 14200, ColorHex = "#2ECC71" },
                        new Faction { ID = 3, Name = "Gürcüstan Krallığı", RulerName = "Çariça Tamara", GovernmentType = "Feodal Krallıq", Treasury = 3100f, DiplomaticReputation = 64, PrimaryMotive = "Expansion", MilitaryPower = 26800, ColorHex = "#78281F" },
                        new Faction { ID = 4, Name = "Bizans İmperiyası", RulerName = "II İsaak Angelos", GovernmentType = "İmperiya", Treasury = 5400f, DiplomaticReputation = 48, PrimaryMotive = "Consolidation", MilitaryPower = 31000, ColorHex = "#6E2C92" },
                        new Faction { ID = 5, Name = "Rum Sultanlığı", RulerName = "II Qılınc Arslan", GovernmentType = "Sultanlıq", Treasury = 3650f, DiplomaticReputation = 55, PrimaryMotive = "Expansion", MilitaryPower = 29400, ColorHex = "#C0392B" },
                        new Faction { ID = 6, Name = "Əyyubilər Dövləti", RulerName = "Səlahəddin Əyyubi", GovernmentType = "Sultanlıq", Treasury = 6100f, DiplomaticReputation = 85, PrimaryMotive = "Expansion", MilitaryPower = 42000, ColorHex = "#F1C40F" },
                        new Faction { ID = 7, Name = "Abbasi Xilafəti", RulerName = "Ən-Nasir Lidinillah", GovernmentType = "Xilafət", Treasury = 4900f, DiplomaticReputation = 78, PrimaryMotive = "Trade", MilitaryPower = 18500, ColorHex = "#1C2833" }
                    };

                    for (int i = 0; i < initialFactions.Length; i++)
                    {
                        _connection.InsertOrReplace(initialFactions[i]);
                    }
                }

                if (forceReseed || _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM FactionRelations;") == 0)
                {
                    var initialRelations = new[]
                    {
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 2, RelationScore = 75, Status = FactionRelation.GetStatusForScore(75) },
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 3, RelationScore = -45, Status = FactionRelation.GetStatusForScore(-45) },
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 4, RelationScore = -10, Status = FactionRelation.GetStatusForScore(-10) },
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 5, RelationScore = 15, Status = FactionRelation.GetStatusForScore(15) },
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 6, RelationScore = 65, Status = FactionRelation.GetStatusForScore(65) },
                        new FactionRelation { Faction1_ID = 1, Faction2_ID = 7, RelationScore = 50, Status = FactionRelation.GetStatusForScore(50) },
                        new FactionRelation { Faction1_ID = 3, Faction2_ID = 4, RelationScore = 40, Status = FactionRelation.GetStatusForScore(40) },
                        new FactionRelation { Faction1_ID = 4, Faction2_ID = 5, RelationScore = -75, Status = FactionRelation.GetStatusForScore(-75) },
                        new FactionRelation { Faction1_ID = 5, Faction2_ID = 6, RelationScore = -30, Status = FactionRelation.GetStatusForScore(-30) }
                    };

                    for (int i = 0; i < initialRelations.Length; i++)
                    {
                        _connection.Insert(initialRelations[i]);
                    }
                }

                if (forceReseed || _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM DiplomaticAgreements;") == 0)
                {
                    _connection.Insert(new DiplomaticAgreement
                    {
                        SourceFaction_ID = 1,
                        TargetFaction_ID = 2,
                        Type = "Ticarət",
                        DurationMonths = 36
                    });
                    _connection.Insert(new DiplomaticAgreement
                    {
                        SourceFaction_ID = 1,
                        TargetFaction_ID = 6,
                        Type = "Müdafiə",
                        DurationMonths = 24
                    });
                }
            });

            Debug.Log("[DatabaseManager] 1187 tarixi Diplomatiya verilənlər bazası (7 Dövlət, Münasibətlər və Müqavilələr) təsdiqləndi/yükləndi.");
        }

        public void ResetDiplomacyDataToHistorical1187()
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: true);
        }

        public System.Collections.Generic.List<Faction> GetAllFactions()
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            return _connection.Query<Faction>("SELECT * FROM Factions ORDER BY ID ASC;");
        }

        public Faction GetFactionById(int factionId)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            var list = _connection.Query<Faction>("SELECT * FROM Factions WHERE ID = ? LIMIT 1;", factionId);
            return list.Count > 0 ? list[0] : null;
        }

        public void UpsertFaction(Faction faction)
        {
            if (faction == null) return;
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            _connection.InsertOrReplace(faction);
        }

        public FactionRelation GetFactionRelation(int faction1Id, int faction2Id)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            FactionRelation.NormalizePair(faction1Id, faction2Id, out int minId, out int maxId);
            var list = _connection.Query<FactionRelation>(
                "SELECT * FROM FactionRelations WHERE (Faction1_ID = ? AND Faction2_ID = ?) OR (Faction1_ID = ? AND Faction2_ID = ?) LIMIT 1;",
                minId, maxId, maxId, minId);
            return list.Count > 0 ? list[0] : null;
        }

        public System.Collections.Generic.List<FactionRelation> GetAllRelationsForFaction(int factionId)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            return _connection.Query<FactionRelation>(
                "SELECT * FROM FactionRelations WHERE Faction1_ID = ? OR Faction2_ID = ?;",
                factionId, factionId);
        }

        public void UpsertFactionRelation(FactionRelation relation)
        {
            if (relation == null) return;
            EnsureDiplomacyTablesAndSeed(forceReseed: false);

            FactionRelation.NormalizePair(relation.Faction1_ID, relation.Faction2_ID, out int minId, out int maxId);
            relation.Faction1_ID = minId;
            relation.Faction2_ID = maxId;

            var existing = GetFactionRelation(minId, maxId);
            if (existing != null)
            {
                existing.Faction1_ID = minId;
                existing.Faction2_ID = maxId;
                existing.RelationScore = relation.RelationScore;
                existing.Status = relation.Status;
                _connection.Update(existing);
                relation.ID = existing.ID;
            }
            else
            {
                _connection.Insert(relation);
            }
        }

        public System.Collections.Generic.List<DiplomaticAgreement> GetAgreementsBetween(int faction1Id, int faction2Id)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            return _connection.Query<DiplomaticAgreement>(
                "SELECT * FROM DiplomaticAgreements WHERE (SourceFaction_ID = ? AND TargetFaction_ID = ?) OR (SourceFaction_ID = ? AND TargetFaction_ID = ?);",
                faction1Id, faction2Id, faction2Id, faction1Id);
        }

        public System.Collections.Generic.List<DiplomaticAgreement> GetAgreementsForFaction(int factionId)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            return _connection.Query<DiplomaticAgreement>(
                "SELECT * FROM DiplomaticAgreements WHERE SourceFaction_ID = ? OR TargetFaction_ID = ?;",
                factionId, factionId);
        }

        public void InsertDiplomaticAgreement(DiplomaticAgreement agreement)
        {
            if (agreement == null) return;
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            _connection.Insert(agreement);
        }

        public void UpdateDiplomaticAgreement(DiplomaticAgreement agreement)
        {
            if (agreement == null) return;
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            _connection.Update(agreement);
        }

        public int DeleteAgreementsBetween(int faction1Id, int faction2Id)
        {
            EnsureDiplomacyTablesAndSeed(forceReseed: false);
            return _connection.Execute(
                "DELETE FROM DiplomaticAgreements WHERE (SourceFaction_ID = ? AND TargetFaction_ID = ?) OR (SourceFaction_ID = ? AND TargetFaction_ID = ?);",
                faction1Id, faction2Id, faction2Id, faction1Id);
        }

        /// <summary>
        /// Test məqsədilə bazaya "Dummy" (Sınaq) məlumatları daxil edir və onları yenidən oxuyaraq konsola yazdırır.
        /// </summary>
        /// <param name="forceReseed">True olarsa, mövcud sınaq qeydlərini təmizləyib yenidən yazır.</param>
        [ContextMenu("Sınaq Məlumatlarını Yaz və Oxu (Dummy Test)")]
        public bool SeedAndTestDummyData(bool forceReseed = true)
        {
            if (!IsConnected && !InitializeDatabase())
            {
                Debug.LogError("[DatabaseManager] Dummy test icra edilə bilmədi: Baza bağlıdır.");
                return false;
            }

            try
            {
                int existingProvinces = _connection.Table<Province>().Count();
                int existingLeaders = _connection.Table<Leader>().Count();
                if (existingProvinces == 0 || existingLeaders == 0 || forceReseed)
                {
                    _connection.RunInTransaction(() =>
                    {
                        if (forceReseed || existingProvinces == 0)
                        {
                            _connection.DeleteAll<Caravan>();
                            _connection.DeleteAll<Leader>();
                            _connection.DeleteAll<Army>();
                            _connection.DeleteAll<Diplomacy>();
                            _connection.DeleteAll<Economy>();
                            _connection.DeleteAll<Population>();
                            _connection.DeleteAll<Province>();
                            _connection.DeleteAll<Nation>();
                            _connection.Execute("DELETE FROM sqlite_sequence;");
                        }

                        // 1. Province (Əyalətlər) sınaq məlumatları (Faza 5-8.1: Gold, Food, Iron, Horses, Silk, Demoqrafiya, Morale, Siege, BirthRate)
                        var anadolu = new Province
                        {
                            Name = "Anadolu",
                            Gold = 5000f,
                            Food = 2500f,
                            Iron = 150f,
                            Horses = 25f,
                            Silk = 5f,
                            StrategicResourceType = "Iron",
                            Babies = 1200,
                            Youths = 2500,
                            Adults = 6800,
                            Elders = 950,
                            Wounded = 150,
                            ProductivityMultiplier = 1.0f,
                            ActiveConstruction = string.Empty,
                            ConstructionMonthsLeft = 0,
                            PublicOrder = 100f,
                            IsBesieged = false,
                            BirthRateModifier = 1.0f,
                            IsExploited = false,
                            Climate = "Mülayim",
                            OwnerFactionId = 1,
                            DevelopmentLevel = 18
                        };

                        var shirvan = new Province
                        {
                            Name = "Şirvan",
                            Gold = 3800f,
                            Food = 1800f,
                            Iron = 20f,
                            Horses = 120f,
                            Silk = 30f,
                            StrategicResourceType = "Horses",
                            Babies = 850,
                            Youths = 800,
                            Adults = 5400,
                            Elders = 550,
                            Wounded = 50,
                            ProductivityMultiplier = 1.0f,
                            ActiveConstruction = string.Empty,
                            ConstructionMonthsLeft = 0,
                            PublicOrder = 100f,
                            IsBesieged = false,
                            BirthRateModifier = 1.0f,
                            IsExploited = false,
                            Climate = "Quraq",
                            OwnerFactionId = 2,
                            DevelopmentLevel = 16
                        };

                        _connection.Insert(anadolu);
                        _connection.Insert(shirvan);

                        // 1b. Nation (Dövlətlər) cədvəli sınaq məlumatları (Faza 5-8.1: Hakim Sülalə, İdarəetmə Həddi və Resurslar)
                        var seljukNation = new Nation
                        {
                            Name = "Səlcuqlu Dövləti (Anadolu)",
                            RulingDynastyName = "Səlcuqlu Sultanlığı",
                            ControlledProvinceCount = 1,
                            DirectControlledProvinceCount = 1,
                            MaxDirectControl = 3,
                            Gold = anadolu.Gold,
                            Food = anadolu.Food,
                            Iron = anadolu.Iron,
                            Horses = anadolu.Horses,
                            Silk = anadolu.Silk,
                            Babies = anadolu.Babies,
                            Youths = anadolu.Youths,
                            Adults = anadolu.Adults,
                            Elders = anadolu.Elders,
                            Wounded = anadolu.Wounded
                        };

                        var shirvanNation = new Nation
                        {
                            Name = "Şirvanşahlar Dövləti (Şirvan)",
                            RulingDynastyName = "Şirvanşahlar Dövləti",
                            ControlledProvinceCount = 1,
                            DirectControlledProvinceCount = 1,
                            MaxDirectControl = 3,
                            DiplomaticReputation = 100f,
                            PeakMilitaryPower = 0,
                            Gold = shirvan.Gold,
                            Food = shirvan.Food,
                            Iron = shirvan.Iron,
                            Horses = shirvan.Horses,
                            Silk = shirvan.Silk,
                            Babies = shirvan.Babies,
                            Youths = shirvan.Youths,
                            Adults = shirvan.Adults,
                            Elders = shirvan.Elders,
                            Wounded = shirvan.Wounded
                        };

                        var byzantineNation = new Nation
                        {
                            Name = "Bizans İmperiyası",
                            RulingDynastyName = "Dukas Sülaləsi",
                            ControlledProvinceCount = 1,
                            DirectControlledProvinceCount = 1,
                            MaxDirectControl = 3,
                            DiplomaticReputation = 100f,
                            PeakMilitaryPower = 0,
                            Gold = 4000f,
                            Food = 2000f,
                            Iron = 40f,
                            Horses = 30f,
                            Silk = 150f,
                            Babies = 800,
                            Youths = 1500,
                            Adults = 4000,
                            Elders = 600,
                            Wounded = 100
                        };

                        _connection.Insert(seljukNation);
                        _connection.Insert(shirvanNation);
                        _connection.Insert(byzantineNation);

                        // 2. Population (Əhali) sınaq məlumatları
                        var anadoluPop = new Population
                        {
                            ProvinceId = anadolu.Id,
                            Infants = anadolu.Babies,
                            Youths = anadolu.Youths,
                            Adults = anadolu.Adults,
                            Elders = anadolu.Elders,
                            Wounded = anadolu.Wounded
                        };

                        var shirvanPop = new Population
                        {
                            ProvinceId = shirvan.Id,
                            Infants = shirvan.Babies,
                            Youths = shirvan.Youths,
                            Adults = shirvan.Adults,
                            Elders = shirvan.Elders,
                            Wounded = shirvan.Wounded
                        };

                        _connection.Insert(anadoluPop);
                        _connection.Insert(shirvanPop);

                        // 3. Economy (Bazar və Resurslar) başlanğıc sınaq məlumatları
                        var anadoluEco = new Economy
                        {
                            ProvinceId = anadolu.Id,
                            FoodProduction = 1468.8f,
                            RawMaterials = 612.0f,
                            LuxuryGoods = 85.2f,
                            LocalTaxIncome = 3570.4f
                        };

                        var shirvanEco = new Economy
                        {
                            ProvinceId = shirvan.Id,
                            FoodProduction = 691.2f,
                            RawMaterials = 432.0f,
                            LuxuryGoods = 195.8f,
                            LocalTaxIncome = 3091.6f
                        };

                        _connection.Insert(anadoluEco);
                        _connection.Insert(shirvanEco);

                        // 4. Diplomacy (Diplomatik Münasibətlər) sınaq məlumatları
                        var treatyTrade = new Diplomacy
                        {
                            FactionA_Id = 1,
                            FactionB_Id = 2,
                            RelationScore = 65,
                            RelationshipScore = 65,
                            TreatyType = "Ticarət",
                            TreatyDuration = 24,
                            CeasefireMonthsLeft = 0,
                            NonAggressionMonthsLeft = 0
                        };

                        var treatyWar = new Diplomacy
                        {
                            FactionA_Id = 1,
                            FactionB_Id = 3,
                            RelationScore = -85,
                            RelationshipScore = -85,
                            TreatyType = "Müharibə",
                            TreatyDuration = 12,
                            CeasefireMonthsLeft = 0,
                            NonAggressionMonthsLeft = 0
                        };

                        _connection.Insert(treatyTrade);
                        _connection.Insert(treatyWar);

                        // 5. Leaders (Liderlər, Sədaqət və Üsyan Sistemi - Faza 6B & 7.1) sınaq məlumatları
                        var kingLeader = new Leader
                        {
                            Name = "Kral",
                            Loyalty = 100f,
                            IsRuler = true,
                            IsIndependent = false,
                            AssignedProvinceId = null,
                            AssignedArmyId = "1",
                            SecretPactFactionId = null
                        };

                        var governorA = new Leader
                        {
                            Name = "Qubernator A",
                            Loyalty = 40f,
                            IsIndependent = false,
                            AssignedProvinceId = anadolu.Id.ToString(),
                            AssignedArmyId = null,
                            SecretPactFactionId = null
                        };

                        _connection.Insert(kingLeader);
                        _connection.Insert(governorA);

                        // 6. Caravans (Faza 8.2: Qlobal Ticarət və Karvanlar)
                        var dummyCaravan = new Caravan
                        {
                            OwnerFactionId = 1,
                            SourceProvinceId = anadolu.Id,
                            TargetProvinceId = shirvan.Id,
                            ResourceType = "Silk",
                            ResourceAmount = 50f,
                            TravelProgress = 20f,
                            IsRaided = false,
                            IsCompleted = false
                        };
                        _connection.Insert(dummyCaravan);
                    });

                    Debug.Log("[DatabaseManager] Dummy (Sınaq) məlumatları bazaya uğurla daxil edildi.");
                }

                // Bazadan məlumatları yenidən oxuyub konsola çap edirik
                List<Nation> nations = _connection.Table<Nation>().ToList();
                List<Province> provinces = _connection.Table<Province>().ToList();
                List<Population> populations = _connection.Table<Population>().ToList();
                List<Economy> economies = _connection.Table<Economy>().ToList();
                List<Diplomacy> diplomacies = _connection.Table<Diplomacy>().ToList();
                List<Leader> leaders = _connection.Table<Leader>().ToList();

                Debug.Log($"=== [DatabaseManager] BAZADAN OXUNAN SINAQ MƏLUMATLARI ===");
                foreach (var n in nations)
                {
                    Debug.Log(n.ToString());
                }

                foreach (var p in provinces)
                {
                    Debug.Log(p.ToString());
                }

                foreach (var pop in populations)
                {
                    Debug.Log(pop.ToString());
                }

                foreach (var eco in economies)
                {
                    Debug.Log(eco.ToString());
                }

                foreach (var dip in diplomacies)
                {
                    Debug.Log(dip.ToString());
                }

                foreach (var ldr in leaders)
                {
                    Debug.Log(ldr.ToString());
                }

                return provinces.Count > 0 && populations.Count > 0 && economies.Count > 0 && diplomacies.Count > 0 && leaders.Count > 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Dummy məlumatların testi zamanı xəta: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Baza faylının fiziki olaraq diskdə mövcudluğunu və SQLite mühərrikinin sorğulara cavab verdiyini test edir.
        /// </summary>
        public bool VerifyConnection()
        {
            if (_connection == null)
            {
                Debug.LogError("[DatabaseManager] Test uğursuz oldu: Bağlantı obyektinə (Connection) müraciət yoxdur.");
                return false;
            }

            try
            {
                int testResult = _connection.ExecuteScalar<int>("SELECT 1;");
                bool fileExists = File.Exists(DatabasePath);

                if (testResult == 1 && fileExists)
                {
                    string sqliteVersion = _connection.ExecuteScalar<string>("SELECT sqlite_version();");
                    Debug.Log($"[DatabaseManager] Bağlantı testi uğurludur! SQLite versiyası: {sqliteVersion} | Fayl mövcuddur: {fileExists}");
                    return true;
                }

                Debug.LogError($"[DatabaseManager] Bağlantı testi uğursuz oldu. Nəticə: {testResult}, Fayl mövcuddur: {fileExists}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] Bağlantı testi zamanı xəta: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Çoxsaylı baza əməliyyatlarını (məs: Aylıq/İllik demoqrafik və ya iqtisadi yeniləmələri)
        /// FPS düşüşü olmadan vahid atomik SQLite tranzaksiyası daxilində toplu şəkildə icra edir.
        /// </summary>
        public void RunInTransaction(Action<SQLiteConnection> transactionAction)
        {
            if (transactionAction == null)
            {
                return;
            }

            if (!IsConnected && !InitializeDatabase())
            {
                Debug.LogError("[DatabaseManager] Tranzaksiya icra edilə bilmədi: Verilənlər bazası bağlıdır.");
                return;
            }

            try
            {
                _connection.RunInTransaction(() =>
                {
                    transactionAction.Invoke(_connection);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DatabaseManager] RunInTransaction zamanı xəta: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// GameManager tərəfindən hər oyun Tick-i (və ya təqvim dövrü) zamanı çağırılan mərkəzi baza sinxronizasiya metodu.
        /// Bütün iqtisadi, demoqrafik və diplomatik yeniləmələri vahid atomik tranzaksiya daxilində icra edir.
        /// </summary>
        public void ProcessGameTick(long currentTick, int day, int month, int year)
        {
            RunInTransaction(conn =>
            {
                OnTickDatabaseTransaction?.Invoke(conn, currentTick);
            });
        }

        /// <summary>
        /// Faza 10.1 - Əyalətin SQLite bazasından real iqtisadi və demoqrafik məlumatlarını oxuyur.
        /// Əgər əyalət bazada yoxdursa, tarixi/stabil deterministik formul tətbiq edir və avtomatik bazaya qeyd edir.
        /// </summary>
        public ProvinceEconomyData GetProvinceEconomyData(int provinceId, string provinceName = null, string adminCountry = null, bool autoCreateIfMissing = true)
        {
            if (!IsConnected && !InitializeDatabase())
            {
                Debug.LogWarning("[DatabaseManager] Verilənlər bazası bağlantısı açıla bilmədi, ehtiyat formul tətbiq olunur.");
            }

            Province prov = null;
            if (_connection != null)
            {
                try
                {
                    if (provinceId > 0)
                    {
                        prov = _connection.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                    }
                    if (prov == null && !string.IsNullOrEmpty(provinceName))
                    {
                        string searchName = provinceName.Trim();
                        prov = _connection.Table<Province>().FirstOrDefault(p => p.Name.ToLower() == searchName.ToLower());
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DatabaseManager] Əyalət sorğulanarkən xəta: {ex.Message}");
                }
            }

            // 1. Bazada əyalət tapıldısa, bağlı Population və Economy cədvəllərindən oxuyuruq
            if (prov != null)
            {
                Population pop = null;
                Economy eco = null;
                try
                {
                    pop = _connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == prov.Id);
                    eco = _connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == prov.Id);
                }
                catch { }

                int totalPop = pop != null ? pop.TotalPopulation : (prov.Adults + prov.Youths + prov.Babies + prov.Elders + prov.Wounded);
                if (totalPop <= 0) totalPop = 50000;

                float goldIncome = eco != null && eco.LocalTaxIncome > 0 ? eco.LocalTaxIncome : Mathf.Max(prov.Adults * 0.5f, 25f);
                float foodProd = eco != null && eco.FoodProduction > 0 ? eco.FoodProduction : Mathf.Max(prov.Adults * 0.12f, 30f);
                float ironProd = prov.Iron > 0 ? prov.Iron : (eco != null && eco.RawMaterials > 0 ? eco.RawMaterials : 15f);

                string pLower = prov.Name != null ? prov.Name.ToLower() : "";
                if ((pLower.Contains("istanbul") || pLower.Contains("constantinople")) && totalPop < 100000)
                {
                    prov.Adults = 108000;
                    prov.Youths = 60000;
                    prov.Babies = 36000;
                    prov.Elders = 24000;
                    prov.Wounded = 12000;
                    prov.Gold = 1850f;
                    prov.Food = 1600f;
                    prov.Iron = 48f;
                    prov.DevelopmentLevel = 10;
                    prov.StrategicResourceType = "Silk";
                    _connection.Update(prov);

                    if (pop != null)
                    {
                        pop.Adults = prov.Adults;
                        pop.Youths = prov.Youths;
                        pop.Infants = prov.Babies;
                        pop.Elders = prov.Elders;
                        pop.Wounded = prov.Wounded;
                        _connection.Update(pop);
                    }
                    if (eco != null)
                    {
                        eco.LocalTaxIncome = 185f;
                        eco.FoodProduction = 160f;
                        eco.RawMaterials = 48f;
                        eco.LuxuryGoods = 40f;
                        _connection.Update(eco);
                    }

                    totalPop = 240000;
                    goldIncome = 185f;
                    foodProd = 160f;
                    ironProd = 48f;
                }

                return new ProvinceEconomyData(
                    prov.Id,
                    prov.Name,
                    totalPop,
                    goldIncome,
                    foodProd,
                    ironProd,
                    prov.Gold,
                    prov.Food,
                    prov.StrategicResourceType,
                    prov.DevelopmentLevel,
                    prov.Climate,
                    isFromDatabase: true
                );
            }

            // 2. Əyalət bazada yoxdursa: Tarixi reallıqlar və ya deterministik formul
            string targetName = !string.IsNullOrEmpty(provinceName) ? provinceName : (provinceId > 0 ? $"Province_{provinceId}" : "Bilinməyən Əyalət");
            string lowerName = targetName.ToLower();

            int popCount;
            float goldInc;
            float foodCount;
            float ironCount;
            int dev = 6;
            string resource = "None";
            string climate = "Temperate";

            if (lowerName.Contains("gəncə") || lowerName.Contains("ganja"))
            {
                popCount = 85000;
                goldInc = 75f;
                foodCount = 95f;
                ironCount = 65f;
                dev = 8;
                resource = "Silk";
            }
            else if (lowerName.Contains("bakı") || lowerName.Contains("baku"))
            {
                popCount = 52000;
                goldInc = 62f;
                foodCount = 58f;
                ironCount = 42f;
                dev = 7;
                resource = "Iron";
            }
            else if (lowerName.Contains("konya") || lowerName.Contains("iconium"))
            {
                popCount = 94000;
                goldInc = 82f;
                foodCount = 115f;
                ironCount = 54f;
                dev = 8;
                resource = "Horses";
            }
            else if (lowerName.Contains("istanbul") || lowerName.Contains("constantinople") || lowerName.Contains("konstantinopol"))
            {
                popCount = 240000;
                goldInc = 185f;
                foodCount = 160f;
                ironCount = 48f;
                dev = 10;
                resource = "Silk";
            }
            else if (lowerName.Contains("tbilisi") || lowerName.Contains("tiflis"))
            {
                popCount = 68000;
                goldInc = 68f;
                foodCount = 72f;
                ironCount = 38f;
                dev = 7;
            }
            else
            {
                int absHash = Mathf.Abs(targetName.GetHashCode() ^ (provinceId * 397));
                popCount = 25000 + (absHash % 65000);
                goldInc = 18f + (absHash % 42);
                foodCount = 22f + ((absHash / 7) % 68);
                ironCount = (absHash % 3 == 0) ? (12f + (absHash % 38)) : 8f;
                dev = 3 + (absHash % 6);
                if (ironCount > 25f) resource = "Iron";
                else if (absHash % 5 == 0) resource = "Silk";
                else if (absHash % 4 == 0) resource = "Horses";
            }

            // 3. Əgər autoCreateIfMissing aktivdirsə, əyaləti SQLite bazasına yazırıq
            if (autoCreateIfMissing && _connection != null)
            {
                try
                {
                    prov = new Province
                    {
                        Name = targetName,
                        Gold = goldInc * 10f,
                        Food = foodCount * 10f,
                        Iron = ironCount,
                        Adults = Mathf.RoundToInt(popCount * 0.45f),
                        Youths = Mathf.RoundToInt(popCount * 0.25f),
                        Babies = Mathf.RoundToInt(popCount * 0.15f),
                        Elders = Mathf.RoundToInt(popCount * 0.10f),
                        Wounded = Mathf.RoundToInt(popCount * 0.05f),
                        DevelopmentLevel = dev,
                        Climate = climate,
                        StrategicResourceType = resource
                    };
                    _connection.Insert(prov);

                    var newPop = new Population
                    {
                        ProvinceId = prov.Id,
                        Adults = prov.Adults,
                        Youths = prov.Youths,
                        Infants = prov.Babies,
                        Elders = prov.Elders,
                        Wounded = prov.Wounded
                    };
                    _connection.Insert(newPop);

                    var newEco = new Economy
                    {
                        ProvinceId = prov.Id,
                        FoodProduction = foodCount,
                        RawMaterials = ironCount,
                        LocalTaxIncome = goldInc,
                        LuxuryGoods = resource == "Silk" ? 30f : 5f
                    };
                    _connection.Insert(newEco);

                    Debug.Log($"[DatabaseManager] Əyalət SQLite bazasına uğurla yazıldı: #{prov.Id} - {targetName} (Əhali: {popCount:N0}, Qızıl: +{goldInc:F0}/ay, Ərzaq: +{foodCount:F0}/ay, Dəmir: +{ironCount:F0}/ay)");

                    return new ProvinceEconomyData(
                        prov.Id,
                        targetName,
                        popCount,
                        goldInc,
                        foodCount,
                        ironCount,
                        prov.Gold,
                        prov.Food,
                        resource,
                        dev,
                        climate,
                        isFromDatabase: true
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DatabaseManager] Əyalət SQLite-a yazılarkən xəta: {ex.Message}");
                }
            }

            return new ProvinceEconomyData(
                provinceId > 0 ? provinceId : 1,
                targetName,
                popCount,
                goldInc,
                foodCount,
                ironCount,
                goldInc * 10f,
                foodCount * 10f,
                resource,
                dev,
                climate,
                isFromDatabase: false
            );
        }

        /// <summary>
        /// Cədvəllərin idarə edilməsi üçün birbaşa SQL əmri icra edir.
        /// </summary>
        public int ExecuteNonQuery(string sql, params object[] args)
        {
            if (!IsConnected && !InitializeDatabase())
            {
                throw new InvalidOperationException("Verilənlər bazası bağlantısı aktiv deyil.");
            }

            return _connection.Execute(sql, args);
        }

        /// <summary>
        /// Tək dəyər qaytaracaq SQL sorğularını (COUNT, SUM və s.) icra edir.
        /// </summary>
        public T ExecuteScalar<T>(string sql, params object[] args)
        {
            if (!IsConnected && !InitializeDatabase())
            {
                throw new InvalidOperationException("Verilənlər bazası bağlantısı aktiv deyil.");
            }

            return _connection.ExecuteScalar<T>(sql, args);
        }

        /// <summary>
        /// Bağlantını təhlükəsiz şəkildə bağlayır və resursları azad edir.
        /// </summary>
        public void CloseConnection()
        {
            if (_connection == null)
            {
                return;
            }

            try
            {
                _connection.Close();
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DatabaseManager] Bağlantı bağlanarkən xəbərdarlıq: {ex.Message}");
            }
            finally
            {
                _connection = null;
            }
        }

        private string GetDatabaseDirectory()
        {
#if UNITY_EDITOR
            if (useAssetsDatabaseFolderInEditor)
            {
                return Path.Combine(Application.dataPath, "Database");
            }
#endif
            return Path.Combine(Application.persistentDataPath, "Database");
        }

        private void OnApplicationQuit()
        {
            CloseConnection();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CloseConnection();
                Instance = null;
            }
        }
    }
}
