using System;
using System.Globalization;
using System.IO;

namespace SmartLaundry
{
    // Doar pornirea aplicatiei, in ordine: Core (baza de date), Seed, Simulation, Server.
    //
    // Programul nu contine nicio regula: doar leaga piesele in ordinea corecta. Ordinea conteaza:
    //  1. baza de date trebuie deschisa si tabelele create inainte de orice altceva;
    //  2. datele demo se adauga dupa ce tabelele exista;
    //  3. simulatorul porneste inainte de server, ca masinile sa fie deja "vii" cand vine prima cerere;
    //  4. serverul porneste ULTIMUL pentru ca Server.Start nu se mai intoarce (asteapta cereri la nesfarsit).
    public class Program
    {
        static void Main(string[] args)
        {
            // Fortam formatul "neutru" pentru numere si date, in toate thread-urile. Altfel pe un Windows
            // in limba romana un numar ca 0.35 ar fi scris "0,35" si am produce JSON-uri sau SQL gresite.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            // folderul proiectului = cel care contine public/index.html
            string root = FindRoot();
            if (root == null)
            {
                Console.WriteLine("Nu gasesc folderul 'public' (cu index.html). Ruleaza programul din folderul smart-laundry.");
                return;
            }
            StaticFiles.PublicDir = Path.GetFullPath(Path.Combine(root, "public"));

            string adminPass = Environment.GetEnvironmentVariable("ADMIN_PASS");
            if (adminPass == null || adminPass == "")
            {
                adminPass = "admin123";
            }
            string port = Environment.GetEnvironmentVariable("PORT");
            if (port == null || port == "")
            {
                port = "3000";
            }

            // 1. Core: baza de date
            Db.Open(Path.Combine(root, "laundry.db"));
            lock (Db.Lock)
            {
                SchemaCreator.Create();
                SettingsRepository.Load();
                // 2. Seed: date demo la prima pornire
                SeedData.Run(adminPass);
            }

            // 3. Simulation: simulatorul de senzori ruleaza pe un thread separat
            SimulatorLoop.Start();

            // 4. Server: primeste cererile HTTP (nu se mai intoarce)
            Console.WriteLine("Smart Laundry Campus (C#) ruleaza pe http://localhost:" + port + "  (admin: admin@campus.ro / " + adminPass + ")");
            Server.Start(port);
        }

        // Cauta in sus, din folderul curent, un folder care are public/index.html.
        // DE CE: programul poate fi pornit din smart-laundry/ ("dotnet run --project backend") sau din
        // smart-laundry/backend/ ("dotnet run"), iar folderul de lucru difera. Cautand in sus, gasim
        // radacina proiectului indiferent de unde pornim, fara cai scrise de mana care ar crapa pe alt calculator.
        static string FindRoot()
        {
            string[] starts = { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            for (int s = 0; s < starts.Length; s++)
            {
                string dir = starts[s];
                for (int level = 0; level < 8; level++)
                {
                    if (File.Exists(Path.Combine(dir, "public", "index.html")))
                    {
                        return dir;
                    }
                    DirectoryInfo parent = Directory.GetParent(dir);
                    if (parent == null)
                    {
                        break;
                    }
                    dir = parent.FullName;
                }
            }
            return null;
        }
    }
}
