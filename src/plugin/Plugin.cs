using System;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace GlowNerf
{
    [BepInPlugin("com.coder23848.glownerf", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
#pragma warning disable IDE0051 // Visual Studio is whiny
        private void OnEnable()
#pragma warning restore IDE0051
        {
            // Plugin startup logic
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;

            
            On.PlayerGraphics.Update += PlayerGraphics_Update;
            On.VoidSea.VoidSeaScene.VoidSeaTreatment += VoidSeaScene_VoidSeaTreatment;
            On.SaveState.RainCycleTick += SaveState_RainCycleTick;
            On.OracleSwarmer.BitByPlayer += OracleSwarmer_BitByPlayer;
            On.SLOracleSwarmer.BitByPlayer += SLOracleSwarmer_BitByPlayer;
            On.Spear.HitSomethingWithoutStopping += Spear_HitSomethingWithoutStopping;
            On.Player.ctor += Player_ctor;
            

            On.AbstractPhysicalObject.ctor += AbstractPhysicalObject_ctor;
            On.AbstractPhysicalObject.ToString += AbstractPhysicalObject_ToString;
            On.SaveState.AbstractPhysicalObjectFromString += SaveState_AbstractPhysicalObjectFromString;
            On.VoidSea.VoidSeaScene.SaintEndUpdate += VoidSeaScene_SaintEndUpdate;
            On.Lantern.Update += Lantern_Update;
            On.Lantern.DrawSprites += Lantern_DrawSprites;
            On.Lantern.TerrainImpact += Lantern_TerrainImpact;
            On.Room.AddObject += Room_AddObject;
            On.Lantern.ApplyPalette += Lantern_ApplyPalette;
            On.Player.StomachGlowLightColor += Player_StomachGlowLightColor;
            _ = new Hook(typeof(Lantern).GetMethod("MoreSlugcats.IProvideWarmth.get_warmth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), Lantern_get_warmth);
            _ = new Hook(typeof(Lantern).GetMethod("MoreSlugcats.IProvideWarmth.get_range", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance), Lantern_get_range);
            IL.Player.Update += Player_Update;
            IL.HologramLight.Needed += HologramLight_Needed;
            IL.BigSpiderAI.IUseARelationshipTracker_UpdateDynamicRelationship += BigSpiderAI_IUseARelationshipTracker_UpdateDynamicRelationship;
        }

        private const float VANILLALANTERNRADBASE = 250f;
        private const float VANILLALANTERNALPHA = 1f;
        // Parsing the string severeal times per frame to get the value sounds very slightly inefficient, so let's move it to a CWT to make it easier.
        static ConditionalWeakTable<AbstractPhysicalObject, StrongBox<int?>> lanternLitCycleCacheTable = new();
        private void AbstractPhysicalObject_ctor(On.AbstractPhysicalObject.orig_ctor orig, AbstractPhysicalObject self, World world, AbstractPhysicalObject.AbstractObjectType type, PhysicalObject realizedObject, WorldCoordinate pos, EntityID ID)
        {
            orig(self, world, type, realizedObject, pos, ID);
            if (PluginOptions.LanternsFade.Value &&
                self.type == AbstractPhysicalObject.AbstractObjectType.Lantern &&
                world != null &&
                world.game != null &&
                world.game.IsStorySession &&
                world.game.GetStorySession.saveState != null)
            {
                SetLanternLitCycle(self, world.game.GetStorySession.saveState.cycleNumber);
            }
        }
        private AbstractPhysicalObject SaveState_AbstractPhysicalObjectFromString(On.SaveState.orig_AbstractPhysicalObjectFromString orig, World world, string objString)
        {
            AbstractPhysicalObject ret = orig(world, objString);
            if (PluginOptions.LanternsFade.Value &&
                ret != null &&
                ret.type != null &&
                ret.type == AbstractPhysicalObject.AbstractObjectType.Lantern &&
                ret.unrecognizedAttributes != null)
            {
                for (int i = 0; i < ret.unrecognizedAttributes.Length; i++)
                {
                    if (ret.unrecognizedAttributes[i].StartsWith("glowNerfLitCycle:"))
                    {
                        string[] data = ret.unrecognizedAttributes[i].Split(':');
                        if (data.Length > 1 && int.TryParse(data[1], out int value))
                        {
                            Debug.Log("[Glow Nerf] Found lantern cycle in save data!");
                            SetLanternLitCycle(ret, value); // cache value found in save data
                            ret.unrecognizedAttributes = ret.unrecognizedAttributes.Where((x, j) => j != i).ToArray(); // remove value from unrecognizedattributes (it will be re-added at the end of the cycle)
                            break;
                        }
                        else
                        {
                            Debug.LogWarning("[Glow Nerf] Unable to parse lantern cycle data: " + ret.unrecognizedAttributes[i]);
                        }
                    }
                }
            }
            return ret;
        }
        private string AbstractPhysicalObject_ToString(On.AbstractPhysicalObject.orig_ToString orig, AbstractPhysicalObject self)
        {
            if (PluginOptions.LanternsFade.Value)
            {
                int? value = GetLanternLitCycle(self);
                if (value != null)
                {
                    // The value is saved in unrecognizedAttributes
                    string data = "glowNerfLitCycle:" + value;

                    // update value in save data
                    string[] normalUA;

                    if (self.unrecognizedAttributes == null)
                    {
                        normalUA = null;

                        self.unrecognizedAttributes = new string[] { data };
                    }
                    else
                    {
                        normalUA = new string[self.unrecognizedAttributes.Length];
                        self.unrecognizedAttributes.CopyTo(normalUA, 0);

                        self.unrecognizedAttributes = self.unrecognizedAttributes.Append(data).ToArray();
                    }

                    string ret = orig(self);

                    if (normalUA == null)
                    {
                        self.unrecognizedAttributes = null;
                    }
                    else
                    {
                        self.unrecognizedAttributes = new string[normalUA.Length];
                        normalUA.CopyTo(self.unrecognizedAttributes, 0);
                    }

                    return ret;
                }
            }

            return orig(self);
        }
        public static int? GetLanternLitCycle(AbstractPhysicalObject abstractLantern)
        {
            if (abstractLantern != null && lanternLitCycleCacheTable.TryGetValue(abstractLantern, out var boxedValue))
            {
                return boxedValue.Value;
            }
            return null;
        }
        public static void SetLanternLitCycle(AbstractPhysicalObject abstractLantern, int value)
        {
            if (abstractLantern == null)
            {
                Debug.LogWarning("[Glow Nerf] " + nameof(SetLanternLitCycle) + " called on null!");
                return;
            }

            Debug.Log("[Glow Nerf] Setting lantern " + abstractLantern.ID + " to cycle " + value);

            lanternLitCycleCacheTable.GetOrCreateValue(abstractLantern).Value = value;
        }

        // put out lanterns in between the Saint's loops
        private void VoidSeaScene_SaintEndUpdate(On.VoidSea.VoidSeaScene.orig_SaintEndUpdate orig, VoidSea.VoidSeaScene self)
        {
            if (PluginOptions.LanternsFade.Value &&
                self.fadeOutSaint < 0f &&
                !self.endingSavedFlag &&
                self.room.game.Players.Count > 0 &&
                self.room.game.FirstAlivePlayer.realizedCreature is Player player &&
                player.objectInStomach != null &&
                player.objectInStomach.type == AbstractPhysicalObject.AbstractObjectType.Lantern)
            {
                Debug.Log("[Glow Nerf] Found lantern in between loops!");
                SetLanternLitCycle(player.objectInStomach, -10 * PluginOptions.MAX_FADE_TIME); // put out the lantern by setting it to a large negative number
            }
            orig(self);
        }

        //static ConditionalWeakTable<AbstractPhysicalObject, StrongBox<float>> lanternMultiplierTable = new();
        private float CalculateLanternMultiplier(Lantern self)
        {
            if (self.stick != null)
            {
                return 1f; // lanterns on sticks always work normally
            }
            return CalculateLanternMultiplier(self.abstractPhysicalObject);
        }
        private float CalculateLanternMultiplier(AbstractPhysicalObject self)
        {
            if (self.type != AbstractPhysicalObject.AbstractObjectType.Lantern)
            {
                return 1f; // non-lanterns always work normally of course
            }
            if (PluginOptions.LanternsFade.Value && self.world != null && self.world.game != null && self.world.game.IsStorySession)
            {
                int? litCycle = GetLanternLitCycle(self);
                if (litCycle.HasValue)
                {
                    float multiplier = Mathf.LerpUnclamped(1, 0, (float)(self.world.game.GetStorySession.saveState.cycleNumber - litCycle.Value) / PluginOptions.LanternFadeTime.Value);
                    return PluginOptions.LanternIntensityMultiplier.Value * multiplier;
                }
            }
            return PluginOptions.LanternIntensityMultiplier.Value;
        }
        public float GetLanternMultiplier(Lantern self)
        {
            return Mathf.Clamp01(GetLanternMultiplierUnclamped(self));
        }
        public float GetLanternMultiplier(AbstractPhysicalObject self)
        {
            return Mathf.Clamp01(GetLanternMultiplierUnclamped(self));
        }
        public float GetLanternMultiplierUnclamped(Lantern self)
        {
            return CalculateLanternMultiplier(self); //lanternMultiplierTable.GetValue(self.abstractPhysicalObject, x => new(CalculateLanternMultiplier(x))).Value;
        }
        public float GetLanternMultiplierUnclamped(AbstractPhysicalObject self)
        {
            return self.type == AbstractPhysicalObject.AbstractObjectType.Lantern ? CalculateLanternMultiplier(self) /*lanternMultiplierTable.GetValue(self, x => new(CalculateLanternMultiplier(x))).Value*/ : 1f;
        }
        // make the lantern dimmer
        private void Lantern_Update(On.Lantern.orig_Update orig, Lantern self, bool eu)
        {
            bool appliedLight = self.lightSource != null;
            orig(self, eu);
            if (appliedLight && self.lightSource != null && self.lightSource.setRad != null && self.lightSource.setAlpha != null)
            {
                self.lightSource.setRad *= GetLanternMultiplier(self);
                self.lightSource.setAlpha *= GetLanternMultiplier(self);
            }
        }
        // make the lantern dimmer
        private void Lantern_DrawSprites(On.Lantern.orig_DrawSprites orig, Lantern self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);
            sLeaser.sprites[2].scale *= Mathf.Clamp01(Mathf.Pow(2 * GetLanternMultiplierUnclamped(self), 0.5f)); // glow more for longer
            sLeaser.sprites[3].scale *= GetLanternMultiplier(self);
            sLeaser.sprites[3].alpha = GetLanternMultiplier(self);
        }
        // make the lantern dimmer visually
        private void Lantern_ApplyPalette(On.Lantern.orig_ApplyPalette orig, Lantern self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);
            // start visibly darkening the lantern sprite once the light has run out
            float colorFade = Mathf.Clamp(1 - GetLanternMultiplierUnclamped(self) - 1, 0, 0.5f);
            sLeaser.sprites[0].color = Color.Lerp(sLeaser.sprites[0].color, palette.blackColor, colorFade);
            sLeaser.sprites[1].color = Color.Lerp(sLeaser.sprites[1].color, palette.blackColor, colorFade);
        }

        // make the lantern emit less sparks when it hits something
        static float? lanternTerrainImpactFlag = null;
        private void Lantern_TerrainImpact(On.Lantern.orig_TerrainImpact orig, Lantern self, int chunk, RWCustom.IntVector2 direction, float speed, bool firstContact)
        {
            lanternTerrainImpactFlag = GetLanternMultiplierUnclamped(self);
            orig(self, chunk, direction, speed, firstContact);
            lanternTerrainImpactFlag = null;
        }
        private void Room_AddObject(On.Room.orig_AddObject orig, Room self, UpdatableAndDeletable obj)
        {
            if (lanternTerrainImpactFlag != null && lanternTerrainImpactFlag <= UnityEngine.Random.value)
            {
                return;
            }
            orig(self, obj);
        }

        // make the lantern dimmer when swallowed
        private Color? Player_StomachGlowLightColor(On.Player.orig_StomachGlowLightColor orig, Player self)
        {
            Color? ret = orig(self);
            if (ret != null)
            {
                AbstractPhysicalObject stomachObject = self.AI == null ? self.objectInStomach : (self.State as MoreSlugcats.PlayerNPCState).StomachObject;
                if (stomachObject != null && stomachObject.type == AbstractPhysicalObject.AbstractObjectType.Lantern)
                {
                    Color color = ret.Value;
                    color.a *= GetLanternMultiplier(stomachObject);
                    return color;
                }
            }
            return ret;
        }
        // make the lantern colder
        private float Lantern_get_range(Func<Lantern, float> orig, Lantern self)
        {
            return orig(self) * GetLanternMultiplier(self);
        }
        // make the lantern colder
        private float Lantern_get_warmth(Func<Lantern, float> orig, Lantern self)
        {
            return orig(self) * GetLanternMultiplier(self);
        }
        // make the lantern colder when swallowed
        private void Player_Update(ILContext il)
        {
            ILCursor cursor = new(il);
            if (cursor.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(AbstractPhysicalObject.AbstractObjectType), nameof(AbstractPhysicalObject.AbstractObjectType.Lantern))) &&
                cursor.TryGotoNext(MoveType.After,
                x => x.MatchCall(typeof(RainWorldGame), "get_DefaultHeatSourceWarmth")))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, typeof(Player).GetField(nameof(Player.objectInStomach)));
                cursor.EmitDelegate(delegate (AbstractPhysicalObject lantern)
                {
                    return GetLanternMultiplier(lantern);
                });
                cursor.Emit(OpCodes.Mul);
            }
            else
            {
                Logger.LogError("Failed to hook Player.Update: no match found.");
            }
        }
        // make the lantern not count against overseer light
        private void HologramLight_Needed(ILContext il)
        {
            ILCursor cursor = new(il);
            if (cursor.TryGotoNext(MoveType.After,
                //x => x.MatchLdarg(0),
                //x => x.MatchCallvirt(typeof(Creature), "get_grasps"),
                //x => x.MatchLdloc(1),
                //x => x.MatchLdelemRef(),
                //x => x.MatchLdfld(typeof(Creature.Grasp), nameof(Creature.Grasp.grabbed)),
                x => x.MatchIsinst(typeof(Lantern))))
            {
                cursor.EmitDelegate(delegate (Lantern lantern)
                {
                    if (lantern != null)
                    Debug.Log(GetLanternMultiplier(lantern));
                    return lantern != null && GetLanternMultiplier(lantern) > 0.15f;
                });
            }
            else
            {
                Logger.LogError("Failed to hook HologramLight.Needed: no match found.");
            }
        }
        private void BigSpiderAI_IUseARelationshipTracker_UpdateDynamicRelationship(ILContext il)
        {
            ILCursor cursor = new(il);
            if (cursor.TryGotoNext(MoveType.After,
                //x => x.MatchLdfld(typeof(RelationshipTracker.DynamicRelationship), nameof(RelationshipTracker.DynamicRelationship.trackerRep)),
                //x => x.MatchLdfld(typeof(Tracker.CreatureRepresentation), nameof(Tracker.CreatureRepresentation.representedCreature)),
                //x => x.MatchCallvirt(typeof(AbstractCreature), "get_realizedCreature"),
                //x => x.MatchCallvirt(typeof(Creature.Grasp), "get_grasps"),
                //x => x.MatchLdloc(6),
                //x => x.MatchLdelemRef(),
                //x => x.MatchLdfld(typeof(Creature.Grasp), nameof(Creature.Grasp.grabbed)),
                x => x.MatchIsinst(typeof(Lantern))))
            {
                cursor.EmitDelegate(delegate (Lantern lantern)
                {
                    return lantern != null && GetLanternMultiplier(lantern) > 0.1f;
                });
            }
            else
            {
                Logger.LogError("Failed to hook BigSpiderAI.IUseARelationshipTracker.UpdateDynamicRelationship: no match found.");
            }
        }



        private const float VANILLALIGHTRAD = 300f;
        private const float VANILLALIGHTALPHA = 1f;
        private const string CYCLES_SINCE_NEURON_SAVE_STRING = "23848.glownerf.CYCLESSINCENEURON";
        private readonly ConditionalWeakTable<SaveState, StrongBox<int>> cyclesSinceNeuronCache = new();
        private int GetCyclesSinceNeuron(SaveState saveState)
        {
            if (cyclesSinceNeuronCache.TryGetValue(saveState, out StrongBox<int> value))
            {
                return value.Value;
            }
            else if (!saveState.unrecognizedSaveStrings.Any(x => x.StartsWith(CYCLES_SINCE_NEURON_SAVE_STRING)))
            {
                cyclesSinceNeuronCache.Add(saveState, new(0));
                return 0;
            }
            else
            {
                int ret = int.Parse(saveState.unrecognizedSaveStrings.First(x => x.StartsWith(CYCLES_SINCE_NEURON_SAVE_STRING)).Split(':')[1]);
                cyclesSinceNeuronCache.Add(saveState, new(ret));
                return ret;
            }
        }
        private void SetCyclesSinceNeuron(SaveState saveState, int cyclesSinceNeuron)
        {
            saveState.unrecognizedSaveStrings.RemoveAll(x => x.StartsWith(CYCLES_SINCE_NEURON_SAVE_STRING));
            saveState.unrecognizedSaveStrings.Add(CYCLES_SINCE_NEURON_SAVE_STRING + ":" + cyclesSinceNeuron);
            cyclesSinceNeuronCache.Remove(saveState);
        }
        private float GlowMultiplier(Player self)
        {
            if (PluginOptions.GlowFades.Value && self.room.world.game.IsStorySession)
            {
                float multiplier = Mathf.Lerp(1, 0, (float)GetCyclesSinceNeuron(self.abstractCreature.world.game.GetStorySession.saveState) / PluginOptions.GlowFadeTime.Value);
                return PluginOptions.IntensityMultiplier.Value * multiplier;
            }
            else
            {
                return PluginOptions.IntensityMultiplier.Value;
            }
        }
        private void ResetGlow(Player player)
        {
            PlayerGraphics graphics = player.graphicsModule as PlayerGraphics;
            if (graphics.lightSource != null)
            {
                graphics.lightSource.HardSetRad(VANILLALIGHTRAD * GlowMultiplier(player));
                graphics.lightSource.HardSetAlpha(VANILLALIGHTALPHA * GlowMultiplier(player));
            }
        }
        private void OracleSwarmer_BitByPlayer(On.OracleSwarmer.orig_BitByPlayer orig, OracleSwarmer self, Creature.Grasp grasp, bool eu)
        {
            orig(self, grasp, eu);
            if (PluginOptions.GlowFades.Value && self.room.world.game.IsStorySession && self.bites < 1 && grasp.grabber is Player p && (!ModManager.MSC || !p.isNPC))
            {
                SetCyclesSinceNeuron(self.room.world.game.GetStorySession.saveState, 0);
                ResetGlow(p);
            }
        }
        private void SLOracleSwarmer_BitByPlayer(On.SLOracleSwarmer.orig_BitByPlayer orig, SLOracleSwarmer self, Creature.Grasp grasp, bool eu)
        {
            orig(self, grasp, eu);
            if (PluginOptions.GlowFades.Value && self.room.world.game.IsStorySession && self.bites < 1 && grasp.grabber is Player p && (!ModManager.MSC || !p.isNPC))
            {
                SetCyclesSinceNeuron(self.room.world.game.GetStorySession.saveState, 0);
                ResetGlow(p);
            }
        }
        private void Spear_HitSomethingWithoutStopping(On.Spear.orig_HitSomethingWithoutStopping orig, Spear self, PhysicalObject obj, BodyChunk chunk, PhysicalObject.Appendage appendage)
        {
            orig(self, obj, chunk, appendage);
            if (PluginOptions.GlowFades.Value && self.room.world.game.IsStorySession && self.Spear_NeedleCanFeed() && obj is OracleSwarmer && self.thrownBy is Player p && (!ModManager.MSC || !p.isNPC))
            {
                SetCyclesSinceNeuron(self.room.world.game.GetStorySession.saveState, 0);
                ResetGlow(p);
            }
        }
        private void SaveState_RainCycleTick(On.SaveState.orig_RainCycleTick orig, SaveState self, RainWorldGame game, bool depleteSwarmRoom)
        {
            orig(self, game, depleteSwarmRoom);
            if (PluginOptions.GlowFades.Value && self.theGlow)
            {
                SetCyclesSinceNeuron(self, GetCyclesSinceNeuron(self) + 1);
            }
        }
        private void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            if (PluginOptions.GlowFades.Value && self.AI == null && self.glowing && abstractCreature.Room.world.game.IsStorySession && GlowMultiplier(self) <= 0)
            {
                self.glowing = false;
            }
        }
        // Actually apply the mod
        private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);
            if (self.lightSource != null && self.lightSource.Rad == VANILLALIGHTRAD && self.lightSource.Alpha == VANILLALIGHTALPHA)
            {
                self.lightSource.HardSetRad(VANILLALIGHTRAD * GlowMultiplier(self.player));
                self.lightSource.HardSetAlpha(VANILLALIGHTALPHA * GlowMultiplier(self.player));
            }
        }
        // The Void Sea sets the player's light source every frame, as it slowly fades it out as you get deeper in.
        private void VoidSeaScene_VoidSeaTreatment(On.VoidSea.VoidSeaScene.orig_VoidSeaTreatment orig, VoidSea.VoidSeaScene self, Player player, float swimSpeed)
        {
            orig(self, player, swimSpeed);
            if (player.graphicsModule != null && (player.graphicsModule as PlayerGraphics).lightSource != null)
            {
                (player.graphicsModule as PlayerGraphics).lightSource.setAlpha *= GlowMultiplier(player);
                (player.graphicsModule as PlayerGraphics).lightSource.setRad *= GlowMultiplier(player);
            }
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            Debug.Log("Glow Nerf config setup: " + MachineConnector.SetRegisteredOI(PluginInfo.PLUGIN_GUID, PluginOptions.Instance));
        }
    }
}