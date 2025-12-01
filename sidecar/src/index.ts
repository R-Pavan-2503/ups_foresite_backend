import express, { Request, Response } from 'express';
import bodyParser from 'body-parser';
import { parseCode } from './parser/treeSitterAdapter';

const app = express();
const PORT = 3002;

app.use(bodyParser.json({ limit: '10mb' }));

app.post('/parse', async (req: Request, res: Response) => {
    try {
        const { code, language } = req.body;

        if (!code || !language) {
            return res.status(400).json({ error: 'Missing code or language' });
        }

        const result = await parseCode(code, language);
        res.json(result);
    } catch (error: any) {
        console.error('Parse error:', error);
        res.status(500).json({ error: error.message });
    }
});

app.get('/health', (req: Request, res: Response) => {
    res.json({ status: 'healthy', timestamp: new Date().toISOString() });
});

app.listen(PORT, () => {
    console.log(`Tree-sitter sidecar listening on port ${PORT}`);
});
