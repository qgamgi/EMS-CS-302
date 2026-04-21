// 05_create_sessions_index.js
// Creates the sessions collection with:
//  - TTL index on expiresAt (auto-delete expired sessions after 24h)
//  - Unique index on jti (token ID) for fast lookup on logout
//  - Index on userId for querying sessions per user

const db = db.getSiblingDB('ems_db');

db.createCollection('sessions');

// TTL index — MongoDB automatically deletes documents when expiresAt is reached
db.sessions.createIndex(
  { expiresAt: 1 },
  { expireAfterSeconds: 0, name: 'ttl_expiresAt' }
);

// Unique index on jti for O(1) session lookup on logout
db.sessions.createIndex(
  { jti: 1 },
  { unique: true, name: 'unique_jti' }
);

// Index on userId to quickly fetch all sessions for a given user
db.sessions.createIndex(
  { userId: 1 },
  { name: 'idx_userId' }
);

// Compound index for listing active sessions efficiently
db.sessions.createIndex(
  { isActive: 1, loginAt: -1 },
  { name: 'idx_active_loginAt' }
);

print('05: sessions collection and indexes created.');
