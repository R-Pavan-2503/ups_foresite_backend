import express, { Request, Response } from 'express';
import bodyParser from 'body-parser';
import { parseCode } from './parser/treeSitterAdapter';

const app = express();
const PORT = 3002;

app.use(bodyParser.json({ limit: '10mb' }));

app.post('/parse', async (req: Request, res: Response) => {
    try {
        const { code, language } = req.body;

        // Validate that code and language exist
        if (!code || !language) {
            return res.status(400).json({ error: 'Missing code or language' });
        }

        // Validate that code is a string
        if (typeof code !== 'string') {
            console.error(`❌ Invalid code type: ${typeof code}, value:`, code);
            return res.status(400).json({
                error: 'Code must be a string',
                receivedType: typeof code
            });
        }

        // Validate that language is a string
        if (typeof language !== 'string') {
            console.error(`❌ Invalid language type: ${typeof language}, value:`, language);
            return res.status(400).json({
                error: 'Language must be a string',
                receivedType: typeof language
            });
        }

        // Check if code is empty or just whitespace
        if (code.trim().length === 0) {
            console.warn(`⚠️ Empty code received for language: ${language}`);
            return res.json({ functions: [], imports: [] }); // Return empty result
        }

        // Log request for debugging (first 100 chars of code)
        console.log(`📥 Parsing ${language} code (${code.length} chars): ${code.substring(0, 100)}...`);

        const result = await parseCode(code, language);
        console.log(`✅ Parsed successfully: ${result.functions.length} functions, ${result.imports.length} imports`);
        res.json(result);
    } catch (error: any) {
        console.error('❌ Parse error:', error.message);
        console.error('Stack:', error.stack);
        res.status(500).json({ error: error.message });
    }
});

app.get('/health', (req: Request, res: Response) => {
    res.json({ status: 'healthy', timestamp: new Date().toISOString() });
});

app.listen(PORT, () => {
    console.log(`Tree-sitter sidecar listening on port ${PORT}`);
});
