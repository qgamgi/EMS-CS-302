const bcrypt = require('bcrypt');

// Generate BCrypt hashes with work factor 12 (matching backend)
const passwords = {
  'admin@ems.local': 'Admin@123',
  'dispatcher@ems.local': 'Dispatch@123',
  'operator@ems.local': 'Operator@123',
  'driver@ems.local': 'Driver@123',
};

async function generateHashes() {
  console.log('Generating BCrypt hashes (work factor 12)...\n');
  
  for (const [email, password] of Object.entries(passwords)) {
    const hash = await bcrypt.hash(password, 12);
    console.log(`${email}: ${password}`);
    console.log(`  Hash: ${hash}\n`);
  }
}

generateHashes().catch(console.error);
