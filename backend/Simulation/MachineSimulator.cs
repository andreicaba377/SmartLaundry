using System;

namespace SmartLaundry
{
    // Joaca rolul senzorilor: genereaza putere, turatii, vibratii si apa in functie de faza
    // ciclului si le trimite prin TelemetryService.Ingest (aceeasi cale ca la un senzor real).
    //
    // DE CE un simulator: in laborator nu avem 28 de masini de spalat conectate la retea. Simulatorul
    // face aplicatia demonstrabila: vedem consum care creste la spalare, vibratii mari la centrifugare,
    // cicluri care se termina singure. Valorile sunt apropiate de cele reale (spalarea incalzeste apa,
    // deci consuma cel mai mult; centrifugarea vibreaza cel mai tare) si au zgomot aleator (+-10%).
    public static class MachineSimulator
    {
        public static void Step(Row m, long t)
        {
            string status = m.Str("status");

            if (status == "running")
            {
                StepRunning(m, t);
            }
            else if (status == "free")
            {
                // o masina libera consuma cativa W (standby)
                TelemetryService.Ingest(m, Math.Round(Util.Between(0, 3), 1), 0, null, null, null);
            }
            else
            {
                // finished / blocked / reserved: nu consuma nimic
                TelemetryService.Ingest(m, 0, 0, null, null, null);
            }
        }

        static void StepRunning(Row m, long t)
        {
            if (t >= m.Long("ends_at"))
            {
                CycleFlow.Finish(m);
                return;
            }

            string type = m.Str("type");
            string phase = PhaseFor(m, t);

            // Puterea de baza (W) depinde de faza: incalzirea apei (Spalare) e cea mai mare, umplerea
            // si clatirea consuma putin. Uscatorul consuma mult cat timp incalzeste aerul.
            double basePower = 300;
            if (phase == "Umplere & prespălare") { basePower = 150; }
            if (phase == "Spălare") { basePower = 1900; }
            if (phase == "Clătire") { basePower = 350; }
            if (phase == "Centrifugare") { basePower = 650; }
            if (phase == "Uscare") { basePower = 2200; }
            if (phase == "Răcire") { basePower = 250; }
            double power = Math.Round(basePower * Util.Between(0.9, 1.1));

            double rpm = 30;
            if (phase == "Centrifugare") { rpm = Util.Between(1100, 1200); }
            else if (type == "dryer") { rpm = 55; }
            else if (phase == "Spălare") { rpm = 45; }

            double vibration = 1.5;
            if (phase == "Centrifugare") { vibration = 4.5; }
            vibration = Math.Round(vibration * Util.Between(0.7, 1.3), 2);

            double water = 0;
            if (type == "washer") { water = Math.Round(Util.Between(8, 20), 1); }

            MachineService.SetPhase(m.Long("id"), phase);
            TelemetryService.Ingest(m, power, vibration, (long)Math.Round(rpm), water, null);

            // Defectiune aleatorie foarte rara: 0,04% pe secunda, adica o masina de spalat care ruleaza
            // continuu s-ar strica in medie o data la ~40 de minute reale. Cu doar cateva masini active
            // odata, in demonstratie apare rar, dar suficient cat sa se vada fluxul "defect detectat de senzor".
            if (type == "washer")
            {
                if (Util.Rnd.NextDouble() < 0.0004)
                {
                    FaultFlow.Raise(m, "Vibrații anormale detectate de senzor (posibil dezechilibru / rulment).");
                }
            }
        }

        // Faza in care se afla ciclul, dupa PROCENTUL de timp scurs din durata totala. Folosim procente
        // (nu minute fixe) ca fazele sa se scaleze singure cu durata programului si cu viteza simularii:
        // un program rapid de 30 de minute si unul intens de 90 au aceleasi faze, doar mai scurte/lungi.
        static string PhaseFor(Row m, long t)
        {
            double total = m.Long("ends_at") - m.Long("started_at");
            double progress = (t - m.Long("started_at")) / total;
            if (m.Str("type") == "dryer")
            {
                if (progress < 0.85)
                {
                    return "Uscare";
                }
                return "Răcire";
            }
            if (progress < 0.12)
            {
                return "Umplere & prespălare";
            }
            if (progress < 0.55)
            {
                return "Spălare";
            }
            if (progress < 0.8)
            {
                return "Clătire";
            }
            return "Centrifugare";
        }
    }
}
