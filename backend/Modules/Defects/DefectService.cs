using System.Collections.Generic;

namespace SmartLaundry
{
    // Regulile defectiunilor. Ce se intampla cu masina cand o defectiune e rezolvata
    // (eliberare, notificare) se face in FaultFlow.
    //
    // O defectiune are doua surse: un STUDENT o raporteaza (user_id = el) sau un SENZOR o detecteaza
    // (user_id = null, source = 'sensor'). Le tinem in acelasi tabel: administratorul are o singura
    // lista de rezolvat, indiferent de unde a venit problema.
    public static class DefectService
    {
        public static Row FindById(long id)
        {
            return DefectRepository.FindById(id);
        }

        public static List<Row> RecentForMachine(long machineId)
        {
            return DefectRepository.RecentForMachine(machineId);
        }

        public static List<Row> ListAll()
        {
            return DefectRepository.ListAll();
        }

        public static List<Row> ListForUser(long userId)
        {
            return DefectRepository.ListForUser(userId);
        }

        public static long CountUnresolved(long machineId)
        {
            return DefectRepository.CountUnresolved(machineId);
        }

        // Un student raporteaza o defectiune. Una grava scoate masina din uz daca e libera.
        public static Result Report(long machineId, long userId, string description, string severity)
        {
            return ReportAt(machineId, userId, description, severity, Util.Now());
        }

        // La fel, dar cu data stabilita (folosit la datele demo)
        public static Result ReportAt(long machineId, long userId, string descriptionIn, string severityIn, long created)
        {
            string description = Util.Trim(descriptionIn);
            Row m = MachineService.FindById(machineId);
            if (m == null || description == "")
            {
                return Result.Fail(400, "Date incomplete");
            }
            if (description.Length > 500)
            {
                description = description.Substring(0, 500);
            }
            // Nu avem incredere in ce trimite clientul: orice valoare in afara celor 3 devine "medium",
            // ca in baza de date sa ajunga doar valori pe care interfata stie sa le afiseze.
            string severity = severityIn;
            if (severity != "low" && severity != "medium" && severity != "high")
            {
                severity = "medium";
            }

            DefectRepository.Insert(machineId, userId, description, severity, created, "user");
            // O defectiune GRAVA scoate imediat masina din uz, dar doar daca e libera: daca ruleaza sau
            // are rufe in ea, nu o oprim in mijlocul ciclului (ar strica rufele studentului care o foloseste).
            if (severity == "high" && m.Str("status") == "free")
            {
                MachineService.SetOutOfOrder(machineId);
            }
            return Result.Success();
        }

        // Defectiune detectata de senzor
        public static void AddFromSensor(long machineId, string description)
        {
            DefectRepository.Insert(machineId, 0, description, "high", Util.Now(), "sensor");
        }

        // Schimba starea (open / in_progress / resolved)
        public static void SetStatus(long id, string status)
        {
            if (status == "resolved")
            {
                DefectRepository.SetStatus(id, status, Util.Now());
            }
            else
            {
                DefectRepository.SetStatus(id, status, null);
            }
        }

        public static bool IsValidStatus(string status)
        {
            return status == "open" || status == "in_progress" || status == "resolved";
        }
    }
}
