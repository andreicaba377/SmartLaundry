namespace SmartLaundry
{
    // Eliberarea unei masini si predarea ei catre primul student din coada.
    // Toate situatiile in care o masina devine libera (ridicare rufe, rezervare expirata,
    // anulare, resetare de admin, defectiune rezolvata) trec prin Release: logica e scrisa o singura data.
    //
    // DE CE e ATAT de important sa fie un singur loc: "masina libera" nu inseamna doar status = free.
    // Daca cineva sta la coada, masina nu trebuie sa ramana libera ci sa fie data lui. Daca am fi
    // scris aceasta regula in 5 locuri, ar fi fost usor sa uitam coada intr-unul si studentul
    // ar fi asteptat degeaba, desi masina e libera.
    public static class ReleaseFlow
    {
        // Masina devine libera; daca cineva asteapta la coada, ii este rezervata automat 5 minute.
        // Pasii: (1) golim masina, (2) cautam primul din coada, (3) daca exista, ii "dam" masina
        // (rezervare activa + masina tinuta 5 minute) si il anuntam.
        public static void Release(Row m)
        {
            long id = m.Long("id");
            MachineService.SetFree(id);

            Row first = ReservationService.FirstInQueue(id);
            if (first != null)
            {
                ReservationService.Activate(first.Long("id"));
                MachineService.Hold(id, first.Long("user_id"), Util.Now() + ReservationService.HoldMs);
                NotificationService.Notify(first.Long("user_id"), "reserved",
                    "🔔 " + m.Str("name") + " (" + CycleFlow.DormName(m) + ") este liberă și rezervată pentru tine 5 minute.");
            }
            SseHub.PushType("machines");
        }

        // Studentul si-a ridicat rufele. Doar proprietarul poate elibera o masina terminata sau blocata:
        // nu vrem ca un student sa "ridice" rufele altuia din aplicatie.
        public static Result Collect(long machineId, long userId)
        {
            Row m = MachineService.FindById(machineId);
            if (m == null)
            {
                return Result.Fail(404, "Mașină inexistentă");
            }
            string status = m.Str("status");
            if ((status != "finished" && status != "blocked") || m.Long("user_id") != userId)
            {
                return Result.Fail(409, "Nu poți elibera această mașină");
            }
            Release(m);
            return Result.Success();
        }

        // Studentul anuleaza o rezervare; daca masina era tinuta pentru el, se elibereaza.
        // Aici se vede de ce avem nevoie de Flow: anularea e o regula de REZERVARI, dar eliberarea
        // masinii e o regula de MASINI + COADA. ReservationService nu are voie sa apeleze MachineService
        // in sens invers, asa ca Flow-ul face cei doi pasi unul dupa altul.
        public static Result CancelReservation(long reservationId, long userId)
        {
            Result res = ReservationService.Cancel(reservationId, userId);
            if (!res.Ok)
            {
                return res;
            }
            // Eliberam masina doar daca era tinuta chiar pentru acest student. Daca anuleaza o rezervare
            // la COADA, masina e inca ocupata de altcineva si nu trebuie atinsa.
            Row m = MachineService.FindById(res.Row.Long("machine_id"));
            if (m != null)
            {
                if (m.Str("status") == "reserved" && m.Long("hold_user_id") == userId)
                {
                    Release(m);
                }
            }
            SseHub.PushType("machines");
            return Result.Success();
        }

        // Studentul nu a pornit masina in cele 5 minute: rezervarea expira
        public static void ExpireHold(Row m)
        {
            long holder = m.Long("hold_user_id");
            ReservationService.Expire(m.Long("id"), holder);
            NotificationService.Notify(holder, "expired", "Rezervarea pentru " + m.Str("name") + " a expirat.");
            Release(m);
        }
    }
}
