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
        }

        private const float VANILLALIGHTRAD = 300f;
        private const float VANILLALIGHTALPHA = 1f;
        private void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);
            if (self.lightSource != null && self.lightSource.Rad == VANILLALIGHTRAD && self.lightSource.Alpha == VANILLALIGHTALPHA)
            {
                self.lightSource.HardSetRad(VANILLALIGHTRAD * PluginOptions.IntensityMultiplier.Value);
                self.lightSource.HardSetAlpha(VANILLALIGHTALPHA * PluginOptions.IntensityMultiplier.Value);
            }
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            Debug.Log("Glow Nerf config setup: " + MachineConnector.SetRegisteredOI(PluginInfo.PLUGIN_GUID, PluginOptions.Instance));
        }
    }
}