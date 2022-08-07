namespace Celeste.Mod.CelesteRTDataProvider
{
    public class CelesteRTDataProviderModuleSettings : EverestModuleSettings
    {
        [SettingNeedsRelaunch]
        [SettingNumberInput(false, 4)]
        [SettingName("WS Port")]
        public int serverPort { get; set; } = 8080;
    }
}
