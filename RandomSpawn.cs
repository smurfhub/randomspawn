using System.Collections.Generic;
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
            SpawnPatch.Initialize(Logger);
            new Harmony("es.uyimbube.randomspawn").PatchAll();
            Logger.LogInfo("RandomSpawn loaded.");
        }
    }

    [HarmonyPatch(typeof(Game), "FindSpawnPoint")]
    public static class SpawnPatch
    {
        private const string StartTemple = "StartTemple";
        private const float SpawnRadius = 5f;
        private const float GroundClearance = 0.25f;

        private static readonly List<Vector3> Candidates = new List<Vector3>();
        private static ManualLogSource logger;
        private static Vector3? spawnAt;

        public static void Initialize(ManualLogSource source) => logger = source;

        public static void Postfix(ref Vector3 point, ref bool usedLogoutPoint, ref bool __result)
        {
            var profile = Game.instance.GetPlayerProfile();
            if (usedLogoutPoint || profile.HaveLogoutPoint() || profile.HaveCustomSpawnPoint())
            {
                return;
            }

            if (spawnAt == null && !TrySelectSpawn())
            {
                return;
            }

            var spawn = spawnAt.Value;
            ZNet.instance.SetReferencePosition(spawn);
            point = spawn;
            __result = false;

            if (!ZNetScene.instance.IsAreaReady(spawn) || !ZoneSystem.instance.IsActiveAreaLoaded())
            {
                return;
            }

            if (!ZoneSystem.instance.GetGroundHeight(spawn, out var groundHeight))
            {
                logger.LogWarning($"No ground found at random spawn {spawn}; selecting another structure.");
                spawnAt = null;
                return;
            }

            point.y = Mathf.Max(point.y, groundHeight + GroundClearance);
            usedLogoutPoint = false;
            __result = true;
            spawnAt = null;
        }

        private static bool TrySelectSpawn()
        {
            Candidates.Clear();
            foreach (var location in ZoneSystem.instance.GetLocationList())
            {
                if (location.m_location.m_prefabName != StartTemple
                    && WorldGenerator.instance.GetBiome(location.m_position) == Heightmap.Biome.Meadows)
                {
                    Candidates.Add(location.m_position);
                }
            }

            if (Candidates.Count == 0)
            {
                logger.LogWarning("No Meadows structures found; using the vanilla start.");
                return false;
            }

            var anchor = Candidates[Random.Range(0, Candidates.Count)];
            var offset = Random.insideUnitCircle * SpawnRadius;
            spawnAt = anchor + new Vector3(offset.x, 2f, offset.y);
            logger.LogInfo($"Selected random spawn near Meadows structure: {spawnAt.Value}.");
            return true;
        }
    }
}
