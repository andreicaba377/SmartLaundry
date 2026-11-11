namespace SmartLaundry
{
    // Monitorizeaza masinile aflate in "asteptare" dupa un student:
    //  - terminata si neridicata  -> reminder la jumatatea timpului, apoi "blocata"
    //  - rezervata si nefolosita  -> rezervarea expira
    //
    // DE CE aceasta verificare se face periodic (la fiecare secunda) si nu "la cerere": nimeni nu apasa
    // un buton cand cineva uita rufele. Nu exista un eveniment care sa declanseze reactia, ci doar
    // trecerea timpului. Un cronometru verifica periodic "a trecut destul timp?" si doar atunci actioneaza.
    public static class BlockDetector
    {
        public static void Check(Row m, long t, long grace, long tickN)
        {
            string status = m.Str("status");

            if (status == "finished")
            {
                long wait = t - m.Long("finished_at");
                if (wait > grace)
                {
                    MachineService.SetBlocked(m.Long("id"));
                    NotificationService.Notify(m.Long("user_id"), "blocked",
                        "⚠️ " + m.Str("name") + " (" + CycleFlow.DormName(m) + ") este blocată de rufele tale. Te rugăm să le ridici.");
                    SseHub.PushType("machines");
                }
                // La jumatatea timpului acordat trimitem un reminder blând, inainte sa blocam masina.
                // "tickN % 5" = verificam doar la fiecare a 5-a secunda: verificarea cere o interogare in
                // baza de date, iar nu avem nevoie sa o facem in fiecare secunda pentru fiecare masina.
                else if (wait > grace / 2 && tickN % 5 == 0)
                {
                    // reminder trimis o singura data
                    if (!NotificationService.ReminderAlreadySent(m.Long("user_id"), m.Long("finished_at")))
                    {
                        NotificationService.Notify(m.Long("user_id"), "reminder", "⏰ Reminder: rufele din " + m.Str("name") + " te așteaptă.");
                    }
                }
            }
            else if (status == "reserved")
            {
                if (t > m.Long("hold_until"))
                {
                    ReleaseFlow.ExpireHold(m);
                }
            }
        }
    }
}
