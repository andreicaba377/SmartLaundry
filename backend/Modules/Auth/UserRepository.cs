using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelele "users" si "sessions"
    public static class UserRepository
    {
        public static Row FindByEmail(string email)
        {
            return Db.One("SELECT * FROM users WHERE lower(email)=@p0", email);
        }

        public static Row FindById(long id)
        {
            return Db.One("SELECT * FROM users WHERE id=@p0", id);
        }

        public static long Insert(string name, string room, long dormId, string email, string passHash, string role)
        {
            return Db.Insert("INSERT INTO users(name, room, dorm_id, created, email, pass_hash, role) VALUES(@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                             name, room, dormId, Util.Now(), email, passHash, role);
        }

        public static List<Row> ListStudentIds()
        {
            return Db.Query("SELECT id FROM users WHERE role='student'");
        }

        public static long Count()
        {
            return Db.Count("SELECT COUNT(*) FROM users");
        }

        // ---------- sesiuni ----------

        public static void InsertSession(string token, long userId, long created, long expires)
        {
            Db.Exec("INSERT INTO sessions(token, user_id, created, expires) VALUES(@p0, @p1, @p2, @p3)", token, userId, created, expires);
        }

        // Utilizatorul care detine o sesiune valida (neexpirata): un singur JOIN intre sesiuni si
        // utilizatori. Se apeleaza la FIECARE cerere, deci trebuie sa fie o singura interogare rapida
        // (token-ul e cheie primara in sessions, deci cautarea foloseste indexul).
        public static Row FindBySession(string token, long now)
        {
            return Db.One("SELECT u.* FROM sessions s JOIN users u ON u.id = s.user_id WHERE s.token=@p0 AND s.expires>@p1", token, now);
        }

        public static void DeleteSession(string token)
        {
            Db.Exec("DELETE FROM sessions WHERE token=@p0", token);
        }

        public static void DeleteExpiredSessions(long now)
        {
            Db.Exec("DELETE FROM sessions WHERE expires < @p0", now);
        }
    }
}
