using System.Collections.Generic;

namespace SmartLaundry
{
    // Masuratorile senzorilor. Atat senzorul real (ESP32), cat si simulatorul folosesc Ingest.
    //
    // DE CE amandoi trec prin acelasi Ingest: simulatorul se comporta exact ca un senzor real.
    // Cand legam un ESP32 adevarat, nu schimbam nimic in restul aplicatiei; doar oprim simulatorul.
    // Avem doua feluri de date: valorile CURENTE (in machines, ultima masuratoare) si ISTORICUL
    // (in telemetry, pentru graficul de consum). Istoricul se pastreaza doar 2 ore (vezi Cleanup).
    public static class TelemetryService
    {
        // Valorile curente se scriu in masina prin MachineService; istoricul se salveaza aici
        // (doar cat timp masina ruleaza). rpm, water, door pot fi null.
        public static void Ingest(Row machine, double power, double vibration, object rpm, object water, object door)
        {
            long id = machine.Long("id");
            MachineService.UpdateSensorValues(id, power, vibration, rpm, water, door);
            // Salvam in istoric doar cat timp masina RULEAZA: cand e libera sau terminata, valorile sunt
            // aproape 0 si nu spun nimic. Fara aceasta conditie, 28 de masini ar adauga ~100.000 de
            // randuri pe ora in tabel, aproape toate inutile.
            if (machine.Str("status") == "running")
            {
                TelemetryRepository.Save(id, Util.Now(), power, vibration);
            }
        }

        // Ultimele masuratori, in ordine cronologica (cea mai veche prima)
        public static List<Row> Recent(long machineId)
        {
            List<Row> desc = TelemetryRepository.LastDescending(machineId);
            List<Row> asc = new List<Row>();
            for (int i = desc.Count - 1; i >= 0; i--)
            {
                asc.Add(desc[i]);
            }
            return asc;
        }

        public static void Cleanup(long olderThan)
        {
            TelemetryRepository.DeleteOlderThan(olderThan);
        }
    }
}
