const http = require('http');

const data = JSON.stringify({
    language: "python",
    code: `import os, sys
import pandas as pd
from typing import List, Dict
from collections import defaultdict as dd

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
        console.log('=== PYTHON IMPORT TEST RESULTS ===');
        console.log('Imports found:', result.imports?.length || 0);
        console.log('Functions found:', result.functions?.length || 0);
        console.log('\nImports:');
        (result.imports || []).forEach(imp => console.log('  -', imp.module));
        console.log('\nFunctions:');
        (result.functions || []).forEach(fn => console.log('  -', fn.name));
    });
});

req.on('error', (e) => console.error('Error:', e.message));
req.write(data);
req.end();
