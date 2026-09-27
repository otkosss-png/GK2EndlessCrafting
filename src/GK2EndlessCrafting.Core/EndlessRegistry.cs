using System.Collections.Generic;

namespace GK2EndlessCrafting.Core
{
    // Какие станции в режиме «∞» и какой рецепт повторять. Без Unity.
    public sealed class EndlessRegistry
    {
        private readonly Dictionary<string, string> _map = new Dictionary<string, string>();

        public int Count => _map.Count;

        public void Set(string stationId, string recipeId)
        {
            if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(recipeId)) return;
            _map[stationId] = recipeId;
        }

        public void Clear(string stationId)
        {
            if (!string.IsNullOrEmpty(stationId)) _map.Remove(stationId);
        }

        public bool IsOn(string stationId)
            => !string.IsNullOrEmpty(stationId) && _map.ContainsKey(stationId);

        public string RecipeFor(string stationId)
            => !string.IsNullOrEmpty(stationId) && _map.TryGetValue(stationId, out var r) ? r : null;

        public IEnumerable<KeyValuePair<string, string>> All() => _map;
    }
}
