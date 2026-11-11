using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SmartLaundry
{
    // Functii mici pentru a construi text JSON "de mana".
    //
    // DE CE nu serializam automat (JsonSerializer)?
    //  - Raspunsurile contin campuri calculate (progres, timp ramas) si coloane din SQL cu nume
    //    exacte pe care le asteapta frontend-ul. Cu JObj controlam fiecare camp.
    //  - Se vede in cod exact ce trimitem clientului, fara reflexie sau atribute.
    public static class J
    {
        // Pune text intre ghilimele si "scapa" caracterele speciale. Fara asta, un nume ca
        // Maria "Mimi" ar strica JSON-ul (ghilimelele ar inchide textul mai devreme).
        public static string Quote(string s)
        {
            if (s == null)
            {
                return "null";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("\"");
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"')
                {
                    sb.Append("\\\"");
                }
                else if (c == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (c == '\r')
                {
                    sb.Append("\\r");
                }
                else if (c == '\t')
                {
                    sb.Append("\\t");
                }
                else if (c < 32)
                {
                    sb.Append("\\u" + ((int)c).ToString("x4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        // Numar real in JSON. InvariantCulture = mereu punct zecimal (0.35), indiferent de limba
        // Windows-ului (altfel pe un calculator romanesc ar iesi 0,35 si JSON-ul ar fi invalid).
        // NaN / infinit nu exista in JSON, asa ca le trimitem ca 0.
        public static string Num(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                return "0";
            }
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        // Transforma o valoare C# in text JSON, dupa tipul ei. Primeste "object" ca sa putem pune in
        // acelasi JObj text, numere, bool si null (null din baza de date devine null in JSON).
        public static string Val(object v)
        {
            if (v == null)
            {
                return "null";
            }
            if (v is string)
            {
                return Quote((string)v);
            }
            if (v is bool)
            {
                if ((bool)v)
                {
                    return "true";
                }
                return "false";
            }
            if (v is double)
            {
                return Num((double)v);
            }
            if (v is float)
            {
                return Num((double)(float)v);
            }
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        // O linie SQL intreaga ca obiect JSON (toate coloanele). Intoarce JObj (nu text), ca apelantul
        // sa mai poata adauga campuri, de exemplu "program_label". Numele coloanelor din SELECT devin
        // numele campurilor, de aceea folosim "AS" in interogari ca sa ajunga la numele dorit.
        public static JObj RowObj(Row r)
        {
            JObj o = new JObj();
            for (int i = 0; i < r.Keys.Count; i++)
            {
                string key = r.Keys[i];
                o.Add(key, r.V[key]);
            }
            return o;
        }

        public static string RowsToJson(List<Row> rows)
        {
            JArr arr = new JArr();
            for (int i = 0; i < rows.Count; i++)
            {
                arr.Add(RowObj(rows[i]).ToString());
            }
            return arr.ToString();
        }
    }
}
