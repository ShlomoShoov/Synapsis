/** The build-time setting Analyze reads (see config.ts). Optional. */
interface ImportMetaEnv {
  /** The address of the Analyze routes, such as http://localhost:8081 (the mock) or http://localhost:8080. */
  readonly VITE_ANALYZE_API_URL?: string;
}
