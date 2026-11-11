using System.Collections.Generic;

namespace SmartLaundry
{
    // Regulile rezervarilor si ale cozii. Foloseste MachineService (intr-un singur sens:
    // MachineService NU apeleaza niciodata ReservationService).
    //
    // CUM functioneaza un sistem cu doua feluri de rezervari, intr-un singur tabel:
    //   - "active": masina e LIBERA si tinuta 5 minute pentru acel student (masina trece in "reserved").
    //   - "queued": masina e OCUPATA; studentul sta la coada si primeste masina cand se elibereaza.
    // Cand masina se elibereaza, ReleaseFlow ia primul "queued" si il transforma in "active".
    // Daca nu pornesti masina in 5 minute, rezervarea expira si masina trece la urmatorul din coada.
    public static class ReservationService
    {
        // cat timp e tinuta o masina rezervata. 5 minute: destul ca studentul sa coboare cu rufele,
        // dar nu atat de mult incat o masina libera sa stea blocata degeaba.
        public const long HoldMs = 5 * 60 * 1000;

        public static long QueueLength(long machineId)
        {
            return ReservationRepository.QueueLength(machineId);
        }

        // Id-ul rezervarii deschise a studentului pe masina, sau 0
        public static long MyOpenReservationId(long machineId, long userId)
        {
            if (userId == 0)
            {
                return 0;
            }
            Row r = ReservationRepository.FindOpen(machineId, userId);
            if (r == null)
            {
                return 0;
            }
            return r.Long("id");
        }

        public static List<Row> ListOpenForUser(long userId)
        {
            return ReservationRepository.ListOpenForUser(userId);
        }

        // Rezerva o masina libera 5 minute sau intra la coada daca e ocupata
        public static Result Reserve(long machineId, long userId)
        {
            Row m = MachineService.FindById(machineId);
            if (m == null)
            {
                return Result.Fail(404, "Mașină inexistentă");
            }
            string status = m.Str("status");
            if (status == "out_of_order")
            {
                return Result.Fail(409, "Mașina este defectă");
            }
            if (ReservationRepository.FindOpen(machineId, userId) != null)
            {
                return Result.Fail(409, "Ai deja o rezervare pe această mașină");
            }
            // Limita de 3 rezervari / cozi active per student: fara ea, un singur student ar putea
            // rezerva toate masinile libere din camin si le-ar tine ocupate degeaba.
            if (ReservationRepository.CountOpenForUser(userId) >= 3)
            {
                return Result.Fail(409, "Ai atins limita de 3 rezervări/cozi active");
            }

            if (status == "free")
            {
                ReservationRepository.Insert(machineId, userId, "active");
                MachineService.Hold(machineId, userId, Util.Now() + HoldMs);
            }
            else if (status == "reserved" && m.Long("hold_user_id") == userId)
            {
                return Result.Fail(409, "Mașina e deja rezervată pentru tine");
            }
            else
            {
                ReservationRepository.Insert(machineId, userId, "queued");
            }
            return Result.Success();
        }

        // Marcheaza rezervarea ca anulata. Intoarce rezervarea (Row) ca sa stim ce masina era.
        // Cautam rezervarea DUPA id SI dupa utilizator (FindOwn): un student nu poate anula rezervarea
        // altuia chiar daca ghiceste un id. Eliberarea masinii (daca era tinuta pentru el) nu se face
        // aici ci in ReleaseFlow.CancelReservation, pentru ca ar cere apelarea unui alt modul.
        public static Result Cancel(long reservationId, long userId)
        {
            Row res = ReservationRepository.FindOwn(reservationId, userId);
            if (res == null)
            {
                return Result.Fail(404, "Rezervare inexistentă");
            }
            ReservationRepository.SetStatus(reservationId, "cancelled");
            Result ok = Result.Success();
            ok.Row = res;
            return ok;
        }

        // Primul din coada (sau null)
        public static Row FirstInQueue(long machineId)
        {
            return ReservationRepository.FirstQueued(machineId);
        }

        // Studentul din coada primeste masina: rezervarea devine activa
        public static void Activate(long reservationId)
        {
            ReservationRepository.SetStatus(reservationId, "active");
        }

        // Studentul si-a pornit programul pe masina rezervata
        public static void MarkUsed(long machineId, long userId)
        {
            ReservationRepository.ChangeActiveStatus(machineId, userId, "done");
        }

        // Timpul de 5 minute a trecut fara ca studentul sa porneasca masina
        public static void Expire(long machineId, long userId)
        {
            ReservationRepository.ChangeActiveStatus(machineId, userId, "expired");
        }
    }
}
