using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Game.Components; // for MyDamageType
using VRageMath;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ModAPI; // for IMyEntity

namespace Lobby.scripts
{
    public static class LobbyPhysics
    {
        // Message IDs (unique, no overlap with LobbyTeleport)
        private const ushort PHYSICS_VELOCITY_ADD = 18420;
        private const ushort PHYSICS_ROTATION_APPLY = 18421;

        private static bool registered = false;

        // --------------------------------------------------------------------
        // Gravity Well – simple pull zone (blackhole prototype)
        // --------------------------------------------------------------------
        private static readonly HashSet<long> ActiveGravityWells = new HashSet<long>();
        private static long nextWellId = 1;

        /// <summary>
        /// Server-side tick: applies gravity wells, damage, teleports
        /// Called once per frame from Lobby.cs when running on dedicated server
        /// </summary>
        public static void DoPhysicsTick()
        {


            if (!MyAPIGateway.Session.IsServer) return;
            if (LobbyServer.ServerNavigationWarnings == null || LobbyServer.ServerNavigationWarnings.Count == 0)
                return;

            foreach (var warning in LobbyServer.ServerNavigationWarnings)
            {
                // Only gravity zones
                if (warning.Type != "Blackhole" && warning.Type != "Whitehole" && warning.Type != "Ejector")
                    continue;

                Vector3D center = new Vector3D(warning.X, warning.Y, warning.Z);
                float radius = (float)warning.Radius;
                float strength = (float)warning.Power; // positive = pull, negative = push
                bool hasDeadZone = warning.Type == "Blackhole";

                CreateGravityWell(center, radius, strength, hasDeadZone);

                // WHITEHOLE detect when something is near center
                // WHITEHOLE TELEPORT — trigger on center contact
                // WHITEHOLE TELEPORT – only player or piloted grid
                if (warning.Type == "Whitehole")
                {
                    float triggerRadius = (float)warning.Radius * 0.06f;


                    var entities = new HashSet<VRage.ModAPI.IMyEntity>();
                    MyAPIGateway.Entities.GetEntities(entities, e => e?.Physics != null && !e.MarkedForClose);

                    foreach (var entity in entities)
                    {
                        double dist = Vector3D.Distance(entity.GetPosition(), center);
                        if (dist < triggerRadius)
                        {
                            string name = entity.DisplayName ?? "Unknown";
                            long id = entity.EntityId;
                            Vector3D exitPos = Vector3D.Zero;

                            // Fixed exit
                            if (warning.ExitX != 0 || warning.ExitY != 0 || warning.ExitZ != 0)
                            {
                                exitPos = new Vector3D(warning.ExitX, warning.ExitY, warning.ExitZ);
                            }

                            else if (warning.ExitRadius > 0)
                            {
                                Random rand = new Random();
                                Vector3D randDir = new Vector3D(
                                    rand.NextDouble() * 2 - 1,
                                    rand.NextDouble() * 2 - 1,
                                    rand.NextDouble() * 2 - 1);
                                exitPos = center + Vector3D.Normalize(randDir) * warning.ExitRadius * (float)rand.NextDouble();
                            }
                            else
                            {
                                exitPos = center + (entity.GetPosition() - center) * 3;
                            }

                            // Use your existing teleport method with EntityId
                            LobbyTeleport.RequestEntityTeleport(id, exitPos);

                            //MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"WHITEHOLE TELEPORT: Entity {name} {id} X{warning.ExitX}, Y{warning.ExitY}, Z{warning.ExitZ}");


                          

                        }

                    }
                   
                }
            }
        }

        //this may still work but was doing funny things.
        //kept to migrate damage processing over later.
        public static void OldDoPhysicsTick()
        {
            if (!MyAPIGateway.Session.IsServer) return;

            // Use the server-side list from LobbyServer
            if (LobbyServer.ServerNavigationWarnings == null || LobbyServer.ServerNavigationWarnings.Count == 0)
                return;

            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities, e => e?.Physics != null && !e.MarkedForClose);

