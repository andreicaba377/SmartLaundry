namespace SmartLaundry
{
    // Defectiuni: scoaterea unei masini din uz si revenirea ei cand defectiunea e rezolvata.
    //
    // Aici apar din nou mai multe module intr-o singura operatie (Machines + Defects + Notifications +
    // eliberarea masinii), deci stau intr-un Flow, nu intr-un serviciu.
    public static class FaultFlow
    {
        // Defectiune detectata de senzor (sau simulata de admin): masina se opreste, se creeaza
        // o defectiune, iar studentul care o folosea e anuntat
        public static void Raise(Row m, string description)
        {
            MachineService.SetFaulted(m.Long("id"));
            DefectService.AddFromSensor(m.Long("id"), description);
            NotificationService.Notify(m.Long("user_id"), "fault",
                "🔧 " + m.Str("name") + " s-a oprit din cauza unei defecțiuni. Rufele pot fi scoase manual - contactează administratorul.");
            SseHub.PushType("machines");
        }

        // Adminul schimba starea unei defectiuni. Cand ultima defectiune a masinii e rezolvata,
        // masina se elibereaza, iar autorul raportului e anuntat.
        public static Result ChangeStatus(long defectId, string status)
        {
            if (!DefectService.IsValidStatus(status))
            {
                return Result.Fail(400, "Stare invalidă");
            }
            Row d = DefectService.FindById(defectId);
            if (d == null)
            {
                return Result.Fail(404, "not found");
            }

            DefectService.SetStatus(defectId, status);

            // O masina poate avea MAI MULTE defectiuni deschise. O eliberam doar cand nu mai ramane
            // niciuna nerezolvata (CountUnresolved == 0) SI inca e scoasa din uz: nu vrem sa punem la
            // dispozitie o masina care mai are o problema, nici sa "eliberam" una care ruleaza normal.
            // Autorul raportului e anuntat de fiecare data cand ii e rezolvata defectiunea lui.
            if (status == "resolved")
            {
                Row m = MachineService.FindById(d.Long("machine_id"));
                if (m != null)
                {
                    if (m.Str("status") == "out_of_order" && DefectService.CountUnresolved(m.Long("id")) == 0)
                    {
                        ReleaseFlow.Release(m);
                    }
                    NotificationService.Notify(d.Long("user_id"), "defect",
                        "🛠️ Defecțiunea raportată la " + m.Str("name") + " a fost rezolvată.");
                }
            }
            SseHub.PushType("machines");
            return Result.Success();
        }
    }
}
