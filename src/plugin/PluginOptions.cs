using Menu.Remix.MixedUI;
using UnityEngine;

namespace GlowNerf
{
    public class PluginOptions : OptionInterface
    {
        public static PluginOptions Instance = new();

        public const int MAX_FADE_TIME = 1000000;
        public static Configurable<float> IntensityMultiplier = Instance.config.Bind("IntensityMultiplier", 0.5f, new ConfigurableInfo("The intensity of the glow effect; 0 is no light, 1 is the normal amount."));
        public static Configurable<float> LanternIntensityMultiplier = Instance.config.Bind("LanternIntensityMultiplier", 1f, new ConfigurableInfo("The intensity of lantern light; 0 is no light, 1 is the normal amount."));
        public static Configurable<bool> GlowFades = Instance.config.Bind("GlowFades", false, new ConfigurableInfo("Causes the glow effect to disappear over time."));
        public static Configurable<bool> LanternsFade = Instance.config.Bind("LanternsFade", false, new ConfigurableInfo("Causes lanterns to lose their light over time."));
        public static Configurable<int> GlowFadeTime = Instance.config.Bind("GlowFadeTime", 10, new ConfigurableInfo("The number of cycles it takes to lose the glow effect.", new ConfigAcceptableRange<int>(1, MAX_FADE_TIME)));
        public static Configurable<int> LanternFadeTime = Instance.config.Bind("LanternFadeTime", 20, new ConfigurableInfo("The number of cycles it takes for a lantern to die out.", new ConfigAcceptableRange<int>(1, MAX_FADE_TIME)));

        public override void Initialize()
        {
            base.Initialize();
            Tabs = new OpTab[1];

            Tabs[0] = new(Instance, "Options");
            SliderOption(IntensityMultiplier, 200, 0, "Glow Intensity");
            SliderOption(LanternIntensityMultiplier, 200, 1, "Lantern Intensity");

            Tabs[0].AddItems(
                new OpCheckBox(GlowFades, new(50, 460)) { description = GlowFades.info.description },
                new OpLabel(new Vector2(90, 460), new Vector2(), "Glow fades over ", FLabelAlignment.Left),
                new OpUpdown(GlowFadeTime, new(150 + 35, 455), 50) { description = GlowFadeTime.info.description },
                new OpLabel(new Vector2(205 + 35, 460), new Vector2(), "cycles", FLabelAlignment.Left));
            Tabs[0].AddItems(
                new OpCheckBox(LanternsFade, new(50, 430)) { description = LanternsFade.info.description },
                new OpLabel(new Vector2(90, 430), new Vector2(), "Lanterns fade over ", FLabelAlignment.Left),
                new OpUpdown(LanternFadeTime, new(150 + 49, 425), 50) { description = LanternFadeTime.info.description },
                new OpLabel(new Vector2(205 + 49, 430), new Vector2(), "cycles", FLabelAlignment.Left));
        }

        private void CheckBoxOption(Configurable<bool> setting, float pos, string label)
        {
            Tabs[0].AddItems(new OpCheckBox(setting, new(50, 550 - pos * 30)) { description = setting.info.description }, new OpLabel(new Vector2(90, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
        private void SliderOption(Configurable<float> setting, int size, float pos, string label)
        {
            Tabs[0].AddItems(new OpFloatSlider(setting, new(50, 545 - pos * 30), size) { description = setting.info.description, Increment = size / 200 }, new OpLabel(new Vector2(60 + size, 550 - pos * 30), new Vector2(), label, FLabelAlignment.Left));
        }
    }
}