namespace TheLastKnight.Core
{
    public enum GameDifficulty { Easy, Normal, Hard }

    public static class GameDifficultyManager
    {
        public static GameDifficulty Current { get; set; } = GameDifficulty.Easy;
        public static float EnemyDamage => GetEnemyDamage(Current);
        public static float PlayerDamage => GetPlayerDamage(Current);
        public static float StaminaConsumption => GetStaminaConsumption(Current);
        public static float Regeneration => GetRegeneration(Current);
        public static bool ShowHelpers => ShouldShowHelpers(Current);
        public static bool ShowEnemyLevel => ShouldShowEnemyLevel(Current);

        // Read comparison values without changing the active world's difficulty.
        public static float GetEnemyDamage(GameDifficulty difficulty) => difficulty == GameDifficulty.Easy ? 0.5f : difficulty == GameDifficulty.Normal ? 1f : 1.6f;
        public static float GetPlayerDamage(GameDifficulty difficulty) => difficulty == GameDifficulty.Easy ? 1.5f : difficulty == GameDifficulty.Normal ? 1f : 0.4f;
        public static float GetStaminaConsumption(GameDifficulty difficulty) => difficulty == GameDifficulty.Easy ? 2f / 3f : difficulty == GameDifficulty.Normal ? 1f : 4f / 3f;
        public static float GetRegeneration(GameDifficulty difficulty) => difficulty == GameDifficulty.Hard ? 0.5f : 1f;
        public static bool ShouldShowHelpers(GameDifficulty difficulty) => difficulty != GameDifficulty.Hard;
        public static bool ShouldShowEnemyLevel(GameDifficulty difficulty) => difficulty == GameDifficulty.Easy;
    }
}
