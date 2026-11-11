namespace SmartLaundry
{
    // Cine are voie sa apeleze o ruta. Verificarea se face o singura data, in Router.
    public enum AccessLevel
    {
        Public,    // oricine
        Logged,    // student sau administrator
        Student,   // doar student
        Admin      // doar administrator
    }
}
