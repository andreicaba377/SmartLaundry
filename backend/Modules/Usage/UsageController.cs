using System.Collections.Generic;

namespace SmartLaundry
{
    // GET /api/history: istoricul de utilizare al studentului logat
    public static class UsageController
    {
        public static void History(Req r)
        {
            List<Row> rows = UsageRepository.RecentForUser(r.UserId);
            JArr arr = new JArr();
            for (int i = 0; i < rows.Count; i++)
            {
                JObj o = J.RowObj(rows[i]);
                o.Add("program_label", Programs.LabelOf(rows[i].Str("program")));
                arr.Add(o.ToString());
            }
            Responses.Json(r.Ctx, 200, arr.ToString());
        }
    }
}
