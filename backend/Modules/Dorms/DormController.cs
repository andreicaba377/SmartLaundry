using System.Collections.Generic;

namespace SmartLaundry
{
    // GET /api/dorms: caminele, cu numarul de masini libere (numaratorile vin de la MachineService).
    //
    // DE CE Dorms nu are Service: nu exista nicio regula de business (doar o lista de camine).
    // Un Service gol ar fi doar un fisier in plus. Numaratorile despre masini nu le facem aici cu SQL
    // propriu (tabelul machines apartine altui modul), ci cerem MachineService, ca regula
    // "un tabel, un proprietar" sa ramana valabila.
    public static class DormController
    {
        public static void List(Req r)
        {
            List<Row> dorms = DormRepository.ListAll();
            JArr arr = new JArr();
            for (int i = 0; i < dorms.Count; i++)
            {
                Row d = dorms[i];
                long id = d.Long("id");
                JObj o = new JObj();
                o.Add("id", id).Add("name", d.Str("name")).Add("address", d.Str("address"));
                o.Add("washers_total", MachineService.CountInDorm(id, "washer", ""));
                o.Add("washers_free", MachineService.CountInDorm(id, "washer", "free"));
                o.Add("dryers_total", MachineService.CountInDorm(id, "dryer", ""));
                o.Add("dryers_free", MachineService.CountInDorm(id, "dryer", "free"));
                o.Add("running", MachineService.CountInDorm(id, "", "running"));
                o.Add("broken", MachineService.CountInDorm(id, "", "out_of_order"));
                arr.Add(o.ToString());
            }
            Responses.Json(r.Ctx, 200, arr.ToString());
        }
    }
}
