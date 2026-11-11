using System;
using System.Globalization;
using System.Text.Json;

namespace SmartLaundry
{
    // Citeste campurile dintr-un body JSON trimis de client.
    //
    // DE CE e tolerant: un JSON stricat sau gol nu opreste programul si nu arunca eroare mai departe.
    // Body devine pur si simplu "invalid", iar orice camp cerut intoarce valoarea implicita
    // (null / 0). Validarea reala ("lipseste emailul") o fac serviciile, cu mesaje clare pentru utilizator.
    public class Body
    {
        JsonElement root;
        bool valid = false;

        public static Body Parse(string text)
        {
            Body b = new Body();
            try
            {
                JsonDocument doc = JsonDocument.Parse(text);
                // Clone(): JsonDocument tine memorie care trebuie eliberata; clona ne lasa sa
                // pastram elementul dupa ce documentul nu mai exista.
                b.root = doc.RootElement.Clone();
                if (b.root.ValueKind == JsonValueKind.Object)
                {
                    b.valid = true;
                }
            }
            catch (Exception)
            {
                b.valid = false;
            }
            return b;
        }

        bool Find(string name, out JsonElement el)
        {
            el = default(JsonElement);
            if (!valid)
            {
                return false;
            }
            return root.TryGetProperty(name, out el);
        }

        public bool Has(string name)
        {
            JsonElement el;
            return Find(name, out el);
        }

        public string Str(string name)
        {
            JsonElement el;
            if (!Find(name, out el))
            {
                return null;
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }
            if (el.ValueKind == JsonValueKind.Number)
            {
                return el.GetRawText();
            }
            return null;
        }

        public double Dbl(string name, double def)
        {
            JsonElement el;
            if (!Find(name, out el))
            {
                return def;
            }
            if (el.ValueKind == JsonValueKind.Number)
            {
                return el.GetDouble();
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                double d;
                if (double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                {
                    return d;
                }
            }
            return def;
        }

        // Clientul poate trimite un numar ca 5 sau ca "5" (text). Citim totul ca double si trunchiem:
        // un singur cod de parsare in loc de doua.
        public long Long(string name, long def)
        {
            double d = Dbl(name, def);
            return (long)d;
        }
    }
}
