import Parser, { SyntaxNode } from 'tree-sitter';
import JavaScript from 'tree-sitter-javascript';
import TypeScript from 'tree-sitter-typescript';
import Python from 'tree-sitter-python';
import Go from 'tree-sitter-go';
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
        default:
            return { functions: [], imports: [] };
    }

    const tree = parser.parse(code);
    const rootNode = tree.rootNode;

    const functions = extractFunctions(rootNode, code, language);
    const imports = extractImports(rootNode, language);

    return { functions, imports };
}