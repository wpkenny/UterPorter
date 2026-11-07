using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace UterPorter
{
    /// <summary>Uter the Porter: call him by name and he gives you a piggyback home, or to your gravestone,
    /// then runs off until he's needed again. No AI, no API key: just chat commands and hotkeys.</summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Entirely client-side: Uter is made, run and removed by the player who calls him. Nobody else (server or other
    // players) needs the mod, and nobody is ever refused a server over it. Players without it just can't see him.
    [Jotunn.Utils.NetworkCompatibility(Jotunn.Utils.CompatibilityLevel.NotEnforced, Jotunn.Utils.VersionStrictness.None)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.wpkenny.uterporter";
        public const string PluginName = "Uter the Porter";
        public const string PluginVersion = "1.0.1";
        public const string PrefabName = "UterPorter";

        public static ManualLogSource Log;

        public static ConfigEntry<string> PorterName;
        public static ConfigEntry<KeyboardShortcut> HomeKey;
        public static ConfigEntry<KeyboardShortcut> GraveKey;
        public static ConfigEntry<float> SpeedBoost;
        public static ConfigEntry<bool> Chatty;
        public static ConfigEntry<float> SeatHeight;
        public static ConfigEntry<float> SeatBehind;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            PorterName = Config.Bind("General", "Name", "Uter",
                "What the porter is called. Start a chat message with this name to call him, e.g. \"Uter, take me home\".");
            HomeKey = Config.Bind("Hotkeys", "TakeMeHome", new KeyboardShortcut(KeyCode.None),
                "Optional key to call him for a ride home. Unset by default.");
            GraveKey = Config.Bind("Hotkeys", "TakeMeToMyGrave", new KeyboardShortcut(KeyCode.None),
                "Optional key to call him for a ride to your nearest gravestone. Unset by default.");
            SpeedBoost = Config.Bind("Ride", "SpeedBoost", 1.3f,
                "How much faster than a normal run he goes with you on his back (1 = normal running speed).");
            Chatty = Config.Bind("Ride", "Grumbling", true,
                "Sarcastic remarks while he carries you. Turn off for a quiet ride.");
            SeatHeight = Config.Bind("Ride", "SeatHeight", 0.75f,
                "How high (metres) you sit on his back.");
            SeatBehind = Config.Bind("Ride", "SeatBehind", -0.18f,
                "How far behind him (metres) you sit. Below 0 moves you forward, onto his back as he leans into a run.");

            CreatureManager.OnVanillaCreaturesAvailable += RegisterPorter;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void RegisterPorter()
        {
            try
            {
                var creature = new CustomCreature(PrefabName, "Dverger", new CreatureConfig
                {
                    Name = PorterName.Value,
                    Faction = Character.Faction.Players,
                });
                var prefab = creature.Prefab;
                var character = prefab.GetComponent<Character>();
                character.m_name = PorterName.Value;
                character.m_faction = Character.Faction.Players;
                character.m_boss = false;

                // No loot from your porter.
                var drop = prefab.GetComponent<CharacterDrop>();
                if (drop) Object.DestroyImmediate(drop);

                var ai = prefab.GetComponent<MonsterAI>();
                if (ai)
                {
                    ai.m_enableHuntPlayer = false;
                    ai.m_attackPlayerObjects = false;
                    ai.m_aggravatable = false;
                }

                prefab.AddComponent<Uter>();
                CreatureManager.Instance.AddCreature(creature);
                Log.LogInfo($"Registered the porter '{PrefabName}'");
            }
            catch (System.Exception e) { Log.LogError("Couldn't register the porter: " + e); }
            finally { CreatureManager.OnVanillaCreaturesAvailable -= RegisterPorter; }
        }

        private void Update()
        {
            var player = Player.m_localPlayer;
            if (!player || player.IsDead() || TypingOrInMenu()) return;
            if (HomeKey.Value.MainKey != KeyCode.None && HomeKey.Value.IsDown()) Call(Destination.Home);
            if (GraveKey.Value.MainKey != KeyCode.None && GraveKey.Value.IsDown()) Call(Destination.Grave);
        }

        private static bool TypingOrInMenu() =>
            (Chat.instance && Chat.instance.HasFocus()) || Console.IsVisible() || InventoryGui.IsVisible()
            || Menu.IsVisible() || TextInput.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible();

        // ---------------- calling him ----------------

        /// <summary>Call the porter for a ride: the one already about gives it, or a new one appears beside you.</summary>
        public static void Call(Destination to)
        {
            var player = Player.m_localPlayer;
            if (!player) return;

            var mine = Uter.All.FirstOrDefault(u => u && u.IsFor(player));
            if (mine) { mine.Begin(to); return; }

            var prefab = PrefabManager.Instance.GetPrefab(PrefabName);
            if (!prefab) { ShowMessage($"{PorterName.Value} isn't ready yet. Try again in a moment."); return; }

            Uter.Pending = to;   // he gives the ride as soon as he's arrived
            var behind = player.transform.position - player.transform.forward * 2.5f + player.transform.right * 1f;
            if (Heightmap.GetHeight(behind, out float h)) behind.y = Mathf.Max(h, player.transform.position.y - 1f) + 0.2f;
            var go = Object.Instantiate(prefab, behind, Quaternion.LookRotation(player.transform.forward));
            var uter = go.GetComponent<Uter>();
            if (uter) uter.SetRider(player);
        }

        public static void ShowMessage(string text)
        {
            if (MessageHud.instance) MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text);
        }
    }

    public enum Destination { Home, Grave }

    /// <summary>Chat: "Uter, take me home", "Uter, take me to my grave", "Uter, this is home", "Uter, use my bed".</summary>
    [HarmonyPatch(typeof(Chat), nameof(Chat.SendText))]
    public static class ChatPatch
    {
        private static bool Prefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var name = Plugin.PorterName.Value.Trim();
            var msg = text.Trim().TrimStart('@');
            if (!msg.StartsWith(name, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (msg.Length > name.Length && char.IsLetterOrDigit(msg[name.Length])) return true;   // "Utero..." isn't him

            var m = msg.Substring(name.Length).ToLowerInvariant();
            var player = Player.m_localPlayer;
            if (!player) return true;

            if (m.Contains("this is home") || m.Contains("set home") || m.Contains("home is here") || m.Contains("make this home"))
            {
                Home.SetHere(player);
                Plugin.ShowMessage($"{name}: Right, this is home now. I'll bring you here.");
            }
            else if (m.Contains("use my bed") || m.Contains("bed is home") || m.Contains("forget home"))
            {
                Home.Clear(player);
                Plugin.ShowMessage($"{name}: Fine, home's your bed again.");
            }
            else if (new[] { "grave", "tomb", "body", "corpse", "where i died" }.Any(m.Contains))
                Plugin.Call(Destination.Grave);
            else if (new[] { "home", "base", "bed" }.Any(m.Contains))
                Plugin.Call(Destination.Home);
            else
                Plugin.ShowMessage($"{name} only does rides: \"{name}, take me home\" or \"{name}, take me to my grave\".");
            return true;   // the message still shows in chat, like any other
        }
    }

    /// <summary>Uter can't be hurt: a ride should never end with a dead porter dropping you in a swamp.</summary>
    [HarmonyPatch(typeof(Character), "RPC_Damage")]
    public static class UnhurtablePatch
    {
        private static bool Prefix(Character __instance) => !__instance.GetComponent<Uter>();
    }

    /// <summary>He's always under the mod's control: no fighting, no wandering, just walking where he's sent.</summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class PorterMovementPatch
    {
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            var uter = __instance.GetComponent<Uter>();
            if (!uter) return true;
            uter.DriveMovement(dt);
            __result = true;
            return false;
        }
    }
}
