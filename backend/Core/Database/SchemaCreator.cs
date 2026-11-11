namespace SmartLaundry
{
    // Creeaza tabelele daca nu exista. Fiecare tabel apartine unui singur modul (vezi comentariile).
    //
    // DE CE "IF NOT EXISTS": metoda ruleaza la FIECARE pornire. Prima data creeaza tabelele,
    // dupa aceea nu face nimic si datele raman. Asa nu avem nevoie de un pas separat de instalare.
    //
    // Timpul (created, started_at, ...) se salveaza ca numar de milisecunde (INTEGER), nu ca text:
    // se compara si se aduna usor, iar JavaScript il intelege direct (Date.now()).
    public static class SchemaCreator
    {
        public static void Create()
        {
            Db.Exec(@"
-- Admin
CREATE TABLE IF NOT EXISTS settings(k TEXT PRIMARY KEY, v TEXT);
-- Dorms
CREATE TABLE IF NOT EXISTS dorms(id INTEGER PRIMARY KEY, name TEXT, address TEXT);
-- Machines
-- status: free | running | finished | blocked | reserved | out_of_order
-- user_id = cine foloseste masina; hold_user_id/hold_until = cine a rezervat-o si pana cand.
-- enabled=0 inseamna ""stearsa"": nu stergem randul ca sa nu pierdem istoricul care il refera.
CREATE TABLE IF NOT EXISTS machines(
  id INTEGER PRIMARY KEY, dorm_id INTEGER, name TEXT, type TEXT,
  status TEXT DEFAULT 'free', program TEXT, started_at INTEGER, ends_at INTEGER, finished_at INTEGER,
  user_id INTEGER, hold_user_id INTEGER, hold_until INTEGER,
  power_w REAL DEFAULT 0, water_l REAL DEFAULT 0, rpm INTEGER DEFAULT 0, temp_c REAL DEFAULT 0, phase TEXT,
  door TEXT DEFAULT 'closed', vibration REAL DEFAULT 0, last_seen INTEGER, api_key TEXT,
  cycles INTEGER DEFAULT 0, kwh_total REAL DEFAULT 0, water_total REAL DEFAULT 0, enabled INTEGER DEFAULT 1);
-- Auth
CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY, name TEXT, room TEXT, dorm_id INTEGER, created INTEGER,
  email TEXT, pass_hash TEXT, role TEXT DEFAULT 'student');
CREATE TABLE IF NOT EXISTS sessions(token TEXT PRIMARY KEY, user_id INTEGER NOT NULL, created INTEGER, expires INTEGER);
-- Reservations
CREATE TABLE IF NOT EXISTS reservations(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, created INTEGER, status TEXT);
-- Usage
CREATE TABLE IF NOT EXISTS usage(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, program TEXT, started_at INTEGER,
  ended_at INTEGER, kwh REAL, water REAL, cost REAL);
-- Defects
CREATE TABLE IF NOT EXISTS defects(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, description TEXT, severity TEXT,
  status TEXT DEFAULT 'open', created INTEGER, resolved INTEGER, source TEXT DEFAULT 'user');
-- Notifications
CREATE TABLE IF NOT EXISTS notifications(id INTEGER PRIMARY KEY, user_id INTEGER, kind TEXT, message TEXT, created INTEGER, is_read INTEGER DEFAULT 0);
-- Telemetry
CREATE TABLE IF NOT EXISTS telemetry(id INTEGER PRIMARY KEY, machine_id INTEGER, ts INTEGER, power_w REAL, vibration REAL);
CREATE INDEX IF NOT EXISTS ix_tel ON telemetry(machine_id, ts);
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users(lower(email));
");
        }
    }
}
