using UnityEngine;

namespace Game
{
	// Player-chosen bot difficulty for single-player. Picked on the level-select
	// screen (BotDifficultyUI), read by BotController when the bot spawns.
	// Persisted in PlayerPrefs so the choice survives app restarts.
	public static class BotDifficulty
	{
		public const int MinTier = 1;
		public const int MaxTier = 10;
		public const int DefaultTier = 4;
		private const string PrefsKey = "bot_difficulty_tier";

		private static int? _cached;

		public static int Tier
		{
			get
			{
				if (_cached == null)
					_cached = Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, DefaultTier), MinTier, MaxTier);
				return _cached.Value;
			}
			set
			{
				int clamped = Mathf.Clamp(value, MinTier, MaxTier);
				_cached = clamped;
				PlayerPrefs.SetInt(PrefsKey, clamped);
			}
		}
	}
}
