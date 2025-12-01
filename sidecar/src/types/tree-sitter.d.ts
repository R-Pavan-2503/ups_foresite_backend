// Type declarations for tree-sitter modules that don't have official types
declare module 'tree-sitter-javascript' {
    const grammar: any;
    export default grammar;
}

declare module 'tree-sitter-typescript' {
    export const typescript: any;
    export const tsx: any;
}

declare module 'tree-sitter-python' {
    const grammar: any;
    export default grammar;
}

declare module 'tree-sitter-go' {
    const grammar: any;
    export default grammar;
}
