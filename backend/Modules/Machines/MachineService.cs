using System.Collections.Generic;

namespace SmartLaundry
{
    // Regulile care tin strict de o masina. Nu stie de rezervari, notificari sau defectiuni:
    // operatiile care implica mai multe module stau in Flows.
    //
    // DE CE e atat de "subtire": majoritatea metodelor doar trimit mai departe catre Repository.
    // Pare repetitiv, dar aici e punctul prin care ALTE module intra in Machines. Ele nu ating
    // niciodata MachineRepository (SQL-ul), deci putem schimba cum se salveaza o masina fara sa
    // modificam Reservations, Defects sau Telemetry.
    // REGULA IMPORTANTA: MachineService nu apeleaza niciun alt serviciu. Altfel Machines si
    // Reservations s-ar apela unul pe altul (dependenta circulara); combinarea lor e treaba Flows.
    public static class MachineService
    {
        public static Row FindById(long id)
        {
            return MachineRepository.FindById(id);
        }

        public static Row FindByApiKey(long id, string apiKey)
        {
            return MachineRepository.FindByApiKey(id, apiKey);
        }

        public static Row FindFree(long dormId, string type)
        {
            return MachineRepository.FindFree(dormId, type);
        }

        public static List<Row> ListEnabled()
        {
            return MachineRepository.ListEnabled();
        }

        public static List<Row> ListEnabledInDorm(long dormId)
        {
            return MachineRepository.ListEnabledInDorm(dormId);
        }

        public static List<Row> ListAll()
        {
            return MachineRepository.ListAll();
        }

        public static long CountInDorm(long dormId, string type, string status)
        {
            return MachineRepository.CountInDorm(dormId, type, status);
        }

        public static long CountActive(long userId, string type)
        {
            return MachineRepository.CountActive(userId, type);
        }

        // Adauga o masina noua (cheia pentru senzorul IoT se genereaza automat)
        public static long Add(long dormId, string name, string type, long cycles)
        {
            return MachineRepository.Add(dormId, name, type, Util.RandomHex(8), Util.Now(), cycles);
        }

        public static Result AddChecked(long dormId, string name, string type)
        {
            if (type != "washer" && type != "dryer")
            {
                return Result.Fail(400, "Date invalide");
            }
            if (name == "" || dormId == 0 || !DormRepository.Exists(dormId))
            {
                return Result.Fail(400, "Date invalide");
            }
            Add(dormId, name, type, 0);
            return Result.Success();
        }

        // Scoate masina din retea (nu o sterge din baza de date). "Stergere moale" (enabled=0): istoricul
        // de utilizare si defectiunile o refera, iar un DELETE ar lasa randuri orfane sau ar pierde istoric.
        public static void Remove(long id)
        {
            MachineRepository.SetEnabled(id, 0);
        }

        // ---------- stari ----------

        // Porneste un program. elapsedMs e folosit doar la datele demo (ciclu deja inceput).
        //
        // Durata reala = minutele programului impartite la viteza simularii. Cu viteza 30, un program
        // de 60 de minute dureaza 2 minute reale: asa putem vedea un ciclu intreg intr-o demonstratie.
        // Salvam momentul de START si cel de SFARSIT (nu "cat a mai ramas"): timpul ramas se calculeaza
        // oricand scazand ora curenta, deci nu trebuie actualizat in baza de date la fiecare secunda.
        public static void StartCycle(long machineId, long userId, string programKey, long elapsedMs)
        {
            ProgramInfo p = Programs.Find(programKey);
            long duration = (long)(p.Min * 60000.0 / SettingsRepository.Speed);
            long t = Util.Now();
            MachineRepository.SetRunning(machineId, programKey, t - elapsedMs, t - elapsedMs + duration, userId, p.Temp);
        }

        public static void SetPhase(long id, string phase)
        {
            MachineRepository.SetPhase(id, phase);
        }

        public static void Finish(long id, double kwh, double water)
        {
            MachineRepository.SetFinished(id, Util.Now(), kwh, water);
        }

        public static void SetFree(long id)
        {
            MachineRepository.SetFree(id);
        }

        // Tine masina pentru un student pana la momentul "until"
        public static void Hold(long id, long userId, long until)
        {
            MachineRepository.SetReserved(id, userId, until);
        }

        public static void SetBlocked(long id)
        {
            MachineRepository.SetBlocked(id);
        }

        public static void SetOutOfOrder(long id)
        {
            MachineRepository.SetOutOfOrder(id);
        }

        public static void SetFaulted(long id)
        {
            MachineRepository.SetFaulted(id);
        }

        // Valorile masurate de senzori (folosita de Telemetry)
        public static void UpdateSensorValues(long id, double power, double vibration, object rpm, object water, object door)
        {
            MachineRepository.UpdateSensorValues(id, power, vibration, rpm, water, door, Util.Now());
        }
    }
}
