const Parser = require('tree-sitter');
const Python = require('tree-sitter-python');

const parser = new Parser();
parser.setLanguage(Python);

const code = `import models as models, crud`;

const tree = parser.parse(code);
const importStatement = tree.rootNode.child(0);

for (let i = 0; i < importStatement.childCount; i++) {
    const child = importStatement.child(i);

    if (child.type === 'aliased_import') {
        console.log('Found aliased_import:', child.text);
        console.log('child.children type:', typeof child.children);
        console.log('Array.isArray:', Array.isArray(child.children));

        // Try the exact code from extractImports.ts
        const nameNode = child.children?.find((c) => c.type === 'dotted_name');
        console.log('nameNode found:', nameNode ? nameNode.text : 'NOT FOUND');

        // Alternative approach
        for (const c of child.children) {
            console.log('Iterating child:', c.type, c.text);
        }
    }
}
