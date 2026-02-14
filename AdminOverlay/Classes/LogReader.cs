using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace AdminOverlay.Classes
{
    // Ha fennvoltál a szerón és ment az overlay vezérlő is akár és amikor felmész új log fájl kezdődik, mert éjfél után van,
    // akkor újra kell indítani az egészet!
    // Játszott perctől valszleg az AFK miatt fog eltérni, mert AFK-ot is beleszámol. Logból meg azt nem lehet megoldani.


    public enum DutyStatus { None, OnDuty, OffDuty }

    public enum InitializationResult { Success, DirectoryNotFound, NoLogFilesFound }

    public class LogReader
    {

        public string LogDirectoryPath { get; set; } = @"C:\SeeMTA\mta\logs";

        public string AdminName { get; set; } = ""; // Admin név

        private string? _currentLogFilePath;
        private long _lastReadLogPosition = 0;


        public int reportCounter { get; private set; } = 0;


        private double _storedOnDutyMinutes = 0;
        private double _storedOffDutyMinutes = 0;


        private DateTime _lastLogTimestamp = DateTime.MinValue;
        private DateTime _currentStatusStartingTimestamp = DateTime.MinValue;
        private DutyStatus _currentStatus = DutyStatus.None;

        // Regex az időbélyeghez: [2026-01-13 15:30:00]
        private Regex _timestampRegex = new Regex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");


        public async Task<InitializationResult> FirstReadAllLogfilesAsync() // Ha hamis, akkor azért lépett ki, mert nem volt jó az útvonal vagy 0 fájl van benne -> Ez fel van használva, hogy a Start button ne kezdődjön el, ha nem jó az útvonal.
        {
            if (!Directory.Exists(LogDirectoryPath)) return InitializationResult.DirectoryNotFound;

            reportCounter = 0;
            _storedOnDutyMinutes = 0;
            _storedOffDutyMinutes = 0;
            _currentStatus = DutyStatus.None;
            _lastLogTimestamp = DateTime.MinValue;

            Regex datumosFajlMinta = new Regex(@"console-\d{4}-\d{2}-\d{2}");

            var fajlok = Directory.GetFiles(LogDirectoryPath, "console-*.log")
                                  .Where(utvonal => datumosFajlMinta.IsMatch(Path.GetFileName(utvonal)))   
                                  .OrderBy(f => f)
                                  .ToList();

            if (fajlok.Count == 0) return InitializationResult.NoLogFilesFound;

            foreach (var fajlUtvonal in fajlok)
            {
                try
                {
                    using (var fs = new FileStream(fajlUtvonal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? sor;
                        while ((sor = await sr.ReadLineAsync()) != null)
                        {
                            ProcessLine(sor);
                        }

                        if (fajlUtvonal == fajlok.Last())
                        {
                            _currentLogFilePath = fajlUtvonal;
                            _lastReadLogPosition = fs.Position;
                        }
                    }
                }
                catch { }
            }

            return InitializationResult.Success;
        }

        public async Task ReadNewLineAsync()
        {
            if (string.IsNullOrEmpty(_currentLogFilePath)) return;


            FileInfo fi = new FileInfo(_currentLogFilePath);

            if (!fi.Exists || fi.Length == _lastReadLogPosition)
            {
                return;
            }

            /// TODO: Megnézni, hogy van-e új log file és ha van akkor az _aktualisFajlUtvonal = azzal    ÉS csak akkor nézze, ha _utolsoOlvasottPozicio-nak időbélyege XXXX-XX-XX 23:59 után van
            // Directory.GetFiles és az _aktualisFaljUtvonal != utolsoFajlUtvonallal, akkor _aktualisFajlUtvonal = utolsoFajlUtvonal

            try
            {

                using (var fs = new FileStream(_currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < _lastReadLogPosition) _lastReadLogPosition = 0;

                    fs.Seek(_lastReadLogPosition, SeekOrigin.Begin);

                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? sor;
                        while ((sor = await sr.ReadLineAsync()) != null)
                        {
                            ProcessLine(sor);
                        }

                        _lastReadLogPosition = fs.Position;
                    }
                }
            }
            catch
            {

            }
        }

        private void ProcessLine(string line)
        {
            // Reportok
            if (line.Contains("[SeeMTA - Siker]: Sikeresen lezártad az ügyet!") ||
                line.Contains("[SeeMTA - Figyelmeztetés]: A bejelentés automatikus lezárásra került"))
            {
                reportCounter++;
            }


            var regexMatch = _timestampRegex.Match(line);
            if (!regexMatch.Success) return;

            DateTime currentLineTimestamp;
            if (!DateTime.TryParse(regexMatch.Groups[1].Value, out currentLineTimestamp)) return;

            // X (60 perces timeoutnál minden lezárul
            if (_lastLogTimestamp != DateTime.MinValue)
            {
                if ((currentLineTimestamp - _lastLogTimestamp).TotalMinutes > 60) 
                {
                    Lezaras(_lastLogTimestamp); // Lezárás az utolsó log időpontnál lesz, vagyis visszaveszi a 60 percét magának, amivel több lenne. Ha 60 percig nem lenne egy log sor se / eltérés van, akkor feljebb lehet venni esetleg.
                }
            }


            if (line.Contains("SeeMTA logger started"))
            {
                if (_lastLogTimestamp != DateTime.MinValue) Lezaras(_lastLogTimestamp);
                _lastLogTimestamp = currentLineTimestamp; // loggernél minden lezárul és az előtte lévő sor időbélyegét nézi
                return;
            }

            // Offduty indítás trigger
            if (line.Contains("[SeeMTA]: Jó szórakozást kívánunk!") ||
                line.Contains($"[SeeMTA - AdminDuty]: {AdminName} kilépett az adminszolgálatból."))
            {
                if (_currentStatus == DutyStatus.OnDuty) Lezaras(currentLineTimestamp);

                if (_currentStatus != DutyStatus.OffDuty)
                {
                    _currentStatus = DutyStatus.OffDuty;
                    _currentStatusStartingTimestamp = currentLineTimestamp;
                }
            }
            // Onduty indítás trigger
            else if (line.Contains($"[SeeMTA - AdminDuty]: {AdminName} adminszolgálatba lépett."))
            {
                if (_currentStatus == DutyStatus.OffDuty) Lezaras(currentLineTimestamp);

                if (_currentStatus != DutyStatus.OnDuty)
                {
                    _currentStatus = DutyStatus.OnDuty;
                    _currentStatusStartingTimestamp = currentLineTimestamp;
                }
            }


            _lastLogTimestamp = currentLineTimestamp;
        }

        private void Lezaras(DateTime currentStatusClosingTimestamp)
        {
            if (_currentStatus == DutyStatus.None) return;

            double minutes = (currentStatusClosingTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
            if (minutes > 0)
            {
                if (_currentStatus == DutyStatus.OnDuty) _storedOnDutyMinutes += minutes;
                else if (_currentStatus == DutyStatus.OffDuty) _storedOffDutyMinutes += minutes;
            }
            _currentStatus = DutyStatus.None;
        }

        // Kiiírás - a jelenleg folyó, lezáratlan időt is veszi
        public string GetDutyTimeStr()
        {
            double total = _storedOnDutyMinutes;
            if (_currentStatus == DutyStatus.OnDuty && _lastLogTimestamp > _currentStatusStartingTimestamp)
            {
                total += (_lastLogTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
            }
            return Math.Floor(total).ToString("F0");
        }

        public string GetOffDutyTimeStr()
        {
            double total = _storedOffDutyMinutes;
            if (_currentStatus == DutyStatus.OffDuty && _lastLogTimestamp > _currentStatusStartingTimestamp)
            {
                total += (_lastLogTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
            }
            return Math.Floor(total).ToString("F0");
        }
    }
}
