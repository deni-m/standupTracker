using System;

namespace StandUpTracker.Models
{
    public class ActiveSample
    {
        public string Process { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public TimeSpan Duration => (End == default ? DateTime.Now : End) - Start;

        public string Key => $"{Process}|{Title}";
    }
}
