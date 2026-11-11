using System;

namespace SmartLaundry
{
    // Masina asa cum o vede clientul: campuri din baza de date + campuri calculate
    // (progres, timp ramas, offline). Informatiile din alte module (coada, rezervarea mea)
    // sunt primite ca parametri, ca sa nu depindem de acele module.
    //
    // DE CE calculam aici si nu salvam in baza de date: progresul si timpul ramas se schimba cu fiecare
    // secunda. Daca le-am salva, simulatorul ar scrie in baza de date pentru fiecare masina la fiecare
    // secunda, degeaba. Le calculam la cerere din ora de start si de sfarsit (date care nu se schimba).
    public static class MachineView
    {
        public static JObj Build(Row m, long viewerId, long queue, long myReservation)
        {
            long t = Util.Now();
            long id = m.Long("id");
            string status = m.Str("status");
            double speed = SettingsRepository.Speed;

            // "offline" NU se salveaza in baza de date: se deduce. Daca senzorul nu a mai dat semn de
            // viata de 15 secunde, masina apare offline; cand semnalul revine, apare automat starea
            // reala. Nu avem nimic de "reparat" in baza de date dupa o pana de retea.
            string shownStatus = status;
            if (!m.IsNull("last_seen"))
            {
                if (t - m.Long("last_seen") > 15000)
                {
                    shownStatus = "offline";
                }
            }

            // Progresul ciclului intre 0 si 1 (pentru inelul din interfata) = timpul scurs / durata totala.
            // Limitat la 1 (ciclul poate depasi putin sfarsitul pana ruleaza urmatorul tick).
            double progress = 0;
            if (status == "running")
            {
                progress = (t - m.Long("started_at")) / (double)(m.Long("ends_at") - m.Long("started_at"));
                if (progress > 1)
                {
                    progress = 1;
                }
            }
            if (status == "finished" || status == "blocked")
            {
                progress = 1;
            }

            long remainingMs = 0;
            long remainingSim = 0;
            if (status == "running")
            {
                remainingMs = m.Long("ends_at") - t;
                if (remainingMs < 0)
                {
                    remainingMs = 0;
                }
                // Timpul ramas in "minute simulate": cate minute ar mai fi la o spalare in timp real.
                // Studentul vede "22 min" chiar daca in demonstratie ciclul se termina in cateva secunde.
                remainingSim = (long)Math.Ceiling(remainingMs * speed / 60000.0);
            }

            long blockedFor = 0;
            if (status == "finished" || status == "blocked")
            {
                blockedFor = t - m.Long("finished_at");
            }

            string programLabel = null;
            if (!m.IsNull("program"))
            {
                programLabel = Programs.LabelOf(m.Str("program"));
            }

            // "mine" / "held_for_me" depind de cine se uita. Pentru admin (viewerId = 0) sunt mereu false:
            // fara aceasta verificare, o masina fara proprietar (user_id citit ca 0) ar parea a adminului.
            bool mineFlag = false;
            bool heldForMe = false;
            if (viewerId != 0)
            {
                if (m.Long("user_id") == viewerId)
                {
                    mineFlag = true;
                }
                if (m.Long("hold_user_id") == viewerId)
                {
                    heldForMe = true;
                }
            }

            JObj o = new JObj();
            o.Add("id", id).Add("dorm_id", m.Long("dorm_id")).Add("name", m.Str("name")).Add("type", m.Str("type"));
            o.Add("status", shownStatus).Add("program", m.Str("program")).Add("program_label", programLabel);
            o.Add("started_at", m.V["started_at"]).Add("ends_at", m.V["ends_at"]).Add("finished_at", m.V["finished_at"]);
            o.Add("remaining_ms", remainingMs).Add("remaining_sim_min", remainingSim).Add("progress", progress);
            o.Add("phase", m.Str("phase")).Add("power_w", m.Dbl("power_w")).Add("water_l", m.Dbl("water_l")).Add("rpm", m.Long("rpm"));
            o.Add("temp_c", m.Dbl("temp_c")).Add("vibration", m.Dbl("vibration")).Add("door", m.Str("door"));
            o.Add("mine", mineFlag).Add("held_for_me", heldForMe).Add("hold_until", m.V["hold_until"]);
            o.Add("queue", queue);
            if (myReservation == 0)
            {
                o.AddRaw("my_reservation", "null");
            }
            else
            {
                o.Add("my_reservation", myReservation);
            }
            o.Add("cycles", m.Long("cycles")).Add("last_seen", m.V["last_seen"]).Add("blocked_for_ms", blockedFor);
            return o;
        }
    }
}
