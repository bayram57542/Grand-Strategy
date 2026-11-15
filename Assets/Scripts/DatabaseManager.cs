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

            Debug.Log("[DatabaseManager] ORM Cədvəlləri yaradıldı/təsdiqləndi: Nations, Provinces, Populations, Economies, Diplomacies, Armies, Leaders, Caravans.");
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
