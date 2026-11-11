using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "telemetry" (istoricul masuratorilor, pastrat 2 ore)
    public static class TelemetryRepository
    {
        public static void Save(long machineId, long ts, double power, double vibration)
        {
            Db.Exec("INSERT INTO telemetry(machine_id, ts, power_w, vibration) VALUES(@p0, @p1, @p2, @p3)", machineId, ts, power, vibration);
        }

        // Ultimele 60 de masuratori, cele mai noi primele
        public static List<Row> LastDescending(long machineId)
        {
            return Db.Query("SELECT ts, power_w, vibration FROM telemetry WHERE machine_id=@p0 ORDER BY ts DESC LIMIT 60", machineId);
        }

        public static void DeleteOlderThan(long ts)
        {
            Db.Exec("DELETE FROM telemetry WHERE ts < @p0", ts);
        }
    }
}
