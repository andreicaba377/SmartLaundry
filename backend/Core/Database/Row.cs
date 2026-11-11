using System;
using System.Collections.Generic;

namespace SmartLaundry
{
    // O linie dintr-un rezultat SQL: numele coloanei -> valoarea.
    //
    // DE CE un dictionar si nu o clasa pentru fiecare tabel (Machine, User, ...)?
    //  - Un singur tip pentru toate tabelele = mult mai putin cod (nu scriem 9 clase + conversii).
    //  - Keys pastreaza ordinea coloanelor, ca JSON-ul sa iasa mereu la fel.
    // Pretul: tastam numele coloanelor ca text ("user_id"). Il acceptam pentru simplitate.
    public class Row
    {
        public Dictionary<string, object> V = new Dictionary<string, object>();
        public List<string> Keys = new List<string>();

        public bool IsNull(string key)
        {
            return V[key] == null;
        }

        public string Str(string key)
        {
            if (V[key] == null)
            {
                return null;
            }
            return Convert.ToString(V[key]);
        }

        // Un camp NULL din baza de date il citim ca 0 (sau null la Str): evitam sa verificam null peste tot.
        // Cand diferenta conteaza (ex. "nu are rezervare" vs "id 0") folosim IsNull inainte.
        public long Long(string key)
        {
            if (V[key] == null)
            {
                return 0;
            }
            return Convert.ToInt64(V[key]);
        }

        public double Dbl(string key)
        {
            if (V[key] == null)
            {
                return 0;
            }
            return Convert.ToDouble(V[key]);
        }
    }
}
