using System.Globalization;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "settings". Viteza simularii e tinuta si in memorie
    // (Speed), ca restul programului sa o poata citi fara sa interogheze baza de date.
    //
    // DE CE viteza simularii: o spalare reala dureaza 30-90 de minute, prea mult pentru o demonstratie.
    // Cu viteza 30, 60 de minute devin 2 minute reale. Adminul o poate schimba (1 = timp real).
    // Se salveaza in tabelul "settings" (cheie/valoare) ca sa ramana si dupa repornire; tinem si o
    // copie in memorie (Speed) pentru ca e citita la aproape fiecare cerere.
    public static class SettingsRepository
    {
        public static double Speed = 30;

        public static void Load()
        {
            Row r = Db.One("SELECT v FROM settings WHERE k='speed'");
            if (r != null)
            {
                double d;
                if (double.TryParse(r.Str("v"), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    Speed = d;
                }
            }
        }

        public static void SetSpeed(double s)
        {
            Speed = s;
            Db.Exec("INSERT OR REPLACE INTO settings(k, v) VALUES('speed', @p0)", J.Num(s));
        }

        // Dupa cat timp o masina neridicata devine "blocata". Reguli reale: 15 minute. Le impartim la
        // viteza simularii ca sa ramana proportionale cu durata ciclurilor. La viteza 30 ar iesi 30 de
        // secunde, dar nu coboram sub 60: studentul trebuie sa aiba timp sa vada notificarea si sa
        // coboare. Deci la viteza implicita (30) pragul real este 60 de secunde.
        public static long GraceMs()
        {
            double g = 15 * 60000 / Speed;
            if (g < 60000)
            {
                g = 60000;
            }
            return (long)g;
        }
    }
}
