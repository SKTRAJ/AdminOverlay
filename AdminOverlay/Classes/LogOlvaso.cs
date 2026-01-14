using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AdminOverlay.Classes
{
    // Ha fennvoltál a szerón és ment az overlay vezérlő is akár és amikor felmész új log fájl kezdődik, mert éjfél után van,
    // akkor újra kell indítani az egészet!
    // Játszott perctől valszleg az AFK miatt fog eltérni, mert AFK-ot is beleszámol. Logból meg azt nem lehet megoldani.


    public enum Statusz { Semmi, OnDuty, OffDuty }

    public class LogOlvaso
    {

        private const string LOG_MAPPA_UTVONAL = @"C:\SeeMTA\mta\logs";

        public string adminName { get; set; } = ""; // Admin név

        private string? _aktualisFajlUtvonal;
        private long _utolsoOlvasottPozicio = 0;


        public int reportSzamlalo { get; private set; } = 0;


        private double _taroltDutyPerc = 0;
        private double _taroltOffDutyPerc = 0;


        private DateTime _utolsoLogIdopont = DateTime.MinValue;
        private DateTime _szakaszKezdete = DateTime.MinValue;
        private Statusz _aktualisStatusz = Statusz.Semmi;

        // Regex az időbélyeghez: [2026-01-13 15:30:00]
        private Regex _idoBelyegRegex = new Regex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})\]");


        public void BeolvasasMindenLogbol()
        {
            if (!Directory.Exists(LOG_MAPPA_UTVONAL)) return;


            reportSzamlalo = 0;
            _taroltDutyPerc = 0;
            _taroltOffDutyPerc = 0;
            _aktualisStatusz = Statusz.Semmi;
            _utolsoLogIdopont = DateTime.MinValue;

            Regex datumosFajlMinta = new Regex(@"console-\d{4}-\d{2}-\d{2}");

            var fajlok = Directory.GetFiles(LOG_MAPPA_UTVONAL, "console-*.log")
                                  .Where(utvonal => datumosFajlMinta.IsMatch(Path.GetFileName(utvonal)))
                                  .OrderBy(f => f)
                                  .ToList();

            if (fajlok.Count == 0) return;

            foreach (var fajlUtvonal in fajlok)
            {
                try
                {
                    using (var fs = new FileStream(fajlUtvonal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? sor;
                        while ((sor = sr.ReadLine()) != null)
                        {
                            FeldolgozSor(sor);
                        }

                        if (fajlUtvonal == fajlok.Last())
                        {
                            _aktualisFajlUtvonal = fajlUtvonal;
                            _utolsoOlvasottPozicio = fs.Position;
                        }
                    }
                }
                catch { }
            }
        }

        public void OlvasdAzUjSorokat()
        {
            if (string.IsNullOrEmpty(_aktualisFajlUtvonal)) return;

            try
            {
                using (var fs = new FileStream(_aktualisFajlUtvonal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < _utolsoOlvasottPozicio) _utolsoOlvasottPozicio = 0;

                    fs.Seek(_utolsoOlvasottPozicio, SeekOrigin.Begin);

                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string? sor;
                        while ((sor = sr.ReadLine()) != null)
                        {
                            FeldolgozSor(sor);
                        }
                        _utolsoOlvasottPozicio = fs.Position;
                    }
                }
            }
            catch { }
        }


        private void FeldolgozSor(string sor)
        {
            // Reportok
            if (sor.Contains("[SeeMTA - Siker]: Sikeresen lezártad az ügyet!") ||
                sor.Contains("[SeeMTA - Figyelmeztetés]: A bejelentés automatikus lezárásra került"))
            {
                reportSzamlalo++;
            }


            var match = _idoBelyegRegex.Match(sor);
            if (!match.Success) return;

            DateTime aktualisSorIdeje;
            if (!DateTime.TryParse(match.Groups[1].Value, out aktualisSorIdeje)) return;

            // X (60 perces timeoutnál minden lezárul
            if (_utolsoLogIdopont != DateTime.MinValue)
            {
                if ((aktualisSorIdeje - _utolsoLogIdopont).TotalMinutes > 60) // (változóba)
                {
                    Lezaras(_utolsoLogIdopont); // Lezárás az utolsó log időpontnál lesz, vagyis visszaveszi a 60 percét magának, amivel több lenne. Ha 60 percig nem lenne egy log sor se / eltérés van, akkor feljebb lehet venni esetleg.
                }
            }


            if (sor.Contains("SeeMTA logger started"))
            {
                if (_utolsoLogIdopont != DateTime.MinValue) Lezaras(_utolsoLogIdopont);
                _utolsoLogIdopont = aktualisSorIdeje; // loggernél minden lezárul és az előtte lévő sor időbélyegét nézi
                return;
            }

            // Offduty indítás trigger
            if (sor.Contains("[SeeMTA]: Jó szórakozást kívánunk!") ||
                sor.Contains($"[SeeMTA - AdminDuty]: {adminName} kilépett az adminszolgálatból."))
            {
                if (_aktualisStatusz == Statusz.OnDuty) Lezaras(aktualisSorIdeje);

                if (_aktualisStatusz != Statusz.OffDuty)
                {
                    _aktualisStatusz = Statusz.OffDuty;
                    _szakaszKezdete = aktualisSorIdeje;
                }
            }
            // Onduty indítás trigger
            else if (sor.Contains($"[SeeMTA - AdminDuty]: {adminName} adminszolgálatba lépett."))
            {
                if (_aktualisStatusz == Statusz.OffDuty) Lezaras(aktualisSorIdeje);

                if (_aktualisStatusz != Statusz.OnDuty)
                {
                    _aktualisStatusz = Statusz.OnDuty;
                    _szakaszKezdete = aktualisSorIdeje;
                }
            }


            _utolsoLogIdopont = aktualisSorIdeje;
        }

        private void Lezaras(DateTime zarasiIdo)
        {
            if (_aktualisStatusz == Statusz.Semmi) return;

            double percek = (zarasiIdo - _szakaszKezdete).TotalMinutes;
            if (percek > 0)
            {
                if (_aktualisStatusz == Statusz.OnDuty) _taroltDutyPerc += percek;
                else if (_aktualisStatusz == Statusz.OffDuty) _taroltOffDutyPerc += percek;
            }
            _aktualisStatusz = Statusz.Semmi;
        }

        // Kiiírás - a jelenleg folyó, lezáratlan időt is veszi
        public string GetDutyTimeStr()
        {
            double total = _taroltDutyPerc;
            if (_aktualisStatusz == Statusz.OnDuty && _utolsoLogIdopont > _szakaszKezdete)
            {
                total += (_utolsoLogIdopont - _szakaszKezdete).TotalMinutes;
            }
            return Math.Floor(total).ToString("F0");
        }

        public string GetOffDutyTimeStr()
        {
            double total = _taroltOffDutyPerc;
            if (_aktualisStatusz == Statusz.OffDuty && _utolsoLogIdopont > _szakaszKezdete)
            {
                total += (_utolsoLogIdopont - _szakaszKezdete).TotalMinutes;
            }
            return Math.Floor(total).ToString("F0");
        }
    }
}
