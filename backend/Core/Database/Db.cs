using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace SmartLaundry
{
    // Acces la baza de date SQLite. Parametrii in SQL se numesc @p0, @p1, @p2 ...
    // Aceasta clasa nu stie nimic despre module: doar executa SQL.
    //
    // DE CE asa:
    //  - Folosim o singura conexiune, partajata de toate thread-urile. SQLite e o baza "de fisier",
    //    iar pentru o aplicatie de camin (zeci de utilizatori) e mai simplu si suficient de rapid.
    //  - Nu folosim un ORM (Entity Framework): ar fi adus mult cod "magic". Cu SQL scris de mana
    //    se vede exact ce se intampla, iar Row (un dictionar) e usor de inteles.
    //  - Toate valorile intra prin parametri (@p0...), niciodata lipite direct in textul SQL:
    //    asa se evita atacurile de tip SQL injection.
    public static class Db
    {
        // O conexiune SQLite NU poate fi folosita de doua thread-uri simultan. Serverul porneste un
        // thread pentru fiecare cerere, deci toata munca cu baza de date se face in lock(Db.Lock).
        // Cererile ajung una dupa alta la baza de date, dar sunt foarte scurte, deci nu se simte.
        public static object Lock = new object();
        static SqliteConnection conn;

        public static void Open(string path)
        {
            conn = new SqliteConnection("Data Source=" + path);
            conn.Open();
            // WAL = scrierile merg intr-un fisier separat si se contopesc mai tarziu: mai rapid si
            // mai sigur daca se opreste curentul in timpul unei scrieri.
            Exec("PRAGMA journal_mode=WAL;");
        }

        static SqliteCommand MakeCommand(string sql, object[] args)
        {
            SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            // Argumentele primite (params object[]) devin parametrii @p0, @p1, ... in ordinea data.
            // Un argument null trebuie trimis ca DBNull, altfel SQLite refuza sa-l salveze.
            for (int i = 0; i < args.Length; i++)
            {
                object a = args[i];
                if (a == null)
                {
                    a = DBNull.Value;
                }
                cmd.Parameters.AddWithValue("@p" + i, a);
            }
            return cmd;
        }

        public static void Exec(string sql, params object[] args)
        {
            using (SqliteCommand cmd = MakeCommand(sql, args))
            {
                cmd.ExecuteNonQuery();
            }
        }

        // Ruleaza un SELECT si intoarce toate liniile. Fiecare linie e un Row (nume coloana -> valoare).
        // Citim totul in memorie si inchidem cititorul imediat: tabelele sunt mici, iar asa nu ramane
        // niciun cursor deschis cat timp alt cod foloseste aceeasi conexiune.
        public static List<Row> Query(string sql, params object[] args)
        {
            List<Row> rows = new List<Row>();
            using (SqliteCommand cmd = MakeCommand(sql, args))
            {
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    for (; ; )
                    {
                        if (!reader.Read())
                        {
                            break;
                        }
                        Row r = new Row();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string name = reader.GetName(i);
                            object value = null;
                            if (!reader.IsDBNull(i))
                            {
                                value = reader.GetValue(i);
                            }
                            r.Keys.Add(name);
                            r.V[name] = value;
                        }
                        rows.Add(r);
                    }
                }
            }
            return rows;
        }

        // Prima linie sau null daca nu exista niciuna
        public static Row One(string sql, params object[] args)
        {
            List<Row> rows = Query(sql, args);
            if (rows.Count == 0)
            {
                return null;
            }
            return rows[0];
        }

        // Pentru interogari de tip SELECT COUNT(*) ...
        public static long Count(string sql, params object[] args)
        {
            using (SqliteCommand cmd = MakeCommand(sql, args))
            {
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value)
                {
                    return 0;
                }
                return Convert.ToInt64(v);
            }
        }

        // INSERT si intoarce id-ul noii linii
        // SQLite nu intoarce id-ul direct dupa INSERT, asa ca il cerem imediat cu last_insert_rowid().
        // E sigur pentru ca toata munca cu baza de date e sub lock: nimeni nu poate insera intre timp.
        public static long Insert(string sql, params object[] args)
        {
            Exec(sql, args);
            return Count("SELECT last_insert_rowid()");
        }

        // Tranzactie: mai multe INSERT-uri se salveaza mult mai repede impreuna
        public static void BeginTransaction()
        {
            Exec("BEGIN");
        }

        public static void Commit()
        {
            Exec("COMMIT");
        }
    }
}
