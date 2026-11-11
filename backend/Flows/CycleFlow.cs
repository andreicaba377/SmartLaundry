using System;

namespace SmartLaundry
{
    // Pornirea si terminarea unui ciclu: implica Machines, Reservations, Usage si Notifications.
    // Flow-urile combina serviciile; serviciile nu se apeleaza intre ele.
    //
    // DE CE exista stratul "Flows": cand terminam un ciclu trebuie sa (1) salvam in istoric, (2) trecem
    // masina in "finished" si (3) anuntam studentul. Sunt 3 module diferite. Daca MachineService ar
    // apela direct istoricul si notificarile, modulele s-ar lega intre ele in cerc si orice schimbare
    // ar atinge mai multe fisiere. Un Flow este "dirijorul": cunoaste ordinea pasilor, iar fiecare
    // modul isi vede doar de treaba lui.
    public static class CycleFlow
    {
        // Un student porneste un program (cu toate verificarile)
        public static Result Start(long machineId, long userId, string programKey)
        {
            Row m = MachineService.FindById(machineId);
            if (m == null)
            {
                return Result.Fail(404, "Mașină inexistentă");
            }

            ProgramInfo prog = Programs.Find(programKey);
            if (prog == null || prog.Type != m.Str("type"))
            {
                return Result.Fail(400, "Program invalid pentru acest tip de mașină");
            }

            // O masina se poate porni in doua cazuri: e LIBERA (oricine o poate lua) sau e REZERVATA
            // exact pentru acest student (hold_user_id). Daca e rezervata pentru altcineva, dam un mesaj
            // diferit de "indisponibila", ca studentul sa inteleaga de ce nu o poate folosi.
            string status = m.Str("status");
            bool okFree = (status == "free");
            bool okHeld = (status == "reserved" && m.Long("hold_user_id") == userId);
            if (!okFree && !okHeld)
            {
                if (status == "reserved")
                {
                    return Result.Fail(409, "Mașina este rezervată de altcineva");
                }
                return Result.Fail(409, "Mașina nu este disponibilă");
            }

            // Maximum 2 masini active de acelasi tip per student (spalat / uscat): altfel un singur student
            // ar putea ocupa toate masinile din camin pe care apoi sa le lase neridicate.
            if (MachineService.CountActive(userId, m.Str("type")) >= 2)
            {
                return Result.Fail(409, "Ai deja 2 mașini active de acest tip");
            }

            // Ordinea conteaza: intai inchidem rezervarea (a fost "folosita"), apoi pornim masina.
            // Toate verificarile de mai sus s-au facut INAINTE de orice modificare, deci nu ramanem
            // niciodata la jumatate (rezervare inchisa dar masina nepornita).
            if (okHeld)
            {
                ReservationService.MarkUsed(machineId, userId);
            }
            StartAt(machineId, userId, prog.Key, 0);
            SseHub.PushType("machines");
            return Result.Success();
        }

        // Porneste ciclul fara verificari (folosit de Seed si DemoTraffic).
        // elapsedMs = cat din ciclu a trecut deja (0 pentru un ciclu nou).
        // Este separat de Start ca datele demo sa poata porni masini "deja la jumatate", pe care un student
        // real nu le poate crea. Dar ajunge tot la MachineService, nu direct in baza de date.
        public static void StartAt(long machineId, long userId, string programKey, long elapsedMs)
        {
            MachineService.StartCycle(machineId, userId, programKey, elapsedMs);
        }

        // Ciclul s-a terminat: se salveaza in istoric si studentul e anuntat
        public static void Finish(Row m)
        {
            ProgramInfo p = Programs.Find(m.Str("program"));
            // Consumul real difera putin de cel nominal al programului: aplicam o variatie aleatoare de +-8%
            // ca istoricul sa para masuratori reale, nu aceeasi valoare repetata la nesfarsit.
            double kwh = 0;
            double water = 0;
            long price = 0;
            if (p != null)
            {
                kwh = Math.Round(p.Kwh * Util.Between(0.92, 1.08), 2);
                water = Math.Round(p.Water * Util.Between(0.92, 1.08), 1);
                price = p.Price;
            }

            // 3 pasi, in ordinea logica a evenimentelor: s-a terminat (istoric) -> masina devine "finished"
            // -> studentul e anuntat. Datele ciclului (program, ora de start) vin din randul "m", citit
            // inainte de orice modificare, deci ordinea nu schimba ce se salveaza. Notificarea e ultima:
            // nu anuntam "rufele sunt gata" pana nu e sigur ca masina chiar a fost marcata terminata.
            UsageRepository.Add(m.Long("id"), m.Long("user_id"), m.Str("program"), m.Long("started_at"), Util.Now(), kwh, water, price);
            MachineService.Finish(m.Long("id"), kwh, water);
            NotificationService.Notify(m.Long("user_id"), "finish",
                "✅ " + m.Str("name") + " (" + DormName(m) + ") a terminat programul. Ridică rufele!");
            SseHub.PushType("machines");
        }

        public static string DormName(Row machine)
        {
            return DormRepository.NameOf(machine.Long("dorm_id"));
        }
    }
}
