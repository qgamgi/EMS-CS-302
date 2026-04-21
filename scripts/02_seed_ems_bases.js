/**
 * MongoDB seed script — EMS bases for Marikina City
 * Data extracted from predict_hospital.py (HospitalPredictor.ems_bases)
 * Run with: mongosh <connection_string> scripts/02_seed_ems_bases.js
 */

const db = connect(process.env.MONGODB_URI || "mongodb://localhost:27017/ems_db");

print("=== EMS DB: Seeding EMS bases ===");

const emsBases = [
  {
    baseId: 163,
    baseName: "163 Base - Barangay Hall IVC",
    barangay: "IVC",
    // GeoJSON Point (longitude first, then latitude — MongoDB convention)
    location: {
      type: "Point",
      coordinates: [121.0797032, 14.6270218]
    },
    isActive: true,
    createdAt: new Date()
  },
  {
    baseId: 166,
    baseName: "166 Base - CHO Office, Barangay Sto. Nino",
    barangay: "Sto. Nino",
    location: {
      type: "Point",
      coordinates: [121.0965973, 14.6399746]
    },
    isActive: true,
    createdAt: new Date()
  },
  {
    baseId: 167,
    baseName: "167 Base - Barangay Hall Kalumpang",
    barangay: "Kalumpang",
    location: {
      type: "Point",
      coordinates: [121.0933239, 14.624179]
    },
    isActive: true,
    createdAt: new Date()
  },
  {
    baseId: 164,
    baseName: "164 Base - DRRMO Building, Barangay Fortune",
    barangay: "Fortune",
    location: {
      type: "Point",
      coordinates: [121.1214235, 14.6628689]
    },
    isActive: true,
    createdAt: new Date()
  },
  {
    baseId: 165,
    baseName: "165 Base - St. Benedict Barangay Nangka",
    barangay: "Nangka",
    location: {
      type: "Point",
      coordinates: [121.108795, 14.6737274]
    },
    isActive: true,
    createdAt: new Date()
  },
  {
    baseId: 169,
    baseName: "169 Base - Pugad Lawin, Barangay Fortune",
    barangay: "Fortune",
    location: {
      type: "Point",
      coordinates: [121.1312048, 14.6584306]
    },
    isActive: true,
    createdAt: new Date()
  }
];

// Upsert so the script is idempotent
let inserted = 0;
let updated = 0;

emsBases.forEach(base => {
  const result = db.ems_bases.updateOne(
    { baseId: base.baseId },
    { $setOnInsert: base },
    { upsert: true }
  );
  if (result.upsertedCount > 0) {
    inserted++;
    print(`  Inserted: ${base.baseName}`);
  } else {
    updated++;
    print(`  Already exists (skipped): ${base.baseName}`);
  }
});

print(`=== Seed complete: ${inserted} inserted, ${updated} already existed ===`);

// ─── Seed default Admin user ──────────────────────────────────────────────────
// Password: Admin@1234  (BCrypt hash — regenerate in production)
const adminEmail = "admin@ems.marikina.gov.ph";
const existingAdmin = db.users.findOne({ email: adminEmail });

if (!existingAdmin) {
  db.users.insertOne({
    fullName: "System Administrator",
    email: adminEmail,
    // BCrypt hash of "Admin@1234" with salt rounds = 12
    passwordHash: "$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewfcQJeW1dXDj5bm",
    role: "Admin",
    isActive: true,
    createdAt: new Date(),
    lastSeenAt: null
  });
  print("Default admin user created: " + adminEmail + "  (password: Admin@1234 — change immediately)");
} else {
  print("Admin user already exists — skipped.");
}
