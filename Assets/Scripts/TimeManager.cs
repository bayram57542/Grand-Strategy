using System;
using System.Collections;
using UnityEngine;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 5 - Zaman Motoru (TimeManager):
    /// Arxa planda işləyən Coroutine (TickLoopCoroutine) vasitəsilə reallıqdakı hər 2 saniyəni (1x sürətdə)
    /// oyunda 1 Aya (1 Aylıq Tick) bərabər edir, hər 12 Aydan (12 Tick) bir isə İllik Yaşlanma (Aging)
    /// mərhələsini işə salır və nəticələri avtomatik olaraq SQLite bazasına yazır (Auto-save).
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        private static TimeManager _instance;
        public static TimeManager Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<TimeManager>());

        [Header("Zaman Motoru Konfiqurasiyası (Coroutine Tick System)")]
        [Tooltip("1x sürətdə 1 Ayın (1 Tick) keçməsi üçün tələb olunan real saniyə (2.0 saniyə = 1 Ay)")]
        [SerializeField, Min(0.1f)] private float secondsPerMonthAt1x = 2.0f;

        [Tooltip("Cari sürət çarpanı: 0 = Pause, 1 = 1x (2s/Ay), 2 = 2x (1s/Ay)")]
        [SerializeField] private int currentSpeedMultiplier = 0;

        private Coroutine _tickLoopCoroutine;
        private float _accumulatedMonthTimer;
        private int _lastActiveSpeedMultiplier = 1;
        private long _totalMonthlyTicks;

        public float SecondsPerMonthAt1x => secondsPerMonthAt1x;
        public int CurrentSpeedMultiplier => currentSpeedMultiplier;
        public bool IsPaused => currentSpeedMultiplier <= 0;
        public long TotalMonthlyTicks => _totalMonthlyTicks;

        public int CurrentMonth => GameManager.Instance != null && GameManager.Instance.CurrentMonth > 0
            ? GameManager.Instance.CurrentMonth
            : 1;

        public int CurrentYear => GameManager.Instance != null && GameManager.Instance.CurrentYear > 0
            ? GameManager.Instance.CurrentYear
            : 1066;

        public event Action<int, int, long> OnMonthlyTickCompleted;
        public event Action<int> OnYearlyTickCompleted;
        public event Action<int, bool> OnTimeStateChanged;

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

        private void OnEnable()
        {
            if (Application.isPlaying && _tickLoopCoroutine == null)
            {
                _tickLoopCoroutine = StartCoroutine(TickLoopCoroutine());
            }
        }

        private void OnDisable()
        {
            if (_tickLoopCoroutine != null)
            {
                StopCoroutine(_tickLoopCoroutine);
                _tickLoopCoroutine = null;
            }
        }

        /// <summary>
        /// Arxa planda işləyən Coroutine (Tick System):
        /// 1x sürətdə hər 2.0 saniyədə (2x sürətdə hər 1.0 saniyədə) 1 Aylıq Tick (StepMonth) icra edir.
        /// </summary>
        private IEnumerator TickLoopCoroutine()
        {
            while (true)
            {
                if (IsPaused)
                {
                    yield return null;
                    continue;
                }

                float effectiveInterval = Mathf.Max(0.1f, secondsPerMonthAt1x / Mathf.Max(1, currentSpeedMultiplier));
                _accumulatedMonthTimer += Time.deltaTime;

                if (_accumulatedMonthTimer >= effectiveInterval)
                {
                    _accumulatedMonthTimer -= effectiveInterval;
                    StepMonth();
                }

                yield return null;
            }
        }

        /// <summary>
        /// Zamanı dayandırır (Pause - ||).
        /// </summary>
        public void Pause()
        {
            SetSpeed(0);
        }

        /// <summary>
        /// Zamanı başladır / davam etdirir (Play - >).
        /// </summary>
        public void Play()
        {
            int targetSpeed = _lastActiveSpeedMultiplier > 0 ? _lastActiveSpeedMultiplier : 1;
            SetSpeed(targetSpeed);
        }

        /// <summary>
        /// Zaman sürətini təyin edir (0 = Pause, 1 = 1x, 2 = 2x).
        /// </summary>
        public void SetSpeed(int speedMultiplier)
        {
            int clampedSpeed = Mathf.Clamp(speedMultiplier, 0, 5);
            if (clampedSpeed > 0)
            {
                _lastActiveSpeedMultiplier = clampedSpeed;
            }

            currentSpeedMultiplier = clampedSpeed;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                GameSpeed mappedSpeed = clampedSpeed switch
                {
                    0 => GameSpeed.Paused,
                    1 => GameSpeed.Normal1x,
                    2 => GameSpeed.Fast2x,
                    3 => GameSpeed.VeryFast3x,
                    _ => GameSpeed.UltraFast5x
                };
                gm.SetGameSpeedFromTimeManager(mappedSpeed);
            }

            OnTimeStateChanged?.Invoke(currentSpeedMultiplier, IsPaused);
            Debug.Log($"[TimeManager] Zaman rejimi dəyişdirildi -> Sürət: {(IsPaused ? "PAUSE (0x)" : $"{currentSpeedMultiplier}x")} | Tarix: Ay {CurrentMonth:D2}, İl {CurrentYear:D4}");
        }

        /// <summary>
        /// Oyunu 1 Ay (1 Aylıq Tick) irəli aparır:
        /// - Aylıq İstehsal (Gold və Food artımı), İstehlak (Food azalması və Aclıq yoxlaması) və Doğum (Babies artımı) hesablanır.
        /// - Hər 12 Ay (1 İl) tamamlandıqda İllik Yaşlanma (Babies -> Youths -> Adults -> Elders -> Ölüm və Wounded -> Adults) icra olunur.
        /// - Bütün dəyişikliklər avtomatik olaraq SQLite bazasına yazılır (Auto-save).
        /// </summary>
        public void StepMonth()
        {
            GameManager gm = GameManager.Instance != null
                ? GameManager.Instance
                : FindAnyObjectByType<GameManager>();

            if (gm == null)
            {
                var gmGo = new GameObject("GameManager");
                gm = gmGo.AddComponent<GameManager>();
            }

            int yearBefore = gm.CurrentYear;
            gm.AdvanceOneMonthFromTimeManager(out int currentMonth, out int currentYear, out bool yearPassed);

            _totalMonthlyTicks++;
            OnMonthlyTickCompleted?.Invoke(currentMonth, currentYear, _totalMonthlyTicks);

            if (yearPassed && currentYear > yearBefore)
            {
                OnYearlyTickCompleted?.Invoke(currentYear);
            }
        }

        /// <summary>
        /// Verilmiş sayda Ayı (Tick-i) ardıcıl olaraq simulyasiya edir (məsələn, 12 Ay = 1 Tam İl).
        /// </summary>
        public void SimulateMonths(int monthsCount)
        {
            int count = Mathf.Max(1, monthsCount);
            for (int i = 0; i < count; i++)
            {
                StepMonth();
            }
        }
    }
}
