using System.Net;

namespace SmartLaundry
{
    // Tot ce stim despre o cerere HTTP. Este completata in Server, inainte de Router.
    //
    // DE CE un obiect Req: in loc sa trecem 6 parametri prin fiecare functie, trecem unul singur.
    // Utilizatorul e aflat O DATA (in Server) si pus aici; controllerele nu mai ating token-uri sau
    // antete. Pastram trei vederi ale aceluiasi lucru ca sa fie comode: Me (oricine e logat),
    // Admin (bool) si User/UserId (doar daca e student) - astfel "UserId == 0" inseamna "nu e student".
    public class Req
    {
        public HttpListenerContext Ctx;
        public string Method;
        public string[] P;        // calea sparta la '/', ex: /api/machines/5/start -> "", "api", "machines", "5", "start"
        public Body B;            // body-ul JSON
        public Row Me;            // utilizatorul logat (student sau admin) sau null
        public bool Admin;        // true daca e administrator
        public Row User;          // studentul logat (null daca e admin sau nelogat)
        public long UserId;       // id-ul studentului, 0 daca nu e student

        // Segmentul de cale de la pozitia index, sau "" daca nu exista
        public string Segment(int index)
        {
            if (P.Length <= index)
            {
                return "";
            }
            return P[index];
        }

        // Segmentul de cale ca numar (ex. "5" -> 5), 0 daca nu e numar
        public long IdAt(int index)
        {
            long id;
            if (long.TryParse(Segment(index), out id))
            {
                return id;
            }
            return 0;
        }

        // Parametru din adresa: /api/machines?dorm=2
        public string Query(string name)
        {
            return Ctx.Request.QueryString[name];
        }
    }
}
