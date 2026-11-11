using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "defects"
    public static class DefectRepository
    {
        // userId = 0 inseamna ca defectiunea a fost detectata de senzor (fara student). In baza de date
        // salvam atunci NULL, nu 0: un 0 ar parea un student real cu id 0 si JOIN-ul cu users ar
        // incerca sa-l gaseasca. Cu NULL, LEFT JOIN-ul din ListAll il lasa pur si simplu fara nume.
        public static long Insert(long machineId, long userId, string description, string severity, long created, string source)
        {
            object user = null;
            if (userId != 0)
            {
                user = userId;
            }
            return Db.Insert("INSERT INTO defects(machine_id, user_id, description, severity, status, created, source) VALUES(@p0, @p1, @p2, @p3, 'open', @p4, @p5)",
                             machineId, user, description, severity, created, source);
        }

        public static Row FindById(long id)
        {
            return Db.One("SELECT * FROM defects WHERE id=@p0", id);
        }

        public static void SetStatus(long id, string status, object resolvedAt)
        {
            Db.Exec("UPDATE defects SET status=@p0, resolved=@p1 WHERE id=@p2", status, resolvedAt, id);
        }

        public static long CountUnresolved(long machineId)
        {
            return Db.Count("SELECT COUNT(*) FROM defects WHERE machine_id=@p0 AND status != 'resolved'", machineId);
        }

        public static List<Row> RecentForMachine(long machineId)
        {
            return Db.Query("SELECT * FROM defects WHERE machine_id=@p0 ORDER BY id DESC LIMIT 5", machineId);
        }

        // Pentru administrator: toate defectiunile, cu numele masinii si al studentului (doar citire din alte tabele)
        public static List<Row> ListAll()
        {
            return Db.Query("SELECT d.*, m.name AS machine_name, m.dorm_id AS dorm_id, u.name AS user_name FROM defects d " +
                            "JOIN machines m ON m.id=d.machine_id LEFT JOIN users u ON u.id=d.user_id ORDER BY d.id DESC");
        }

        public static List<Row> ListForUser(long userId)
        {
            return Db.Query("SELECT d.*, m.name AS machine_name, m.dorm_id AS dorm_id FROM defects d " +
                            "JOIN machines m ON m.id=d.machine_id WHERE d.user_id=@p0 ORDER BY d.id DESC", userId);
        }
    }
}
