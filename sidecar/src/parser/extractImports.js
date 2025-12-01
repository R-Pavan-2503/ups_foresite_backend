"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.extractImports = extractImports;
function extractImports(rootNode, language) {
    const imports = [];
    function traverse(node) {
        // JavaScript/TypeScript imports
        if ((language === 'javascript' || language === 'typescript') &&
            node.type === 'import_statement') {
            // Find the string node (module path) - it's typically the 4th child (index 3)
            // and has type 'string'
            for (const child of node.children) {
                if (child.type === 'string') {
                    const moduleName = child.text.replace(/['\"]/g, '');
                    imports.push({ module: moduleName });
                    break;
                }
            }
        }
        // JavaScript/TypeScript CommonJS require() 
        if ((language === 'javascript' || language === 'typescript') &&
            node.type === 'call_expression') {
            // Check if it's a require() call
            const identifierNode = node.children.find((c) => c.type === 'identifier');
            if (identifierNode && identifierNode.text === 'require') {
                // Find the arguments node
                const argsNode = node.children.find((c) => c.type === 'arguments');
                if (argsNode) {
                    // Find the string argument
                    const stringNode = argsNode.children.find((c) => c.type === 'string');
                    if (stringNode) {
                        const moduleName = stringNode.text.replace(/['\"]/g, '');
                        imports.push({ module: moduleName });
                    }
                }
            }
        }
        // Python import statement (e.g., import os)
        if (language === 'python' && node.type === 'import_statement') {
            // Find dotted_name child
            for (const child of node.children) {
                if (child.type === 'dotted_name') {
                    imports.push({ module: child.text });
                    break;
                }
            }
        }
        // Python import_from statement (e.g., from typing import List)
        if (language === 'python' && node.type === 'import_from_statement') {
            // Find the module name - can be dotted_name or relative_import
            for (const child of node.children) {
                if (child.type === 'dotted_name' || child.type === 'relative_import') {
                    imports.push({ module: child.text });
                    break;
                }
            }
        }
        // Go imports - handle both single import and import list
        if (language === 'go' && node.type === 'import_declaration') {
            // Check for import_spec (single import)
            for (const child of node.children) {
                if (child.type === 'import_spec') {
                    // The import_spec contains an interpreted_string_literal
                    const stringNode = child.children.find((c) => c.type === 'interpreted_string_literal');
                    if (stringNode) {
                        imports.push({ module: stringNode.text.replace(/"/g, '') });
                    }
                }
                // Check for import_spec_list (multiple imports in parens)
                else if (child.type === 'import_spec_list') {
                    for (const spec of child.children) {
                        if (spec.type === 'import_spec') {
                            const stringNode = spec.children.find((c) => c.type === 'interpreted_string_literal');
                            if (stringNode) {
                                imports.push({ module: stringNode.text.replace(/"/g, '') });
                            }
                        }
                    }
                }
            }
        }
        // Traverse children
        if (node.children) {
            for (const child of node.children) {
                traverse(child);
            }
        }
    }
    traverse(rootNode);
    return imports;
}
