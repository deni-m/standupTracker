using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using StandUpTracker.Config;
using StandUpTracker.Models;

namespace StandUpTracker.Services
{
    public class ActivityLogger
    {
        private readonly Dictionary<string, TimeSpan> _totalsToday = new();

        // ➜ керуємо активною датою файлу
        private DateTime _currentDate = DateTime.Now.Date;

        // ➜ накопичувач активного часу за день (у секундах)
        private int _totalActiveSecToday = 0;

        // ➜ чи відкрита "сесія" зараз (між SESSION_START і BREAK_START/SESSION_END)
        private bool _sessionOpen = false;

        private string CurrentCsvPath =>
            Path.Combine(AppSettings.LogsFolder, $"{_currentDate:yyyy-MM-dd}.csv");

        public ActivityLogger()
        {
            Directory.CreateDirectory(AppSettings.LogsFolder);
            EnsureHeader();
        }

        private void EnsureHeader()
        {
            if (!File.Exists(CurrentCsvPath))
            {
                using (var sw = new StreamWriter(CurrentCsvPath, false, new UTF8Encoding(true)))
                {
                    sw.WriteLine("start,end,duration_sec,process,title");
                }
            }
        }

        // ➜ Ротація файлу при зміні дати (опівночі)
        private void RotateIfNeeded(DateTime now)
        {
            if (now.Date != _currentDate)
            {
                // Закриваємо вчорашній день
                if (_sessionOpen)
                {
                    WriteEvent(now, "SESSION_END");
                    _sessionOpen = false;
                }
                WriteDailyTotalLine();

                // Переходимо на новий день
                _currentDate = now.Date;
                _totalsToday.Clear();
                _totalActiveSecToday = 0;
                EnsureHeader();
            }
        }

        // ➜ Запис активності вікна
        public void Append(ActiveSample s)
        {
            RotateIfNeeded(DateTime.Now);

            s.End = DateTime.Now;
            var dur = s.Duration;
            if (dur.TotalSeconds < 1) return;

            using (var sw = new StreamWriter(CurrentCsvPath, true, new UTF8Encoding(true)))
            {
                var line = $"{s.Start:HH:mm:ss},{s.End:HH:mm:ss},{(int)dur.TotalSeconds},{CsvEscape(s.Process)},{CsvEscape(s.Title)}";
                sw.WriteLine(line);
            }

            var key = s.Key;
            if (_totalsToday.ContainsKey(key)) _totalsToday[key] += dur;
            else _totalsToday[key] = dur;

            _totalActiveSecToday += (int)dur.TotalSeconds;
        }

        // ➜ Маркери сесій/перерв
        public void LogSessionStart()
        {
            RotateIfNeeded(DateTime.Now);
            if (!_sessionOpen)
            {
                WriteEvent(DateTime.Now, "SESSION_START");
                _sessionOpen = true;
            }
        }

        public void LogBreakStart()
        {
            RotateIfNeeded(DateTime.Now);
            if (_sessionOpen)
            {
                WriteEvent(DateTime.Now, "BREAK_START");
                _sessionOpen = false;
            }
        }

        // ➜ В кінці дня/при виході закриваємо сесію і пишемо підсумок
        public void LogSessionEndAndDailyTotal()
        {
            RotateIfNeeded(DateTime.Now);
            if (_sessionOpen)
            {
                WriteEvent(DateTime.Now, "SESSION_END");
                _sessionOpen = false;
            }
            WriteDailyTotalLine();
        }

        private void WriteDailyTotalLine()
        {
            using var sw = new StreamWriter(CurrentCsvPath, true, new UTF8Encoding(true));
            sw.WriteLine($"#TOTAL_ACTIVE_SEC,{_totalActiveSecToday}");
        }

        private void WriteEvent(DateTime when, string evt)
        {
            using (var sw = new StreamWriter(CurrentCsvPath, true, new UTF8Encoding(true)))
            {
                string line = $"{when:HH:mm:ss},{when:HH:mm:ss},0,{evt},\"\"";
                sw.WriteLine(line);
            }
        }

        public IReadOnlyDictionary<string, TimeSpan> TotalsToday => _totalsToday;

        public static string CsvEscape(string s)
        {
            if (s.Contains('\"') || s.Contains(',')) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
