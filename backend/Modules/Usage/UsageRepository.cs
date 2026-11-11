using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "usage" (istoricul ciclurilor terminate).
    //
    // DE CE un tabel separat de "machines": machines tine starea CURENTA (se suprascrie la fiecare
    // ciclu), usage tine ISTORICUL (un rand nou la fiecare ciclu, nu se modifica niciodata). Din
    // istoric calculam consumul unui student si statisticile adminului. Modulul n-are Service pentru
    // ca nu are reguli: doar adauga si citeste randuri.
    public static class UsageRepository
    {
        public static void Add(long machineId, long userId, string program, long startedAt, long endedAt, double kwh, double water, double cost)
        {
            Db.Exec("INSERT INTO usage(machine_id, user_id, program, started_at, ended_at, kwh, water, cost) VALUES(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                    machineId, userId, program, startedAt, endedAt, kwh, water, cost);
        }

        // Cicluri, energie, apa si cost total pentru un student
        public static Row StatsForUser(long userId)
        {
            return Db.One("SELECT COUNT(*) AS n, COALESCE(SUM(kwh),0) AS kwh, COALESCE(SUM(water),0) AS water, COALESCE(SUM(cost),0) AS cost FROM usage WHERE user_id=@p0", userId);
        }

        // Ultimele 100 de utilizari ale unui student, cu numele masinii (doar citire din machines)
        public static List<Row> RecentForUser(long userId)
        {
            return Db.Query("SELECT u.*, m.name AS machine_name, m.type AS type, m.dorm_id AS dorm_id FROM usage u " +
                            "JOIN machines m ON m.id=u.machine_id WHERE u.user_id=@p0 ORDER BY u.id DESC LIMIT 100", userId);
        }

        public static List<Row> RecentForMachine(long machineId)
        {
            return Db.Query("SELECT program, started_at, ended_at, kwh, water FROM usage WHERE machine_id=@p0 ORDER BY id DESC LIMIT 5", machineId);
        }
    }
}
