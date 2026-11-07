using UnityEngine;

namespace UterPorter
{
    /// <summary>Where "home" is: a spot the player picked ("Uter, this is home"), or else their bed (spawn point).
    /// Saved per world, since the same character can visit several worlds.</summary>
    public static class Home
    {
        private static string Key => "uter_home_" + (ZNet.instance ? ZNet.instance.GetWorldUID().ToString() : "0");

        public static bool TryGet(Player player, out Vector3 home)
        {
            home = Vector3.zero;
            if (player && player.m_customData.TryGetValue(Key, out var s))
            {
                var p = s.Split('|');
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var st = System.Globalization.NumberStyles.Float;
                if (p.Length == 3 && float.TryParse(p[0], st, inv, out var x) && float.TryParse(p[1], st, inv, out var y)
                    && float.TryParse(p[2], st, inv, out var z))
                {
                    home = new Vector3(x, y, z);
                    return true;
                }
            }
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            if (profile != null && profile.HaveCustomSpawnPoint())
            {
                home = profile.GetCustomSpawnPoint();
                return true;
            }
            return false;
        }

        public static void SetHere(Player player)
        {
            var p = player.transform.position;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            player.m_customData[Key] = $"{p.x.ToString("R", inv)}|{p.y.ToString("R", inv)}|{p.z.ToString("R", inv)}";
        }

        public static void Clear(Player player) => player.m_customData.Remove(Key);
    }
}
