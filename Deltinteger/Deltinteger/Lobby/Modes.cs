using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Deltin.Deltinteger.Lobby
{
    public class ModesRoot
    {
        public HeroList All { get; set; }
        
        public HeroList Assault { get; set; }
        
        public HeroList Control { get; set; }
        
        public HeroList Escort { get; set; }
        
        public HeroList Hybrid { get; set; }
        
        [JsonProperty("Capture The Flag")]
        public HeroList CaptureTheFlag { get; set; }
        
        public HeroList Deathmatch { get; set; }
        
        public HeroList Elimination { get; set; }
        
        [JsonProperty("Team Deathmatch")]
        public HeroList TeamDeathmatch { get; set; }
        
        public HeroList Skirmish { get; set; }
        
        [JsonProperty("Practice Range")]
        public HeroList PracticeRange { get; set; }

        public void MergeModeSettings()
        {
            if (All == null) return;
            foreach (var value in All)
            {
                MergeTo(value, Assault);
                MergeTo(value, CaptureTheFlag);
                MergeTo(value, Control);
                MergeTo(value, Deathmatch);
                MergeTo(value, Elimination);
                MergeTo(value, Escort);
                MergeTo(value, Hybrid);
                MergeTo(value, PracticeRange);
                MergeTo(value, Skirmish);
                MergeTo(value, TeamDeathmatch);
            }
        }

        private void MergeTo(KeyValuePair<string, WorkshopValuePair> pair, HeroList set)
        {
            if (set == null || set.ContainsKey(pair.Key)) return;
            set.Add(pair.Key, pair.Value);
        }
    }
    
    public class ModeSettingCollection : LobbySettingCollection<ModeSettingCollection>
    {
        private static LobbySetting[] DefaultModeSettings = new LobbySetting[]
        {
            new SwitchValue("Enemy Health Bars", true),
            new SelectValue("Game Mode Start", "All Slots Filled", "Immediately", "Manual"),
            new RangeValue("Health Pack Respawn Time Scalar", 10, 500),
            new SwitchValue("Kill Cam", true),
            new SwitchValue("Kill Feed", true),
            new SwitchValue("Skins", true),
            new SelectValue("Spawn Health Packs", "Determined By Mode", "Enabled", "Disabled"),
            new SwitchValue("Allow Hero Switching", true),
            new SelectValue("Hero Limit", "1 Per Team", "2 Per Team", "1 Per Game", "2 Per Game"),
            new SelectValue("Limit Roles", "2 Of Each Role Per Team", "Off"),
            new SwitchValue("Respawn As Random Hero", false),
            new RangeValue("Respawn Time Scalar", 0, 100)
        };
        private static LobbySetting CaptureSpeed = new RangeValue("Capture Speed Modifier", 10, 500);
        private static LobbySetting PayloadSpeed = new RangeValue("Payload Speed Modifier", 10, 500);
        private static LobbySetting CompetitiveRules = new SwitchValue("Competitive Rules", false);
        private static LobbySetting Enabled_DefaultOn = new SwitchValue("Enabled", true) { ReferenceName = "Enabled_DefaultOn" };
        private static LobbySetting Enabled_DefaultOff = new SwitchValue("Enabled", false) { ReferenceName = "Enabled_DefaultOff" };

        public static ModeSettingCollection[] AllModeSettings = new ModeSettingCollection[] {
            new ModeSettingCollection("All"),
            new ModeSettingCollection("Assault", true).Competitive().AddCaptureSpeed(),
            new ModeSettingCollection("Control", true).Competitive().AddCaptureSpeed().AddSelect("Limit Valid Control Points", "All", "First", "Second", "Third").AddIntRange("Score To Win", 1, 3, 2).AddRange("Scoring Speed Modifier", 10, 500),
            new ModeSettingCollection("Escort", true).Competitive().AddPayloadSpeed(),
            new ModeSettingCollection("Hybrid", true).Competitive().AddCaptureSpeed().AddPayloadSpeed(),
        };

        public string ModeName { get; }

        public ModeSettingCollection(string title)
        {
            ModeName = title;
            Title = title;
            AddRange(DefaultModeSettings);
        }

        public ModeSettingCollection(string modeName, bool defaultEnabled)
        {
            ModeName = modeName;
            Title = $"{ModeName} settings.";

            if (defaultEnabled) Add(Enabled_DefaultOn);
            else Add(Enabled_DefaultOff);

            AddRange(DefaultModeSettings);
        }

        public ModeSettingCollection AddCaptureSpeed()
        {
            Add(CaptureSpeed);
            return this;
        }

        public ModeSettingCollection AddPayloadSpeed()
        {
            Add(PayloadSpeed);
            return this;
        }

        public ModeSettingCollection Competitive()
        {
            Add(CompetitiveRules);
            return this;
        }
    }
}