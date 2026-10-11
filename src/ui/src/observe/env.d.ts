/** The build-time settings Observe reads (see config.ts). Both are optional. */
interface ImportMetaEnv {
  /** The address of Elasticsearch, such as http://localhost:9200. */
  readonly VITE_ES_URL?: string;
  /** The address of the C# API, such as http://localhost:8080. */
  readonly VITE_API_URL?: string;
}
