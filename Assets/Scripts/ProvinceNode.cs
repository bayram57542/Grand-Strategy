using GrandStrategy.Database.Models;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GrandStrategy.Map
{
    /// <summary>
    /// 2D Qlobal Xəritədə tək bir əyaləti (Province) təmsil edən interaktiv obyekt (Prefab komponenti).
    /// Həm OnMouseDown, həm də IPointerClickHandler vasitəsilə tıklamağı dəstəkləyir.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class ProvinceNode : MonoBehaviour, IPointerClickHandler
    {
        [Header("Əyalət Məlumatları")]
        [SerializeField] private int provinceId;
        [SerializeField] private string provinceName;
        [SerializeField] private string climate;
        [SerializeField] private int ownerFactionId;
        [SerializeField] private int developmentLevel;

        [Header("Vizual Komponentlər")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D boxCollider;
        [SerializeField] private TextMesh labelTextMesh;

        [Header("Rəng Konfiqurasiyası")]
        [SerializeField] private Color temperateColor = new Color(0.28f, 0.58f, 0.34f, 1f);
        [SerializeField] private Color aridColor = new Color(0.76f, 0.58f, 0.28f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.82f, 0.25f, 1f);

        private Color _baseColor;
        private bool _isSelected;
        private float _lastClickTime = -1f;

        public int ProvinceId => provinceId;
        public string ProvinceName => provinceName;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
            }
        }

        /// <summary>
        /// DatabaseManager-dən oxunan Province modeli ilə bu node-u konfiqurasiya edir.
        /// </summary>
        public void Setup(Province provinceData)
        {
            if (provinceData == null)
            {
                return;
            }

            provinceId = provinceData.Id;
            provinceName = provinceData.Name;
            climate = provinceData.Climate;
            ownerFactionId = provinceData.OwnerFactionId;
            developmentLevel = provinceData.DevelopmentLevel;

            gameObject.name = $"ProvinceNode_{provinceName}_{provinceId}";

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            bool isArid = !string.IsNullOrEmpty(climate) &&
                          climate.IndexOf("Quraq", System.StringComparison.OrdinalIgnoreCase) >= 0;
            _baseColor = isArid ? aridColor : temperateColor;

            UpdateVisualState();

            if (labelTextMesh != null)
            {
                labelTextMesh.text = $"{provinceName}\n({climate})";
            }
        }

        /// <summary>
        /// Oyunçu əyalətin üzərinə tıkladıqda (OnMouseDown) çağırılır.
        /// </summary>
        private void OnMouseDown()
        {
            if (Time.unscaledTime - _lastClickTime < 0.05f)
            {
                return;
            }

            _lastClickTime = Time.unscaledTime;
            SelectProvince();
        }

        /// <summary>
        /// EventSystem + Physics2DRaycaster üzərindən tıklama dəstəyi (New Input System üçün).
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - _lastClickTime < 0.05f)
            {
                return;
            }

            _lastClickTime = Time.unscaledTime;
            SelectProvince();
        }

        /// <summary>
        /// Bu əyaləti seçilmiş kimi işarələyir və UIManager-dəki Əyalət Məlumat Panelini açır.
        /// </summary>
        public void SelectProvince()
        {
            if (MapController.Instance != null)
            {
                MapController.Instance.OnProvinceNodeSelected(this);
            }

            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.Instance.ShowProvincePanel(provinceId);
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = _isSelected ? selectedColor : _baseColor;
            }
        }
    }
}