            foreach (var warning in LobbyServer.ServerNavigationWarnings)
            {
                if (warning.Type != "Blackhole" && warning.Type != "Whitehole" && warning.Type != "Ejector") continue;

                Vector3D center = new Vector3D(warning.X, warning.Y, warning.Z);
                float radius = (float)warning.Radius;
                float power = Math.Abs((float)warning.Power);
                bool isPull = warning.Type == "Blackhole";


                foreach (var entity in entities)
                {
                    Vector3D pos = entity.GetPosition();
                    Vector3D dir = center - pos;
                    double dist = dir.Length();
                    if (dist >= radius || dist < 1) continue;

                    Vector3D pullDir = Vector3D.Normalize(dir);
                    float strength = power * (float)(1.0 - dist / radius);

                    // Apply pull (blackhole) or push (eject/whitehole entry)
                    entity.Physics.LinearVelocity += pullDir * strength * (isPull ? 0.1f : -0.1f);

                    // Blackhole event horizon damage
                    if (isPull && dist < radius * 0.3f)
                    {
                        // Damage scales from 5 (edge) to 50 (center) per tick
                        float damage = 10f + (float)((radius * 0.3f - dist) * 30f);

                        // GRID: Damage first block 
                        IMyCubeGrid grid = entity as IMyCubeGrid;
                        if (grid != null)
                        {
                            var blocks = new List<IMySlimBlock>();
                            grid.GetBlocks(blocks);
                            if (blocks.Count > 0)
                            {
                                blocks[0].DoDamage(damage, MyDamageType.Deformation, true);
                            }
                        }
                        // PLAYER: Full damage (they're in a cockpit or jetpack)
                        else
                        {
                            IMyCharacter character = entity as IMyCharacter;
                            if (character != null)
                            {
                                character.DoDamage(damage, MyDamageType.Deformation, true);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply realistic post-jump stagger: strong side roll (left/right) + light secondary wobble
        /// Used by /override and debug /phys stagger
        /// </summary>
        public static void ShipStagger(long playerIdentityId)
        {
            var player = GetPlayerByIdentityId(playerIdentityId);
            if (player == null) return;

            var grid = GetControlledGrid(player);
            if (grid == null || grid.Physics == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", "No grid to stagger");
                return;
            }

            Random rand = new Random();

            // 1. Strong side roll (left or right) – 0 to 25 deg/s
            bool rollLeft = rand.Next(2) == 0;
            Vector3D rollAxis = rollLeft ? grid.WorldMatrix.Left : grid.WorldMatrix.Right;
            //float rollDeg = (float)rand.NextDouble() * 25f;
            float rollDeg = 12f + (float)rand.NextDouble() * 13f; // 12.0 to 24.999

            // 2. Light secondary wobble (up/down/forward/backward) – 0 to 12 deg/s
            Vector3D[] secondaryAxes = { grid.WorldMatrix.Up, -grid.WorldMatrix.Up, grid.WorldMatrix.Forward, -grid.WorldMatrix.Forward };
            Vector3D secondaryAxis = secondaryAxes[rand.Next(secondaryAxes.Length)];
            float secondaryDeg = (float)rand.NextDouble() * 12f;

            // Combine into one angular velocity vector
            float toRad = 0.017453292f;
            Vector3D totalAngularVel = (Vector3D.Normalize(rollAxis) * rollDeg +
                                       Vector3D.Normalize(secondaryAxis) * secondaryDeg) * toRad;

            grid.Physics.AngularVelocity = totalAngularVel;

            //MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"Ship stagger: {rollDeg:F1}°/s roll + {secondaryDeg:F1}°/s wobble");
        }

        /// <summary>
        /// General way to apply rotation for potential future effects
        /// </summary>
        public static void ApplyRotation(long playerIdentityId, Vector3D worldAxis, float degreesPerSecond)
        {
            var player = GetPlayerByIdentityId(playerIdentityId);
            if (player == null) return;

            var grid = GetControlledGrid(player);
            if (grid == null || grid.Physics == null)
            {
                MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", "No grid controlled");
                return;
            }

            float radPerSec = degreesPerSecond * 0.017453292f;
            Vector3D angularVel = worldAxis * radPerSec;

            grid.Physics.AngularVelocity = angularVel;

            MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"Grid reeling {degreesPerSecond:F1}°/s");
        }
       
        /// <summary>
        /// Server Side/Safe Gravity well physics and behaviour.
        /// </summary>
        public static void CreateGravityWell(Vector3D center, float radius, float strength, bool deadZone=true)
        {
            var entities = new HashSet<VRage.ModAPI.IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);

            int affected = 0;

            foreach (var entity in entities)
            {
                if (entity == null || entity.Physics == null || entity.MarkedForClose) continue;

                Vector3D pos = entity.GetPosition();
                Vector3D dir = center - pos;
                double dist = dir.Length();
                if (dist >= radius || dist < 1) continue;

                float gradient = (float)Math.Pow(1.0 - dist / radius, 2); // Quadratic – weak edge, strong center
                float pull = strength * gradient;
                pull = Math.Min(pull, 100f); // Cap to prevent crashes

                Vector3D pullDir = Vector3D.Normalize(dir);

                // Dead zone (5% radius) – lock in place
                if (deadZone && dist < radius * 0.05f)
                {
                    entity.Physics.LinearVelocity = Vector3D.Zero;
                    affected++;
                    continue;
                }

                // Pull (positive strength) or push (negative strength)
                entity.Physics.LinearVelocity += pullDir * pull * 0.1f;

                affected++;
            }

            // MyAPIGateway.Utilities.ShowMessage("LobbyPhysics", $"Gravity well applied – {affected} entities affected");
        }

        /// <summary>
        /// Gravity Well – simple one-shot pull (offline testable, no timer)
        /// More agressive version of CreateGravityWell mostly for an admin test Gravity Bomb effect.
        /// </summary>
        public static void ClientCreateGravityWell(Vector3D center, float radius, float strength)
        {
            var entities = new HashSet<VRage.ModAPI.IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);

            int affected = 0;

            foreach (var entity in entities)
            {
                if (entity == null || entity.Physics == null || entity.MarkedForClose)
                    continue;

                Vector3D pos = entity.GetPosition();
                Vector3D dir = center - pos;
                double dist = dir.Length();

                if (dist >= radius || dist < 1)
                    continue;

                Vector3D pullDir = Vector3D.Normalize(dir);
                float pull = strength * (float)(1.0 - dist / radius);

                IMyCubeGrid grid = entity as IMyCubeGrid;
                if (grid != null && grid.Physics != null)
                {
                    grid.Physics.LinearVelocity += pullDir * pull * 0.1f;
                    affected++;
                    continue; // skip character check if it's a grid
                }

                // Character check (jetpack player)
                IMyCharacter character = entity as IMyCharacter;
                if (character != null && character.Physics != null)
                {
                    character.Physics.LinearVelocity += pullDir * pull * 0.1f;
                    affected++;
                }
            }

            MyAPIGateway.Utilities.ShowMessage("LobbyPhysics",
                $"Gravity pull applied – {affected} entities affected");
        }

        /// <summary>
        /// Looks up a players IdentityID to resolve WHO to apply physics effects to.
        /// Duplication: Also exists in LobbyTeleport.cs schedule for merge.
        /// </summary>
        private static IMyPlayer GetPlayerByIdentityId(long identityId)
        {
            var list = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(list);
            foreach (var p in list)
            {
                if (p.IdentityId == identityId) return p;
            }
            return null;
        }

        /// <summary>
        /// Looks up a grid ID of ship being controled by specified player.
        /// </summary>
        private static IMyCubeGrid GetControlledGrid(IMyPlayer player)
        {
            var entity = player.Controller?.ControlledEntity?.Entity;
            var block = entity as IMyCubeBlock;
            if (block != null) return block.CubeGrid;
            return entity as IMyCubeGrid;
        }

        /// <summary>
        /// Initialises server/client networking.
        /// </summary>
        public static void InitNetworking()
        {
            if (registered || MyAPIGateway.Multiplayer == null) return;

              MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PHYSICS_VELOCITY_ADD,
                (ushort id, byte[] data, ulong sender, bool ignore) => { });

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(PHYSICS_ROTATION_APPLY,
                (ushort id, byte[] data, ulong sender, bool ignore) => { });

            registered = true;
        }

        /// <summary>
        /// Shuts down server/client networking for exiting server.
        /// </summary>
        public static void UnloadNetworking()
        {
            if (!registered || MyAPIGateway.Multiplayer == null) return;

            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PHYSICS_VELOCITY_ADD, null);
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(PHYSICS_ROTATION_APPLY, null);

            registered = false;
        }
    }
}