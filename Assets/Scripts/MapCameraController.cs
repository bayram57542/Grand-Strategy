using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GrandStrategy.Map
{
    /// <summary>
    /// Faza 9.9 - Peşəkar Qlobal Xəritə Kamera İdarəedicisi (Pan & Zoom).
    /// Klaviatura (WASD / Oxlar), Siçan ilə sürüşdürmə (Right / Middle drag pan),
    /// hamar təkər zoom (min: 0.8f, max: 7.5f), sərhəd məhdudiyyəti (clamp),
    /// sürətli mərkəzləşdirmə (R / Home) və dinamik sərhəd qalınlığı tənzimləməsini təmin edir.
    /// Yeni və köhnə Input System-ə tam uyğun, istisna (exception) təhlükəsiz yazılmışdır.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour
    {
        private static MapCameraController _instance;
        public static MapCameraController Instance => _instance != null ? _instance : (_instance = UnityEngine.Object.FindAnyObjectByType<MapCameraController>());

        [Header("Kamera Komponenti")]
        [SerializeField] private Camera targetCamera;

        [Header("Hərəkət Sürəti (Pan Speed)")]
        [Tooltip("Klaviatura ilə hərəkət baza sürəti (Zoom səviyyəsinə mütənasib dəyişir)")]
        [SerializeField] private float panSpeed = 9.0f;
        [Tooltip("Kamera hərəkətinin yumşaqlığı")]
        [SerializeField] private float panSmoothSpeed = 14.0f;

        [Header("Zoom Tənzimləmələri")]
        [Tooltip("Ən yaxın zoom səviyyəsi (Bakı, Gəncə kimi kiçik əyalətləri detallı görmək üçün)")]
        [SerializeField] private float minZoom = 0.8f;
        [Tooltip("Ən uzaq zoom səviyyəsi (Bütün 563 əyaləti və dənizləri görmək üçün)")]
        [SerializeField] private float maxZoom = 7.5f;
        [Tooltip("Başlanğıc və mərkəzləşdirmə zoom dəyəri")]
        [SerializeField] private float defaultZoom = 5.33f;
        [Tooltip("Siçan təkəri zoom həssaslığı")]
        [SerializeField] private float zoomSensitivity = 1.35f;
        [Tooltip("Zoom keçidinin yumşaqlığı (Lerp sürəti)")]
        [SerializeField] private float zoomSmoothSpeed = 12.0f;

        [Header("Xəritə Sərhədləri (Map Bounds Clamping)")]
        [Tooltip("Kameranın hərəkət edə biləcəyi minimum X və Y sərhədləri")]
        [SerializeField] private Vector2 minBounds = new Vector2(-12.0f, -7.0f);
        [Tooltip("Kameranın hərəkət edə biləcəyi maksimum X və Y sərhədləri")]
        [SerializeField] private Vector2 maxBounds = new Vector2(12.0f, 7.0f);
        [Tooltip("Kameranın sabit Z oxu")]
        [SerializeField] private float cameraZ = -10.0f;

        [Header("Dinamik Sərhəd Qalınlığı")]
        [Tooltip("Yaxınlaşdıqda və ya uzaqlaşdıqda xəritə sərhəd qalınlığını avtomatik uyğunlaşdır")]
        [SerializeField] private bool enableDynamicBorders = true;
        [SerializeField] private float baseBorderWidth = 0.018f;
        [SerializeField] private float baseHighlightBorderWidth = 0.035f;
        [SerializeField] private float baseSelectedBorderWidth = 0.055f;

        // Vəziyyət dəyişənləri
        private Vector3 _targetPosition;
        private float _targetZoom;
        private bool _isDragging;
        private Vector2 _dragStartMouseScreen;
        private Vector3 _dragStartCameraPos;
        private float _lastBorderUpdateZoom;

        // Xarici çıxışlar (Properties)
        public Camera TargetCamera => targetCamera;
        public Vector3 TargetPosition => _targetPosition;
        public float TargetZoom => _targetZoom;
        public float MinZoom => minZoom;
        public float MaxZoom => maxZoom;
        public float DefaultZoom => defaultZoom;
        public Vector2 MinBounds => minBounds;
        public Vector2 MaxBounds => maxBounds;
        public bool IsDragging => _isDragging;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
                if (targetCamera == null) targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                targetCamera.orthographic = true;
                _targetZoom = targetCamera.orthographicSize > 0.1f ? targetCamera.orthographicSize : defaultZoom;
            }
            else
            {
                _targetZoom = defaultZoom;
            }

            _targetPosition = transform.position;
            _targetPosition.z = cameraZ;
            _lastBorderUpdateZoom = _targetZoom;
        }

        private void Start()
        {
            ClampTargetPosition();
            transform.position = _targetPosition;
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = _targetZoom;
            }
            UpdateDynamicBorders();
        }

        private void Update()
        {
            HandleKeyboardPanning();
            HandleMouseDragPan();
            HandleScrollZoom();
            HandleQuickReset();
        }

        private void LateUpdate()
        {
            ClampTargetPosition();

            // Mövqenin hamar tətbiqi (Sürüşdürmə zamanı dərhal reaksiya, klaviatura ilə hamar Lerp)
            if (_isDragging)
            {
                transform.position = _targetPosition;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.unscaledDeltaTime * panSmoothSpeed);
            }

            // Zoom dəyərinin hamar tətbiqi
            if (targetCamera != null)
            {
                float currentSize = targetCamera.orthographicSize;
                float newSize = Mathf.Lerp(currentSize, _targetZoom, Time.unscaledDeltaTime * zoomSmoothSpeed);
                targetCamera.orthographicSize = newSize;

                // Dinamik sərhəd qalınlığı yenilənməsi
                if (Mathf.Abs(newSize - _lastBorderUpdateZoom) > 0.08f)
                {
                    _lastBorderUpdateZoom = newSize;
                    UpdateDynamicBorders();
                }
            }
        }

        /// <summary>
        /// WASD və ya Ox düymələri ilə kameranı hərəkət etdirir.
        /// Zoom səviyyəsinə mütənasib olaraq hərəkət sürəti tənzimlənir.
        /// </summary>
        private void HandleKeyboardPanning()
        {
            if (_isDragging) return;

            float h = GetSafeHorizontalInput();
            float v = GetSafeVerticalInput();

            if (Mathf.Abs(h) > 0.001f || Mathf.Abs(v) > 0.001f)
            {
                float currentZoom = targetCamera != null ? targetCamera.orthographicSize : _targetZoom;
                float zoomRatio = currentZoom / defaultZoom;
                float effectiveSpeed = panSpeed * zoomRatio;

                Vector3 direction = new Vector3(h, v, 0f).normalized;
                _targetPosition += direction * (effectiveSpeed * Time.unscaledDeltaTime);
            }
        }

        /// <summary>
        /// Siçanın sağ və ya orta təkər düyməsi basıldıqda xəritəni tutub çəkir (Drag Pan).
        /// Sol klik toxunulmaz saxlanılır (əyalət seçimi üçün).
        /// </summary>
        private void HandleMouseDragPan()
        {
            if (IsSafeDragButtonDown())
            {
                _isDragging = true;
                _dragStartMouseScreen = GetSafeMousePosition();
                _dragStartCameraPos = _targetPosition;
            }
            else if (_isDragging && IsSafeDragButtonPressed())
            {
                Vector2 currentMouseScreen = GetSafeMousePosition();
                Vector2 screenDelta = currentMouseScreen - _dragStartMouseScreen;

                if (targetCamera != null && Screen.height > 0)
                {
                    // 1 pikselin dünya koordinatındakı ekvivalenti
                    float screenToWorld = (targetCamera.orthographicSize * 2.0f) / Screen.height;
                    Vector3 worldDelta = new Vector3(-screenDelta.x * screenToWorld, -screenDelta.y * screenToWorld, 0f);
                    _targetPosition = _dragStartCameraPos + worldDelta;
                }
            }
            else if (!IsSafeDragButtonPressed())
            {
                _isDragging = false;
            }
        }

        /// <summary>
        /// Siçan təkəri ilə kameranın orthographicSize dəyərini hamar artırıb-azaldır.
        /// </summary>
        private void HandleScrollZoom()
        {
            float scroll = GetSafeScrollWheelInput();
            if (Mathf.Abs(scroll) > 0.001f)
            {
                // UI üzərində olduqda xəritəni zoom etmə
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    float currentZoom = _targetZoom;
                    float zoomStep = scroll * zoomSensitivity * (currentZoom / defaultZoom);
                    _targetZoom = Mathf.Clamp(currentZoom - zoomStep, minZoom, maxZoom);
                }
            }
        }

        /// <summary>
        /// R və ya Home düyməsinə basıldıqda kameranı mərkəzə (0,0,-10) və baza zoom-a qaytarır.
        /// </summary>
        private void HandleQuickReset()
        {
            if (IsSafeResetPressed())
            {
                ResetCamera();
            }
        }

        /// <summary>
        /// Kameranın xəritədən kənara çıxmaması üçün mövqeyi məhdudlaşdırır (Clamp).
        /// </summary>
        private void ClampTargetPosition()
        {
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minBounds.x, maxBounds.x);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, minBounds.y, maxBounds.y);
            _targetPosition.z = cameraZ;
        }

        /// <summary>
        /// Kameranı başlanğıc mərkəzi mövqeyə və zoom səviyyəsinə qaytarır.
        /// </summary>
        public void ResetCamera()
        {
            _targetPosition = new Vector3(0f, 0f, cameraZ);
            _targetZoom = defaultZoom;
            _isDragging = false;

            transform.position = _targetPosition;
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = defaultZoom;
            }
            UpdateDynamicBorders();
            Debug.Log("[MapCameraController] Kamera mərkəzləşdirildi (0, 0, -10) | Zoom: 5.33f");
        }

        /// <summary>
        /// Proqramatik və ya test üçün kameranı istiqamət üzrə hərəkət etdirir.
        /// </summary>
        public void Pan(Vector2 direction, float deltaTime = 1f)
        {
            float currentZoom = targetCamera != null ? targetCamera.orthographicSize : _targetZoom;
            float zoomRatio = currentZoom / defaultZoom;
            float effectiveSpeed = panSpeed * zoomRatio;

            Vector3 delta = new Vector3(direction.x, direction.y, 0f) * (effectiveSpeed * deltaTime);
            _targetPosition += delta;
            ClampTargetPosition();
            transform.position = _targetPosition;
        }

        /// <summary>
        /// Kameranın hədəf mövqeyini birbaşa təyin edir (sərhədlərlə məhdudlaşdırılır).
        /// </summary>
        public void SetPosition(Vector2 position)
        {
            _targetPosition = new Vector3(position.x, position.y, cameraZ);
            ClampTargetPosition();
            transform.position = _targetPosition;
        }

        /// <summary>
        /// Proqramatik və ya test üçün zoom səviyyəsini dəyişdirir.
        /// </summary>
        public void Zoom(float delta)
        {
            _targetZoom = Mathf.Clamp(_targetZoom - delta, minZoom, maxZoom);
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = _targetZoom;
            }
            UpdateDynamicBorders();
        }

        /// <summary>
        /// Zoom səviyyəsini birbaşa təyin edir.
        /// </summary>
        public void SetZoom(float zoom)
        {
            _targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = _targetZoom;
            }
            UpdateDynamicBorders();
        }

        /// <summary>
        /// Kamera yaxınlaşdıqda sərhədlərin həddindən artıq qalın olmaması,
        /// uzaqlaşdıqda isə itməməsi üçün dinamik balans yaradır.
        /// </summary>
        public void UpdateDynamicBorders()
        {
            if (!enableDynamicBorders) return;

            float currentZoom = targetCamera != null ? targetCamera.orthographicSize : _targetZoom;
            float scaleFactor = Mathf.Clamp(currentZoom / defaultZoom, 0.45f, 1.35f);

            float dynamicDefault = baseBorderWidth * scaleFactor;
            float dynamicHighlight = baseHighlightBorderWidth * scaleFactor;
            float dynamicSelected = baseSelectedBorderWidth * scaleFactor;

            if (GISMapLoader.Instance != null && GISMapLoader.Instance.SpawnedProvinces.Count > 0)
            {
                var provinces = GISMapLoader.Instance.SpawnedProvinces;
                for (int i = 0; i < provinces.Count; i++)
                {
                    var prov = provinces[i];
                    if (prov != null)
                    {
                        prov.SetDynamicBorderWidth(dynamicDefault, dynamicHighlight, dynamicSelected);
                    }
                }
            }
        }

        #region Təhlükəsiz Giriş (Safe Cross-Compatible Input Helpers)

        private float GetSafeHorizontalInput()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    float val = 0f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) val += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) val -= 1f;
                    if (Mathf.Abs(val) > 0.01f) return val;
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                float h = Input.GetAxisRaw("Horizontal");
                if (Mathf.Abs(h) > 0.01f) return h;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) return -1f;
