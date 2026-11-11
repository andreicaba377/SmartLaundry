using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace SmartLaundry
{
    // Serverul HTTP: primeste cererile, completeaza Req si le da mai departe Router-ului.
    //
    // DE CE HttpListener si nu ASP.NET: ASP.NET are multa "magie" (middleware, lambda-uri, injectare de
    // dependente). HttpListener e simplu: GetContext() asteapta o cerere si ne-o da. Controlam tot fluxul
    // cu cod obisnuit (if / for), exact ce cere proiectul.
    public static class Server
    {
        public static void Start(string port)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:" + port + "/");
            listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            listener.Start();

            // Bucla principala. GetContext() blocheaza pana vine o cerere. Fiecare cerere primeste propriul
            // thread: asa o conexiune lenta sau una SSE (care ramane deschisa ore) nu blocheaza pe celelalte.
            // Thread-urile sunt "background", deci se opresc singure cand programul se inchide.
            for (; ; )
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = listener.GetContext();
                }
                catch (Exception)
                {
                    continue;
                }
                Thread t = new Thread(HandleRequest);
                t.IsBackground = true;
                t.Start(ctx);
            }
        }

        static void HandleRequest(object state)
        {
            HttpListenerContext ctx = (HttpListenerContext)state;
            try
            {
                // Tot ce nu incepe cu /api/ e un fisier al paginii (index.html, style.css, app.js):
                // il servim direct, fara baza de date si fara autentificare.
                string path = ctx.Request.Url.AbsolutePath;
                if (!path.StartsWith("/api/"))
                {
                    StaticFiles.Serve(ctx, path);
                    return;
                }

                Req r = new Req();
                r.Ctx = ctx;
                r.Method = ctx.Request.HttpMethod;
                r.P = path.Split('/');
                r.B = Body.Parse(ReadBody(ctx));

                // Body-ul se citeste INAINTE de lock: citirea depinde de reteaua clientului (poate dura),
                // iar noi nu vrem sa tinem baza de date blocata pentru altii cat asteptam un client lent.
                //
                // De aici incolo tot ce se intampla (aflam cine e utilizatorul + rularea rutei) e intr-un
                // singur lock. Asa o cerere vede baza de date "intreaga": nicio alta cerere sau tick de
                // simulator nu schimba datele la mijlocul ei (ex. doi studenti nu pot lua aceeasi masina).
                lock (Db.Lock)
                {
                    r.Me = AuthService.CurrentUser(ctx);
                    if (r.Me != null)
                    {
                        if (r.Me.Str("role") == "admin")
                        {
                            r.Admin = true;
                        }
                        else
                        {
                            r.User = r.Me;
                            r.UserId = r.Me.Long("id");
                        }
                    }
                    Router.Route(r);
                }
            }
            catch (Exception e)
            {
                // Orice eroare neprevazuta nu trebuie sa opreasca serverul. O scriem in consola
                // (ca sa o vedem) si clientul primeste un 500 generic, fara detalii interne.
                Console.WriteLine("Eroare: " + e.Message);
                try
                {
                    Responses.Error(ctx, 500, "Eroare internă");
                }
                catch (Exception)
                {
                }
            }
        }

        static string ReadBody(HttpListenerContext ctx)
        {
            if (ctx.Request.HttpMethod == "GET" || ctx.Request.HttpMethod == "DELETE")
            {
                return "";
            }
            // Limitam body-ul la 100.000 de caractere: o cerere uriasa nu trebuie sa umple memoria
            // serverului. Citim 100.001; daca s-au citit mai mult de 100.000, body-ul e prea mare.
            StreamReader reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            char[] buffer = new char[100001];
            int n = reader.ReadBlock(buffer, 0, buffer.Length);
            if (n > 100000)
            {
                return "";   // body prea mare
            }
            return new string(buffer, 0, n);
        }
    }
}
