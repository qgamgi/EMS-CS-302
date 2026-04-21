// 05_create_sessions_index.js
// Adds supplementary indexes to the sessions collection.
// NOTE: The sessions TTL index (sessions_ttl) and userId index (sessions_user)
// are already created by 01_create_indexes.js — do NOT duplicate them here.

const db = db.getSiblingDB('ems_db');

// Ensure the collection exists (idempotent)
if (!db.getCollectionNames().includes('sessions')) {
  db.createCollection('sessions');
}

// Unique index on jti for O(1) session lookup on logout
// Use createIndex with a try/catch to be idempotent on re-runs
try {
  db.sessions.createIndex(
    { jti: 1 },
    { unique: true, name: 'unique_jti' }
  );
} catch (e) {
  print('05: unique_jti index already exists, skipping.');
}

// Compound index for listing active sessions efficiently
try {
  db.sessions.createIndex(
    { isActive: 1, loginAt: -1 },
    { name: 'idx_active_loginAt' }
  );
} catch (e) {
  print('05: idx_active_loginAt index already exists, skipping.');
}

print('05: sessions supplementary indexes ensured.');
