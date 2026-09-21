namespace TheLastKnight.Core
{
    public enum GameDifficulty { Easy, Normal, Hard }

    public static class GameDifficultyManager
    {
        public static GameDifficulty Current { get; set; } = GameDifficulty.Easy;
        public static float EnemyDamage => Current == GameDifficulty.Easy ? 1f : Current == GameDifficulty.Normal ? 1.3f : 1.6f;
        public static float PlayerDamage => Current == GameDifficulty.Easy ? 1f : Current == GameDifficulty.Normal ? 0.7f : 0.4f;
        public static float Regeneration => Current == GameDifficulty.Hard ? 0.5f : 1f;
        public static bool ShowHelpers => Current != GameDifficulty.Hard;
        public static bool ShowEnemyLevel => Current == GameDifficulty.Easy;
    }
}
