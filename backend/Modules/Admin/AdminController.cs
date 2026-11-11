namespace SmartLaundry
{
    // Rutele de administrare. Nu contine reguli proprii: apeleaza serviciile si flow-urile celorlalte module.
    //
    // DE CE adminul nu are propria logica pentru "reseteaza masina" sau "simuleaza defect": sunt
    // aceleasi operatii pe care le face si aplicatia (ReleaseFlow, FaultFlow). Reutilizandu-le,
    // administratorul trece prin exact aceleasi reguli (de ex. o masina resetata o primeste automat
    // primul din coada), iar noi nu scriem aceeasi logica de doua ori.
    // Faptul ca doar adminul ajunge aici nu se verifica in acest fisier, ci in Router (AccessLevel.Admin).
    public static class AdminController
    {
        // GET /api/config (public)
        public static void Config(Req r)
        {
            JObj o = new JObj();
            o.Add("speed", SettingsRepository.Speed);
            o.Add("hold_min", ReservationService.HoldMs / 60000);
            o.Add("grace_s", SettingsRepository.GraceMs() / 1000);
            Responses.Json(r.Ctx, 200, o.ToString());
        }

        // GET /api/admin/stats
        public static void Stats(Req r)
        {
            Responses.Json(r.Ctx, 200, StatsService.Build());
        }

        // POST /api/admin/speed   body: {speed}
        public static void Speed(Req r)
        {
            double s = r.B.Dbl("speed", 30);
            if (s < 1) { s = 1; }
            if (s > 600) { s = 600; }
            SettingsRepository.SetSpeed(s);
            Responses.Json(r.Ctx, 200, new JObj().Add("speed", s).ToString());
        }

        // POST /api/admin/broadcast   body: {message}
        public static void Broadcast(Req r)
        {
            string message = Util.Trim(r.B.Str("message"));
            if (message == "")
            {
                Responses.Error(r.Ctx, 400, "Mesaj gol");
                return;
            }
            NotificationService.Broadcast(message);
            Responses.Ok(r.Ctx);
        }

        // POST /api/admin/machines   body: {dorm_id, type, name}
        public static void AddMachine(Req r)
        {
            Result res = MachineService.AddChecked(r.B.Long("dorm_id", 0), Util.Trim(r.B.Str("name")), r.B.Str("type"));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            SseHub.PushType("machines");
            Responses.Ok(r.Ctx);
        }

        // DELETE /api/admin/machines/{id}
        public static void DeleteMachine(Req r)
        {
            MachineService.Remove(r.IdAt(4));
            SseHub.PushType("machines");
            Responses.Ok(r.Ctx);
        }

        // POST /api/admin/machines/{id}/{disable|enable|release|fault}
        public static void MachineAction(Req r)
        {
            long id = r.IdAt(4);
            string action = r.Segment(5);
            Row m = MachineService.FindById(id);
            if (m == null)
            {
                Responses.Error(r.Ctx, 404, "not found");
                return;
            }

            if (action == "disable")
            {
                MachineService.SetOutOfOrder(id);
            }
            else if (action == "enable" || action == "release")
            {
                ReleaseFlow.Release(m);
            }
            else if (action == "fault")
            {
                FaultFlow.Raise(m, "Defecțiune simulată de administrator.");
            }
            else
            {
                Responses.Error(r.Ctx, 404, "Rută inexistentă");
                return;
            }
            SseHub.PushType("machines");
            Responses.Ok(r.Ctx);
        }
    }
}
