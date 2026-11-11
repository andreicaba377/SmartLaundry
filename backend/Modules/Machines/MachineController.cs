using System.Collections.Generic;

namespace SmartLaundry
{
    // Rutele pentru masini: lista, detalii, pornire program, ridicare rufe.
    // Citeste, in plus, informatii din Reservations / Telemetry / Defects / Usage pentru a completa afisajul.
    //
    // DE CE Start si Collect apeleaza un Flow si nu MachineService direct: pornirea unui program atinge
    // si rezervarile (marcheaza rezervarea folosita), iar ridicarea rufelor poate preda masina urmatorului
    // din coada. Aceste operatii implica mai multe module, deci stau in Flows. Controllerul ramane scurt.
    public static class MachineController
    {
        public static void Programs(Req r)
        {
            Responses.Json(r.Ctx, 200, SmartLaundry.Programs.ToJson());
        }

        // GET /api/machines?dorm=2
        public static void List(Req r)
        {
            long dorm = 0;
            string dormText = r.Query("dorm");
            if (dormText != null)
            {
                long.TryParse(dormText, out dorm);
            }

            List<Row> machines;
            if (dorm != 0)
            {
                machines = MachineService.ListEnabledInDorm(dorm);
            }
            else
            {
                machines = MachineService.ListEnabled();
            }

            JArr arr = new JArr();
            for (int i = 0; i < machines.Count; i++)
            {
                arr.Add(ViewOf(machines[i], r.UserId).ToString());
            }
            Responses.Json(r.Ctx, 200, arr.ToString());
        }

        // GET /api/machines/{id}
        public static void Detail(Req r)
        {
            long id = r.IdAt(3);
            Row m = MachineService.FindById(id);
            if (m == null)
            {
                Responses.Error(r.Ctx, 404, "not found");
                return;
            }

            JObj o = ViewOf(m, r.UserId);
            o.AddRaw("telemetry", J.RowsToJson(TelemetryService.Recent(id)));
            o.AddRaw("defects", J.RowsToJson(DefectService.RecentForMachine(id)));
            o.AddRaw("history", J.RowsToJson(UsageRepository.RecentForMachine(id)));
            o.Add("kwh_total", m.Dbl("kwh_total")).Add("water_total", m.Dbl("water_total"));
            if (r.Admin)
            {
                o.Add("api_key", m.Str("api_key"));
            }
            Responses.Json(r.Ctx, 200, o.ToString());
        }

        // POST /api/machines/{id}/start   body: {"program":"rapid"}
        public static void Start(Req r)
        {
            Result res = CycleFlow.Start(r.IdAt(3), r.UserId, r.B.Str("program"));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            Responses.Ok(r.Ctx);
        }

        // POST /api/machines/{id}/collect
        public static void Collect(Req r)
        {
            Result res = ReleaseFlow.Collect(r.IdAt(3), r.UserId);
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            Responses.Ok(r.Ctx);
        }

        // Masina + informatiile despre coada si rezervarea utilizatorului.
        // Informatiile vin de la ReservationService si sunt date ca parametri lui MachineView: asa
        // modulul Machines nu depinde de Reservations (doar acest controller le combina).
        // Compromis: facem 2 interogari mici per masina (N+1). Pentru ~30 de masini e neglijabil si
        // codul ramane simplu; la sute de masini am aduce numaratorile dintr-o singura interogare.
        static JObj ViewOf(Row m, long viewerId)
        {
            long id = m.Long("id");
            long queue = ReservationService.QueueLength(id);
            long mine = ReservationService.MyOpenReservationId(id, viewerId);
            return MachineView.Build(m, viewerId, queue, mine);
        }
    }
}
