using Game.Settings;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        [SettingsUISection(RoadSection, RoadGroup)]
        public bool UseRoadNormalMap { get; set; } = false;

        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        public bool UseSidewalkNormalMap { get; set; } = false;

        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        public bool UseAlleySidewalkNormalMap { get; set; } = false;

        [SettingsUISection(GravelSection, GravelGroup)]
        public bool UseRoadGravelNormalMap { get; set; } = false;

        [SettingsUISection(GravelSidewalkSection, ShoulderGroup)]
        public bool UseGravelShoulderNormalMap { get; set; } = false;

        [SettingsUISection(RailSection, RailGroup)]
        public bool UseRailNormalMap { get; set; } = false;

        [SettingsUISection(GrassSection, GrassGroup)]
        public bool UseGrassNormalMap { get; set; } = false;

        [SettingsUISection(FootpathSection, FootpathGroup)]
        public bool UseFootpathNormalMap { get; set; } = false;

        internal bool GetCustomNormalEnabled(string surface)
        {
            switch (surface)
            {
                case "road": case "Road_BaseColor.png": return UseRoadNormalMap;
                case "sidewalk": case "Sidewalk_BaseColor.png": return UseSidewalkNormalMap;
                case "alley": case "AlleySidewalk_BaseColor.png": return UseAlleySidewalkNormalMap;
                case "gravel": case "RoadGravel_BaseColor.png": return UseRoadGravelNormalMap;
                case "shoulder": case "GravelShoulder_BaseColor.png": return UseGravelShoulderNormalMap;
                case "rail": case "RailGravel_BaseColor.png": return UseRailNormalMap;
                case "grass": case "Grass_BaseColor.png": return UseGrassNormalMap;
                case "footpath": case "Footpath_BaseColor.png": return UseFootpathNormalMap;
                default: return false;
            }
        }
        internal bool SetCustomNormalEnabled(string surface, bool enabled)
        {
            switch (surface)
            {
                case "road": UseRoadNormalMap = enabled; break;
                case "sidewalk": UseSidewalkNormalMap = enabled; break;
                case "alley": UseAlleySidewalkNormalMap = enabled; break;
                case "gravel": UseRoadGravelNormalMap = enabled; break;
                case "shoulder": UseGravelShoulderNormalMap = enabled; break;
                case "rail": UseRailNormalMap = enabled; break;
                case "grass": UseGrassNormalMap = enabled; break;
                case "footpath": UseFootpathNormalMap = enabled; break;
                default: return false;
            }
            return true;
        }
    }
}
