// scripts/03_seed_users.js
// Step 1 of 2: Insert seed users with plaintext passwords in a temporary field.
// The actual passwordHash is set by 04_hash_passwords.js using pre-computed
// BCrypt $2a$ hashes that are compatible with BCrypt.Net.
//
// Run with mongosh against ems_db (executed automatically via docker-entrypoint-initdb.d):
//   mongosh ems_db scripts/03_seed_users.js
//
// Default credentials (finalised after 04_hash_passwords.js runs):
//   admin@ems.local       / Admin@123
//   dispatcher@ems.local  / Dispatch@123
//   operator@ems.local    / Operator@123
//   driver@ems.local      / Driver@123

db.users.deleteMany({
  email: {
    $in: [
      "admin@ems.local",
      "dispatcher@ems.local",
      "operator@ems.local",
      "driver@ems.local"
    ]
  }
});

db.users.insertMany([
  {
    fullName: "EMS Admin",
    email: "admin@ems.local",
    passwordHash: "",          // populated by 04_hash_passwords.js
    _plainPassword: "Admin@123",
    role: "Admin",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Dispatcher",
    email: "dispatcher@ems.local",
    passwordHash: "",
    _plainPassword: "Dispatch@123",
    role: "Dispatcher",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Operator",
    email: "operator@ems.local",
    passwordHash: "",
    _plainPassword: "Operator@123",
    role: "EmsOperator",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  },
  {
    fullName: "EMS Driver",
    email: "driver@ems.local",
    passwordHash: "",
    _plainPassword: "Driver@123",
    role: "Driver",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  }
]);

print("03: Users inserted (passwordHash pending — run 04_hash_passwords.js next)");
