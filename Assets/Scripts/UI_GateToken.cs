using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.Tactical
{
    /// <summary>
    /// Faza 3.2 - Qala Qapısı Tokeni (UI_GateToken):
    /// Mühit Siege (Qala mühasirəsi) olduqda lövhənin mərkəzində (X = 0) yaradılır.
    /// 2000 HP cana malikdir, yerindən tərpənmir (moveSpeed = 0), hücum etmir (attackDamage = 0).
    /// Hücum edən (sol tərəf) tokenlər qapı sındırılmadan mərkəzi xətti keçə bilməz və
    /// məcburən ilk hədəf olaraq qapıya hücum edirlər.
    /// Sındırıldıqda ekranda "QALA QAPISI SINDIRILDI!" xəbərdarlığı çıxır və içəri keçid açılır.
    /// </summary>
    public class UI_GateToken : UI_BattalionToken
    {
        [Header("Qala Qapısı Referansları")]
        [SerializeField] private Text gateTitleText;

        public const float GateDefaultMaxHP = 2000f;
        public const string GateDestroyedMessage = "QALA QAPISI SINDIRILDI!";

        public string CurrentGateTitleLabel => gateTitleText != null ? gateTitleText.text : "QALA QAPISI";

        /// <summary>
        /// Qala qapısını ilkin olaraq 2000 HP və hərəkətsiz/hücumsuz statusda hazırlayır.
        /// </summary>
        public void InitializeGate(
            int defenderArmyId,
            int defenderFactionId,
            Vector2 spawnAnchoredPosition,
            float customMaxHp = GateDefaultMaxHP)
        {
            // Qapı müdafiə ordusunun (sağ tərəf) aktiv bir müdafiə obyekti kimi davranır
            InitializeToken(
                BattalionType.AdultInfantry,
                defenderArmyId,
                defenderFactionId,
                soldiersCount: 1,
                playerControlled: false,
                spawnAnchoredPosition);

            OverrideCombatStats(
                newMaxHp: customMaxHp,
                newAttackDamage: 0f,
                newMoveSpeed: 0f);

            RectTransform.sizeDelta = new Vector2(48f, 440f);
            AnchoredPosition = spawnAnchoredPosition;
            SetFacingDirection(Vector2.left); // Qapının ön tərəfi hücumçuların tərəfinə (sola) baxır

            EnsureGateVisuals();
        }

        private void EnsureGateVisuals()
        {
            ApplyTokenColorTheme();

            if (gateTitleText == null)
            {
                Transform titleTr = transform.Find("GateTitleText");
                if (titleTr != null)
                {
                    gateTitleText = titleTr.GetComponent<Text>();
                }
                else
                {
                    var titleGo = new GameObject("GateTitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    titleGo.transform.SetParent(transform, false);
                    var tr = titleGo.GetComponent<RectTransform>();
                    tr.anchorMin = new Vector2(0.5f, 1f);
                    tr.anchorMax = new Vector2(0.5f, 1f);
                    tr.sizeDelta = new Vector2(180f, 28f);
                    tr.anchoredPosition = new Vector2(0f, 22f);
                    gateTitleText = titleGo.GetComponent<Text>();
                    gateTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    gateTitleText.text = "QALA QAPISI";
                    gateTitleText.fontSize = 16;
                    gateTitleText.fontStyle = FontStyle.Bold;
                    gateTitleText.alignment = TextAnchor.MiddleCenter;
                    gateTitleText.color = new Color(0.95f, 0.90f, 0.70f, 1f);
                    gateTitleText.raycastTarget = false;

                    var outline = titleGo.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                    outline.effectDistance = new Vector2(1f, -1f);
                }
            }
        }

        protected override void ApplyTokenColorTheme()
        {
            Image body = TokenBodyImage;
            if (body != null)
            {
                // Qala Qapısı: Möhkəm dəmir-ağac qapı rəngi (Tünd qəhvəyi / bürünc-dəmir)
                body.color = new Color(0.38f, 0.24f, 0.15f, 1f);
            }
        }

        public override string ToString()
        {
            return $"[UI_GateToken | HP: {CurrentHP:F1}/{MaxHP:F1} | Pos: {AnchoredPosition}]";
        }
    }
}
