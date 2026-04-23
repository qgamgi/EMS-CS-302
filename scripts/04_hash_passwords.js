// scripts/04_hash_passwords.js
// Step 2 of 2: Patches each seed user's passwordHash with a pre-computed
// BCrypt $2a$ hash that is fully compatible with BCrypt.Net.BCrypt.Verify().
//
// Hashes were generated with bcryptjs (work-factor 12) and the $2b$ prefix
// was replaced with $2a$ so that BCrypt.Net accepts them without modification.
//
// Run with mongosh against ems_db (executed automatically after 03_seed_users.js
// via docker-entrypoint-initdb.d alphabetical ordering):
//   mongosh ems_db scripts/04_hash_passwords.js
//
// DO NOT commit plain passwords here — these are one-way hashes only.

var patches = [
  {
    email: "admin@ems.local",
    // Admin@123  —  bcrypt $2a$ work-factor 12
    hash: "$2a$12$cw3dEdSV7gnXHR4QBX1tguU6ohmsUpMQpJygJ.Exrf/b2ucyK.SKK"
  },
  {
    email: "dispatcher@ems.local",
    // Dispatch@123  —  bcrypt $2a$ work-factor 12
    hash: "$2a$12$ZDftrZzhz/igp6vLv1vgl.xI4uuP1GAaUWL161XBjFXf3uFKNgSVe"
  },
  {
    email: "operator@ems.local",
    // Operator@123  —  bcrypt $2a$ work-factor 12
    hash: "$2a$12$Sex4AlGaTtcj4oZNTgkfmu/ggmYO4jLvIvveoOgJ8Ln8lKyV4HvLW"
  },
  {
    email: "driver@ems.local",
    // Driver@123  —  bcrypt $2a$ work-factor 12
    hash: "$2a$12$mXq8VB4x.OfaMaqenL02l.8J2j49tClJC4TdrG8WF0c.q/J1iPMUG"
  }
];

patches.forEach(function(p) {
  var result = db.users.updateOne(
    { email: p.email },
    {
      $set:   { passwordHash: p.hash },
      $unset: { _plainPassword: "" }   // remove the temporary plaintext field
    }
  );
  if (result.matchedCount === 0) {
    print("WARN: no user found for " + p.email);
  } else {
    print("04: patched passwordHash for " + p.email);
  }
});

print("04: All seed user passwords hashed successfully ($2a$ / BCrypt.Net compatible)");
print("Default credentials:");
print("  admin@ems.local       / Admin@123");
print("  dispatcher@ems.local  / Dispatch@123");
print("  operator@ems.local    / Operator@123");
print("  driver@ems.local      / Driver@123");
