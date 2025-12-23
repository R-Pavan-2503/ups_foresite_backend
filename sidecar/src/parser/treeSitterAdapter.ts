import Parser, { SyntaxNode } from 'tree-sitter';
import JavaScript from 'tree-sitter-javascript';
import TypeScript from 'tree-sitter-typescript';
import Python from 'tree-sitter-python';
import Go from 'tree-sitter-go';
import Java from 'tree-sitter-java';
import { extractFunctions } from './extractFunctions';
import { extractImports } from './extractImports';


interface ParseResult {
    functions: Array<{
        name: string;
        code: string;
        startLine: number;
        endLine: number;
    }>;
    imports: Array<{
        module: string;
        importedName?: string;
    }>;
}

export async function parseCode(code: string, language: string): Promise<ParseResult> {
    // Validate inputs
    if (typeof code !== 'string') {
        throw new Error(`Code must be a string, received: ${typeof code}`);
    }

    if (typeof language !== 'string') {
        throw new Error(`Language must be a string, received: ${typeof language}`);
    }

    if (code.trim().length === 0) {
        console.warn('⚠️ Empty code provided, returning empty result');
        return { functions: [], imports: [] };
    }

    // Warn about very large files (Tree-sitter may struggle with these)
    const MAX_SAFE_SIZE = 50000; // 50KB
    if (code.length > MAX_SAFE_SIZE) {
        console.warn(`⚠️ Large file detected: ${code.length} chars (>${MAX_SAFE_SIZE}). Tree-sitter may have issues.`);
    }

    const parser = new Parser();

    // Set language
    switch (language.toLowerCase()) {
        case 'javascript':
        case 'jsx':
            parser.setLanguage(JavaScript);
            break;
        case 'typescript':
        case 'tsx':
            parser.setLanguage(TypeScript.typescript);
            break;
        case 'python':
            parser.setLanguage(Python);
            break;
        case 'go':
            parser.setLanguage(Go);
            break;
        case 'java':
            parser.setLanguage(Java);
            break;
        default:
            console.warn(`⚠️ Unsupported language: ${language}`);
            return { functions: [], imports: [] };
    }

    // Final safety check before parsing
    if (!code || typeof code !== 'string') {
        throw new Error('Invalid code input for Tree-sitter parser');
    }

    // Try to parse with enhanced error handling
    try {
        const tree = parser.parse(code);
        const rootNode = tree.rootNode;

        const functions = extractFunctions(rootNode, code, language);
        const imports = extractImports(rootNode, language);

        return { functions, imports };
    } catch (error: any) {
        // Log detailed error information
        console.error('❌ Tree-sitter parser.parse() failed:');
        console.error(`   Language: ${language}`);
        console.error(`   Code length: ${code.length} characters`);
        console.error(`   Error: ${error.message}`);
        console.error(`   First 200 chars: ${code.substring(0, 200)}`);
        console.error(`   Last 200 chars: ${code.substring(code.length - 200)}`);

        // Check for specific patterns that might cause issues
        const lineCount = code.split('\n').length;
        console.error(`   Line count: ${lineCount}`);

        // Re-throw with more context
        throw new Error(`Tree-sitter parsing failed for ${language} file (${code.length} chars, ${lineCount} lines): ${error.message}`);
    }
}