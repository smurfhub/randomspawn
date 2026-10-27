using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RandomSpawn
{
    [BepInPlugin("es.uyimbube.randomspawn", "RandomSpawn", "1.0.0")]
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
        private const float MinSpawnRadius = 5f;
        private const float MaxSpawnRadius = 10f;
        private const float GroundClearance = 0.25f;
        private const float MaxGroundVariation = 0.5f;
        private const float CandidateTimeoutSeconds = 60f;
        private const float GlobalTimeoutSeconds = 180f;
        private const int MaxSpawnAttempts = 10;

        private static readonly List<Vector3> Candidates = new List<Vector3>();
        private static ManualLogSource logger;
        private static int obstacleMask;
        private static int spawnAttempts;
        private static float candidateWaitSeconds;
        private static float totalWaitSeconds;
        private static bool useVanillaSpawn;
        private static bool spawnValidated;
        private static Vector3? spawnAt;

        public static void Initialize(ManualLogSource source)
        {
            logger = source;
            obstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
        }

        public static void ResetForNewWorld()
        {
            ResetSpawnSearch();
            Candidates.Clear();
            useVanillaSpawn = false;
        }

        public static void Postfix(
            ref Vector3 point,
            ref bool usedLogoutPoint,
            ref bool __result,
            float dt,
            bool ___m_respawnAfterDeath)
        {
            if (useVanillaSpawn)
            {
                if (__result)
                {
                    useVanillaSpawn = false;
                    ResetSpawnSearch();
                }
                return;
            }

            var profile = Game.instance.GetPlayerProfile();
            if (usedLogoutPoint
                || (profile.HaveLogoutPoint() && !___m_respawnAfterDeath)
                || profile.HaveCustomSpawnPoint())
            {
                return;
            }

            var vanillaPoint = point;
            var vanillaUsedLogoutPoint = usedLogoutPoint;
            var vanillaResult = __result;
            totalWaitSeconds += dt;
            if (totalWaitSeconds >= GlobalTimeoutSeconds)
            {
                FallBackToVanilla(ref point, ref usedLogoutPoint, ref __result,
                    vanillaPoint, vanillaUsedLogoutPoint, vanillaResult,
                    $"Random spawn search exceeded {GlobalTimeoutSeconds:F0} seconds.");
                return;
            }

            if (spawnAt != null && !spawnValidated)
            {
                candidateWaitSeconds += dt;
                if (candidateWaitSeconds >= CandidateTimeoutSeconds)
                {
                    logger.LogWarning($"Random spawn candidate {spawnAt.Value} did not load within " +
                        $"{CandidateTimeoutSeconds:F0} seconds; trying another candidate.");
                    RejectSpawn("Candidate load timed out.");
                }
            }

            if (spawnAt == null && !TrySelectSpawn())
            {
                if (useVanillaSpawn && __result)
                {
                    useVanillaSpawn = false;
                    ResetSpawnSearch();
                }
                return;
            }

            var spawn = spawnAt.Value;
            ZNet.instance.SetReferencePosition(spawn);
            point = spawn;
            __result = false;

            if (!ZNetScene.instance.IsAreaReady(spawn))
            {
                return;
            }

            if (!spawnValidated)
            {
                if (!TryGetGroundHeight(spawn, out var groundHeight))
                {
                    RejectSpawn($"Ground is missing or too uneven at random spawn {spawn}.");
                    return;
                }

                var groundSpawn = new Vector3(spawn.x, groundHeight + GroundClearance, spawn.z);
                point.y = Mathf.Max(spawn.y, groundSpawn.y);
                var capsule = GetPlayerCapsule();
                if (capsule == null || !HasClearance(groundSpawn, capsule) || !HasClearance(point, capsule))
                {
                    RejectSpawn($"Random spawn {spawn} is obstructed.");
                    return;
                }

                spawnAt = point;
                spawnValidated = true;
            }

            if (!ZoneSystem.instance.IsActiveAreaLoaded())
            {
                return;
            }

            usedLogoutPoint = false;
            __result = true;
            ResetSpawnSearch();
        }

        private static bool TrySelectSpawn()
        {
            if (spawnAttempts >= MaxSpawnAttempts)
            {
                logger.LogWarning("No clear spawn point found; using the vanilla start.");
                ResetSpawnSearch();
                useVanillaSpawn = true;
                return false;
            }

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
                useVanillaSpawn = true;
                return false;
            }

            spawnAttempts++;
            var anchor = Candidates[Random.Range(0, Candidates.Count)];
            var offset = Random.insideUnitCircle.normalized * Random.Range(MinSpawnRadius, MaxSpawnRadius);
            spawnAt = anchor + new Vector3(offset.x, 2f, offset.y);
            spawnValidated = false;
            candidateWaitSeconds = 0f;
            logger.LogInfo($"Selected random spawn near Meadows structure: {spawnAt.Value}.");
            return true;
        }

        private static CapsuleCollider GetPlayerCapsule()
        {
            var playerPrefab = ZNetScene.instance.GetPrefab("Player");
            return playerPrefab != null ? playerPrefab.GetComponent<CapsuleCollider>() : null;
        }

        private static bool HasClearance(Vector3 feetPosition, CapsuleCollider capsule)
        {
            var scale = capsule.transform.lossyScale;
            var radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var height = Mathf.Max(capsule.height * Mathf.Abs(scale.y), radius * 2f);
            var center = feetPosition + Vector3.Scale(capsule.center, scale);
            var halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            var lower = center + Vector3.down * halfSegment;
            var upper = center + Vector3.up * halfSegment;
            if (Physics.CheckCapsule(lower, upper, radius, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            foreach (var collider in Physics.OverlapCapsule(lower, upper, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (collider.GetComponentInParent<Player>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetGroundHeight(Vector3 position, out float groundHeight)
        {
            groundHeight = 0f;
            var capsule = GetPlayerCapsule();
            if (capsule == null || !ZoneSystem.instance.GetGroundHeight(position, out groundHeight))
            {
                return false;
            }

            var scale = capsule.transform.lossyScale;
            var radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var sampleRadius = radius * 0.8f;
            var minHeight = groundHeight;
            var maxHeight = groundHeight;

            for (var i = 0; i < 4; i++)
            {
                var angle = i * Mathf.PI * 0.5f;
                var sample = position + new Vector3(Mathf.Cos(angle) * sampleRadius, 0f, Mathf.Sin(angle) * sampleRadius);
                if (!ZoneSystem.instance.GetGroundHeight(sample, out var sampleHeight))
                {
                    return false;
                }

                minHeight = Mathf.Min(minHeight, sampleHeight);
                maxHeight = Mathf.Max(maxHeight, sampleHeight);
            }

            if (maxHeight - minHeight > MaxGroundVariation)
            {
                return false;
            }

            groundHeight = maxHeight;
            return true;
        }

        private static void RejectSpawn(string reason)
        {
            logger.LogInfo($"{reason} Trying another location.");
            spawnAt = null;
            spawnValidated = false;
            candidateWaitSeconds = 0f;
        }

        private static void ResetSpawnSearch()
        {
            spawnAt = null;
            spawnAttempts = 0;
            spawnValidated = false;
            candidateWaitSeconds = 0f;
            totalWaitSeconds = 0f;
        }

        private static void FallBackToVanilla(
            ref Vector3 point,
            ref bool usedLogoutPoint,
            ref bool result,
            Vector3 vanillaPoint,
            bool vanillaUsedLogoutPoint,
            bool vanillaResult,
            string reason)
        {
            logger.LogWarning($"{reason} Using the vanilla spawn.");
            point = vanillaPoint;
            usedLogoutPoint = vanillaUsedLogoutPoint;
            result = vanillaResult;
            ZNet.instance.SetReferencePosition(vanillaPoint);
            ResetSpawnSearch();
            useVanillaSpawn = !vanillaResult;
        }
    }

    [HarmonyPatch(typeof(ZoneSystem), "Awake")]
    public static class ZoneSystemPatch
    {
        private static void Postfix()
        {
            SpawnPatch.ResetForNewWorld();
        }
    }
}
