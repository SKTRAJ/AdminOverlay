using AdminOverlay.Enums;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;


namespace AdminOverlay.Classes
{

    // Játszott perctől valszleg az AFK miatt fog eltérni, mert AFK-ot is beleszámol. Logból meg azt nem lehet megoldani.

    public class LogReader
    {

        public string LogDirectoryPath { get; set; } = @"C:\SeeMTA\mta\logs";

        public string AdminName { get; set; } = ""; // Admin név

        private const int _newLogFileDirectoryCheckIntervalSeconds = 60;

        private string? _currentLogFilePath;
        private long _lastReadLogPosition = 0;

        private DateTime _lastDirectoryCheck = DateTime.MinValue;


        public DateTime? FilterStartDate { get; set; }
        public DateTime? FilterEndDate { get; set; }


        public int ReportCounter { get; private set; } = 0;


        private double _storedOnDutyMinutes = 0;
        private double _storedOffDutyMinutes = 0;


        private DateTime _lastLogTimestamp = DateTime.MinValue;
        private DateTime _currentStatusStartingTimestamp = DateTime.MinValue;
        private DutyStatus _currentStatus = DutyStatus.None;

        // Regex az időbélyeghez: [2026-01-13 15:30:00]
        private Regex _timestampRegex = new Regex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]", RegexOptions.Compiled);

        // Regex a log file nevéhez: console-2026-12-02

