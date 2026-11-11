using System.Collections.Generic;

namespace SmartLaundry
{
    // Singurul loc cu SQL pentru tabelul "dorms"
    public static class DormRepository
    {
        public static List<Row> ListAll()
        {
            return Db.Query("SELECT * FROM dorms ORDER BY id");
        }

        public static bool Exists(long id)
        {
            return Db.One("SELECT id FROM dorms WHERE id=@p0", id) != null;
        }

        // Numele unui camin ("" daca nu exista)
        public static string NameOf(long id)
        {
            Row r = Db.One("SELECT name FROM dorms WHERE id=@p0", id);
            if (r == null)
            {
                return "";
            }
            return r.Str("name");
        }

        public static long Count()
        {
            return Db.Count("SELECT COUNT(*) FROM dorms");
        }

        public static long Add(string name, string address)
        {
            return Db.Insert("INSERT INTO dorms(name, address) VALUES(@p0, @p1)", name, address);
        }
    }
}
