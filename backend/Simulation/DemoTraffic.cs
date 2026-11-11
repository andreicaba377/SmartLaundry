using System.Collections.Generic;

namespace SmartLaundry
{
    // Trafic demo: studentii seed (id 2..8) pornesc si ridica rufe singuri,
    // ca panoul sa ramana "viu". Se opreste cu DEMO=0.
    //
    // DE CE exista: fara el, dupa cateva minute toate masinile pornite la inceput se termina, nimeni
    // nu le ridica si panoul devine static (toate "blocate" sau "libere"). Cu trafic demo, simulatorul
    // face ca din cand in cand un "student" sa porneasca o masina libera si sa-si ridice rufele dupa
    // un timp, deci se vad mereu stari diferite. Studentul 1 (Andrei, contul demo) e lasat pe loc:
    // el e cel cu care ne logam, nu vrem sa-i luam masinile pe sub mana.
    public static class DemoTraffic
    {
        public static void Run(List<Row> machines, long t)
        {
            for (int i = 0; i < machines.Count; i++)
            {
                Row m = machines[i];
                string status = m.Str("status");
                long owner = m.Long("user_id");
                bool demoOwner = (owner >= 2 && owner <= 8);

                if ((status == "finished" || status == "blocked") && demoOwner)
                {
                    long waited = t - m.Long("finished_at");
                    if (waited > 12000 + Util.Rnd.Next(20000))
                    {
                        ReleaseFlow.Release(m);
                    }
                }
                else if (status == "free")
                {
                    // 1,2% sansa pe fiecare verificare (la 3 secunde) ca o masina libera sa porneasca:
                    // destul cat sa existe activitate, dar nu atat de des incat sa nu mai ramana masini libere.
                    if (Util.Rnd.NextDouble() < 0.012)
                    {
                        List<string> keys = Programs.KeysOfType(m.Str("type"));
                        string key = keys[Util.Rnd.Next(keys.Count)];
                        CycleFlow.StartAt(m.Long("id"), 2 + Util.Rnd.Next(7), key, 0);
                        SseHub.PushType("machines");
                    }
                }
            }
        }
    }
}
