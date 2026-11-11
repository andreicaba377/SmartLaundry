namespace SmartLaundry
{
    // POST /api/iot/telemetry: aici trimit datele senzorii reali (autentificare cu cheia masinii).
    //
    // DE CE o cheie pe masina si nu un cont de student: un microcontroler nu "se logheaza" ca un om.
    // Fiecare masina are o cheie aleatoare (api_key), vizibila doar adminului. Cheia identifica
    // masina SI o autentifica: cine nu o stie nu poate trimite date false pentru acea masina.
    // Raspundem 403 atat pentru masina inexistenta, cat si pentru cheie gresita (acelasi mesaj).
    public static class TelemetryController
    {
        public static void Post(Req r)
        {
            Row m = MachineService.FindByApiKey(r.B.Long("machine_id", 0), r.B.Str("api_key"));
            if (m == null)
            {
                Responses.Error(r.Ctx, 403, "Cheie dispozitiv invalidă");
                return;
            }

            // Campurile optionale (rpm, apa, usa) raman null daca senzorul nu le trimite. Se pastreaza
            // atunci valoarea veche (vezi COALESCE in MachineRepository.UpdateSensorValues). Folosim Has()
            // ca sa distingem "nu a trimis rpm" de "a trimis rpm = 0".
            object rpm = null;
            if (r.B.Has("rpm"))
            {
                rpm = r.B.Long("rpm", 0);
            }
            object water = null;
            if (r.B.Has("water_l"))
            {
                water = r.B.Dbl("water_l", 0);
            }
            object door = r.B.Str("door");

            TelemetryService.Ingest(m, r.B.Dbl("power_w", 0), r.B.Dbl("vibration", 0), rpm, water, door);
            Responses.Ok(r.Ctx);
        }
    }
}
