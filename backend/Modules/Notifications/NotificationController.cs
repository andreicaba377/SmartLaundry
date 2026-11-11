namespace SmartLaundry
{
    // Rutele pentru notificari si pentru fluxul live
    public static class NotificationController
    {
        // POST /api/notifications/read
        public static void MarkRead(Req r)
        {
            if (r.User != null)
            {
                NotificationService.MarkAllRead(r.UserId);
            }
            Responses.Ok(r.Ctx);
        }

        // GET /api/stream: conexiune deschisa, serverul trimite evenimente cand se schimba ceva.
        // Spre deosebire de celelalte controllere, NU inchidem raspunsul (nu apelam Responses.Send):
        // el ramane deschis si SseHub scrie in el mai tarziu. Adminul se aboneaza si el (cu flag-ul
        // Admin) si primeste toate evenimentele, ca panoul lui sa se actualizeze live.
        public static void Stream(Req r)
        {
            SseHub.Register(r.Ctx.Response, r.Me.Long("id"), r.Admin);
        }
    }
}
