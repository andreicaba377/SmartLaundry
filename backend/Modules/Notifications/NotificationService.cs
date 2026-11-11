using System.Collections.Generic;

namespace SmartLaundry
{
    // Notificarile: se salveaza in baza de date si se trimit live catre student.
    // Este singura dependenta permisa intre module (toate celelalte il pot apela).
    //
    // DE CE facem DOUA lucruri (salvam + trimitem live): daca studentul are pagina deschisa, vede
    // notificarea instant (SSE). Daca e deconectat, o gaseste in lista cand revine, pentru ca e
    // salvata. Doar live ar pierde mesajele; doar salvate ar cere ca el sa reincarce pagina.
    public static class NotificationService
    {
        // userId = 0 inseamna "nimeni" (ex. o masina fara proprietar): nu se face nimic
        public static void Notify(long userId, string kind, string message)
        {
            if (userId == 0)
            {
                return;
            }
            NotificationRepository.Insert(userId, kind, message, Util.Now());
            JObj evt = new JObj().Add("type", "notification").Add("userId", userId).Add("message", message);
            SseHub.Push(evt, userId);
        }

        // Anunt catre toti studentii
        public static void Broadcast(string message)
        {
            List<Row> students = AuthService.ListStudentIds();
            for (int i = 0; i < students.Count; i++)
            {
                Notify(students[i].Long("id"), "broadcast", "📢 " + message);
            }
        }

        public static List<Row> Recent(long userId)
        {
            return NotificationRepository.Recent(userId, 50);
        }

        public static long CountUnread(long userId)
        {
            return NotificationRepository.CountUnread(userId);
        }

        public static void MarkAllRead(long userId)
        {
            NotificationRepository.MarkAllRead(userId);
        }

        // Verificarea vine din baza de date, nu dintr-o variabila: reminderul trebuie trimis o singura
        // data, iar simulatorul se uita la masina in fiecare secunda. Daca serverul s-ar reporni,
        // un indicator tinut in memorie s-ar pierde si studentul ar primi reminderul inca o data.
        public static bool ReminderAlreadySent(long userId, long since)
        {
            return NotificationRepository.HasKindSince(userId, "reminder", since);
        }
    }
}
