using System;
using System.Collections.Generic;

namespace CustomRPC
{
    /// <summary>
    /// One user-defined Dynamic Presence preset.
    /// </summary>
    [Serializable]
    public sealed class DynamicStatus
    {
        public string Name { get; set; }
        public string Details { get; set; }
        public string DetailsURL { get; set; }
        public string State { get; set; }
        public string StateURL { get; set; }

        public int Type { get; set; }
        public int Display { get; set; }

        public int PartySize { get; set; }
        public int PartyMax { get; set; }

        public int Timestamps { get; set; }
        public DateTime CustomTimestamp { get; set; }
        public bool CustomTimestampEndEnabled { get; set; }
        public DateTime CustomTimestampEnd { get; set; }

        public string LargeKey { get; set; }
        public string LargeText { get; set; }
        public string LargeURL { get; set; }
        public string SmallKey { get; set; }
        public string SmallText { get; set; }
        public string SmallURL { get; set; }

        public string Button1Text { get; set; }
        public string Button1URL { get; set; }
        public string Button2Text { get; set; }
        public string Button2URL { get; set; }

        public bool ProcessTriggerEnabled { get; set; }
        public bool Fallback { get; set; }
        public int Priority { get; set; }
        public List<string> Processes { get; set; }

        public DynamicStatus()
        {
            Name = "";
            Details = "";
            DetailsURL = "";
            State = "";
            StateURL = "";
            Type = 0;
            Display = 0;
            PartySize = 0;
            PartyMax = 0;
            Timestamps = 0;
            CustomTimestamp = DateTime.Now;
            CustomTimestampEndEnabled = false;
            CustomTimestampEnd = DateTime.Now;
            LargeKey = "";
            LargeText = "";
            LargeURL = "";
            SmallKey = "";
            SmallText = "";
            SmallURL = "";
            Button1Text = "";
            Button1URL = "";
            Button2Text = "";
            Button2URL = "";
            ProcessTriggerEnabled = false;
            Fallback = false;
            Priority = 100;
            Processes = new List<string>();
        }
    }
}
