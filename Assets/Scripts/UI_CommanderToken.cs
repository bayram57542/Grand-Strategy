using UnityEngine;
using UnityEngine.UI;

namespace GrandStrategy.Tactical
{
    /// <summary>
    /// Faza 6B - Sərkərdə Tokeni (UI_CommanderToken):
    /// Ordunun Lider-i (sərkərdəsi) varsa, 2D Taktiki Lövhədə ordunun arxa-mərkəzində yaradılan
    /// ulduz ikonlu xüsusi token. UI_BattalionToken-in bütün hərəkət/döyüş məntiqini miras alır,
    /// əlavə olaraq Lider kimliyini (LeaderId, LeaderName, IsKing) saxlayır.
    /// "Kral"ın Ulduz tokeni ölərsə, TacticalBattleManager "KRAL ÖLDÜ! TAXT BÖHRANI!" yazısını göstərir.
    /// </summary>
    public class UI_CommanderToken : UI_BattalionToken
    {
        [Header("Sərkərdə (Lider) Kimliyi")]
        [SerializeField] private int leaderId;
        [SerializeField] private string leaderName = "Sərkərdə";
        [SerializeField] private bool isKing;
        [SerializeField] private bool isRebelLeader;

        [Header("Sərkərdə UI Referansları")]
        [SerializeField] private Text leaderNameText;

        public int LeaderId => leaderId;
        public string LeaderName => leaderName;
        public bool IsKing => isKing;
        public bool IsRebelLeader => isRebelLeader;
        public string CurrentLeaderNameLabel => leaderNameText != null ? leaderNameText.text : leaderName;

        /// <summary>
        /// Sərkərdə tokenini lider məlumatları ilə konfiqurasiya edir və döyüş statlarını təyin edir.
        /// </summary>
        public void InitializeCommander(
            int owningArmyId,
            int factionId,
            int commanderLeaderId,
            string commanderName,
            bool commanderIsKing,
            bool commanderIsRebel,
            bool playerControlled,
            Vector2 spawnAnchoredPosition)
        {
            leaderId = commanderLeaderId;
            leaderName = string.IsNullOrWhiteSpace(commanderName) ? "Sərkərdə" : commanderName;
            isKing = commanderIsKing;
            isRebelLeader = commanderIsRebel;

            // Sərkərdənin şəxsi mühafizə dəstəsi (50 əsgər) - Ağır Piyada kimi davranır
            InitializeToken(
                BattalionType.AdultInfantry,
                owningArmyId,
                factionId,
                50,
                playerControlled,
                spawnAnchoredPosition);

            // Sərkərdə daha dözümlüdür, lakin daha yavaş hərəkət edir
            OverrideCombatStats(newMaxHp: 150f, newAttackDamage: 10f, newMoveSpeed: 85f);

            if (leaderNameText == null)
            {
                Transform nameTr = transform.Find("LeaderNameText");
                if (nameTr != null)
                {
                    leaderNameText = nameTr.GetComponent<Text>();
                }
            }

            if (leaderNameText != null)
            {
                leaderNameText.text = isKing ? $"★ {leaderName} ★" : leaderName;
            }

            ApplyTokenColorTheme();
        }

        /// <summary>
        /// Sərkərdə ulduzunun rəngi: Kral - parlaq qızılı, Üsyançı - bənövşəyi, digər sərkərdələr - gümüşü.
        /// </summary>
        protected override void ApplyTokenColorTheme()
        {
            Image body = TokenBodyImage;
            if (body == null)
            {
                return;
            }

            if (isKing)
            {
                body.color = new Color(1.00f, 0.82f, 0.18f, 1f);
            }
            else if (isRebelLeader)
            {
                body.color = new Color(0.72f, 0.30f, 0.92f, 1f);
            }
            else
            {
                body.color = new Color(0.86f, 0.88f, 0.92f, 1f);
            }
        }
    }
}
