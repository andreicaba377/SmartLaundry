namespace SmartLaundry
{
    // Rutele pentru rezervari
    public static class ReservationController
    {
        // POST /api/machines/{id}/reserve
        public static void Reserve(Req r)
        {
            Result res = ReservationService.Reserve(r.IdAt(3), r.UserId);
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            SseHub.PushType("machines");
            Responses.Ok(r.Ctx);
        }

        // POST /api/reservations/{id}/cancel
        public static void Cancel(Req r)
        {
            Result res = ReleaseFlow.CancelReservation(r.IdAt(3), r.UserId);
            if (Responses.SendIfFailed(r.Ctx, res))
            {
                return;
            }
            Responses.Ok(r.Ctx);
        }
    }
}
