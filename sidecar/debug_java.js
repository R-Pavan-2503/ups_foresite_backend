// Debug script to test Java parsing
const Parser = require('tree-sitter');
const Java = require('tree-sitter-java');

const parser = new Parser();
parser.setLanguage(Java);

const code = `
import java.util.List;
import java.util.ArrayList;
import static java.lang.Math.PI;
import com.example.models.*;

public class Main {
    public Main() {
        System.out.println("Constructor");
    }
    
    public void doSomething() {
        System.out.println("Hello");
    }
    
    public static int calculate(int x) {
        return x * 2;
    }
}
`;

const tree = parser.parse(code);

console.log('=== Root Node Type ===');
console.log(tree.rootNode.type);

console.log('\n=== Looking for import_declaration nodes ===');
function findImports(node) {
    if (node.type === 'import_declaration') {
        console.log('Found import_declaration:');
        console.log('  Full text:', node.text);
        for (const child of node.children) {
            console.log('  Child:', child.type, '->', child.text);
        }
    }
    for (const child of node.children) {
        findImports(child);
    }
}
findImports(tree.rootNode);

console.log('\n=== Looking for method_declaration nodes ===');
function findMethods(node) {
    if (node.type === 'method_declaration' || node.type === 'constructor_declaration') {
        console.log('Found', node.type + ':');
        const nameNode = node.childForFieldName ? node.childForFieldName('name') : null;
        console.log('  Name:', nameNode ? nameNode.text : 'N/A');
    }
    for (const child of node.children) {
        findMethods(child);
    }
}
findMethods(tree.rootNode);
