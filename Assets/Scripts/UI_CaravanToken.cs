using System.Collections;
using GrandStrategy.Database.Models;
using UnityEngine;

namespace GrandStrategy.UI
{
    /// <summary>
    /// Faza 8.2 - 2D Qlobal Xəritədə hərəkət edən Fiziki Karvan Tokeni (UI_CaravanToken).
    /// Mənbə və Hədəf əyalətləri arasında TravelProgress-ə uyğun olaraq addım-addım irəliləyir.
    /// Yağmalandıqda qılınc toqquşması simvolu ("⚔️ YAĞMALANDI!") göstərərək məhv olur.
    /// </summary>
    public class UI_CaravanToken : MonoBehaviour
    {
        [Header("Karvan Məlumatları")]
        [SerializeField] private int caravanId;
        [SerializeField] private int ownerFactionId;
        [SerializeField] private string resourceType = "Silk";
        [SerializeField] private float resourceAmount = 50f;
        [SerializeField] private float travelProgress = 0f;

        [Header("Mövqe Nöqtələri")]
        [SerializeField] private Vector3 sourcePosition;
        [SerializeField] private Vector3 targetPosition;

        [Header("Vizual Komponentlər")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private TextMesh labelTextMesh;
        [SerializeField] private GameObject raidEffectObject;
        [SerializeField] private TextMesh raidEffectTextMesh;

        private Caravan _caravanData;
        private bool _isRaided;
        private bool _isCompleted;

        public int CaravanId => caravanId;
        public int OwnerFactionId => ownerFactionId;
        public string ResourceType => resourceType;
        public float ResourceAmount => resourceAmount;
        public float TravelProgress => travelProgress;
        public Caravan CaravanData => _caravanData;
        public bool IsRaided => _isRaided;
        public bool IsCompleted => _isCompleted;
        public Vector3 CurrentWorldPosition => transform.position;

        private void Awake()
        {
            EnsureComponents();
        }

        private void EnsureComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (labelTextMesh == null)
            {
                Transform labelTr = transform.Find("CaravanLabel");
                if (labelTr != null)
                {
                    labelTextMesh = labelTr.GetComponent<TextMesh>();
                }
                else
                {
                    var labelObj = new GameObject("CaravanLabel");
                    labelObj.transform.SetParent(transform, false);
                    labelObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                    labelTextMesh = labelObj.AddComponent<TextMesh>();
                    labelTextMesh.fontSize = 28;
                    labelTextMesh.characterSize = 0.05f;
                    labelTextMesh.alignment = TextAlignment.Center;
                    labelTextMesh.anchor = TextAnchor.MiddleCenter;
                    labelTextMesh.color = Color.yellow;
                }
            }

            // Yağmalanma effekti üçün obyekt
            if (raidEffectObject == null)
            {
                Transform raidTr = transform.Find("RaidEffect");
                if (raidTr != null)
                {
                    raidEffectObject = raidTr.gameObject;
                    raidEffectTextMesh = raidTr.GetComponent<TextMesh>();
                }
                else
                {
                    raidEffectObject = new GameObject("RaidEffect");
                    raidEffectObject.transform.SetParent(transform, false);
                    raidEffectObject.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                    raidEffectTextMesh = raidEffectObject.AddComponent<TextMesh>();
                    raidEffectTextMesh.fontSize = 32;
                    raidEffectTextMesh.characterSize = 0.06f;
                    raidEffectTextMesh.alignment = TextAlignment.Center;
                    raidEffectTextMesh.anchor = TextAnchor.MiddleCenter;
                    raidEffectTextMesh.color = Color.red;
                    raidEffectObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Tokeni məlumat bazasındakı Karvan məlumatı və koordinatlar ilə başladır.
        /// </summary>
        public void Initialize(Caravan data, Vector3 srcPos, Vector3 dstPos)
        {
            EnsureComponents();

            _caravanData = data;
            if (data != null)
            {
                caravanId = data.Id;
                ownerFactionId = data.OwnerFactionId;
                resourceType = data.ResourceType;
                resourceAmount = data.ResourceAmount;
                travelProgress = data.TravelProgress;
                _isRaided = data.IsRaided;
                _isCompleted = data.IsCompleted;
            }

            sourcePosition = srcPos;
            targetPosition = dstPos;

            ApplyResourceColorTheme();
            UpdatePosition(travelProgress);
        }

        /// <summary>
        /// Karvanın daşıdığı resursa görə rəng temasını tənzimləyir.
        /// </summary>
        private void ApplyResourceColorTheme()
        {
            if (spriteRenderer == null) return;

            // Əgər sprite yoxdursa sadə 2D dairəvi kvadrat yaradırıq
            if (spriteRenderer.sprite == null)
            {
                Texture2D tex = new Texture2D(32, 32);
                Color[] colors = new Color[32 * 32];
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
                tex.SetPixels(colors);
                tex.Apply();
                spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
            }

            transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // Resurs tipinə görə fərqli parlaq rənglər
            switch (resourceType)
            {
                case "Silk":
                    spriteRenderer.color = new Color(0.95f, 0.35f, 0.85f, 1f); // Bənövşəyi/İpək
                    break;
                case "Iron":
                    spriteRenderer.color = new Color(0.6f, 0.65f, 0.75f, 1f); // Polad/Dəmir
                    break;
                case "Horses":
                    spriteRenderer.color = new Color(0.85f, 0.55f, 0.25f, 1f); // Qəhvəyi/At
                    break;
                case "Gold":
                    spriteRenderer.color = new Color(1.0f, 0.85f, 0.15f, 1f); // Qızılı
                    break;
                case "Food":
                default:
                    spriteRenderer.color = new Color(0.35f, 0.85f, 0.35f, 1f); // Yaşıl/Ərzaq
                    break;
            }
        }

        /// <summary>
        /// TravelProgress faizinə görə xəritədəki cari mövqeyini hesablayır və mətni yeniləyir.
        /// </summary>
        public void UpdatePosition(float progress)
        {
            travelProgress = Mathf.Clamp(progress, 0f, 100f);
            float t = travelProgress / 100f;
            transform.position = Vector3.Lerp(sourcePosition, targetPosition, t);

            if (labelTextMesh != null)
            {
                string resIcon = GetResourceIcon(resourceType);
                labelTextMesh.text = $"{resIcon} {resourceAmount:F0} {resourceType}\n({travelProgress:F0}%)";
            }
        }

        private static string GetResourceIcon(string type)
        {
            switch (type)
            {
                case "Silk": return "🧶";
                case "Iron": return "⛏️";
                case "Horses": return "🐎";
                case "Gold": return "💰";
                case "Food": return "🌾";
                default: return "🐪";
            }
        }

        /// <summary>
        /// Karvan düşmən tərəfindən yağmalandıqda animasiya və effekt işə salır.
        /// </summary>
        public void PlayRaidEffect(System.Action onFinished = null)
        {
            _isRaided = true;
            if (raidEffectObject != null)
            {
                raidEffectObject.SetActive(true);
                if (raidEffectTextMesh != null)
                {
                    raidEffectTextMesh.text = "⚔️ YAĞMALANDI!";
                }
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }

            StartCoroutine(RaidAnimationRoutine(onFinished));
        }

        private IEnumerator RaidAnimationRoutine(System.Action onFinished)
        {
            float elapsed = 0f;
            Vector3 origScale = transform.localScale;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                float s = Mathf.PingPong(elapsed * 6f, 0.4f) + 0.5f;
                transform.localScale = origScale * s;
                yield return null;
            }

            onFinished?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// Karvan hədəfə uğurla çatdıqda vizual effekt.
        /// </summary>
        public void PlayArrivalEffect(System.Action onFinished = null)
        {
            _isCompleted = true;
            if (raidEffectObject != null)
            {
                raidEffectObject.SetActive(true);
                if (raidEffectTextMesh != null)
                {
                    raidEffectTextMesh.color = Color.yellow;
                    raidEffectTextMesh.text = "💰 ÇATDI!";
                }
            }

            StartCoroutine(ArrivalAnimationRoutine(onFinished));
        }

        private IEnumerator ArrivalAnimationRoutine(System.Action onFinished)
        {
            yield return new WaitForSeconds(0.4f);
            onFinished?.Invoke();
            Destroy(gameObject);
        }
    }
}
