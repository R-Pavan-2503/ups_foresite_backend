const http = require('http');

const data = JSON.stringify({
    language: "python",
    code: `import models as models, crud
from database import get_db, engine
import pinecone_utils
from pinecone_utils import get_courses_for_skill

def main():
    pass`
});

const options = {
    hostname: 'localhost',
    port: 3002,
    path: '/parse',
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Content-Length': data.length
    }
};

const req = http.request(options, (res) => {
    let body = '';
    res.on('data', (chunk) => body += chunk);
    res.on('end', () => {
        const result = JSON.parse(body);
        console.log('=== MAIN.PY IMPORT TEST ===');
        console.log('Imports found:', result.imports?.length || 0);
        console.log('\nExpected: models, crud, database, pinecone_utils, pinecone_utils');
        console.log('\nActual imports:');
        (result.imports || []).forEach((imp, i) => console.log(`  ${i + 1}. ${imp.module}`));
    });
});

req.on('error', (e) => console.error('Error:', e.message));
req.write(data);
req.end();
