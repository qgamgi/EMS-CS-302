// scripts/03_seed_users.js
// Creates default accounts for every role in the EMS system.
//
// Run with mongosh:
//   mongosh ems_db < scripts/03_seed_users.js
//
// Password hashes were generated with BCrypt work-factor 12.
// Default credentials:
//   admin@ems.local       / Admin@123
//   dispatcher@ems.local  / Dispatch@123
//   operator@ems.local    / Operator@123
//   driver@ems.local      / Driver@123

db.users.deleteMany({ email: { $in: ["admin@ems.local", "dispatcher@ems.local", "operator@ems.local", "driver@ems.local"] } });

db.users.insertMany([
  {
    fullName: "EMS Admin",
    email: "admin@ems.local",
    passwordHash: "$2b$12$S3XCv.kfDefotrWHSnvkduxRXyJ8giO4MouGhZWGl9b0dBPSoGmom",
    role: "Admin",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Dispatcher",
    email: "dispatcher@ems.local",
    passwordHash: "$2b$12$7MKa.DMPa4a7HybIl480S.ai5MvIG2b9yxLHVHmEVQniWi8XZCXmi",
    role: "Dispatcher",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Operator",
    email: "operator@ems.local",
    passwordHash: "$2b$12$/CqmjBFXriifWm7.ZsIQp.cqE54ozZ9MeWZz19Rru4P.IkfOuCyPy",
    role: "EmsOperator",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Driver",
    email: "driver@ems.local",
    passwordHash: "$2b$12$oGm.KOZam2FVJwMJRfBgYePHhYkb925vrzGkeAy8CAhTlOWw8OrBq",
    role: "Driver",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  }
]);

print("✓ Users seeded successfully");
print("Default credentials:");
print("  admin@ems.local       / Admin@123");
print("  dispatcher@ems.local  / Dispatch@123");
print("  operator@ems.local    / Operator@123");
print("  driver@ems.local      / Driver@123");
