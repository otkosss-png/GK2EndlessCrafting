using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2EndlessCrafting
{
    // Раз в Mod.PollSeconds проходит по станциям в режиме «∞» и, если очередь
    // пуста, ставит сохранённый рецепт снова. Ошибки не выключают режим.
    internal sealed class EndlessRunner : MonoBehaviour
    {
        private static EndlessRunner _instance;
        private static bool _running = true;
        private float _timer;

        internal static void Start()
        {
            if (_instance == null)
            {
                var go = new GameObject("GK2EndlessCrafting");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<EndlessRunner>();
            }
            _running = true;
        }

        internal static void StopIfRunning()
        {
            _running = false;
        }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        private void Update()
        {
            if (_instance == null) _instance = this;
            if (!_running) return;
            if (Plugin.Mod == null || Plugin.Mod.Enabled == null || !Plugin.Mod.Enabled.Value) return;
            if (Plugin.Registry == null || Plugin.Registry.Count == 0) return;

            float step = Plugin.Mod.PollSeconds != null ? Mathf.Max(0.25f, Plugin.Mod.PollSeconds.Value) : 1f;
            _timer += Time.unscaledDeltaTime;
            if (_timer < step) return;
            _timer = 0f;
            Tick();
        }

        private static void Tick()
        {
            try
            {
                foreach (var e in Plugin.Registry.All())
                {
                    try
                    {
                        var craft = StationKey.Resolve(e.Key);
                        if (craft == null) { Trace("station " + e.Key + ": no craft component"); continue; }
                        if (craft.HasCraftsInQueue) continue;

                        var def = FindRecipe(craft, e.Value);
                        if (def == null) { Trace("station " + e.Key + ": recipe " + e.Value + " not found"); continue; }

                        craft.AddToQueue(new CraftElement(def), false, -1);
                        Trace("station " + e.Key + ": re-queued " + e.Value);
                    }
                    catch (Exception ex)
                    {
                        Warn("tick " + e.Key + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Warn("tick: " + ex.Message);
            }
        }

        private static CraftDefBase FindRecipe(CraftComponent craft, string recipeId)
        {
            if (craft == null || string.IsNullOrEmpty(recipeId)) return null;
            var found = FindIn(craft.AvailableCrafts, recipeId);
            if (found != null) return found;
            return FindIn(craft.CraftsIn, recipeId);
        }

        private static CraftDefBase FindIn(List<CraftDefBase> list, string id)
        {
            if (list == null) return null;
            foreach (var d in list)
                if (d != null && d.id == id) return d;
            return null;
        }

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }

        private static void Warn(string msg)
        {
            Plugin.Log?.LogWarning("endless: " + msg);
        }
    }
}
