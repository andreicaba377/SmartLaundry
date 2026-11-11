using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SmartLaundry
{
    public class FailInfo
    {
        public int N;
        public long T;
    }

    // Conturi, parole, sesiuni. Singurul loc care citeste token-ul din cerere.
    public static class AuthService
    {
        // Retine cate incercari esuate a facut fiecare adresa IP. E in memorie (nu in baza de date):
        // e o protectie temporara, iar repornirea serverului o reseteaza fara probleme. Nu are nevoie
        // de lock propriu pentru ca toate apelurile vin deja din lock(Db.Lock).
        static Dictionary<string, FailInfo> fails = new Dictionary<string, FailInfo>();

        // ---------- parole ----------

        // NICIODATA nu salvam parola ca text. Salvam un "hash": o transformare care nu se poate inversa.
        // La login hash-uim parola introdusa si comparam hash-urile.
        //  - PBKDF2 cu 100.000 de iteratii: intentionat LENT (cateva zeci de ms). Pentru un utilizator
        //    nu conteaza, dar un atacator care a furat baza de date ar incerca parole foarte greu.
        //  - Sare (salt) aleatoare de 16 octeti, diferita pentru fiecare cont: doua conturi cu aceeasi
        //    parola au hash-uri diferite, iar tabelele de hash-uri pregatite dinainte nu merg.
        //  - Salvam "sare:hash" intr-o singura coloana, ca sa avem tot ce ne trebuie la verificare.
        // Am ales PBKDF2 (exista direct in .NET), nu BCrypt/Argon2 (ar fi cerut un pachet NuGet in plus).
        static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            return Convert.ToHexString(salt).ToLower() + ":" + Convert.ToHexString(hash).ToLower();
        }

        static bool CheckPassword(string password, string stored)
        {
            if (stored == null)
            {
                return false;
            }
            string[] parts = stored.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }
            byte[] salt = Convert.FromHexString(parts[0]);
            byte[] expected = Convert.FromHexString(parts[1]);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100000, HashAlgorithmName.SHA256, 32);
            // FixedTimeEquals dureaza la fel indiferent unde difera hash-urile. Un "==" obisnuit se opreste
            // la prima diferenta, iar masurand timpul un atacator ar putea ghici hash-ul octet cu octet.
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }

        // ---------- sesiuni ----------

        // Creeaza o sesiune valabila 7 zile si intoarce token-ul.
        // DE CE sesiuni in baza de date si nu "token semnat" (JWT): putem anula o sesiune oricand
        // (logout = DELETE), iar sesiunile supravietuiesc repornirii serverului. Token-ul e un sir
        // aleator de 48 de caractere hex: nu contine nimic despre utilizator, doar il cautam in tabel.
        static string NewSession(long userId)
        {
            string token = Util.RandomHex(24);
            long now = Util.Now();
            UserRepository.InsertSession(token, userId, now, now + 7L * 86400000L);
            return token;
        }

        // Token-ul vine in antetul "Authorization: Bearer ..." (apelurile obisnuite) sau in ?token=...
        // Varianta cu ?token= exista pentru fluxul live (EventSource din browser), care NU permite
        // sa-i punem antete proprii.
        public static string TokenFromRequest(HttpListenerContext ctx)
        {
            string h = ctx.Request.Headers["Authorization"];
            if (h != null)
            {
                if (h.StartsWith("Bearer "))
                {
                    return h.Substring(7);
                }
            }
            return ctx.Request.QueryString["token"];
        }

        // Utilizatorul logat (linia din tabelul users) sau null
        public static Row CurrentUser(HttpListenerContext ctx)
        {
            string token = TokenFromRequest(ctx);
            if (token == null || token == "")
            {
                return null;
            }
            return UserRepository.FindBySession(token, Util.Now());
        }

        public static void Logout(HttpListenerContext ctx)
        {
            string token = TokenFromRequest(ctx);
            if (token != null)
            {
                UserRepository.DeleteSession(token);
            }
        }

        public static void DeleteExpiredSessions()
        {
            UserRepository.DeleteExpiredSessions(Util.Now());
        }

        // ---------- conturi ----------

        // Din "Ștefan Țurcanu" face "stefan.turcanu" (folosit pentru emailurile conturilor demo).
        // Normalize(FormD) desparte litera de diacritica ("ș" devine "s" + semnul de dedesubt);
        // apoi sarim peste semnele (categoria NonSpacingMark) si ramanem cu litera simpla.
        public static string Slug(string name)
        {
            string normalized = name.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                if (c == ' ')
                {
                    sb.Append('.');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().ToLower();
        }

        // Creeaza un cont fara validari (folosit si de Seed). Intoarce id-ul.
        // Parola se hash-uieste AICI: niciun alt modul nu vede si nu trebuie sa stie cum se protejeaza.
        public static long CreateUser(string name, string room, long dormId, string email, string password, string role)
        {
            return UserRepository.Insert(name, room, dormId, email, HashPassword(password), role);
        }

        // Inregistrare student nou. La succes: Id = id-ul, Text = token, Row = utilizatorul.
        public static Result Register(string nameIn, string emailIn, string passwordIn, string roomIn, long dormId)
        {
            string name = Util.Trim(nameIn);
            string email = Util.Trim(emailIn).ToLower();
            string room = Util.Trim(roomIn);
            string password = passwordIn;
            if (password == null)
            {
                password = "";
            }

            // Validarile se fac in ordinea in care utilizatorul completeaza formularul, iar prima care
            // pica opreste totul cu un mesaj clar. Emailul il comparam cu litere mici ca
            // "Ana@x.ro" si "ana@x.ro" sa fie acelasi cont.
            if (!Validators.IsValidName(name)) { return Result.Fail(400, "Introdu numele complet (minim 3 caractere)"); }
            if (!Validators.IsValidEmail(email)) { return Result.Fail(400, "Adresă de email invalidă"); }
            if (!Validators.IsValidPassword(password)) { return Result.Fail(400, "Parola trebuie să aibă minim 6 caractere"); }
            if (room == "") { return Result.Fail(400, "Introdu numărul camerei"); }
            if (!DormRepository.Exists(dormId)) { return Result.Fail(400, "Alege căminul"); }
            if (UserRepository.FindByEmail(email) != null) { return Result.Fail(409, "Există deja un cont cu acest email"); }

            long id = CreateUser(name, room, dormId, email, password, "student");
            Result ok = Result.Success();
            ok.Id = id;
            ok.Text = NewSession(id);
            ok.Row = UserRepository.FindById(id);
            return ok;
        }

        // Autentificare, cu limitare: maximum 8 greseli in 5 minute de la aceeasi adresa IP.
        // Limitarea opreste atacurile "brute force" (cineva care incearca mii de parole). Dupa 8 greseli
        // refuzam chiar si parola corecta pana trec 5 minute.
        public static Result Login(string ip, string emailIn, string passwordIn)
        {
            long now = Util.Now();
            FailInfo f = null;
            if (fails.ContainsKey(ip))
            {
                f = fails[ip];
            }
            if (f != null && f.N >= 8 && now - f.T < 5 * 60000)
            {
                return Result.Fail(429, "Prea multe încercări. Reîncearcă în câteva minute.");
            }

            string email = Util.Trim(emailIn).ToLower();
            string password = passwordIn;
            if (password == null)
            {
                password = "";
            }
            Row u = UserRepository.FindByEmail(email);
            bool ok = false;
            if (u != null)
            {
                ok = CheckPassword(password, u.Str("pass_hash"));
            }
            if (!ok)
            {
                FailInfo nf = new FailInfo();
                nf.N = 1;
                nf.T = now;
                if (f != null && now - f.T < 5 * 60000)
                {
                    nf.N = f.N + 1;
                }
                fails[ip] = nf;
                // Acelasi mesaj si cand emailul nu exista, si cand parola e gresita: raspunsul nu trebuie
                // sa-i spuna unui atacator care emailuri au cont in sistem.
                return Result.Fail(401, "Email sau parolă greșită");
            }

            fails.Remove(ip);
            Result good = Result.Success();
            good.Id = u.Long("id");
            good.Text = NewSession(u.Long("id"));
            good.Row = u;
            return good;
        }

        public static long CountUsers()
        {
            return UserRepository.Count();
        }

        public static List<Row> ListStudentIds()
        {
            return UserRepository.ListStudentIds();
        }
    }
}
