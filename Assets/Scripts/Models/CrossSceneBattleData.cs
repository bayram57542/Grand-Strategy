using System;

namespace GrandStrategy.Core
{
    /// <summary>
    /// Faza 10.13 - Qlobal xəritə (GlobalMapScene) ilə 2D Taktiki Döyüş səhnəsi (2DBattleScene / TacticalBattleScene)
    /// arasında aktiv döyüş məlumatlarını və döyüş nəticəsini daşıyan statik sinif.
    /// </summary>
    public static class CrossSceneBattleData
    {
        public static int ActiveProvinceId { get; set; }
        public static int AttackerArmyId { get; set; }
        public static int DefenderArmyId { get; set; }
        public static bool PlayerWon { get; set; }
        public static bool HasPendingResult { get; set; }
        public static int PlayerFactionId { get; set; } = 1;
        public static int EnemyArmyId { get; set; }

        public static void SetPendingBattle(
            int provinceId,
            int attackerArmyId,
            int defenderArmyId,
            int playerFactionId = 1,
            int enemyArmyId = 0)
        {
            ActiveProvinceId = provinceId;
            AttackerArmyId = attackerArmyId;
            DefenderArmyId = defenderArmyId;
            PlayerFactionId = playerFactionId > 0 ? playerFactionId : 1;
            EnemyArmyId = enemyArmyId;
            PlayerWon = false;
            HasPendingResult = false;
        }

        public static void RecordResult(bool playerWon)
        {
            PlayerWon = playerWon;
            HasPendingResult = true;
        }

        public static void Clear()
        {
            ActiveProvinceId = 0;
            AttackerArmyId = 0;
            DefenderArmyId = 0;
            PlayerWon = false;
            HasPendingResult = false;
            EnemyArmyId = 0;
        }
    }
}
