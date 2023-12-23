using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
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