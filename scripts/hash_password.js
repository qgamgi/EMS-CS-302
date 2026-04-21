// scripts/hash_password.js
// ─────────────────────────────────────────────────────────────────────────────
// Helper — generate a BCrypt hash (work factor 12) for any password.
// Use the output to update hardcoded hashes in 03_seed_users.js.
//
// Usage (Git Bash / any terminal):
//
//   node scripts/hash_password.js "YourPassword"
//
// Requirements: Node.js must be installed.
//   npm install bcryptjs   (one-time, from repo root)
// ─────────────────────────────────────────────────────────────────────────────

const bcrypt = require("bcryptjs");

const password = process.argv[2];

if (!password) {
  console.error("Usage: node scripts/hash_password.js \"YourPassword\"");
  process.exit(1);
}

const hash = bcrypt.hashSync(password, 12);
console.log("\nPassword : " + password);
console.log("BCrypt   : " + hash);
console.log("\nPaste the hash into 03_seed_users.js as the passwordHash value.");
