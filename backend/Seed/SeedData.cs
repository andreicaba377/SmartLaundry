using System;
using System.Collections.Generic;

namespace SmartLaundry
{
    // Date de pornire: camine, masini, studenti, istoric de utilizare.
    // Nu contine SQL: foloseste metodele publice ale modulelor, deci datele demo
    // trec prin aceleasi reguli ca aplicatia.
    //
    // DE CE nu inseram direct cu SQL: daca regulile se schimba (ex. parola se hash-uieste altfel),
    // datele demo raman corecte automat, pentru ca trec prin aceleasi metode ca utilizatorii reali.
    public static class SeedData
    {
        public static void Run(string adminPassword)
        {
            // Ruleaza O SINGURA DATA: daca exista deja camine, baza de date a fost initializata si nu
            // adaugam nimic (altfel am dubla datele la fiecare pornire).
            if (DormRepository.Count() > 0)
            {
                return;
            }

            // Tranzactie: sute de INSERT-uri intr-un singur "pachet" se salveaza in cateva milisecunde,
            // in loc de cateva secunde daca fiecare s-ar salva separat pe disc.
            Db.BeginTransaction();

            // 4 camine, fiecare cu 5 masini de spalat si 2 uscatoare
            string[] dormNames = { "Cămin 1 - Observator", "Cămin 2 - Hasdeu", "Cămin 3 - Economica", "Cămin 4 - Memorandumului" };
            string[] dormAddresses = { "Str. Observatorului 34", "Str. Hasdeu 12", "Str. Teodor Mihali 58", "Str. Memorandumului 28" };
            for (int d = 0; d < 4; d++)
            {
                long dormId = DormRepository.Add(dormNames[d], dormAddresses[d]);
                for (int w = 1; w <= 5; w++)
                {
                    MachineService.Add(dormId, "Mașina " + w, "washer", 100 + Util.Rnd.Next(400));
                }
                for (int u = 1; u <= 2; u++)
                {
                    MachineService.Add(dormId, "Uscător " + u, "dryer", 50 + Util.Rnd.Next(200));
                }
            }

            // 8 studenti demo (parola: student123) si contul de administrator
            string[] names = { "Andrei Popescu", "Maria Ionescu", "Radu Mureșan", "Elena Stoica", "Vlad Dumitru", "Ioana Pop", "Tudor Lazăr", "Ana Munteanu" };
            for (int i = 0; i < names.Length; i++)
            {
                string email = AuthService.Slug(names[i]) + "@student.ro";
                AuthService.CreateUser(names[i], (100 + i * 7).ToString(), (i % 4) + 1, email, "student123", "student");
            }
            AuthService.CreateUser("Administrator", "-", 1, "admin@campus.ro", adminPassword, "admin");

            // Istoric sintetic pe 15 zile, ca graficele adminului sa arate bine de la prima pornire.
            // Orele de varf sunt alese aproape toate seara (17-22), ca in realitate: lista "peak"
            // contine ore repetate, iar o ora care apare de mai multe ori e mai probabila.
            List<Row> machines = MachineService.ListAll();
            int[] peak = { 7, 8, 9, 12, 14, 17, 18, 18, 19, 19, 19, 20, 20, 21, 21, 22 };
            long now = Util.Now();
            for (int day = 14; day >= 0; day--)
            {
                int uses = 25 + Util.Rnd.Next(25);
                for (int k = 0; k < uses; k++)
                {
                    Row m = machines[Util.Rnd.Next(machines.Count)];
                    List<string> keys = Programs.KeysOfType(m.Str("type"));
                    ProgramInfo p = Programs.Find(keys[Util.Rnd.Next(keys.Count)]);
                    int hour = peak[Util.Rnd.Next(peak.Length)];
                    DateTime start = DateTime.Today.AddDays(-day).AddHours(hour).AddMinutes(Util.Rnd.Next(60));
                    long startMs = new DateTimeOffset(start).ToUnixTimeMilliseconds();
                    if (startMs > now)
                    {
                        continue;
                    }
                    double kwh = Math.Round(p.Kwh * Util.Between(0.9, 1.1), 2);
                    double water = Math.Round(p.Water * Util.Between(0.9, 1.1), 1);
                    UsageRepository.Add(m.Long("id"), Util.Rnd.Next(8) + 1, p.Key, startMs, startMs + p.Min * 60000L, kwh, water, p.Price);
                }
            }

            // Inchidem tranzactia INAINTE de pasii urmatori: ei apeleaza flow-uri si servicii care pot face
            // ele insele scrieri, iar nu vrem sa le amestecam in aceeasi tranzactie cu istoricul.
            Db.Commit();

            // fiecare a treia masina porneste deja, la un moment aleator din ciclu
            int student = 0;
            for (int i = 0; i < machines.Count; i += 3)
            {
                Row m = machines[i];
                string key = "uscare";
                if (m.Str("type") == "washer")
                {
                    key = "standard";
                }
                ProgramInfo p = Programs.Find(key);
                long elapsed = (long)(Util.Rnd.NextDouble() * 0.6 * p.Min * 60000.0 / SettingsRepository.Speed);
                CycleFlow.StartAt(m.Long("id"), (student % 8) + 1, key, elapsed);
                student++;
            }

            // o masina defecta in Camin 2 (raportata acum o ora de studentul 2)
            Row broken = MachineService.FindFree(2, "washer");
            if (broken != null)
            {
                DefectService.ReportAt(broken.Long("id"), 2, "Nu scoate apa la centrifugare, rămâne apă în tambur.", "high", Util.Now() - 3600000);
            }
        }
    }
}
