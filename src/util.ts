
export function combinePartials<T>(base: T, ...newValue: (Partial<T>|undefined|null)[]): T {
    return Object.fromEntries([...Object.entries((base ?? {}) as any),
        ...newValue.flatMap(v => Object.entries(v ?? {}))]
        .filter(v => v[1] !== undefined)) as T;
}

