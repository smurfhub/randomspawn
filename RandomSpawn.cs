using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RandomSpawn
{
    [BepInPlugin("es.uyimbube.randomspawn", "RandomSpawn", "0.1.0")]
    public class ModRandomSpawn : BaseUnityPlugin
    {
        private void Awake()
        {
            Spot.Initialize(Logger);
            new Harmony("es.uyimbube.randomspawn").PatchAll();
            Logger.LogInfo("RandomSpawn loaded.");
        }
    }

    // El inicio de cada jugador se guarda en el mundo como una global key ("rs_<nombre> x;y;z"):
    // el juego la persiste y la envia a todos los clientes.
    public static class Spot
    {
        private const float ProtectedRadius = 25f;
        private const string Prefix = "rs_";
        private const string NoAnchor = "none";

        private static string requestedPlayer;
        private static bool noAnchor;
        private static readonly HashSet<string> loggedStatuses = new HashSet<string>();
        private static ManualLogSource logger;

        public static void Initialize(ManualLogSource source) => logger = source;

        public static void LogOnce(string status, string message, bool warning = false)
        {
            if (!loggedStatuses.Add(status))
            {
                return;
            }

            if (warning)
            {
                logger.LogWarning(message);
            }
            else
            {
                logger.LogInfo(message);
            }
        }

        private static string Key(string player) => Prefix + player.Replace(' ', '_').ToLower();

        private static bool TryParse(string text, out Vector3 position)
        {
            var p = text.Split(';');
            if (p.Length != 3
                || !float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                position = default;
                return false;
            }

            position = new Vector3(x, y, z);
            return true;
        }

        public static string Format(Vector3 v) => string.Join(";", v.x.ToString("F1", CultureInfo.InvariantCulture), v.y.ToString("F1", CultureInfo.InvariantCulture), v.z.ToString("F1", CultureInfo.InvariantCulture));

        public static Vector3? Home(string player)
        {
            return ZoneSystem.instance.GetGlobalKey(Key(player), out var value) && TryParse(value, out var position)
                ? position
                : (Vector3?)null;
        }

        // Hay algun inicio de jugador a menos de "radius" metros (en horizontal).
        public static bool NearHome(Vector3 pos, float radius)
        {
            foreach (var key in ZoneSystem.instance.m_globalKeysValues)
            {
                if (key.Key.StartsWith(Prefix))
                {
                    if (TryParse(key.Value, out var home)
                        && Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(home.x, home.z)) < radius)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool InProtectedZone(Vector3 pos) => NearHome(pos, ProtectedRadius);

        // Pide un inicio (una sola vez): el servidor/host lo asigna directamente, un cliente lo pide por RPC.
        // Devuelve false si el servidor local no ha encontrado ninguna ancla valida.
        public static bool Request(string player)
        {
            if (Home(player) != null)
            {
                return true;
            }

            if (requestedPlayer != player)
            {
                requestedPlayer = player;
                noAnchor = false;
                if (ZNet.instance.IsServer())
                {
                    LogOnce("search:" + player, $"Searching for a spawn point for '{player}'.");
                    noAnchor = !OnRequest(0, player);
                }
                else
                {
                    LogOnce("request:" + player, $"Requesting a spawn point for '{player}' from the server.");
                    ZRoutedRpc.instance.InvokeRoutedRPC("RandomSpawnRequest", player);
                }
            }
            return !noAnchor;
        }

        public static void OnResponse(string player, bool success)
        {
            if (requestedPlayer != player)
            {
                return;
            }

            noAnchor = !success;
            if (!success)
            {
                LogOnce("server-no-anchor:" + player,
                    $"The server found no valid Meadows structure for '{player}'; using the vanilla start.",
                    true);
            }
        }

        public static void ResetRequest()
        {
            requestedPlayer = null;
            noAnchor = false;
            loggedStatuses.Clear();
        }

        // Servidor: asigna un inicio al jugador si aun no tiene. Devuelve false si no hay ancla valida.
        public static bool OnRequest(long sender, string player)
        {
            if (ZoneSystem.instance.GetGlobalKey(Key(player), out var value))
            {
                if (TryParse(value, out var existing))
                {
                    var distance = new Vector2(existing.x, existing.z).magnitude;
                    LogOnce("existing:" + player,
                        $"Using saved spawn point for '{player}': {Format(existing)} (distance from center: {distance:F0} m).");
                    return true;
                }

                if (value == NoAnchor)
                {
                    LogOnce("retry:" + player,
                        $"Retrying spawn search for '{player}'; a previous attempt saved '{NoAnchor}'.");
                }
            }

            var pos = Find(false) ?? Find(true);
            if (pos == null)
            {
                LogOnce("not-found:" + player,
                    $"No valid spawn location found for '{player}'. Saving '{NoAnchor}'; vanilla start will be used.",
                    true);
                ZoneSystem.instance.SetGlobalKey(Key(player) + " " + NoAnchor);
                return false;
            }

            var centerDistance = new Vector2(pos.Value.x, pos.Value.z).magnitude;
            LogOnce("selected:" + player,
                $"Selected spawn point for '{player}': {Format(pos.Value)} (distance from center: {centerDistance:F0} m).");
            ZoneSystem.instance.SetGlobalKey(Key(player) + " " + Format(pos.Value));
            return true;
        }

        // Todas las ubicaciones en Meadows tienen la misma probabilidad.
        // Solo se permiten ubicaciones usadas si no queda ninguna libre.
        private static Vector3? Find(bool allowTaken)
        {
            var candidates = new List<Vector3>();
            var total = 0;
            var skippedStart = 0;
            var invalidBiome = 0;
            var alreadyUsed = 0;
            foreach (var instance in ZoneSystem.instance.GetLocationList())
            {
                total++;
                var name = instance.m_location.m_prefabName;
                if (name == "StartTemple")
                {
                    skippedStart++;
                    continue;
                }

                if (WorldGenerator.instance.GetBiome(instance.m_position) != Heightmap.Biome.Meadows)
                {
                    invalidBiome++;
                    continue;
                }

                if (!allowTaken && NearHome(instance.m_position, 20f))
                {
                    alreadyUsed++;
                    continue;
                }

                candidates.Add(instance.m_position);
            }

            logger.LogInfo($"Spawn search (allow used: {allowTaken}): {total} locations, {skippedStart} StartTemple, " +
                $"{invalidBiome} outside Meadows, {alreadyUsed} already assigned, {candidates.Count} candidates.");

            if (candidates.Count > 0)
            {
                var anchor = candidates[Random.Range(0, candidates.Count)];
                var offset = Random.insideUnitCircle.normalized * 5f;
                return anchor + new Vector3(offset.x, 0f, offset.y);
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(ZoneSystem), "Start")]
    public static class RegisterPatch
    {
        public static void Postfix()
        {
            Spot.ResetRequest();
            ZRoutedRpc.instance.Register<string>("RandomSpawnRequest", HandleRequest);
            ZRoutedRpc.instance.Register<string, bool>("RandomSpawnResponse",
                (sender, player, success) => Spot.OnResponse(player, success));
        }

        private static void HandleRequest(long sender, string player)
        {
            var success = Spot.OnRequest(sender, player);
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RandomSpawnResponse", player, success);
        }
    }

    // Solo mientras se busca el punto de spawn, el StartTemple vanilla pasa a ser el inicio del jugador.
    // El icono del mapa no se toca.
    [HarmonyPatch(typeof(ZoneSystem), "GetLocationIcon")]
    public static class LocationIconPatch
    {
        public static Vector3? SpawnAt;

        public static void Postfix(string name, ref Vector3 pos, ref bool __result)
        {
            if (SpawnAt is Vector3 home && name == "StartTemple")
            {
                pos = home;
                __result = true;
                Spot.LogOnce("icon-override", $"Replaced StartTemple spawn with {Spot.Format(home)}.");
            }
        }
    }

    [HarmonyPatch(typeof(Game), "FindSpawnPoint")]
    public static class SpawnPatch
    {
        // Sin inicio asignado, lo pide y espera; con inicio, espera a que la zona este cargada.
        public static bool Prefix(ref bool __result)
        {
            var profile = Game.instance.GetPlayerProfile();
            if (profile.HaveLogoutPoint() || profile.HaveCustomSpawnPoint())
            {
                Spot.LogOnce("profile-spawn:" + profile.GetName(),
                    $"Using the profile's saved logout/custom spawn for '{profile.GetName()}'; random spawn is skipped.");
                return true;
            }

            if (!(Spot.Home(profile.GetName()) is Vector3 home))
            {
                if (!Spot.Request(profile.GetName()))
                {
                    Spot.LogOnce("vanilla-fallback:" + profile.GetName(),
                        $"No random spawn is available for '{profile.GetName()}'; using the vanilla start.",
                        true);
                    return true; // sin ancla valida: spawn vanilla
                }
                __result = false;
                return false;
            }

            ZNet.instance.SetReferencePosition(home);
            if (!ZoneSystem.instance.IsActiveAreaLoaded() || !ZNetScene.instance.IsAreaReady(home))
            {
                Spot.LogOnce("zone-wait:" + profile.GetName(),
                    $"Spawn point {Spot.Format(home)} selected for '{profile.GetName()}', waiting for its zone to load.");
                __result = false;
                return false;
            }

            var centerDistance = new Vector2(home.x, home.z).magnitude;
            Spot.LogOnce("zone-ready:" + profile.GetName(),
                $"Spawn zone ready for '{profile.GetName()}': {Spot.Format(home)} (distance from center: {centerDistance:F0} m).");
            LocationIconPatch.SpawnAt = home;
            return true;
        }

        public static void Postfix()
        {
            LocationIconPatch.SpawnAt = null;
        }
    }

    // Zona protegida alrededor del inicio: sin construir ni modificar el terreno.
    [HarmonyPatch(typeof(Location), "IsInsideNoBuildLocation")]
    public static class NoBuildPatch
    {
        public static void Postfix(Vector3 point, ref bool __result)
        {
            __result = __result || (ZoneSystem.instance != null && Spot.InProtectedZone(point));
        }
    }
}
