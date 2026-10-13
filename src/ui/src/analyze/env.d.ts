/** The build-time setting Analyze reads (see config.ts). Optional. */
interface ImportMetaEnv {
  /** The address of the Analyze routes, such as http://localhost:8080 (the default). */
  readonly VITE_ANALYZE_API_URL?: string;
  /** "1" answers the read API from fixtures/analyze/ (the development mode without the stores). */
  readonly VITE_ANALYZE_FIXTURES?: string;
}
