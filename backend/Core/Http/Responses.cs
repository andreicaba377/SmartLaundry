using System.Net;
using System.Text;

namespace SmartLaundry
{
    // Rezultatul unei operatii de business: a reusit sau a esuat (cu cod HTTP si mesaj).
    //
    // DE CE Result si nu exceptii: "Mașina e ocupată" nu e o eroare de program, e o situatie normala.
    // Exceptiile sunt lente si fac fluxul greu de urmarit. Un Result se intoarce ca orice valoare,
    // iar serviciul decide codul (400, 404, 409), controllerul doar il trimite mai departe.
    // Serviciile nu trebuie sa stie nimic despre HTTP, dar tot pot spune "asta e un conflict (409)".
    public class Result
    {
        public bool Ok = true;
        public int Code = 200;
        public string Error;
        public long Id;       // ex. id-ul obiectului creat
        public string Text;   // ex. token-ul unei sesiuni
        public Row Row;       // ex. utilizatorul creat

        public static Result Success()
        {
            return new Result();
        }

        public static Result Fail(int code, string error)
        {
            Result r = new Result();
            r.Ok = false;
            r.Code = code;
            r.Error = error;
            return r;
        }
    }

    // Raspunsuri HTTP simple
    public static class Responses
    {
        // Trimite un raspuns si inchide conexiunea. ContentLength64 ii spune browserului cat de lung e
        // raspunsul (fara el, HttpListener ar trimite raspunsul "pe bucati", mai greu de folosit).
        public static void Send(HttpListenerContext ctx, int code, string contentType, byte[] data)
        {
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = contentType;
            ctx.Response.ContentLength64 = data.Length;
            ctx.Response.OutputStream.Write(data, 0, data.Length);
            ctx.Response.Close();
        }

        public static void Json(HttpListenerContext ctx, int code, string json)
        {
            Send(ctx, code, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }

        public static void Error(HttpListenerContext ctx, int code, string message)
        {
            Json(ctx, code, new JObj().Add("error", message).ToString());
        }

        public static void Ok(HttpListenerContext ctx)
        {
            Json(ctx, 200, "{\"ok\":true}");
        }

        // Scurtatura folosita de aproape toate controllerele:
        //     if (Responses.SendIfFailed(ctx, res)) { return; }
        // Daca operatia a esuat, trimite eroarea si intoarce true (controllerul se opreste).
        // Daca a reusit, nu trimite nimic si intoarce false (controllerul isi continua treaba).
        public static bool SendIfFailed(HttpListenerContext ctx, Result res)
        {
            if (res.Ok)
            {
                return false;
            }
            Error(ctx, res.Code, res.Error);
            return true;
        }
    }
}
