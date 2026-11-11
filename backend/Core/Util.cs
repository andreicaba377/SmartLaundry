using System;
using System.Security.Cryptography;

namespace SmartLaundry
{
    // Functii ajutatoare folosite in tot programul (fara reguli de business)
    public static class Util
    {
        // Un singur generator de numere aleatoare pentru tot programul. Nu e thread-safe, dar il folosim
        // doar in cod care ruleaza sub lock(Db.Lock), deci nu e niciodata apelat de doua thread-uri odata.
        public static Random Rnd = new Random();

        // Timpul curent in milisecunde (ca Date.now() din JavaScript). Folosim UTC si milisecunde peste
        // tot, ca frontend-ul sa poata compara direct cu Date.now() fara conversii de fus orar.
        public static long Now()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // Sir aleator in hexazecimal, pentru token-uri de sesiune si chei de dispozitiv. Folosim
        // RandomNumberGenerator (aleator criptografic), NU Random: token-urile trebuie sa nu poata fi ghicite.
        public static string RandomHex(int bytes)
        {
            byte[] b = RandomNumberGenerator.GetBytes(bytes);
            return Convert.ToHexString(b).ToLower();
        }

        // Numar real aleator intre a si b
        public static double Between(double a, double b)
        {
            return a + Rnd.NextDouble() * (b - a);
        }

        public static string Trim(string s)
        {
            if (s == null)
            {
                return "";
            }
            return s.Trim();
        }
    }
}
