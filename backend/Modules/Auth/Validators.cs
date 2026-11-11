namespace SmartLaundry
{
    // Verificari simple pentru datele introduse la inregistrare
    public static class Validators
    {
        public static bool IsValidName(string name)
        {
            return name != null && name.Length >= 3;
        }

        public static bool IsValidPassword(string password)
        {
            return password != null && password.Length >= 6;
        }

        // Verificare simpla de email: ceva@ceva.ceva, fara spatii
        public static bool IsValidEmail(string email)
        {
            if (email == null || email.Length < 5)
            {
                return false;
            }
            int at = email.IndexOf('@');
            if (at < 1)
            {
                return false;
            }
            if (email.IndexOf('@', at + 1) != -1)
            {
                return false;
            }
            for (int i = 0; i < email.Length; i++)
            {
                if (email[i] == ' ')
                {
                    return false;
                }
            }
            int dot = email.LastIndexOf('.');
            if (dot <= at + 1)
            {
                return false;
            }
            if (dot >= email.Length - 1)
            {
                return false;
            }
            return true;
        }
    }
}
