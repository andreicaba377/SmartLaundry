using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "machines".
    //
    // DE CE exista un Repository separat: daca schimbam o coloana, o corectam intr-un singur fisier.
    // Aici nu exista nicio regula ("max 2 masini"): doar "cum scriu / citesc". Regulile sunt in Service.
    // Fiecare schimbare de stare are propria metoda (SetRunning, SetFree, ...) in loc de un UPDATE
    // generic, ca fiecare stare sa seteze EXACT campurile care ii apartin si sa nu uitam unul.
    public static class MachineRepository
    {
        public static Row FindById(long id)
        {
            return Db.One("SELECT * FROM machines WHERE id=@p0", id);
        }

        public static Row FindByApiKey(long id, string apiKey)
        {
            return Db.One("SELECT * FROM machines WHERE id=@p0 AND api_key=@p1", id, apiKey);
        }

        // O masina libera de un anumit tip dintr-un camin (sau null)
        public static Row FindFree(long dormId, string type)
        {
            return Db.One("SELECT * FROM machines WHERE dorm_id=@p0 AND type=@p1 AND status='free' LIMIT 1", dormId, type);
        }

        public static List<Row> ListEnabled()
        {
            return Db.Query("SELECT * FROM machines WHERE enabled=1 ORDER BY id");
        }

        public static List<Row> ListEnabledInDorm(long dormId)
        {
            return Db.Query("SELECT * FROM machines WHERE enabled=1 AND dorm_id=@p0 ORDER BY id", dormId);
        }

        public static List<Row> ListAll()
        {
            return Db.Query("SELECT * FROM machines ORDER BY id");
        }

        public static long Add(long dormId, string name, string type, string apiKey, long lastSeen, long cycles)
        {
            return Db.Insert("INSERT INTO machines(dorm_id, name, type, api_key, last_seen, cycles) VALUES(@p0, @p1, @p2, @p3, @p4, @p5)",
                             dormId, name, type, apiKey, lastSeen, cycles);
        }

        public static void SetEnabled(long id, int enabled)
        {
            Db.Exec("UPDATE machines SET enabled=@p0 WHERE id=@p1", enabled, id);
        }

        // ---------- schimbari de stare ----------

        public static void SetRunning(long id, string programKey, long startedAt, long endsAt, long userId, int temp)
        {
            Db.Exec("UPDATE machines SET status='running', program=@p0, started_at=@p1, ends_at=@p2, finished_at=NULL, user_id=@p3, " +
                    "hold_user_id=NULL, hold_until=NULL, door='locked', temp_c=@p4, phase=NULL WHERE id=@p5",
                    programKey, startedAt, endsAt, userId, temp, id);
        }

        public static void SetPhase(long id, string phase)
        {
            Db.Exec("UPDATE machines SET phase=@p0 WHERE id=@p1", phase, id);
        }

        public static void SetFinished(long id, long now, double kwh, double water)
        {
            Db.Exec("UPDATE machines SET status='finished', finished_at=@p0, ends_at=@p0, power_w=0, rpm=0, water_l=0, phase='Terminat', door='unlocked', " +
                    "cycles=cycles+1, kwh_total=kwh_total+@p1, water_total=water_total+@p2 WHERE id=@p3", now, kwh, water, id);
        }

        // Masina devine libera: stergem tot ce tine de utilizarea anterioara (program, proprietar, timpi).
        // IMPORTANT: golim si hold_user_id / hold_until. Daca ramaneau, o masina libera ar parea
        // "tinuta" pentru ultimul student care a rezervat-o si butonul de rezervare ar disparea pentru el.
        public static void SetFree(long id)
        {
            Db.Exec("UPDATE machines SET status='free', program=NULL, user_id=NULL, started_at=NULL, ends_at=NULL, finished_at=NULL, phase=NULL, " +
                    "hold_user_id=NULL, hold_until=NULL, door='closed', power_w=0, rpm=0, water_l=0, vibration=0 WHERE id=@p0", id);
        }

        public static void SetReserved(long id, long holdUserId, long holdUntil)
        {
            Db.Exec("UPDATE machines SET status='reserved', hold_user_id=@p0, hold_until=@p1 WHERE id=@p2", holdUserId, holdUntil, id);
        }

        public static void SetBlocked(long id)
        {
            Db.Exec("UPDATE machines SET status='blocked' WHERE id=@p0", id);
        }

        public static void SetOutOfOrder(long id)
        {
            Db.Exec("UPDATE machines SET status='out_of_order', power_w=0 WHERE id=@p0", id);
        }

        // Defectiune detectata de senzor: se opreste tot (putere, turatii) si usa se deblocheaza,
        // ca studentul sa-si poata scoate rufele cu mana. Diferit de SetOutOfOrder (scoatere simpla din uz).
        public static void SetFaulted(long id)
        {
            Db.Exec("UPDATE machines SET status='out_of_order', power_w=0, rpm=0, phase='Defect', door='unlocked' WHERE id=@p0", id);
        }

        // Valorile curente ale senzorilor. rpm, water, door pot fi null (se pastreaza valoarea veche).
        // COALESCE(@p2, rpm) = "foloseste valoarea noua, iar daca e NULL, las-o pe cea veche". Un senzor
        // real poate trimite doar putere si vibratii; nu vrem sa stergem turatiile trimise inainte.
        public static void UpdateSensorValues(long id, double power, double vibration, object rpm, object water, object door, long lastSeen)
        {
            Db.Exec("UPDATE machines SET power_w=@p0, vibration=@p1, rpm=COALESCE(@p2, rpm), water_l=COALESCE(@p3, water_l), " +
                    "door=COALESCE(@p4, door), last_seen=@p5 WHERE id=@p6",
                    power, vibration, rpm, water, door, lastSeen, id);
        }

        // ---------- numaratori ----------

        // Cate masini active (pornite sau neridicate) are un student de un anumit tip
        public static long CountActive(long userId, string type)
        {
            return Db.Count("SELECT COUNT(*) FROM machines WHERE user_id=@p0 AND status IN ('running','finished','blocked') AND type=@p1", userId, type);
        }

        // type sau status = "" inseamna "oricare". Un singur SQL pentru toate numaratorile caminelor:
        // (@p1='' OR type=@p1) devine adevarat pentru orice tip cand @p1 e gol, deci nu scriem
        // 6 interogari aproape identice.
        public static long CountInDorm(long dormId, string type, string status)
        {
            return Db.Count("SELECT COUNT(*) FROM machines WHERE dorm_id=@p0 AND enabled=1 AND (@p1='' OR type=@p1) AND (@p2='' OR status=@p2)",
                            dormId, type, status);
        }
    }
}
