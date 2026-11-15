using System;
using System.Collections.Generic;
using System.Text;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// İnfrastruktur tikinti əmrinin nəticəsini saxlayan struktur.
    /// </summary>
    public struct ConstructionStartResult
    {
        public bool Success;
        public int ProvinceId;
        public string ProvinceName;
        public string ProjectName;
        public float GoldCost;
        public float GoldBefore;
        public float GoldAfter;
        public int DurationMonths;
        public string Message;
    }

    /// <summary>
    /// Tamamlanan infrastruktur layihəsi haqqında hesabat strukturu.
    /// </summary>
    public struct ConstructionCompletionReport
    {
        public int ProvinceId;
        public string ProvinceName;
        public string CompletedProjectName;
        public float OldProductivityMultiplier;
        public float NewProductivityMultiplier;
        public int Month;
        public int Year;
    }

    /// <summary>
    /// Faza 6A - İnfrastruktur və Qlobal Əmrlər Sistemi (InfrastructureController):
    /// Əyalətlərdə makro infrastruktur layihələrinin (məs: "Kanal" - 5000 Qızıl, 6 Ay, +0.5f Məhsuldarlıq)
    /// başladılmasını və hər Aylıq Tick zamanı irəliləyib tamamlanmasını SQLite üzərində idarə edir.
    /// </summary>
    public class InfrastructureController : MonoBehaviour
    {
        public const string DefaultCanalProjectName = "Kanal";
        public const float DefaultCanalGoldCost = 5000f;
        public const int DefaultCanalDurationMonths = 6;

        private static InfrastructureController _instance;
        public static InfrastructureController Instance =>
            _instance != null ? _instance : (_instance = FindAnyObjectByType<InfrastructureController>());

        [Header("İnfrastruktur Layihə Konfiqurasiyası")]
        [Tooltip("Kanal layihəsi tamamlandıqda əyalətin ProductivityMultiplier dəyərinə əlavə olunan bonus (+0.5f = +50%)")]
        [SerializeField, Min(0.05f)] private float canalProductivityBonus = 0.50f;

        [Tooltip("Digər standart infrastruktur layihələri üçün məhsuldarlıq bonusu")]
        [SerializeField, Min(0.05f)] private float defaultProjectProductivityBonus = 0.25f;

        public float CanalProductivityBonus => canalProductivityBonus;

        public event Action<ConstructionStartResult> OnConstructionStarted;
        public event Action<Province> OnConstructionProgressed;
        public event Action<ConstructionCompletionReport> OnConstructionCompleted;

        public List<ConstructionCompletionReport> LastCompletedProjects { get; } = new List<ConstructionCompletionReport>();

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

        /// <summary>
        /// Layihə adına görə məhsuldarlıq bonusunu qaytarır (məsələn, "Kanal" üçün +0.5f).
        /// </summary>
        public float GetProjectProductivityBonus(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return defaultProjectProductivityBonus;
            }

            if (projectName.IndexOf("Kanal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return canalProductivityBonus;
            }

            return defaultProjectProductivityBonus;
        }

        /// <summary>
        /// Verilmiş əyalətdə yeni infrastruktur tikintisi başladır:
        /// 1. Dövlətin/Əyalətin qızılını (Gold >= goldCost) yoxlayır.
        /// 2. Kifayət qədər qızıl varsa büdcədən çıxır.
        /// 3. Əyalətin ActiveConstruction və ConstructionMonthsLeft dəyərlərini təyin edib SQLite bazasını yeniləyir.
        /// </summary>
        public ConstructionStartResult StartConstruction(
            string provinceId,
            string projectName,
            float goldCost,
            int durationMonths)
        {
            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            if (db == null)
            {
                return new ConstructionStartResult
                {
                    Success = false,
                    ProjectName = projectName,
                    Message = "[InfrastructureController] DatabaseManager tapılmadı!"
                };
            }

            if (!db.IsConnected)
            {
                db.InitializeDatabase();
            }

            int resolvedId = ResolveProvinceId(db, provinceId);
            return StartConstruction(db, resolvedId, projectName, goldCost, durationMonths);
        }

        public ConstructionStartResult StartConstruction(
            DatabaseManager dbManager,
            int provinceId,
            string projectName,
            float goldCost,
            int durationMonths)
        {
            var result = new ConstructionStartResult
            {
                Success = false,
                ProvinceId = provinceId,
                ProjectName = string.IsNullOrWhiteSpace(projectName) ? DefaultCanalProjectName : projectName.Trim(),
                GoldCost = Mathf.Max(0f, goldCost),
                DurationMonths = Mathf.Max(1, durationMonths)
            };

            if (dbManager == null || (!dbManager.IsConnected && !dbManager.InitializeDatabase()))
            {
                result.Message = "[InfrastructureController] Verilənlər bazası hazır deyil!";
                Debug.LogError(result.Message);
                return result;
            }

            Province updatedProvince = null;

            dbManager.RunInTransaction(conn =>
            {
                Province prov = conn.Table<Province>().FirstOrDefault(p => p.Id == provinceId);
                if (prov == null)
                {
                    result.Message = $"[InfrastructureController] Province #{provinceId} bazada tapılmadı!";
                    return;
                }

                result.ProvinceName = prov.Name;
                result.GoldBefore = prov.Gold;

                if (prov.ProductivityMultiplier <= 0f)
                {
                    prov.ProductivityMultiplier = 1.0f;
                }

                // Əgər hazırda aktiv tikinti gedirsə, yeni tikintiyə icazə vermirik
                if (!string.IsNullOrWhiteSpace(prov.ActiveConstruction) && prov.ConstructionMonthsLeft > 0)
                {
                    result.Message =
                        $"{prov.Name} əyalətində artıq '{prov.ActiveConstruction}' tikintisi davam edir ({prov.ConstructionMonthsLeft} ay qaldı).";
                    return;
                }

                // Dövlətin/Əyalətin qızıl ehtiyatını yoxlayırıq
                if (prov.Gold < result.GoldCost)
                {
                    result.Message =
                        $"Kifayət qədər qızıl yoxdur! Tələb olunan: {result.GoldCost:F0} Qızıl, Mövcud: {prov.Gold:F0} Qızıl.";
                    return;
                }

                // Xərci büdcədən çıxırıq və tikinti parametrlərini təyin edirik
                prov.Gold -= result.GoldCost;
                prov.ActiveConstruction = result.ProjectName;
                prov.ConstructionMonthsLeft = result.DurationMonths;

                conn.Update(prov);

                // Uyğun Nation qeydini də sinxronlaşdırırıq
                Nation nation = conn.Table<Nation>().FirstOrDefault(n => n.Id == prov.Id);
                if (nation != null)
                {
                    nation.Gold = prov.Gold;
                    conn.Update(nation);
                }

                result.GoldAfter = prov.Gold;
                result.Success = true;
                result.Message =
                    $"{prov.Name}: '{result.ProjectName}' tikintisinə başlanıldı! (-{result.GoldCost:F0} Qızıl, Müddət: {result.DurationMonths} ay)";
                updatedProvince = prov;
            });

            if (result.Success)
            {
                Debug.Log($"[InfrastructureController] {result.Message} | Qalan Qızıl: {result.GoldAfter:F1}");
                OnConstructionStarted?.Invoke(result);
                if (updatedProvince != null)
                {
                    OnConstructionProgressed?.Invoke(updatedProvince);
                }
            }
            else
            {
                Debug.LogWarning($"[InfrastructureController] Tikinti başladıla bilmədi -> {result.Message}");
            }

            return result;
        }

        /// <summary>
        /// TimeManager / GameManager-in aylıq Tick döngüsündə çağırılır:
        /// - Tikintisi gedən əyalətlərdə ConstructionMonthsLeft dəyərini 1 vahid azaldır.
        /// - 0 olduqda tikinti bitir, ProductivityMultiplier (məsələn, Kanal üçün +0.5f) artır və ActiveConstruction sıfırlanır.
        /// - Dəyişikliklər avtomatik olaraq SQLite bazasına yazılır (Auto-save).
        /// </summary>
        public void ProcessMonthlyInfrastructure()
        {
            DatabaseManager db = DatabaseManager.Instance != null
                ? DatabaseManager.Instance
                : FindAnyObjectByType<DatabaseManager>();

            GameManager gm = GameManager.Instance;
            int month = gm != null ? gm.CurrentMonth : 1;
            int year = gm != null ? gm.CurrentYear : 1066;

            ProcessMonthlyInfrastructure(db, month, year);
        }

        public void ProcessMonthlyInfrastructure(DatabaseManager dbManager, int month, int year)
        {
            if (dbManager == null || !dbManager.IsConnected)
            {
                return;
            }

            LastCompletedProjects.Clear();
            var progressedProvinces = new List<Province>();

            dbManager.RunInTransaction(conn =>
            {
                List<Province> provinces = conn.Table<Province>().ToList();
                bool anyChanged = false;

                for (int i = 0; i < provinces.Count; i++)
                {
                    Province prov = provinces[i];
                    if (prov.ProductivityMultiplier <= 0f)
                    {
                        prov.ProductivityMultiplier = 1.0f;
                        anyChanged = true;
                    }

                    if (string.IsNullOrWhiteSpace(prov.ActiveConstruction) || prov.ConstructionMonthsLeft <= 0)
                    {
                        continue;
                    }

                    anyChanged = true;
                    prov.ConstructionMonthsLeft = Mathf.Max(0, prov.ConstructionMonthsLeft - 1);

                    if (prov.ConstructionMonthsLeft == 0)
                    {
                        string finishedProject = prov.ActiveConstruction;
                        float oldMultiplier = prov.ProductivityMultiplier;
                        float bonus = GetProjectProductivityBonus(finishedProject);

                        prov.ProductivityMultiplier = oldMultiplier + bonus;
                        prov.ActiveConstruction = string.Empty;
                        prov.ConstructionMonthsLeft = 0;

                        var completion = new ConstructionCompletionReport
                        {
                            ProvinceId = prov.Id,
                            ProvinceName = prov.Name,
                            CompletedProjectName = finishedProject,
                            OldProductivityMultiplier = oldMultiplier,
                            NewProductivityMultiplier = prov.ProductivityMultiplier,
                            Month = month,
                            Year = year
                        };

                        LastCompletedProjects.Add(completion);
                        Debug.Log(
                            $"[InfrastructureController] Tikinti Tamamlandı! (Ay {month:D2}, İl {year:D4}) -> " +
                            $"{prov.Name}: '{finishedProject}' bitdi! Məhsuldarlıq: {oldMultiplier * 100f:F0}% -> {prov.ProductivityMultiplier * 100f:F0}%");
                    }
                    else
                    {
                        Debug.Log(
                            $"[InfrastructureController] Tikinti davam edir (Ay {month:D2}, İl {year:D4}) -> " +
                            $"{prov.Name}: '{prov.ActiveConstruction}' ({prov.ConstructionMonthsLeft} ay qaldı)");
                    }

                    progressedProvinces.Add(prov);
                }

                if (anyChanged)
                {
                    conn.UpdateAll(provinces, runInTransaction: false);
                }
            });

            for (int i = 0; i < progressedProvinces.Count; i++)
            {
                OnConstructionProgressed?.Invoke(progressedProvinces[i]);
            }

            for (int i = 0; i < LastCompletedProjects.Count; i++)
            {
                OnConstructionCompleted?.Invoke(LastCompletedProjects[i]);
            }
        }

        private static int ResolveProvinceId(DatabaseManager db, string rawProvinceId)
        {
            if (string.IsNullOrWhiteSpace(rawProvinceId))
            {
                return 1;
            }

            string trimmed = rawProvinceId.Trim();
            if (int.TryParse(trimmed, out int parsedId) && parsedId > 0)
            {
                return parsedId;
            }

            // Əgər əyalət adı göndərilibsə (məs: "Anadolu")
            if (db != null && db.IsConnected)
            {
                Province byName = db.Connection.Table<Province>()
                    .FirstOrDefault(p => p.Name.ToLower() == trimmed.ToLower());
                if (byName != null)
                {
                    return byName.Id;
                }
            }

            var digits = new StringBuilder();
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsDigit(trimmed[i]))
                {
                    digits.Append(trimmed[i]);
                }
            }

            if (digits.Length > 0 && int.TryParse(digits.ToString(), out int extractedId) && extractedId > 0)
            {
                return extractedId;
            }

            return 1;
        }
    }
}
