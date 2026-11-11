using System.Collections.Generic;

namespace SmartLaundry
{
    // Rutele pentru cont: inregistrare, login, logout, profilul meu
    public static class AuthController
    {
        // Profilul unui student: date personale + consum total + notificari necitite.
        // Se compune aici (nu in AuthService), ca Auth sa nu depinda de Usage / Notifications.
        static string ProfileJson(Row u)
        {
            long id = u.Long("id");
            JObj o = new JObj();
            o.Add("id", id).Add("name", u.Str("name")).Add("room", u.Str("room")).Add("dorm_id", u.Long("dorm_id"));
            o.AddRaw("stats", J.RowObj(UsageRepository.StatsForUser(id)).ToString());
            o.Add("unread", NotificationService.CountUnread(id));
            return o.ToString();
        }

        // POST /api/register
        public static void Register(Req r)
        {
            Result res = AuthService.Register(r.B.Str("name"), r.B.Str("email"), r.B.Str("password"), r.B.Str("room"), r.B.Long("dorm_id", 0));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            JObj o = new JObj();
            o.Add("token", res.Text).Add("role", "student").AddRaw("user", ProfileJson(res.Row));
            Responses.Json(r.Ctx, 200, o.ToString());
        }

        // POST /api/login
        // Controllerul stie ce e HTTP (adresa IP a clientului); serviciul primeste doar un text "ip" si
        // nu stie de unde vine. Asa il putem testa sau refolosi fara un server HTTP.
        public static void Login(Req r)
        {
            string ip = r.Ctx.Request.RemoteEndPoint.Address.ToString();
            Result res = AuthService.Login(ip, r.B.Str("email"), r.B.Str("password"));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            JObj o = new JObj();
            o.Add("token", res.Text).Add("role", res.Row.Str("role"));
            if (res.Row.Str("role") == "admin")
            {
                o.AddRaw("user", "null");
            }
            else
            {
                o.AddRaw("user", ProfileJson(res.Row));
            }
            Responses.Json(r.Ctx, 200, o.ToString());
        }

        // POST /api/logout
        public static void Logout(Req r)
        {
            AuthService.Logout(r.Ctx);
            Responses.Ok(r.Ctx);
        }

        // GET /api/me: profil + notificari + rezervari.
        // Un singur apel intoarce tot ce are nevoie interfata dupa login (clopotelul, rezervarile),
        // in loc de trei cereri separate: mai putine drumuri la server, pagina apare mai repede.
        public static void Me(Req r)
        {
            if (r.Admin)
            {
                Responses.Json(r.Ctx, 200, "{\"admin\":true}");
                return;
            }
            JObj o = new JObj();
            o.AddRaw("user", ProfileJson(r.User));
            o.AddRaw("notifications", J.RowsToJson(NotificationService.Recent(r.UserId)));
            o.AddRaw("reservations", J.RowsToJson(ReservationService.ListOpenForUser(r.UserId)));
            Responses.Json(r.Ctx, 200, o.ToString());
        }
    }
}
