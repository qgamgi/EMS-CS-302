// scripts/03_seed_users.js
// ─────────────────────────────────────────────────────────────────────────────
// Creates default accounts for every role in the EMS system.
//
// Run with mongosh (ships with MongoDB or MongoDB Tools):
//
//   mongosh ems_db scripts/03_seed_users.js
//
// Or if MongoDB is running in Docker:
//
//   docker exec -i ems-mongodb mongosh ems_db < scripts/03_seed_users.js
//
// ─────────────────────────────────────────────────────────────────────────────
//
// BCrypt note: mongosh is plain JavaScript — it has no bcrypt library.
// The password hashes below were generated with BCrypt work-factor 12,
// matching the backend (BCrypt.Net.BCrypt.HashPassword with workFactor: 12).
//
// Default credentials (CHANGE THESE IN PRODUCTION):
//
//   admin@ems.local        password: Admin@123
//   dispatcher@ems.local   password: Dispatch@123
//   operator@ems.local     password: Operator@123
//   driver@ems.local       password: Driver@123
//
// To regenerate hashes with a different password, install the helper:
//   npm install -g bcrypt-cli
//   bcrypt-cli "YourPassword" 12
// ─────────────────────────────────────────────────────────────────────────────

const users = [
  {
    fullName: "EMS Admin",
    email: "admin@ems.local",
    // BCrypt hash of "Admin@123" (work factor 12)
    passwordHash: "$2b$12$S3XCv.kfDefotrWHSnvkduxRXyJ8giO4MouGhZWGl9b0dBPSoGmom",
    role: "Admin",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null,
  },
  {
    fullName: "EMS Dispatcher",
    email: "dispatcher@ems.local",
    // BCrypt hash of "Dispatch@123" (work factor 12)
    passwordHash: "$2b$12$7MKa.DMPa4a7HybIl480S.ai5MvIG2b9yxLHVHmEVQniWi8XZCXmi",
    role: "Dispatcher",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null,
  },
  {
    fullName: "EMS Operator",
    email: "operator@ems.local",
    // BCrypt hash of "Operator@123" (work factor 12)
    passwordHash: "$2b$12$/CqmjBFXriifWm7.ZsIQp.cqE54ozZ9MeWZz19Rru4P.IkfOuCyPy",
    role: "EmsOperator",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null,
  },
  {
    fullName: "EMS Driver",
    email: "driver@ems.local",
    // BCrypt hash of "Driver@123" (work factor 12)
    passwordHash: "$2b$12$oGm.KOZam2FVJwMJRfBgYePHhYkb925vrzGkeAy8CAhTlOWw8OrBq",
    role: "Driver",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null,
  },
];

// ─── Insert (skip if email already exists) ───────────────────────────────────
let inserted = 0;
let skipped = 0;

users.forEach(function (u) {
  const exists = db.users.findOne({ email: u.email });
  if (exists) {
    print("SKIP  " + u.email + " (already exists, role: " + exists.role + ")");
    skipped++;
  } else {
    db.users.insertOne(u);
    print("ADDED " + u.email + " [" + u.role + "]");
    inserted++;
  }
});

print("");
print("Done — " + inserted + " inserted, " + skipped + " skipped.");
print("");
print("Default credentials:");
print("  admin@ems.local       / Admin@123");
print("  dispatcher@ems.local  / Dispatch@123");
print("  operator@ems.local    / Operator@123");
print("  driver@ems.local      / Driver@123");
