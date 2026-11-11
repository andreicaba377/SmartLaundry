namespace SmartLaundry
{
    // Singurul loc care stie ce ruta merge la ce controller si cine are voie s-o apeleze.
    //
    // DE CE doi pasi separati (1. cine are voie, 2. cine primeste cererea)?
    //  - Verificarea rolului se face o SINGURA data, aici. Daca ar sta in fiecare controller, ar fi
    //    usor sa uiti un "if (!admin)" la o ruta noua si sa lasi o gaura de securitate.
    //  - Controllerele raman scurte: presupun ca utilizatorul e deja valid.
    public static class Router
    {
        public static void Route(Req r)
        {
            AccessLevel level = RequiredLevel(r);
            if (!Allowed(r, level))
            {
                return;
            }
            Dispatch(r);
        }

        // ---------- 1. cine are voie ----------

        static AccessLevel RequiredLevel(Req r)
        {
            string a = r.Segment(2);
            string m = r.Method;

            if (a == "admin")
            {
                return AccessLevel.Admin;
            }
            if (a == "me" || a == "stream" || a == "notifications")
            {
                return AccessLevel.Logged;
            }
            // Aceeasi cale poate cere drepturi diferite dupa metoda HTTP: oricine logat poate CITI
            // masinile (GET), dar doar un student le poate FOLOSI (POST: start, collect, reserve).
            // Adminul nu porneste spalari, de aceea POST pe masini e doar pentru studenti.
            if (a == "machines")
            {
                if (m == "GET")
                {
                    return AccessLevel.Logged;
                }
                return AccessLevel.Student;
            }
            if (a == "reservations" || a == "history")
            {
                return AccessLevel.Student;
            }
            if (a == "defects")
            {
                if (m == "GET")
                {
                    return AccessLevel.Logged;
                }
                return AccessLevel.Student;
            }
            // register, login, logout, dorms, programs, config, iot: publice. IoT nu are nevoie de cont
            // de student: senzorul se autentifica singur, cu cheia lui (verificata in TelemetryController).
            // Si o ruta necunoscuta ajunge aici: va primi 404 la pasul urmator, nu 401.
            return AccessLevel.Public;
        }

        static bool Allowed(Req r, AccessLevel level)
        {
            if (level == AccessLevel.Public)
            {
                return true;
            }
            // 401 = "nu stiu cine esti" (nelogat); 403 = "stiu cine esti, dar nu ai voie".
            // Frontend-ul foloseste diferenta: la 401 te duce la login, la 403 doar arata mesajul.
            if (r.Me == null)
            {
                Responses.Error(r.Ctx, 401, "Autentificare necesară");
                return false;
            }
            if (level == AccessLevel.Student && r.User == null)
            {
                Responses.Error(r.Ctx, 403, "Doar studenții pot face asta");
                return false;
            }
            if (level == AccessLevel.Admin && !r.Admin)
            {
                Responses.Error(r.Ctx, 403, "Acces interzis");
                return false;
            }
            return true;
        }

        // ---------- 2. ce controller primeste cererea ----------

        // Cai de forma /api/machines/5/start se sparg la '/' in segmente: "", "api", "machines", "5", "start".
        // Alegem controllerul dupa segmentul 2 (a), metoda HTTP (m) si numarul de segmente (n).
        // Verificam si numarul de segmente ca /api/machines (lista) sa nu se confunde cu
        // /api/machines/5 (detaliu). Nu folosim un dictionar de rute: un lant de if-uri e usor de citit.
        static void Dispatch(Req r)
        {
            string a = r.Segment(2);
            string m = r.Method;
            int n = r.P.Length;

            // Auth
            if (a == "register" && m == "POST") { AuthController.Register(r); return; }
            if (a == "login" && m == "POST") { AuthController.Login(r); return; }
            if (a == "logout" && m == "POST") { AuthController.Logout(r); return; }
            if (a == "me" && m == "GET") { AuthController.Me(r); return; }

            // Dorms
            if (a == "dorms" && m == "GET") { DormController.List(r); return; }

            // Machines (+ programe)
            if (a == "programs" && m == "GET") { MachineController.Programs(r); return; }
            if (a == "machines" && m == "GET" && n == 3) { MachineController.List(r); return; }
            if (a == "machines" && m == "GET" && n == 4) { MachineController.Detail(r); return; }
            if (a == "machines" && m == "POST" && n == 5 && r.P[4] == "start") { MachineController.Start(r); return; }
            if (a == "machines" && m == "POST" && n == 5 && r.P[4] == "collect") { MachineController.Collect(r); return; }

            // Reservations
            if (a == "machines" && m == "POST" && n == 5 && r.P[4] == "reserve") { ReservationController.Reserve(r); return; }
            if (a == "reservations" && m == "POST" && n == 5 && r.P[4] == "cancel") { ReservationController.Cancel(r); return; }

            // Defects
            if (a == "defects" && m == "GET") { DefectController.List(r); return; }
            if (a == "defects" && m == "POST") { DefectController.Report(r); return; }

            // Notifications + flux live
            if (a == "notifications" && m == "POST" && n == 4 && r.P[3] == "read") { NotificationController.MarkRead(r); return; }
            if (a == "stream" && m == "GET") { NotificationController.Stream(r); return; }

            // Usage
            if (a == "history" && m == "GET") { UsageController.History(r); return; }

            // Telemetry (senzori IoT)
            if (a == "iot" && m == "POST" && n == 4 && r.P[3] == "telemetry") { TelemetryController.Post(r); return; }

            // Admin
            if (a == "config" && m == "GET") { AdminController.Config(r); return; }
            if (a == "admin")
            {
                string b = r.Segment(3);
                if (b == "stats" && m == "GET" && n == 4) { AdminController.Stats(r); return; }
                if (b == "speed" && m == "POST" && n == 4) { AdminController.Speed(r); return; }
                if (b == "broadcast" && m == "POST" && n == 4) { AdminController.Broadcast(r); return; }
                if (b == "machines" && m == "POST" && n == 4) { AdminController.AddMachine(r); return; }
                if (b == "machines" && m == "DELETE" && n == 5) { AdminController.DeleteMachine(r); return; }
                if (b == "machines" && m == "POST" && n == 6) { AdminController.MachineAction(r); return; }
                if (b == "defects" && m == "POST" && n == 5) { DefectController.ChangeStatus(r); return; }
            }

            Responses.Error(r.Ctx, 404, "Rută inexistentă");
        }
    }
}
