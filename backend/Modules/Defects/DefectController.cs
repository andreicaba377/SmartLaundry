using System.Collections.Generic;

namespace SmartLaundry
{
    // Rutele pentru defectiuni
    public static class DefectController
    {
        // GET /api/defects: adminul le vede pe toate, studentul doar pe ale lui
        public static void List(Req r)
        {
            List<Row> rows;
            if (r.Admin)
            {
                rows = DefectService.ListAll();
            }
            else
            {
                rows = DefectService.ListForUser(r.UserId);
            }
            Responses.Json(r.Ctx, 200, J.RowsToJson(rows));
        }

        // POST /api/defects   body: {machine_id, description, severity}
        public static void Report(Req r)
        {
            Result res = DefectService.Report(r.B.Long("machine_id", 0), r.UserId, r.B.Str("description"), r.B.Str("severity"));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            SseHub.PushType("machines");
            Responses.Ok(r.Ctx);
        }

        // POST /api/admin/defects/{id}   body: {status}
        public static void ChangeStatus(Req r)
        {
            Result res = FaultFlow.ChangeStatus(r.IdAt(4), r.B.Str("status"));
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            Responses.Ok(r.Ctx);
        }
    }
}
