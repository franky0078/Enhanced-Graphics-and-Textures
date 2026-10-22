using System;
using Game.Settings;
using Game.UI;

namespace Advanced_Graphics
{
    public sealed partial class Setting
    {
        private static float ClampEndcapScale(float value) => float.IsNaN(value) || float.IsInfinity(value)
            ? 1f : Math.Max(0.1f, Math.Min(8f, (float)Math.Round(value, 1)));
        private float m_SidewalkEndcapU = 1f;
        private float? m_SidewalkEndcapUDraft;
        [SettingsUIHidden]
        public float SidewalkEndcapU { get => m_SidewalkEndcapU; set => m_SidewalkEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkEndcapUDraft { get => m_SidewalkEndcapUDraft ?? SidewalkEndcapU; set => m_SidewalkEndcapUDraft = ClampEndcapScale(value); }
        private float m_SidewalkEndcapV = 1f;
        private float? m_SidewalkEndcapVDraft;
        [SettingsUIHidden]
        public float SidewalkEndcapV { get => m_SidewalkEndcapV; set => m_SidewalkEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float SidewalkEndcapVDraft { get => m_SidewalkEndcapVDraft ?? SidewalkEndcapV; set => m_SidewalkEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(SidewalkSection, SidewalkGroup)]
        [SettingsUIButton]
        public bool ApplySidewalkEndcap { set { if (value) { ApplyEndcapValues("sidewalk", SidewalkEndcapUDraft, SidewalkEndcapVDraft); ApplyAndSave(); } } }
        private float m_AlleyEndcapU = 1f;
        private float? m_AlleyEndcapUDraft;
        [SettingsUIHidden]
        public float AlleyEndcapU { get => m_AlleyEndcapU; set => m_AlleyEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleyEndcapUDraft { get => m_AlleyEndcapUDraft ?? AlleyEndcapU; set => m_AlleyEndcapUDraft = ClampEndcapScale(value); }
        private float m_AlleyEndcapV = 1f;
        private float? m_AlleyEndcapVDraft;
        [SettingsUIHidden]
        public float AlleyEndcapV { get => m_AlleyEndcapV; set => m_AlleyEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float AlleyEndcapVDraft { get => m_AlleyEndcapVDraft ?? AlleyEndcapV; set => m_AlleyEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(AlleySidewalkSection, AlleySidewalkGroup)]
        [SettingsUIButton]
        public bool ApplyAlleyEndcap { set { if (value) { ApplyEndcapValues("alley", AlleyEndcapUDraft, AlleyEndcapVDraft); ApplyAndSave(); } } }
        private float m_ParkingEndcapU = 1f;
        private float? m_ParkingEndcapUDraft;
        [SettingsUIHidden]
        public float ParkingEndcapU { get => m_ParkingEndcapU; set => m_ParkingEndcapU = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingEndcapUDraft { get => m_ParkingEndcapUDraft ?? ParkingEndcapU; set => m_ParkingEndcapUDraft = ClampEndcapScale(value); }
        private float m_ParkingEndcapV = 1f;
        private float? m_ParkingEndcapVDraft;
        [SettingsUIHidden]
        public float ParkingEndcapV { get => m_ParkingEndcapV; set => m_ParkingEndcapV = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUISlider(min = 0.1f, max = 8f, step = 0.1f, scalarMultiplier = 1, unit = Unit.kFloatSingleFraction)]
        public float ParkingEndcapVDraft { get => m_ParkingEndcapVDraft ?? ParkingEndcapV; set => m_ParkingEndcapVDraft = ClampEndcapScale(value); }
        [SettingsUISection(RoadSection, RoadGroup)]
        [SettingsUIButton]
        public bool ApplyParkingEndcap { set { if (value) { ApplyEndcapValues("parking", ParkingEndcapUDraft, ParkingEndcapVDraft); ApplyAndSave(); } } }
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
            }
        }
        private void ResetEndcapValues()
        {
            ApplyEndcapValues("sidewalk", 1f, 1f);
            ApplyEndcapValues("alley", 1f, 1f);
            ApplyEndcapValues("parking", 1f, 1f);
            m_SidewalkEndcapUDraft = m_SidewalkEndcapVDraft = null;
            m_AlleyEndcapUDraft = m_AlleyEndcapVDraft = null;
            m_ParkingEndcapUDraft = m_ParkingEndcapVDraft = null;
        }
    }
}
