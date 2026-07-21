// Ambient types for Vite's build-time env, so `import.meta.env.VITE_*` type-checks
// without depending on `vite/client` type resolution.
interface ImportMetaEnv {
  readonly VITE_DEMO?: string;
  readonly VITE_BASE?: string;
  readonly [key: string]: string | boolean | undefined;
}
interface ImportMeta {
  readonly env: ImportMetaEnv;
}
