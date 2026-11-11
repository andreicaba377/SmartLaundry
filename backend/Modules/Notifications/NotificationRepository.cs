using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "notifications"
    public static class NotificationRepository
    {
        public static void Insert(long userId, string kind, string message, long created)
        {
            Db.Exec("INSERT INTO notifications(user_id, kind, message, created) VALUES(@p0, @p1, @p2, @p3)", userId, kind, message, created);
        }

        public static List<Row> Recent(long userId, int limit)
        {
            return Db.Query("SELECT * FROM notifications WHERE user_id=@p0 ORDER BY id DESC LIMIT @p1", userId, limit);
        }

        public static void MarkAllRead(long userId)
        {
            Db.Exec("UPDATE notifications SET is_read=1 WHERE user_id=@p0", userId);
        }

        public static long CountUnread(long userId)
        {
            return Db.Count("SELECT COUNT(*) FROM notifications WHERE user_id=@p0 AND is_read=0", userId);
        }

        // A primit deja studentul un reminder dupa momentul "since"?
        public static bool HasKindSince(long userId, string kind, long since)
        {
            return Db.One("SELECT 1 AS x FROM notifications WHERE user_id=@p0 AND kind=@p1 AND created>@p2", userId, kind, since) != null;
        }
    }
}
