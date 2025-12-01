"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.extractFunctions = extractFunctions;
function extractFunctions(rootNode, code, language) {
    const functions = [];
    function traverse(node) {
        // JavaScript/TypeScript function declarations
        if (node.type === 'function_declaration' ||
            node.type === 'function' ||
            node.type === 'arrow_function' ||
            node.type === 'method_definition') {
            const nameNode = node.childForFieldName ? node.childForFieldName('name') : null;
            const name = nameNode ? nameNode.text : 'anonymous';
            const functionCode = node.text;
            functions.push({
                name,
                code: functionCode,
                startLine: node.startPosition.row + 1,
                endLine: node.endPosition.row + 1
            });
        }
        // Python function definitions
        if (language === 'python' && node.type === 'function_definition') {
            const nameNode = node.childForFieldName ? node.childForFieldName('name') : null;
            const name = nameNode ? nameNode.text : 'anonymous';
            const functionCode = node.text;
            functions.push({
                name,
                code: functionCode,
                startLine: node.startPosition.row + 1,
                endLine: node.endPosition.row + 1
            });
        }
        // Go function declarations
        if (language === 'go' && node.type === 'function_declaration') {
            const nameNode = node.childForFieldName ? node.childForFieldName('name') : null;
            const name = nameNode ? nameNode.text : 'anonymous';
            const functionCode = node.text;
            functions.push({
                name,
                code: functionCode,
                startLine: node.startPosition.row + 1,
                endLine: node.endPosition.row + 1
            });
        }
        // Traverse children
        if (node.children) {
            for (const child of node.children) {
                traverse(child);
            }
        }
    }
    traverse(rootNode);
    return functions;
}
