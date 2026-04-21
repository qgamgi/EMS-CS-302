/**
 * MongoDB index creation script for EMS-CS-302
 * Run with: mongosh <connection_string> scripts/01_create_indexes.js
 */

const db = connect(process.env.MONGODB_URI || "mongodb://localhost:27017/ems_db");

print("=== EMS DB: Creating indexes ===");

// ─── users ───────────────────────────────────────────────────────────────────
db.users.createIndex({ email: 1 }, { unique: true, name: "users_email_unique" });
db.users.createIndex({ role: 1 }, { name: "users_role" });
db.users.createIndex({ isActive: 1 }, { name: "users_is_active" });
print("users: indexes created");

// ─── dispatches ──────────────────────────────────────────────────────────────
db.dispatches.createIndex({ status: 1 }, { name: "dispatches_status" });
db.dispatches.createIndex({ assignedDriverId: 1 }, { name: "dispatches_assigned_driver" });
db.dispatches.createIndex({ createdAt: -1 }, { name: "dispatches_created_at_desc" });
db.dispatches.createIndex(
  { status: 1, createdAt: -1 },
  { name: "dispatches_status_created_compound" }
);
// Geo index for patient location queries
db.dispatches.createIndex(
  { "location.coordinates": "2dsphere" },
  { name: "dispatches_location_geo" }
);
print("dispatches: indexes created");

// ─── drivers ─────────────────────────────────────────────────────────────────
db.drivers.createIndex({ userId: 1 }, { unique: true, name: "drivers_user_unique" });
db.drivers.createIndex({ status: 1 }, { name: "drivers_status" });
db.drivers.createIndex({ activeDispatchId: 1 }, { name: "drivers_active_dispatch" });
db.drivers.createIndex(
  { "currentLocation.coordinates": "2dsphere" },
  { name: "drivers_location_geo" }
);
print("drivers: indexes created");

// ─── ems_bases ────────────────────────────────────────────────────────────────
db.ems_bases.createIndex({ baseId: 1 }, { unique: true, name: "ems_bases_base_id_unique" });
db.ems_bases.createIndex(
  { "location.coordinates": "2dsphere" },
  { name: "ems_bases_location_geo" }
);
print("ems_bases: indexes created");

// ─── sessions ────────────────────────────────────────────────────────────────
db.sessions.createIndex({ userId: 1 }, { name: "sessions_user" });
db.sessions.createIndex(
  { expiresAt: 1 },
  { expireAfterSeconds: 0, name: "sessions_ttl" } // MongoDB TTL auto-cleanup
);
print("sessions: indexes created");

print("=== All indexes created successfully ===");
