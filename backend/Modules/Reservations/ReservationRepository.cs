using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "reservations"
    // Statusuri: active (masina tinuta pentru student), queued (la coada), done, expired, cancelled
    public static class ReservationRepository
    {
        public static long Insert(long machineId, long userId, string status)
        {
            return Db.Insert("INSERT INTO reservations(machine_id, user_id, created, status) VALUES(@p0, @p1, @p2, @p3)",
                             machineId, userId, Util.Now(), status);
        }

        // Rezervarea deschisa (la coada sau activa) a unui student pe o masina
        public static Row FindOpen(long machineId, long userId)
        {
            return Db.One("SELECT * FROM reservations WHERE machine_id=@p0 AND user_id=@p1 AND status IN ('queued','active')", machineId, userId);
        }

        public static Row FindOwn(long id, long userId)
        {
            return Db.One("SELECT * FROM reservations WHERE id=@p0 AND user_id=@p1", id, userId);
        }

        public static long CountOpenForUser(long userId)
        {
            return Db.Count("SELECT COUNT(*) FROM reservations WHERE user_id=@p0 AND status IN ('queued','active')", userId);
        }

        public static long QueueLength(long machineId)
        {
            return Db.Count("SELECT COUNT(*) FROM reservations WHERE machine_id=@p0 AND status='queued'", machineId);
        }

        // Primul student din coada unei masini (sau null). Ordonam dupa id: id-urile cresc in ordinea
        // inserarii, deci cel mai vechi (care a asteptat cel mai mult) primeste masina primul.
        // Asa coada e "corecta" (primul venit, primul servit), fara o coloana separata de pozitie.
        public static Row FirstQueued(long machineId)
        {
            return Db.One("SELECT * FROM reservations WHERE machine_id=@p0 AND status='queued' ORDER BY id LIMIT 1", machineId);
        }

        public static void SetStatus(long id, string status)
        {
            Db.Exec("UPDATE reservations SET status=@p0 WHERE id=@p1", status, id);
        }

        // Rezervarea activa a unui student pe o masina trece in alt status (done / expired)
        public static void ChangeActiveStatus(long machineId, long userId, string newStatus)
        {
            Db.Exec("UPDATE reservations SET status=@p0 WHERE machine_id=@p1 AND user_id=@p2 AND status='active'", newStatus, machineId, userId);
        }

        // Rezervarile deschise ale unui student, cu numele masinii (doar citire din machines)
        public static List<Row> ListOpenForUser(long userId)
        {
            return Db.Query("SELECT r.*, m.name AS machine_name, m.dorm_id AS dorm_id, m.status AS mstatus FROM reservations r " +
                            "JOIN machines m ON m.id=r.machine_id WHERE r.user_id=@p0 AND r.status IN ('queued','active') ORDER BY r.id DESC", userId);
        }
    }
}
