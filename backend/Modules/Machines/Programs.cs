using System.Collections.Generic;

namespace SmartLaundry
{
    // Un program de spalare / uscare
    public class ProgramInfo
    {
        public string Key;
        public string Label;
        public string Type;
        public int Min;
        public int Temp;
        public double Kwh;
        public double Water;
        public int Price;

        public ProgramInfo(string key, string label, string type, int min, int temp, double kwh, double water, int price)
        {
            Key = key;
            Label = label;
            Type = type;
            Min = min;
            Temp = temp;
            Kwh = kwh;
            Water = water;
            Price = price;
        }
    }

    // Lista programelor (date fixe, nu stau in baza de date).
    //
    // DE CE in cod si nu intr-un tabel: programele se schimba rar si doar de dezvoltator (alt pret,
    // alta durata). Un tabel ar fi cerut ecran de administrare, interogari si migrari pentru un
    // beneficiu mic. Daca vom vrea ca adminul sa le editeze, le mutam intr-un tabel, iar restul
    // codului (care apeleaza doar Find / LabelOf) nu se schimba.
    public static class Programs
    {
        public static ProgramInfo[] All = new ProgramInfo[6];

        // Constructor static: ruleaza o singura data, prima oara cand se foloseste clasa.
        // Parametrii: cheie, eticheta, tip masina, minute, temperatura, kWh, litri apa, pret (lei).
        static Programs()
        {
            All[0] = new ProgramInfo("rapid", "Rapid 30°", "washer", 30, 30, 0.35, 28, 3);
            All[1] = new ProgramInfo("standard", "Standard 40°", "washer", 60, 40, 0.75, 45, 5);
            All[2] = new ProgramInfo("intens", "Intens 60°", "washer", 90, 60, 1.3, 55, 7);
            All[3] = new ProgramInfo("delicate", "Delicate 30°", "washer", 45, 30, 0.45, 40, 4);
            All[4] = new ProgramInfo("uscare", "Uscare normală", "dryer", 40, 70, 1.8, 0, 4);
            All[5] = new ProgramInfo("uscare_s", "Uscare scurtă", "dryer", 20, 60, 0.9, 0, 2);
        }

        public static ProgramInfo Find(string key)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Key == key)
                {
                    return All[i];
                }
            }
            return null;
        }

        // Eticheta unui program (sau null daca nu exista). O folosesc istoricul si statisticile, ca
        // sa afiseze "Standard 40°" in loc de cheia interna "standard".
        public static string LabelOf(string key)
        {
            ProgramInfo p = Find(key);
            if (p == null)
            {
                return null;
            }
            return p.Label;
        }

        public static List<string> KeysOfType(string type)
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Type == type)
                {
                    keys.Add(All[i].Key);
                }
            }
            return keys;
        }

        public static string ToJson()
        {
            JObj o = new JObj();
            for (int i = 0; i < All.Length; i++)
            {
                ProgramInfo p = All[i];
                JObj one = new JObj();
                one.Add("label", p.Label).Add("type", p.Type).Add("min", p.Min).Add("temp", p.Temp);
                one.Add("kwh", p.Kwh).Add("water", p.Water).Add("price", p.Price);
                o.AddRaw(p.Key, one.ToString());
            }
            return o.ToString();
        }
    }
}