#endif
            }
            catch (System.Exception) { }

            return 0f;
        }

        private float GetSafeVerticalInput()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    float val = 0f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) val += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) val -= 1f;
                    if (Mathf.Abs(val) > 0.01f) return val;
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                float v = Input.GetAxisRaw("Vertical");
                if (Mathf.Abs(v) > 0.01f) return v;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) return 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) return -1f;
#endif
            }
            catch (System.Exception) { }

            return 0f;
        }

        private float GetSafeScrollWheelInput()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    float scrollY = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                    if (Mathf.Abs(scrollY) > 0.01f)
                    {
                        return Mathf.Clamp(scrollY / 120.0f, -1.0f, 1.0f);
                    }
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.001f) return Mathf.Sign(scroll);
#endif
            }
            catch (System.Exception) { }

            return 0f;
        }

        private bool IsSafeDragButtonDown()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    var m = UnityEngine.InputSystem.Mouse.current;
                    if (m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame) return true;
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) return true;
#endif
            }
            catch (System.Exception) { }

            return false;
        }

        private bool IsSafeDragButtonPressed()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    var m = UnityEngine.InputSystem.Mouse.current;
                    if (m.rightButton.isPressed || m.middleButton.isPressed) return true;
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) return true;
#endif
            }
            catch (System.Exception) { }

            return false;
        }

        private bool IsSafeResetPressed()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb.rKey.wasPressedThisFrame || kb.homeKey.wasPressedThisFrame) return true;
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Home)) return true;
#endif
            }
            catch (System.Exception) { }

            return false;
        }

        private Vector2 GetSafeMousePosition()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Mouse.current != null)
                {
                    return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }
#endif
            }
            catch (System.Exception) { }

            try
            {
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                return Input.mousePosition;
#endif
            }
            catch (System.Exception) { }

            return Vector2.zero;
        }

        #endregion
    }
}
