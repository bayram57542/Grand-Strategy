using System;
using System.Collections.Generic;
using UnityEngine;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Faza 9.6 - "1187-ci il: Səlahəddin və Atabəylər" Tarixi Ssenari İdarəedicisi.
    /// Qlobal xəritədəki 563 əyaləti 1187-ci ilin Yaxın Şərq və Qafqaz reallıqlarına uyğun olaraq
    /// tarixi dövlətlərə (Eldəgəzlər, Şirvanşahlar, Gürcüstan Krallığı, Bizans, Rum Sultanlığı, Əyyubilər və s.)
    /// mənimsədir və Age of History siyasi üslubunda rəngləyir.
    /// </summary>
    public class HistoricalScenarioManager : MonoBehaviour
    {
        private static HistoricalScenarioManager _instance;
        public static HistoricalScenarioManager Instance => _instance != null ? _instance : (_instance = UnityEngine.Object.FindAnyObjectByType<HistoricalScenarioManager>());

        [Header("Ssenari Tənzimləmələri")]
        [Tooltip("Xəritə yükləndikdə 1187 ssenarisi avtomatik tətbiq olunsun")]
        [SerializeField] private bool applyScenarioOnStart = true;

        [Tooltip("Eyni dövlətin əyalətləri arasında kartoqrafik incə ton fərqi (+/- 2.5%)")]
        [SerializeField] private bool applyTonalVariation = true;

        [Range(0f, 0.15f)]
        [SerializeField] private float tonalVariationFactor = 0.05f;

        [Header("Ssenari Məlumatları")]
        [SerializeField] private string scenarioName = "1187-ci il: Səlahəddin və Atabəylər";
        [SerializeField] private int assignedProvinceCount = 0;

        [Header("Wasteland (Boş / Neytral Ərazilər)")]
        [Tooltip("1187 ssenarisində heç bir fraksiyaya aid olmayan boş/neytral əyalətlərin poliqon rəngi (#D2B48C Solğun Qum)")]
        [SerializeField] private Color wastelandColor = new Color(0.824f, 0.706f, 0.549f, 0.95f); // #D2B48C Solğun Qum Rəngi
        [Tooltip("Wasteland ərazilərinin solğun sərhəd xətti")]
        [SerializeField] private Color wastelandBorderColor = new Color(0.55f, 0.48f, 0.38f, 0.35f);

        public Color WastelandColor => wastelandColor;
        public Color WastelandBorderColor => wastelandBorderColor;

        public string ScenarioName => scenarioName;
        public int AssignedProvinceCount => assignedProvinceCount;

        public event Action<int> OnScenarioApplied;

        [Serializable]
        public class HistoricalFaction
        {
            public int FactionId;
            public string Name;
            public string Leader;
            public Color BaseColor;
            public string Description;

            public HistoricalFaction(int id, string name, string leader, Color color, string desc)
            {
                FactionId = id;
                Name = name;
                Leader = leader;
                BaseColor = color;
                Description = desc;
            }
        }

        // 1187-ci ilin əsas tarixi fraksiyaları və rəsmi rəng palitrası
        public static readonly HistoricalFaction FactionEldegez = new HistoricalFaction(
            1, "Eldəgəzlər (Atabəylər)", "Qızıl Arslan",
            new Color(0.122f, 0.380f, 0.553f, 0.95f), // #1F618D Tünd Mavi
            "Azərbaycan Atabəyləri (Eldəgəzlər) dövləti. Mərkəzi Təbriz, Naxçıvan və Həmədan."
        );

        public static readonly HistoricalFaction FactionShirvan = new HistoricalFaction(
            2, "Şirvanşahlar Dövləti", "I Axsitan",
            new Color(0.180f, 0.800f, 0.443f, 0.95f), // #2ECC71 Zümrüd Yaşılı
            "Şimali Azərbaycan və Cənubi Dağıstanı əhatə edən Şirvanşahlar dövləti. Paytaxt Şamaxı və Bakı."
        );

        public static readonly HistoricalFaction FactionGeorgia = new HistoricalFaction(
            3, "Gürcüstan Krallığı", "Tamara (Tamar)",
            new Color(0.471f, 0.157f, 0.122f, 0.95f), // #78281F Bordo
            "Gürcüstanın Qızıl Dövrü. Çariça Tamara hakimiyyəti."
        );

        public static readonly HistoricalFaction FactionByzantine = new HistoricalFaction(
            4, "Bizans İmperiyası", "II İsaak Angelos",
            new Color(0.431f, 0.173f, 0.573f, 0.95f), // #6E2C92 İmperiya Bənövşəyisi
            "Konstantinopol mərkəzli Şərqi Roma İmperiyası, Yunanıstan, Qərbi Anadolu və sahillər."
        );

        public static readonly HistoricalFaction FactionRum = new HistoricalFaction(
            5, "Rum Sultanlığı", "II Qılınc Arslan",
            new Color(0.753f, 0.224f, 0.169f, 0.95f), // #C0392B Tünd Qırmızı
            "Anadolu Səlcuqlu Dövləti. Paytaxt Konya."
        );

        public static readonly HistoricalFaction FactionAyyubid = new HistoricalFaction(
            6, "Əyyubilər Dövləti", "Səlahəddin Əyyubi",
            new Color(0.945f, 0.769f, 0.059f, 0.95f), // #F1C40F Qızılı Sarı
            "Sultan Səlahəddinin idarə etdiyi, Misir, Suriya, Levant və Qüdsü birləşdirən imperiya."
        );

        public static readonly HistoricalFaction FactionAbbasid = new HistoricalFaction(
            7, "Abbasi Xilafəti", "Ən-Nasir Lidinillah",
            new Color(0.110f, 0.157f, 0.200f, 0.95f), // #1C2833 Qara / Tünd Boz
            "Bağdad mərkəzli Abbasi Xilafəti."
        );

        public static readonly HistoricalFaction FactionCilicia = new HistoricalFaction(
            8, "Kilikiya Erməni Çarlığı", "II Levon",
            new Color(0.533f, 0.306f, 0.627f, 0.95f), // #884EA0 Bənövşəyi-Bordo
            "Kilikiya (Çukurova) bölgəsində yerləşən knyazlıq."
        );

        public static readonly HistoricalFaction FactionPersiaSeljuk = new HistoricalFaction(
            9, "İran Atabəyləri / Səlcuq", "Salqurlar / Xarəzm",
            new Color(0.827f, 0.329f, 0.000f, 0.95f), // #D35400 Oxra / Narıncı
            "Fars, İsfahan, Kirman və Xorasan torpaqları."
        );

        public static readonly HistoricalFaction FactionCuman = new HistoricalFaction(
            10, "Dəşt-i Qıpçaq", "Köçək Xan",
            new Color(0.490f, 0.400f, 0.031f, 0.95f), // #7D6608 Qumlu Çöl Yaşılı
            "Qara dəniz və Xəzər şimalındakı Kuman-Qıpçaq çöl konfederasiyası."
        );

        public static readonly HistoricalFaction FactionBalkans = new HistoricalFaction(
            11, "Balkan Knyazlıqları", "Stefan Nemanya",
            new Color(0.180f, 0.251f, 0.325f, 0.95f), // #2E4053 Polad Mavisi
            "Serbiya və Bosniya knyazlıqları."
        );

        public static readonly HistoricalFaction FactionKhwarazm = new HistoricalFaction(
            12, "Xorəzmşahlar Dövləti", "Əlaəddin Təkəş",
            new Color(0.855f, 0.647f, 0.125f, 0.95f), // #DAA520 Xardal / Qızılı Kəhrəba
            "Mərkəzi Asiya, Xorasan və Mavəraünnəhri əhatə edən qüdrətli Xorəzmşahlar imperiyası."
        );

        public static readonly HistoricalFaction FactionTribes = new HistoricalFaction(
            13, "Yerli Tayfalar", "Müstəqil Əmirlər",
            new Color(0.627f, 0.502f, 0.376f, 0.95f), // #A08060 Torpaq / Çöl Tonu
            "Bölgədəki müstəqil çöl, dağ və bədəvi tayfa birlikləri."
        );

        public static readonly HistoricalFaction[] AllFactions = new HistoricalFaction[]
        {
            FactionEldegez,
            FactionShirvan,
            FactionGeorgia,
            FactionByzantine,
            FactionRum,
            FactionAyyubid,
            FactionAbbasid,
            FactionCilicia,
            FactionPersiaSeljuk,
            FactionCuman,
            FactionBalkans,
            FactionKhwarazm,
            FactionTribes
        };

        /// <summary>
        /// Dövlət və ya fraksiya adına əsasən 1187 fraksiyasını tapır.
        /// </summary>
        public HistoricalFaction GetFactionByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < AllFactions.Length; i++)
            {
                var f = AllFactions[i];
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf(f.Name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return f;
                }
            }
            return null;
        }

        /// <summary>
        /// Fraksiya adına əsasən 1187-ci ilin hökmdarını (ruler/leader) qaytarır.
        /// </summary>
        public string GetRulerForFaction(string factionName)
        {
            var f = GetFactionByName(factionName);
            return f != null && !string.IsNullOrEmpty(f.Leader) ? f.Leader : "Məlum deyil";
        }

        // Şirvanşahlar əyalətləri (Şimali Azərbaycan və Quba/Şamaxı bölgəsi)
        private static readonly HashSet<string> ShirvanProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Bakı", "Abşeron", "Sumqayıt", "Şamaxı", "Qobustan", "Quba", "Qusar",
            "Xaçmaz", "Dəvəçi", "Siyəzən", "Xizı", "İsmayıllı", "Qəbələ", "Ağsu"
        };

        // Eldəgəzlərə aid Şimal-Qərbi İran əyalətləri (Cənubi Azərbaycan, Həmədan, Zəncan və s.)
        private static readonly HashSet<string> EldegezIranProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "East Azarbaijan", "West Azarbaijan", "Ardebil", "Zanjan", "Hamadan",
            "Gilan", "Qazvin", "Kordestan", "Kermanshah", "Markazi", "Tehran", "Alborz", "Qom"
        };

        // Gürcüstan Krallığına aid Şimali Ermənistan əraziləri (Lori, Ani, Lori-Tavush)
        private static readonly HashSet<string> GeorgiaArmeniaProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Lori", "Tavush", "Shirak"
        };

        // Kilikiya bölgəsinə aid cənub sahil əyalətləri
        private static readonly HashSet<string> CiliciaTurkeyProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Adana", "Mersin", "Hatay", "Osmaniye"
        };

        // Bizansa aid Qərbi Anadolu, Pontus və Mərmərə sahilləri
        private static readonly HashSet<string> ByzantineTurkeyProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Istanbul", "Edirne", "Tekirdag", "Kirklareli", "Çanakkale", "Kocaeli", "Yalova",
            "Sakarya", "Bursa", "Balikesir", "Bilecik", "Izmir", "Manisa", "Aydin", "Mugla",
            "Denizli", "Usak", "Trabzon", "Rize", "Artvin", "Giresun", "Ordu", "Samsun",
            "Sinop", "Bartin", "Zonguldak", "Duzce"
        };

        // Əyyubilər/Zəngilər tabeliyindəki Şimali İraq (Cəzirə / Mosul / Ərbil)
        private static readonly HashSet<string> AyyubidIraqProvinces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ninawa", "Dihok", "Arbil", "As-Sulaymaniyyah", "At-Ta'mim", "Salah ad-Din"
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.OnMapLoaded += HandleMapLoaded;
            }
        }

        private void OnDisable()
        {
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.OnMapLoaded -= HandleMapLoaded;
            }
        }

        private void Start()
        {
            if (GISMapLoader.Instance != null)
            {
                GISMapLoader.Instance.OnMapLoaded += HandleMapLoaded;
            }

            if (applyScenarioOnStart)
            {
                Apply1187Scenario();
            }
        }

        private void HandleMapLoaded(List<MapInteractionController> provinces)
        {
            if (applyScenarioOnStart)
            {
                Apply1187Scenario(provinces);
            }
        }

        /// <summary>
        /// Inspector üzərindən sağ klikləyərək 1187 ssenarisini anında tətbiq etmək üçün Editor metodu.
        /// </summary>
        [ContextMenu("Load 1187 Scenario")]
        public int Load1187Scenario()
        {
            return Apply1187Scenario();
        }

        /// <summary>
        /// Xəritədəki bütün aktiv əyalətləri 1187-ci il ssenarisinə uyğun olaraq tarixi dövlətlərə bölür və rəngləyir.
        /// </summary>
        public int Apply1187Scenario(List<MapInteractionController> targetProvinces = null)
        {
            var provinces = targetProvinces;
            if (provinces == null || provinces.Count == 0)
            {
                if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
                {
                    provinces = new List<MapInteractionController>(GISMapLoader.Instance.SpawnedProvinces);
                }
                else
                {
                    provinces = new List<MapInteractionController>(UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude));
                }
            }

            if (provinces == null || provinces.Count == 0)
            {
                Debug.LogWarning("[HistoricalScenarioManager] Ssenari tətbiq etmək üçün xəritədə heç bir əyalət tapılmadı!");
                return 0;
            }

            int count = 0;
            var statistics = new Dictionary<string, int>();

            for (int i = 0; i < provinces.Count; i++)
            {
                var prov = provinces[i];
                if (prov == null) continue;

                var faction = DetermineFaction(prov.ProvinceName, prov.AdminCountry);
                if (faction == null || faction.FactionId <= 0 || string.Equals(faction.Name, "Neytral", StringComparison.OrdinalIgnoreCase) || string.Equals(faction.Name, "Wasteland", StringComparison.OrdinalIgnoreCase))
                {
                    // Boş / Neytral Ərazi (Wasteland)
                    Color wFill = applyTonalVariation
                        ? ApplyTonalVariation(wastelandColor, prov.ProvinceName, prov.ProvinceId, tonalVariationFactor * 0.4f)
                        : wastelandColor;
                    prov.SetWasteland(wFill, wastelandBorderColor);

                    string wKey = "Keçilməz Ərazi (Wasteland)";
                    if (!statistics.ContainsKey(wKey))
                    {
                        statistics[wKey] = 0;
                    }
                    statistics[wKey]++;
                }
                else
                {
                    Color fillColor = applyTonalVariation
                        ? ApplyTonalVariation(faction.BaseColor, prov.ProvinceName, prov.ProvinceId, tonalVariationFactor)
                        : faction.BaseColor;

                    prov.SetHistoricalOwner(faction.Name, faction.FactionId, fillColor, faction.Leader);

                    if (!statistics.ContainsKey(faction.Name))
                    {
                        statistics[faction.Name] = 0;
                    }
                    statistics[faction.Name]++;
                }
                count++;
            }

            assignedProvinceCount = count;
            Debug.Log($"[HistoricalScenarioManager] '{scenarioName}' ssenarisi uğurla tətbiq edildi! Cəmi: {count} əyalət.");
            foreach (var kvp in statistics)
            {
                Debug.Log($"  -> {kvp.Key}: {kvp.Value} əyalət");
            }

            OnScenarioApplied?.Invoke(count);
            return count;
        }

        /// <summary>
        /// Verilmiş müasir əyalət və ölkə adına əsasən 1187-ci ilin tarixi fraksiyasını təyin edir.
        /// </summary>
        public HistoricalFaction DetermineFaction(string provinceName, string adminCountry)
        {
            string admin = adminCountry ?? "";
            string name = provinceName ?? "";

            // 1. AZƏRBAYCAN
            if (string.Equals(admin, "Azerbaijan", StringComparison.OrdinalIgnoreCase))
            {
                if (ShirvanProvinces.Contains(name))
                {
                    return FactionShirvan;
                }
                return FactionEldegez;
            }

            // 2. İRAN
            if (string.Equals(admin, "Iran", StringComparison.OrdinalIgnoreCase))
            {
                if (EldegezIranProvinces.Contains(name))
                {
                    return FactionEldegez;
                }
                return FactionPersiaSeljuk;
            }

            // 3. ERMƏNİSTAN
            if (string.Equals(admin, "Armenia", StringComparison.OrdinalIgnoreCase))
            {
                if (GeorgiaArmeniaProvinces.Contains(name))
                {
                    return FactionGeorgia;
                }
                return FactionEldegez;
            }

            // 4. GÜRCÜSTAN
            if (string.Equals(admin, "Georgia", StringComparison.OrdinalIgnoreCase))
            {
                return FactionGeorgia;
            }

            // 5. TÜRKİYƏ / ANADOLU
            if (string.Equals(admin, "Turkey", StringComparison.OrdinalIgnoreCase))
            {
                if (CiliciaTurkeyProvinces.Contains(name))
                {
                    return FactionCilicia;
                }
                if (ByzantineTurkeyProvinces.Contains(name))
                {
                    return FactionByzantine;
                }
                return FactionRum;
            }

            // 6. ƏYYUBİLƏR (Misir, Suriya, İordaniya, Livan, Fələstin)
            if (string.Equals(admin, "Egypt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Syria", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Jordan", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Lebanon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Israel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Gaza Strip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "West Bank", StringComparison.OrdinalIgnoreCase))
            {
                return FactionAyyubid;
            }

            // 7. İRAQ (Şimal: Əyyubilər/Zəngilər; Mərkəz və Cənub: Abbasi Xilafəti)
            if (string.Equals(admin, "Iraq", StringComparison.OrdinalIgnoreCase))
            {
                if (AyyubidIraqProvinces.Contains(name))
                {
                    return FactionAyyubid;
                }
                return FactionAbbasid;
            }

            // 8. BİZANS İMPERİYASI (Yunanıstan, Kipr, Bolqarıstan, Makedoniya)
            if (string.Equals(admin, "Greece", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Cyprus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Northern Cyprus", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Akrotiri Sovereign Base Area", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Dhekelia Sovereign Base Area", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Bulgaria", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Macedonia", StringComparison.OrdinalIgnoreCase))
            {
                return FactionByzantine;
            }

            // 9. BALKAN KNYAZLIQLARI (Serbiya, Çernoqoriya, Kosovo, Bosniya, Albaniya)
            if (string.Equals(admin, "Republic of Serbia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Montenegro", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Kosovo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Bosnia and Herzegovina", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Albania", StringComparison.OrdinalIgnoreCase))
            {
                return FactionBalkans;
            }

            // 10. DƏŞT-İ QIPÇAQ (Şimal və Qara dəniz çölləri: Rusiya, Ukrayna, Moldova, Qazaxıstan, Rumıniya)
            if (string.Equals(admin, "Russia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Ukraine", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Moldova", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Kazakhstan", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Romania", StringComparison.OrdinalIgnoreCase))
            {
                return FactionCuman;
            }

            // 11. ƏRƏBİSTAN VƏ KÜVEYT (Abbasi Xilafəti və Ərəb Əmirlikləri)
            if (string.Equals(admin, "Saudi Arabia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Kuwait", StringComparison.OrdinalIgnoreCase))
            {
                return FactionAbbasid;
            }

            // 12. XORƏZMŞAHLAR / MƏRKƏZİ ASİYA (Türkmənistan, Özbəkistan)
            if (string.Equals(admin, "Turkmenistan", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(admin, "Uzbekistan", StringComparison.OrdinalIgnoreCase))
            {
                return FactionKhwarazm;
            }

            // 13. DİGƏR / YERLİ TAYFALAR
            return FactionTribes;
        }

        /// <summary>
        /// Kartoqrafik estetik üçün əyalətin rənginə kiçik (+/- 2.5%) təbii ton variasiyası qatır.
        /// </summary>
        public static Color ApplyTonalVariation(Color baseColor, string provinceName, int provinceId, float factor = 0.05f)
        {
            int hash = (provinceName != null ? provinceName.GetHashCode() : 0) ^ (provinceId * 397);
            float jitter = ((Mathf.Abs(hash) % 100) / 100f - 0.5f) * factor;

            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + jitter);
            s = Mathf.Clamp01(s + jitter * 0.5f);
            Color res = Color.HSVToRGB(h, s, v);
            res.a = baseColor.a;
            return res;
        }

        /// <summary>
        /// Hazırkı dövlətlər üzrə əyalət statistikalarını qaytarır.
        /// </summary>
        public Dictionary<string, int> GetScenarioStatistics()
        {
            var dict = new Dictionary<string, int>();
            var provinces = GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0
                ? GISMapLoader.Instance.SpawnedProvinces
                : (IReadOnlyList<MapInteractionController>)UnityEngine.Object.FindObjectsByType<MapInteractionController>(FindObjectsInactive.Exclude);

            foreach (var prov in provinces)
            {
                if (prov == null) continue;
                string owner = prov.IsWasteland ? "Keçilməz Ərazi (Wasteland)" : prov.ProvinceOwner;
                if (!dict.ContainsKey(owner)) dict[owner] = 0;
                dict[owner]++;
            }
            return dict;
        }
    }
}
