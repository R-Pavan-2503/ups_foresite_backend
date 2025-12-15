const Parser = require('tree-sitter');
const Python = require('tree-sitter-python');

const parser = new Parser();
parser.setLanguage(Python);

const code = `import models as models, crud`;

const tree = parser.parse(code);
const importStatement = tree.rootNode.child(0); // import_statement

console.log('Import statement type:', importStatement.type);
console.log('Children count:', importStatement.childCount);

for (let i = 0; i < importStatement.childCount; i++) {
    const child = importStatement.child(i);
    console.log(`\nChild ${i}: type="${child.type}", text="${child.text}"`);

    if (child.type === 'aliased_import') {
        console.log('  --> aliased_import childCount:', child.childCount);
        console.log('  --> child.children:', child.children);

        // Try different ways to access children
        for (let j = 0; j < child.childCount; j++) {
            const subChild = child.child(j);
            console.log(`  --> subchild ${j}: type="${subChild.type}", text="${subChild.text}"`);
        }
    }
}
