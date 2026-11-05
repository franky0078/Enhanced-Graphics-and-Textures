using System;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        private static float ClampEndcapScale(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? 1f : Math.Max(0.1f, Math.Min(8f, (float)Math.Round(value, 2)));
        private float m_SidewalkEndcapU = 1f;
        private float? m_SidewalkEndcapUDraft;
        [SettingsUIHidden]
        public float SidewalkEndcapU { get => m_SidewalkEndcapU; set => m_SidewalkEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkEndcapUDraft { get => m_SidewalkEndcapUDraft ?? SidewalkEndcapU; set => m_SidewalkEndcapUDraft = ClampEndcapScale(value); }
        private float m_SidewalkEndcapV = 1f;
        private float? m_SidewalkEndcapVDraft;
        [SettingsUIHidden]
        public float SidewalkEndcapV { get => m_SidewalkEndcapV; set => m_SidewalkEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkEndcapVDraft { get => m_SidewalkEndcapVDraft ?? SidewalkEndcapV; set => m_SidewalkEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ApplySidewalkEndcap { set { if (value) { ApplyEndcapValues("sidewalk", SidewalkEndcapUDraft, SidewalkEndcapVDraft); ApplyAndSave(); } } }
        private float m_AlleyEndcapU = 1f;
        private float? m_AlleyEndcapUDraft;
        [SettingsUIHidden]
        public float AlleyEndcapU { get => m_AlleyEndcapU; set => m_AlleyEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleyEndcapUDraft { get => m_AlleyEndcapUDraft ?? AlleyEndcapU; set => m_AlleyEndcapUDraft = ClampEndcapScale(value); }
        private float m_AlleyEndcapV = 1f;
        private float? m_AlleyEndcapVDraft;
        [SettingsUIHidden]
        public float AlleyEndcapV { get => m_AlleyEndcapV; set => m_AlleyEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleyEndcapVDraft { get => m_AlleyEndcapVDraft ?? AlleyEndcapV; set => m_AlleyEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ApplyAlleyEndcap { set { if (value) { ApplyEndcapValues("alley", AlleyEndcapUDraft, AlleyEndcapVDraft); ApplyAndSave(); } } }
        private float m_ParkingEndcapU = 1f;
        private float? m_ParkingEndcapUDraft;
        [SettingsUIHidden]
        public float ParkingEndcapU { get => m_ParkingEndcapU; set => m_ParkingEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingEndcapUDraft { get => m_ParkingEndcapUDraft ?? ParkingEndcapU; set => m_ParkingEndcapUDraft = ClampEndcapScale(value); }
        private float m_ParkingEndcapV = 1f;
        private float? m_ParkingEndcapVDraft;
        [SettingsUIHidden]
        public float ParkingEndcapV { get => m_ParkingEndcapV; set => m_ParkingEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingEndcapVDraft { get => m_ParkingEndcapVDraft ?? ParkingEndcapV; set => m_ParkingEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ApplyParkingEndcap { set { if (value) { ApplyEndcapValues("parking", ParkingEndcapUDraft, ParkingEndcapVDraft); ApplyAndSave(); } } }
        private float m_LargeRoadEndcapU = 1f, m_LargeRoadEndcapV = 1f;
        private float? m_LargeRoadEndcapUDraft, m_LargeRoadEndcapVDraft;
        [SettingsUIHidden]
        public float LargeRoadEndcapU { get => m_LargeRoadEndcapU; set => m_LargeRoadEndcapU = ClampEndcapScale(value); }
        [SettingsUIHidden]
        public float LargeRoadEndcapV { get => m_LargeRoadEndcapV; set => m_LargeRoadEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float LargeRoadEndcapUDraft { get => m_LargeRoadEndcapUDraft ?? LargeRoadEndcapU; set => m_LargeRoadEndcapUDraft = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.05f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float LargeRoadEndcapVDraft { get => m_LargeRoadEndcapVDraft ?? LargeRoadEndcapV; set => m_LargeRoadEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(TexturesDisabled))]
        [SettingsUIButton]
        public bool ApplyLargeRoadEndcap { set { if (value) { ApplyEndcapValues("large", LargeRoadEndcapUDraft, LargeRoadEndcapVDraft); ApplyAndSave(); } } }
        internal bool HasSidewalkEndcapChanges => SidewalkEndcapU != 1f || SidewalkEndcapV != 1f ||
            RoadEndTilingU != 1f || RoadEndTilingV != 1f || LargeRoadEndcapU != 1f || LargeRoadEndcapV != 1f;

        private float m_RoadEndTilingU = 1f, m_RoadEndTilingV = 1f;
        [SettingsUIHidden]
        public float RoadEndTilingU { get => m_RoadEndTilingU; set => m_RoadEndTilingU = ClampEndcapScale(value); }
        [SettingsUIHidden]
        public float RoadEndTilingV { get => m_RoadEndTilingV; set => m_RoadEndTilingV = ClampEndcapScale(value); }
        private float m_ParkingSidewalkScaleU = 1f, m_ParkingSidewalkScaleV = 1f;
        [SettingsUIHidden]
        public float ParkingSidewalkScaleU { get => m_ParkingSidewalkScaleU; set => m_ParkingSidewalkScaleU = ClampEndcapScale(value); }
        [SettingsUIHidden]
        public float ParkingSidewalkScaleV { get => m_ParkingSidewalkScaleV; set => m_ParkingSidewalkScaleV = ClampEndcapScale(value); }
        private int m_SidewalkShiftU, m_SidewalkShiftV;
        [SettingsUIHidden]
        public int SidewalkShiftU { get => m_SidewalkShiftU; set => m_SidewalkShiftU = Math.Max(0, Math.Min(100, value)); }
        [SettingsUIHidden]
        public int SidewalkShiftV { get => m_SidewalkShiftV; set => m_SidewalkShiftV = Math.Max(0, Math.Min(100, value)); }
        internal void ApplyEndcapValues(string kind, float u, float v)
        {
            switch (kind)
            {
                case "sidewalk":
                    SidewalkEndcapU = SidewalkEndcapUDraft = u;
                    SidewalkEndcapV = SidewalkEndcapVDraft = v;
                    break;
                case "alley":
                    AlleyEndcapU = AlleyEndcapUDraft = u;
                    AlleyEndcapV = AlleyEndcapVDraft = v;
                    break;
                case "parking":
                    ParkingEndcapU = ParkingEndcapUDraft = u;
                    ParkingEndcapV = ParkingEndcapVDraft = v;
                    break;
                case "large":
                    LargeRoadEndcapU = LargeRoadEndcapUDraft = u;
                    LargeRoadEndcapV = LargeRoadEndcapVDraft = v;
                    break;
                case "roadend":
                    RoadEndTilingU = u;
                    RoadEndTilingV = v;
                    break;
            }
        }
        private void ResetEndcapValues()
        {
            ApplyEndcapValues("sidewalk", 1f, 1f);
            ApplyEndcapValues("large", 1f, 1f);
            ApplyEndcapValues("alley", 1f, 1f);
            ApplyEndcapValues("parking", 1f, 1f);
            ApplyEndcapValues("roadend", 1f, 1f);
            m_LargeRoadEndcapUDraft = m_LargeRoadEndcapVDraft = null;
            m_SidewalkEndcapUDraft = m_SidewalkEndcapVDraft = null;
            m_AlleyEndcapUDraft = m_AlleyEndcapVDraft = null;
            m_ParkingEndcapUDraft = m_ParkingEndcapVDraft = null;
        }
    }
}


