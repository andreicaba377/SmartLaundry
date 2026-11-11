using System;
using System.Collections.Generic;
using System.Threading;

namespace SmartLaundry
{
    // Bucla simulatorului: o data pe secunda parcurge masinile si le da pe rand
    // simulatorului de senzori, detectorului de blocaje si traficului demo.
    // Nu contine SQL si nu are reguli proprii: foloseste serviciile si flow-urile modulelor.
    public static class SimulatorLoop
    {
        static long tickN = 0;
        static bool demo = true;

        public static void Start()
        {
            if (Environment.GetEnvironmentVariable("DEMO") == "0")
            {
                demo = false;
            }
            Thread t = new Thread(Loop);
            t.IsBackground = true;
            t.Start();
        }

        // Ruleaza pe propriul thread, in paralel cu serverul. Dupa fiecare "tick" doarme o secunda.
        // Orice eroare e prinsa si afisata, dar bucla CONTINUA: o problema la o masina nu trebuie sa
        // opreasca simularea pentru toate celelalte.
        static void Loop()
        {
            for (; ; )
            {
                Thread.Sleep(1000);
                try
                {
                    // Tick-ul ia acelasi lock ca si cererile HTTP: cat timp simulatorul schimba starea
                    // masinilor, nicio cerere nu citeste date "la jumatate" (si invers). Ping-ul SSE se
                    // trimite in afara lock-ului, ca sa nu tinem baza de date blocata cat scriem la retea.
                    lock (Db.Lock)
                    {
                        Tick();
                    }
                    if (tickN % 20 == 0)
                    {
                        SseHub.Ping();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Eroare in simulator: " + e.Message);
                }
            }
        }

        static void Tick()
        {
            tickN++;
            long t = Util.Now();
            long grace = SettingsRepository.GraceMs();
            List<Row> machines = MachineService.ListEnabled();

            // Luam lista masinilor O SINGURA DATA la inceputul tick-ului (un singur SELECT) si dam fiecare
            // masina pe rand celor doua "ochi": MachineSimulator (ce masoara senzorul) si BlockDetector
            // (ce trebuie facut daca a trecut timpul). Fiecare isi verifica singur statusul si ignora
            // masinile care nu-l privesc, deci nu se calca pe picioare.
            for (int i = 0; i < machines.Count; i++)
            {
                MachineSimulator.Step(machines[i], t);
                BlockDetector.Check(machines[i], t, grace, tickN);
            }

            if (demo && tickN % 3 == 0)
            {
                DemoTraffic.Run(machines, t);
            }

            // curatenie periodica: telemetria veche de 2 ore si sesiunile expirate
            if (tickN % 120 == 0)
            {
                TelemetryService.Cleanup(t - 2 * 3600000L);
                AuthService.DeleteExpiredSessions();
            }

            // La sfarsit anuntam toate browserele ca a trecut o secunda. Interfata decide singura daca
            // trebuie sa reimprospateze datele (de ex. numai pe paginile cu progres live).
            SseHub.PushType("tick");
        }
    }
}
