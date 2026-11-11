using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace SmartLaundry
{
    // Un browser conectat la fluxul live (Server-Sent Events)
    public class SseClient
    {
        public HttpListenerResponse Res;
        public long UserId;
        public bool Admin;
    }

    // Tine lista browserelor conectate si le trimite evenimente live.
    //
    // DE CE Server-Sent Events (SSE) si nu WebSocket sau polling:
    //  - Polling (browserul intreaba "s-a schimbat ceva?" la fiecare secunda) ar incarca serverul inutil.
    //  - WebSocket e bidirectional si mai complicat de implementat de mana. Noi trimitem doar de la
    //    server spre browser (stare noua, notificare), iar actiunile utilizatorului merg prin cereri
    //    HTTP normale. SSE e exact asta: o conexiune HTTP care ramane deschisa si prin care serverul
    //    scrie linii "data: ...". Browserul are suport integrat (EventSource) si se reconecteaza singur.
    public static class SseHub
    {
        // Lista e atinsa din mai multe thread-uri (cererile noi se adauga, simulatorul trimite evenimente),
        // de aceea fiecare acces e in lock(clients). Lock-ul e SEPARAT de Db.Lock ca sa nu se blocheze reciproc.
        static List<SseClient> clients = new List<SseClient>();

        // Inregistreaza un browser nou. Raspunsul ramane deschis; clientul e scos din lista
        // cand nu mai raspunde.
        public static void Register(HttpListenerResponse res, long userId, bool admin)
        {
            SseClient c = new SseClient();
            c.Res = res;
            c.UserId = userId;
            c.Admin = admin;

            res.StatusCode = 200;
            res.ContentType = "text/event-stream";
            res.SendChunked = true;
            res.Headers.Add("Cache-Control", "no-cache");
            try
            {
                WriteText(c, "retry: 2000\n\n");
            }
            catch (Exception)
            {
                return;
            }
            lock (clients)
            {
                clients.Add(c);
            }
        }

        static void WriteText(SseClient c, string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text);
            c.Res.OutputStream.Write(data, 0, data.Length);
            c.Res.OutputStream.Flush();
        }

        // userId = 0 inseamna "toti"; altfel doar acel utilizator (si adminii).
        // Evenimentul are formatul SSE: o linie "data: {json}" urmata de o linie goala.
        // Notificarile private (ex. "rufele tale sunt gata") nu trebuie sa ajunga la alti studenti.
        public static void Push(JObj evt, long userId)
        {
            SendToAll("data: " + evt.ToString() + "\n\n", userId);
        }

        // Eveniment simplu, fara date: "tick" sau "machines"
        public static void PushType(string type)
        {
            Push(new JObj().Add("type", type), 0);
        }

        // O linie care incepe cu ":" e comentariu in SSE: browserul o ignora. O trimitem periodic ca sa
        // detectam conexiunile moarte (scrierea esueaza) si sa le scoatem din lista, in loc sa le
        // pastram la nesfarsit. Mai tine si conexiunea "vie" prin proxy-uri care o inchid daca tace.
        public static void Ping()
        {
            SendToAll(": ping\n\n", 0);
        }

        static void SendToAll(string text, long userId)
        {
            lock (clients)
            {
                // Parcurgem lista de la coada spre cap: daca scoatem un client (RemoveAt) in timpul
                // parcurgerii, elementele ramase au indecsi neschimbati, deci nu sarim peste niciunul.
                for (int i = clients.Count - 1; i >= 0; i--)
                {
                    SseClient c = clients[i];
                    bool send = false;
                    if (userId == 0)
                    {
                        send = true;
                    }
                    if (c.UserId == userId)
                    {
                        send = true;
                    }
                    if (c.Admin)
                    {
                        send = true;
                    }
                    if (!send)
                    {
                        continue;
                    }
                    try
                    {
                        WriteText(c, text);
                    }
                    catch (Exception)
                    {
                        clients.RemoveAt(i);
                    }
                }
            }
        }
    }
}
