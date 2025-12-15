const Parser = require('tree-sitter');
const Python = require('tree-sitter-python');

const parser = new Parser();
parser.setLanguage(Python);

const code = `import models as models, crud
from database import get_db`;

const tree = parser.parse(code);

function printNode(node, indent = 0) {
    const spaces = '  '.repeat(indent);
    console.log(`${spaces}${node.type}: "${node.text.substring(0, 50)}"`);
    for (let i = 0; i < node.childCount; i++) {
        printNode(node.child(i), indent + 1);
    }
}

console.log('=== AST Structure ===\n');
printNode(tree.rootNode);