        private Regex _logFileNameRegex = new Regex(@"console-\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);


        public async Task<InitializationResult> FirstReadAndProcessAllLogfilesAsync()
        {
            if (!Directory.Exists(LogDirectoryPath)) return InitializationResult.DirectoryNotFound;

            ReportCounter = 0;
            _storedOnDutyMinutes = 0;
            _storedOffDutyMinutes = 0;
            _currentStatus = DutyStatus.None;
            _lastLogTimestamp = DateTime.MinValue;

            var files = Directory.GetFiles(LogDirectoryPath, "console-*.log")
                                  .Where(filePath => _logFileNameRegex.IsMatch(Path.GetFileName(filePath)))
                                  .OrderBy(f => f)
                                  .ToList();

           






            if (FilterStartDate.HasValue)
            {
                string skipBeforeDateStr = FilterStartDate.Value.AddDays(-1).ToString("yyyy-MM-dd");
                files = files.Where(f => string.Compare(Path.GetFileNameWithoutExtension(f), $"console-{skipBeforeDateStr}", StringComparison.Ordinal) >= 0).ToList();
            }
            
            if (FilterEndDate.HasValue)
            {
                
                string skipAfterDateStr = FilterEndDate.Value.ToString("yyyy-MM-dd");
                files = files.Where(f => string.Compare(Path.GetFileNameWithoutExtension(f), $"console-{skipAfterDateStr}", StringComparison.Ordinal) <= 0).ToList();
            }


            if (files.Count == 0) return InitializationResult.NoLogFilesFound;






            foreach (var filePath in files)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? sor;
                        while ((sor = await sr.ReadLineAsync()) != null)
                        {
                            ProcessLine(sor);
                        }

                        if (filePath == files.Last())
                        {
                            _currentLogFilePath = filePath;
                            _lastReadLogPosition = fs.Position;
                        }
                    }
                }
                catch { }
            }

            return InitializationResult.Success;
        }

        public async Task ReadAndProcessNewLinesAsync()
        {
            if (FilterEndDate.HasValue && FilterEndDate.Value.Date < DateTime.Today) return;

            if (string.IsNullOrEmpty(_currentLogFilePath)) return;


            FileInfo fi = new FileInfo(_currentLogFilePath);

            if (fi.Exists && fi.Length != _lastReadLogPosition)
            {
                try
                {

                    using (var fs = new FileStream(_currentLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {

                        fs.Seek(_lastReadLogPosition, SeekOrigin.Begin);

                        using (var sr = new StreamReader(fs, Encoding.UTF8))
                        {
                            string? line;
                            while ((line = await sr.ReadLineAsync()) != null)
                            {
                                ProcessLine(line);
                            }

                            _lastReadLogPosition = fs.Position;
                        }
                    }
                }
                catch
                {

                }
            }

            CheckForNewLogFile();

        }

        private void CheckForNewLogFile()
        {
            if ((DateTime.Now - _lastDirectoryCheck).TotalSeconds < _newLogFileDirectoryCheckIntervalSeconds) return;

            _lastDirectoryCheck = DateTime.Now;

            if (string.IsNullOrEmpty(_currentLogFilePath)) return;

            try
            {

                var latestLogFile = Directory.EnumerateFiles(LogDirectoryPath, "console-*.log")
                                             .Where(f => _logFileNameRegex.IsMatch(f))
                                             .OrderByDescending(f => f)
                                             .FirstOrDefault();

                if (latestLogFile != null && string.Compare(latestLogFile, _currentLogFilePath, StringComparison.Ordinal) > 0)
                {
                    _currentLogFilePath = latestLogFile;
                    _lastReadLogPosition = 0;
                }
            }
            catch { }
        }

        private void ProcessLine(string line) 
        {

            var regexMatch = _timestampRegex.Match(line);
            if (!regexMatch.Success) return;

            DateTime currentLineTimestamp;
            if (!DateTime.TryParse(regexMatch.Groups[1].Value, out currentLineTimestamp)) return;


            // Reportok

            bool isInFilterRange = true;

            if (FilterStartDate.HasValue && currentLineTimestamp < FilterStartDate.Value.Date)
                isInFilterRange = false;
            if (FilterEndDate.HasValue && currentLineTimestamp >= FilterEndDate.Value.Date.AddDays(1))
                isInFilterRange = false;

            if (isInFilterRange)
            {
                if (line.Contains("[SeeMTA - Siker]: Sikeresen lezártad az ügyet!") ||
                line.Contains("[SeeMTA - Figyelmeztetés]: A bejelentés automatikus lezárásra került"))
                {
                    ReportCounter++;
                }
            } 
            



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
            return;
        }

        private void Lezaras(DateTime currentStatusClosingTimestamp)
        {
            //if (_currentStatus == DutyStatus.None) return;

            //double minutes = (currentStatusClosingTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
            //if (minutes > 0)
            //{
            //    if (_currentStatus == DutyStatus.OnDuty) _storedOnDutyMinutes += minutes;
            //    else if (_currentStatus == DutyStatus.OffDuty) _storedOffDutyMinutes += minutes;
            //}
            //_currentStatus = DutyStatus.None;




            if (_currentStatus == DutyStatus.None) return;

            DateTime effectiveStart = _currentStatusStartingTimestamp;
            DateTime effectiveEnd = currentStatusClosingTimestamp;

            if (FilterStartDate.HasValue && effectiveStart < FilterStartDate.Value.Date)
                effectiveStart = FilterStartDate.Value.Date; // Ha korábban kezdődött, szűrés elejétől

            if (FilterEndDate.HasValue)
            {
                DateTime limitEnd = FilterEndDate.Value.Date.AddDays(1); // alapvetően a datetime óra perc másodperc nélkül 00:00:00-át ad, ezért hozzá kell adni, hogy az a nap is benne legyen (+1 nap 00:00:00)
                if (effectiveEnd > limitEnd) // (Nincs sok lezárás, szóval nem annyira megterhelő hogy itt mindig beállítjuk a limitEnd-et)
                    effectiveEnd = limitEnd; // Ha nagyobb lenne a vége a 00:00-nál, akkor 00:00-ra tegye vissza, azzal számoljon
            }

            double minutes = (effectiveEnd - effectiveStart).TotalMinutes;
            if (minutes > 0)
            {
                if (_currentStatus == DutyStatus.OnDuty) _storedOnDutyMinutes += minutes;
                else if (_currentStatus == DutyStatus.OffDuty) _storedOffDutyMinutes += minutes;
            }
            _currentStatus = DutyStatus.None;
        
        
        }

        // Kiiírás - a jelenleg folyó, lezáratlan időt is veszi
        //public string GetDutyTimeStr()
        //{
        //    double total = _storedOnDutyMinutes;
        //    if (_currentStatus == DutyStatus.OnDuty && _lastLogTimestamp > _currentStatusStartingTimestamp)
        //    {
        //        total += (_lastLogTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
        //    }
        //    return Math.Floor(total).ToString("F0");
        //}

        //public string GetOffDutyTimeStr()
        //{
        //    double total = _storedOffDutyMinutes;
        //    if (_currentStatus == DutyStatus.OffDuty && _lastLogTimestamp > _currentStatusStartingTimestamp)
        //    {
        //        total += (_lastLogTimestamp - _currentStatusStartingTimestamp).TotalMinutes;
        //    }
        //    return Math.Floor(total).ToString("F0");
        //}

        public string GetDutyTimeStr() // ezek mutatják real timeba, de fontos hogy nem írják felül pluszban a storedOn/OffDutyMinutes-t, csak valós időben látszódik a változás. Ha leváltódik a státusz, akkor a trigger -> lezárás miatt a stored-ba kerül.
        => Math.Floor(_storedOnDutyMinutes + (_currentStatus == DutyStatus.OnDuty ? GetCurrentSegmentMinutes() : 0)).ToString("F0");

        public string GetOffDutyTimeStr()
        => Math.Floor(_storedOffDutyMinutes + (_currentStatus == DutyStatus.OffDuty ? GetCurrentSegmentMinutes() : 0)).ToString("F0");

        private double GetCurrentSegmentMinutes()
        {
            if (_currentStatus == DutyStatus.None || _lastLogTimestamp <= _currentStatusStartingTimestamp)
                return 0;

            // "ha-akkor"
            DateTime start = (FilterStartDate.HasValue && _currentStatusStartingTimestamp < FilterStartDate.Value.Date) ? FilterStartDate.Value.Date : _currentStatusStartingTimestamp;
            DateTime end = (FilterEndDate.HasValue && _lastLogTimestamp > FilterEndDate.Value.Date.AddDays(1)) ? FilterEndDate.Value.Date.AddDays(1) : _lastLogTimestamp;

            return end > start ? (end - start).TotalMinutes : 0;
        }
    }
}
