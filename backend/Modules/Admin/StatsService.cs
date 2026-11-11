using System.Collections.Generic;

namespace SmartLaundry
{
    // Statisticile pentru graficele de administrare. Este singurul loc care citeste din mai multe
    // tabele (doar SELECT, nu modifica nimic).
    //
    // DE CE are SQL propriu (singura exceptie de la "SQL doar in Repository"): un grafic ca "consumul
    // pe camin" uneste 3 tabele (dorms, machines, usage) si grupeaza datele. Daca ar trece prin
    // serviciile fiecarui modul, am aduce mii de randuri in memorie si le-am aduna cu bucle in C#.
    // Lasam baza de date sa faca ce stie ea mai bine (GROUP BY, SUM, COUNT): e mai rapid si mai scurt.
    // Exceptia e sigura: sunt doar SELECT-uri, nu modifica nimic.
    public static class StatsService
    {
        public static string Build()
        {
            JObj o = new JObj();

            // masini pe stari
            List<Row> st = Db.Query("SELECT status, COUNT(*) AS n FROM machines WHERE enabled=1 GROUP BY status");
            JObj byStatus = new JObj();
            for (int i = 0; i < st.Count; i++)
            {
                byStatus.Add(st[i].Str("status"), st[i].Long("n"));
            }
            o.AddRaw("byStatus", byStatus.ToString());

            Row totals = Db.One("SELECT COUNT(*) AS cycles, COALESCE(SUM(kwh),0) AS kwh, COALESCE(SUM(water),0) AS water, COALESCE(SUM(cost),0) AS revenue FROM usage");
            o.AddRaw("totals", J.RowObj(totals).ToString());

            long since = Util.Now() - 14L * 86400000L;
            o.AddRaw("perDay", J.RowsToJson(Db.Query(
                "SELECT strftime('%Y-%m-%d', started_at/1000, 'unixepoch', 'localtime') AS d, COUNT(*) AS n, SUM(kwh) AS kwh, SUM(water) AS water " +
                "FROM usage WHERE started_at > @p0 GROUP BY d ORDER BY d", since)));
            o.AddRaw("perHour", J.RowsToJson(Db.Query(
                "SELECT CAST(strftime('%H', started_at/1000, 'unixepoch', 'localtime') AS INTEGER) AS h, COUNT(*) AS n FROM usage GROUP BY h ORDER BY h")));
            o.AddRaw("perDorm", J.RowsToJson(Db.Query(
                "SELECT d.name AS name, COUNT(u.id) AS n, COALESCE(SUM(u.kwh),0) AS kwh, COALESCE(SUM(u.water),0) AS water FROM dorms d " +
                "LEFT JOIN machines m ON m.dorm_id=d.id LEFT JOIN usage u ON u.machine_id=m.id GROUP BY d.id")));

            List<Row> pp = Db.Query("SELECT program, COUNT(*) AS n FROM usage GROUP BY program");
            JArr perProgram = new JArr();
            for (int i = 0; i < pp.Count; i++)
            {
                JObj one = J.RowObj(pp[i]);
                one.Add("label", Programs.LabelOf(pp[i].Str("program")));
                perProgram.Add(one.ToString());
            }
            o.AddRaw("perProgram", perProgram.ToString());

            o.AddRaw("topMachines", J.RowsToJson(Db.Query(
                "SELECT m.name AS name, d.name AS dorm, m.cycles AS cycles FROM machines m JOIN dorms d ON d.id=m.dorm_id WHERE m.enabled=1 ORDER BY m.cycles DESC LIMIT 5")));
            o.AddRaw("defects", J.RowsToJson(Db.Query("SELECT status, COUNT(*) AS n FROM defects GROUP BY status")));
            o.Add("users", AuthService.CountUsers());

            long total = Db.Count("SELECT COUNT(*) FROM machines WHERE enabled=1");
            long busy = Db.Count("SELECT COUNT(*) FROM machines WHERE enabled=1 AND status IN ('running','finished','blocked')");
            double utilization = 0;
            if (total > 0)
            {
                utilization = (double)busy / total;
            }
            o.Add("utilization", utilization).Add("machines", total);
            return o.ToString();
        }
    }
}
