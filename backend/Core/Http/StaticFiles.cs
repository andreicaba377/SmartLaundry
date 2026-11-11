using System;
using System.IO;
using System.Net;
using System.Text;

namespace SmartLaundry
{
    // Serveste fisierele din folderul public (index.html, style.css, app.js)
    public static class StaticFiles
    {
        public static string PublicDir;

        static string MimeType(string file)
        {
            string ext = Path.GetExtension(file).ToLower();
            if (ext == ".html") { return "text/html; charset=utf-8"; }
            if (ext == ".js") { return "text/javascript; charset=utf-8"; }
            if (ext == ".css") { return "text/css; charset=utf-8"; }
            if (ext == ".svg") { return "image/svg+xml"; }
            if (ext == ".png") { return "image/png"; }
            if (ext == ".ico") { return "image/x-icon"; }
            if (ext == ".json") { return "application/json"; }
            return "application/octet-stream";
        }

        public static void Serve(HttpListenerContext ctx, string urlPath)
        {
            // Adresa vine codificata (%20 = spatiu); o decodam INAINTE de verificari, altfel cineva
            // ar putea ascunde "../" ca "%2e%2e/" si ar trece de filtru.
            string rel = Uri.UnescapeDataString(urlPath);
            if (rel == "/")
            {
                rel = "/index.html";
            }
            // Protectie impotriva "path traversal": o cerere ca /../backend/Program.cs ar citi fisiere din
            // afara folderului public (cod sursa, baza de date). Refuzam orice cale cu ".." sau "\".
            if (rel.Contains("..") || rel.Contains("\\"))
            {
                Responses.Send(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"));
                return;
            }
            // A doua plasa de siguranta: calea completa trebuie sa ramana in folderul public.
            // Raspundem la fel (404) si daca fisierul lipseste, ca sa nu aflam atacatorului ce exista.
            string full = Path.GetFullPath(Path.Combine(PublicDir, rel.TrimStart('/')));
            if (!full.StartsWith(PublicDir) || !File.Exists(full))
            {
                Responses.Send(ctx, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"));
                return;
            }
            // no-cache: browserul intreaba serverul de fiecare data daca fisierul s-a schimbat.
            // Cat timp dezvoltam, vedem modificarile imediat, fara sa golim cache-ul de mana.
            ctx.Response.Headers.Add("Cache-Control", "no-cache");
            Responses.Send(ctx, 200, MimeType(full), File.ReadAllBytes(full));
        }
    }
}
