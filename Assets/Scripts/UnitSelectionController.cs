using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandStrategy.Tactical
{
    /// <summary>
    /// 2D Taktiki Lövhə (UI Canvas) İdarəetmə Sistemi:
    /// - 3D Raycast əvəzinə UI Canvas üzərində Sol klik (Single Select) və ya Çərçivəyə alaraq (UI Box Select)
    ///   oyunçunun UI_BattalionToken birliklərini seçir.
    /// - Canvas üzərində Sağ klik etdikdə seçilmiş tokenlərə həmin nöqtəyə (MoveTo) getməyi əmr edir.
    /// </summary>
    public class UnitSelectionController : MonoBehaviour
    {
        private static UnitSelectionController _instance;
        public static UnitSelectionController Instance => _instance != null ? _instance : (_instance = FindAnyObjectByType<UnitSelectionController>());

        [Header("2D UI Lövhə və Seçim Tənzimləmələri")]
        [SerializeField] private Canvas tacticalCanvas;
        [SerializeField] private RectTransform tacticalBoardRect;
        [SerializeField] private float dragThresholdPixels = 10f;

        [Header("Faza 3.1 - Kalkan Duvarı (Shield Wall) UI Düyməsi")]
        [SerializeField] private UnityEngine.UI.Button shieldWallButton;
        [SerializeField] private UnityEngine.UI.Text shieldWallButtonText;

        [Header("2D Birgə Hərəkət (Formasiya) Parametrləri")]
        [SerializeField] private int formationRowsPerColumn = 4;
        [SerializeField] private float formationSpacingY = 74f;
        [SerializeField] private float formationSpacingX = 68f;

        private readonly List<UI_BattalionToken> _selectedUnits = new List<UI_BattalionToken>();

        private bool _isDraggingSelectionBox;
        private Vector2 _dragStartScreenPos;
        private Vector2 _currentMouseScreenPos;

        private Texture2D _boxFillTexture;
        private Texture2D _boxBorderTexture;

        public IReadOnlyList<UI_BattalionToken> SelectedUnits => _selectedUnits;
        public UnityEngine.UI.Button ShieldWallButton => shieldWallButton;
        public UnityEngine.UI.Text ShieldWallButtonText => shieldWallButtonText;

        public void ConfigureShieldWallUI(UnityEngine.UI.Button button, UnityEngine.UI.Text buttonText)
        {
            shieldWallButton = button;
            shieldWallButtonText = buttonText;
            if (shieldWallButton != null)
            {
                shieldWallButton.onClick.RemoveListener(ToggleSelectedUnitsShieldWall);
                shieldWallButton.onClick.AddListener(ToggleSelectedUnitsShieldWall);
            }
            RefreshShieldWallButtonUI();
        }

        public void ConfigureCanvasAndBoard(Canvas canvas, RectTransform boardRect)
        {
            tacticalCanvas = canvas;
            tacticalBoardRect = boardRect;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ResolveBoardReferences();
        }

        private void Start()
        {
            if (shieldWallButton != null)
            {
                shieldWallButton.onClick.RemoveListener(ToggleSelectedUnitsShieldWall);
                shieldWallButton.onClick.AddListener(ToggleSelectedUnitsShieldWall);
            }
            RefreshShieldWallButtonUI();
        }

        private void ResolveBoardReferences()
        {
            if (tacticalBoardRect == null && TacticalBattleManager.Instance != null)
            {
                tacticalBoardRect = TacticalBattleManager.Instance.TacticalBoardRect;
            }

            if (tacticalCanvas == null && tacticalBoardRect != null)
            {
                tacticalCanvas = tacticalBoardRect.GetComponentInParent<Canvas>();
            }
        }

        private Camera GetCanvasEventCamera()
        {
            ResolveBoardReferences();
            if (tacticalCanvas == null || tacticalCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return tacticalCanvas.worldCamera != null ? tacticalCanvas.worldCamera : Camera.main;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            ResolveBoardReferences();
            Vector2 mousePos = mouse.position.ReadValue();

            // 1. Sol düymə basıldı (Başlanğıc nöqtəsi)
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _dragStartScreenPos = mousePos;
                _currentMouseScreenPos = mousePos;
                _isDraggingSelectionBox = false;
            }

            // 2. Sol düymə basılı saxlanılır (UI Box Select)
            if (mouse.leftButton.isPressed)
            {
                _currentMouseScreenPos = mousePos;
                if (Vector2.Distance(_dragStartScreenPos, _currentMouseScreenPos) >= dragThresholdPixels)
                {
                    _isDraggingSelectionBox = true;
                }
            }

            // 3. Sol düymə buraxıldı (Tək token seçimi və ya UI Box Select tamamlanması)
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (_isDraggingSelectionBox)
                {
                    Rect selectionRect = GetScreenRect(_dragStartScreenPos, _currentMouseScreenPos);
                    SelectUnitsInScreenRect(selectionRect);
                    _isDraggingSelectionBox = false;
                }
                else
                {
                    HandleSingleLeftClick(mousePos);
                }
            }

            // 4. Sağ klik (Seçilmiş tokenləri Canvas üzərindəki hədəf nöqtəyə doğru birgə hərəkət etdir)
            if (mouse.rightButton.wasPressedThisFrame)
            {
                HandleRightClickMove(mousePos);
            }

            // 5. Kalkan Duvarı klaviatura qısayolu (S və ya Q düyməsi)
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.sKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame))
            {
                ToggleSelectedUnitsShieldWall();
            }
        }

        private void HandleSingleLeftClick(Vector2 screenPosition)
        {
            Camera uiCam = GetCanvasEventCamera();
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            IReadOnlyList<UI_BattalionToken> candidates = battleManager != null
                ? battleManager.ActiveBattalions
                : FindObjectsByType<UI_BattalionToken>(FindObjectsSortMode.None);

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                UI_BattalionToken token = candidates[i];
                if (token == null || token.IsDead || !token.gameObject.activeInHierarchy || !token.IsPlayerControlled)
                {
                    continue;
                }

                if (RectTransformUtility.RectangleContainsScreenPoint(token.RectTransform, screenPosition, uiCam))
                {
                    SelectSingleUnit(token);
                    return;
                }
            }

            ClearSelection();
        }

        private void HandleRightClickMove(Vector2 screenPosition)
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            ResolveBoardReferences();
            if (tacticalBoardRect == null)
            {
                return;
            }

            Camera uiCam = GetCanvasEventCamera();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(tacticalBoardRect, screenPosition, uiCam, out Vector2 boardLocalPos))
            {
                IssueMoveCommandToPosition(boardLocalPos);
            }
        }

        /// <summary>
        /// Tək bir 2D tokeni seçir və onun SelectionRing-ni aktivləşdirir.
        /// </summary>
        public void SelectSingleUnit(UI_BattalionToken token)
        {
            ClearSelection();

            if (token == null || token.IsDead || !token.IsPlayerControlled)
            {
                RefreshShieldWallButtonUI();
                return;
            }

            token.SetSelected(true);
            _selectedUnits.Add(token);
            RefreshShieldWallButtonUI();
            Debug.Log($"[UnitSelectionController] Tək 2D Token seçildi: {token.name} ({token.Type}, Speed: {token.MoveSpeed})");
        }

        /// <summary>
        /// Ekran düzbucaqlısı (Screen Rect) daxilinə düşən bütün oyunçu tokenlərini seçir.
        /// </summary>
        public int SelectUnitsInScreenRect(Rect screenRect)
        {
            ClearSelection();

            Camera uiCam = GetCanvasEventCamera();
            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            IReadOnlyList<UI_BattalionToken> candidates = battleManager != null
                ? battleManager.ActiveBattalions
                : FindObjectsByType<UI_BattalionToken>(FindObjectsSortMode.None);

            for (int i = 0; i < candidates.Count; i++)
            {
                UI_BattalionToken token = candidates[i];
                if (token == null || token.IsDead || !token.gameObject.activeInHierarchy || !token.IsPlayerControlled)
                {
                    continue;
                }

                Vector2 tokenScreenPos = RectTransformUtility.WorldToScreenPoint(uiCam, token.RectTransform.position);
                if (screenRect.Contains(tokenScreenPos, true))
                {
                    token.SetSelected(true);
                    _selectedUnits.Add(token);
                }
            }

            RefreshShieldWallButtonUI();
            Debug.Log($"[UnitSelectionController] UI Box Select (ScreenRect) -> Seçilmiş token sayı: {_selectedUnits.Count}");
            return _selectedUnits.Count;
        }

        /// <summary>
        /// TacticalBoardCanvas koordinatlarındakı (anchoredPosition) çərçivə daxilində olan bütün oyunçu tokenlərini seçir.
        /// </summary>
        public int SelectUnitsInCanvasRect(Rect canvasAnchoredRect)
        {
            ClearSelection();

            TacticalBattleManager battleManager = TacticalBattleManager.Instance;
            IReadOnlyList<UI_BattalionToken> candidates = battleManager != null
                ? battleManager.ActiveBattalions
                : FindObjectsByType<UI_BattalionToken>(FindObjectsSortMode.None);

            for (int i = 0; i < candidates.Count; i++)
            {
                UI_BattalionToken token = candidates[i];
                if (token == null || token.IsDead || !token.gameObject.activeInHierarchy || !token.IsPlayerControlled)
                {
                    continue;
                }

                if (canvasAnchoredRect.Contains(token.AnchoredPosition, true))
                {
                    token.SetSelected(true);
                    _selectedUnits.Add(token);
                }
            }

            RefreshShieldWallButtonUI();
            Debug.Log($"[UnitSelectionController] UI Box Select (CanvasRect) -> Seçilmiş token sayı: {_selectedUnits.Count}");
            return _selectedUnits.Count;
        }

        /// <summary>
        /// Bütün seçilmiş tokenlərin seçimini ləğv edir.
        /// </summary>
        public void ClearSelection()
        {
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                if (_selectedUnits[i] != null)
                {
                    _selectedUnits[i].SetSelected(false);
                }
            }

            _selectedUnits.Clear();
            RefreshShieldWallButtonUI();
        }

        /// <summary>
        /// Seçilmiş bütün oyunçu tokenlərinin Kalkan Duvarı (Shield Wall) rejimini açır və ya bağlayır.
        /// </summary>
        public void ToggleSelectedUnitsShieldWall()
        {
            if (_selectedUnits.Count == 0)
            {
                return;
            }

            bool anyInactive = false;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                if (_selectedUnits[i] != null && !_selectedUnits[i].IsDead && !_selectedUnits[i].IsShieldWallActive)
                {
                    anyInactive = true;
                    break;
                }
            }

            bool targetState = anyInactive;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                UI_BattalionToken token = _selectedUnits[i];
                if (token != null && !token.IsDead && token.IsPlayerControlled)
                {
                    token.SetShieldWall(targetState);
                }
            }

            RefreshShieldWallButtonUI();
        }

        public void RefreshShieldWallButtonUI()
        {
            if (shieldWallButton == null)
            {
                return;
            }

            if (_selectedUnits.Count == 0)
            {
                shieldWallButton.gameObject.SetActive(false);
                return;
            }

            shieldWallButton.gameObject.SetActive(true);

            bool allActive = true;
            for (int i = 0; i < _selectedUnits.Count; i++)
            {
                UI_BattalionToken token = _selectedUnits[i];
                if (token != null && !token.IsDead && !token.IsShieldWallActive)
                {
                    allActive = false;
                    break;
                }
            }

            if (shieldWallButtonText != null)
            {
                shieldWallButtonText.text = allActive ? "Kalkan Duvarını Dağıt" : "Kalkan Duvarı Qur";
            }
        }

        /// <summary>
        /// Seçilmiş tokenləri Canvas üzərindəki hədəf anchoredPosition nöqtəsinə doğru
        /// Vector2.MoveTowards vasitəsilə birgə (formasiya ilə) hərəkət etdirir.
        /// </summary>
        public int IssueMoveCommandToPosition(Vector2 targetAnchoredCenter)
        {
            int movedCount = 0;
            int count = _selectedUnits.Count;
            if (count == 0)
            {
                return 0;
            }

            int rowsPerCol = Mathf.Max(1, Mathf.Min(formationRowsPerColumn, count));

            for (int i = 0; i < count; i++)
            {
                UI_BattalionToken token = _selectedUnits[i];
                if (token == null || token.IsDead || !token.gameObject.activeInHierarchy)
                {
                    continue;
                }

                int col = i / rowsPerCol;
                int row = i % rowsPerCol;
                int unitsInCol = Mathf.Min(rowsPerCol, count - (col * rowsPerCol));

                float staggeredY = (col % 2 == 1) ? (formationSpacingY * 0.35f) : 0f;
                float colHeight = (unitsInCol - 1) * formationSpacingY;
                float offsetY = (colHeight * 0.5f) - (row * formationSpacingY) + staggeredY;
                float offsetX = -(col * formationSpacingX);

                Vector2 tokenDestination = count == 1
                    ? targetAnchoredCenter
                    : targetAnchoredCenter + new Vector2(offsetX, offsetY);

                if (token.MoveTo(tokenDestination))
                {
                    movedCount++;
                }
            }

            Debug.Log($"[UnitSelectionController] 2D Sağ Klik Hərəkət Əmri -> {movedCount}/{count} token {targetAnchoredCenter} nöqtəsinə doğru hərəkətə başladı.");
            return movedCount;
        }

        private void OnGUI()
        {
            if (!_isDraggingSelectionBox)
            {
                return;
            }

            EnsureBoxTextures();

            Vector2 p1 = new Vector2(_dragStartScreenPos.x, Screen.height - _dragStartScreenPos.y);
            Vector2 p2 = new Vector2(_currentMouseScreenPos.x, Screen.height - _currentMouseScreenPos.y);

            Rect guiRect = Rect.MinMaxRect(
                Mathf.Min(p1.x, p2.x),
                Mathf.Min(p1.y, p2.y),
                Mathf.Max(p1.x, p2.x),
                Mathf.Max(p1.y, p2.y));

            GUI.DrawTexture(guiRect, _boxFillTexture);
            DrawRectBorder(guiRect, 2f, _boxBorderTexture);
        }

        private void EnsureBoxTextures()
        {
            if (_boxFillTexture == null)
            {
                _boxFillTexture = new Texture2D(1, 1);
                _boxFillTexture.SetPixel(0, 0, new Color(0.20f, 0.75f, 0.95f, 0.22f));
                _boxFillTexture.Apply();
            }

            if (_boxBorderTexture == null)
            {
                _boxBorderTexture = new Texture2D(1, 1);
                _boxBorderTexture.SetPixel(0, 0, new Color(0.35f, 0.88f, 1.0f, 0.90f));
                _boxBorderTexture.Apply();
            }
        }

        private static void DrawRectBorder(Rect rect, float thickness, Texture2D tex)
        {
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), tex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), tex);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), tex);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), tex);
        }

        private static Rect GetScreenRect(Vector2 start, Vector2 end)
        {
            return Rect.MinMaxRect(
                Mathf.Min(start.x, end.x),
                Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x),
                Mathf.Max(start.y, end.y));
        }
    }
}
