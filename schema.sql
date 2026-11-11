-- Smart Laundry Campus - schema bazei de date (SQLite, fisier: laundry.db)
-- Se creeaza automat la prima pornire a serverului (server.mjs). Fisierul e doar documentatie / referinta.

CREATE TABLE dorms(id INTEGER PRIMARY KEY, name TEXT, address TEXT);

CREATE TABLE users(
  id INTEGER PRIMARY KEY, name TEXT, room TEXT, dorm_id INTEGER REFERENCES dorms(id),
  email TEXT,                 -- unic (case-insensitive)
  pass_hash TEXT,             -- scrypt: salt:hash
  role TEXT DEFAULT 'student',-- student | admin
  created INTEGER
);
CREATE UNIQUE INDEX ux_users_email ON users(lower(email));

CREATE TABLE sessions(token TEXT PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id), created INTEGER, expires INTEGER);

CREATE TABLE machines(
  id INTEGER PRIMARY KEY, dorm_id INTEGER REFERENCES dorms(id), name TEXT, type TEXT,  -- washer | dryer
  status TEXT DEFAULT 'free',  -- free | running | finished | blocked | reserved | out_of_order
  program TEXT, started_at INTEGER, ends_at INTEGER, finished_at INTEGER,
  user_id INTEGER, hold_user_id INTEGER, hold_until INTEGER,
  power_w REAL, water_l REAL, rpm INTEGER, temp_c REAL, phase TEXT, door TEXT, vibration REAL,  -- telemetrie curenta
  last_seen INTEGER, api_key TEXT,                                                              -- cheie dispozitiv IoT
  cycles INTEGER DEFAULT 0, kwh_total REAL DEFAULT 0, water_total REAL DEFAULT 0, enabled INTEGER DEFAULT 1
);

CREATE TABLE reservations(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, created INTEGER, status TEXT); -- active | queued | done | expired | cancelled
CREATE TABLE usage(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, program TEXT, started_at INTEGER, ended_at INTEGER, kwh REAL, water REAL, cost REAL);
CREATE TABLE defects(id INTEGER PRIMARY KEY, machine_id INTEGER, user_id INTEGER, description TEXT, severity TEXT, status TEXT DEFAULT 'open', created INTEGER, resolved INTEGER, source TEXT DEFAULT 'user'); -- source: user | sensor
CREATE TABLE notifications(id INTEGER PRIMARY KEY, user_id INTEGER, kind TEXT, message TEXT, created INTEGER, is_read INTEGER DEFAULT 0);
CREATE TABLE telemetry(id INTEGER PRIMARY KEY, machine_id INTEGER, ts INTEGER, power_w REAL, vibration REAL);  -- pastrat 2h
CREATE INDEX ix_tel ON telemetry(machine_id, ts);
CREATE TABLE settings(k TEXT PRIMARY KEY, v TEXT);
