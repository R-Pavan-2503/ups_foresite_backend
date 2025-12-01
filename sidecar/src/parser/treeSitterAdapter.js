"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.parseCode = parseCode;
const tree_sitter_1 = __importDefault(require("tree-sitter"));
const tree_sitter_javascript_1 = __importDefault(require("tree-sitter-javascript"));
const tree_sitter_typescript_1 = __importDefault(require("tree-sitter-typescript"));
const tree_sitter_python_1 = __importDefault(require("tree-sitter-python"));
const tree_sitter_go_1 = __importDefault(require("tree-sitter-go"));
const extractFunctions_1 = require("./extractFunctions");
const extractImports_1 = require("./extractImports");
async function parseCode(code, language) {
    const parser = new tree_sitter_1.default();
    // Set language
    switch (language.toLowerCase()) {
        case 'javascript':
        case 'jsx':
            parser.setLanguage(tree_sitter_javascript_1.default);
            break;
        case 'typescript':
        case 'tsx':
            parser.setLanguage(tree_sitter_typescript_1.default.typescript);
            break;
        case 'python':
            parser.setLanguage(tree_sitter_python_1.default);
            break;
        case 'go':
            parser.setLanguage(tree_sitter_go_1.default);
            break;
        default:
            return { functions: [], imports: [] };
    }
    const tree = parser.parse(code);
    const rootNode = tree.rootNode;
    const functions = (0, extractFunctions_1.extractFunctions)(rootNode, code, language);
    const imports = (0, extractImports_1.extractImports)(rootNode, language);
    return { functions, imports };
}
