/**
 * The shapes the services send, exactly as contracts/synapsis-api.yaml defines them (snake_case, times as UTC text).
 * Nothing here is computed; ./model.ts holds the shapes Observe works with after reading these.
 */

/** One document of the Elasticsearch index synapsis-ingest-log: what one batch brought from one source. */
export interface IngestLogDocument {
  /** When the API received the batch (UTC). */
  timestamp: string;
  batch_id: string;
  /** Who sent the batch, such as "feeder:kev". */
  producer: string;
  /** The source the records name, such as "src:cisa:kev"; "unknown" for a refused record without one. */
  source: string;
  received: number;
  new: number;
  changed: number;
  unchanged: number;
  duplicates: number;
  rejected: number;
  rejection_counts: { code: string; count: number }[];
  /** The first 50 refused records. */
  rejections: { index: number; id?: string | null; code: string; path: string }[];
  /** Time the API took for the whole batch. */
  duration_ms: number;
}

/** GET /schemas: one package in the list. */
export interface SchemaSummary {
  name: string;
  version: number;
  /** Changes whenever the package changes. */
  fingerprint: string;
}

/** GET /schemas/{package}: one package with its content. */
export interface SchemaDocument extends SchemaSummary {
  package: SchemaPackage | null;
}

/** An attribute declaration (schemas/sil/schema-package.schema.json, $defs.attribute). */
export interface AttributeDefinition {
  type: 'string' | 'integer' | 'boolean' | 'date' | 'string_list';
  title?: string;
  required?: boolean;
  max_length?: number;
  max_items?: number;
  enum?: (string | number | boolean)[];
  pattern?: string;
  min?: number;
  max?: number;
}

/** A source declaration. */
export interface SourceDefinition {
  id: string;
  title: string;
  publisher: string;
  url?: string;
  license?: string;
  reliability: number;
  reliability_basis: string;
  publication_lag_days: number;
  known_at_rule: string;
  notes?: string;
}

/** An entity type. */
export interface EntityTypeDefinition {
  code: string;
  title: string;
  attributes?: Record<string, AttributeDefinition>;
}

/** An event type. */
export interface EventTypeDefinition {
  code: string;
  title: string;
  /** The place forms its records may use. */
  place: ('point' | 'admin' | 'logical' | 'unplaced')[];
  /** Allowed roles, each with the entity types it takes. */
  roles?: Record<string, string[]>;
  /** The measures its records may carry, by name. */
  measures?: string[];
  text?: 'none' | 'optional' | 'required';
  attributes?: Record<string, AttributeDefinition>;
}

/** A relation type. */
export interface RelationTypeDefinition {
  code: string;
  title: string;
  from: string[];
  to: string[];
  attributes?: Record<string, AttributeDefinition>;
}

/** A conclusion type. */
export interface ConclusionTypeDefinition {
  code: string;
  title: string;
}

/** A role. */
export interface RoleDefinition {
  code: string;
  title: string;
}

/** A measure. */
export interface MeasureDefinition {
  name: string;
  title: string;
  unit: string;
  signed?: boolean;
  min?: number;
  max?: number;
}

/** A logical space. */
export interface LogicalSpaceDefinition {
  space: string;
  title: string;
  max_depth: number;
  closed?: boolean;
  values?: string[];
}

/** A shared authority namespace (core package only). */
export interface SharedNamespaceDefinition {
  namespace: string;
  entity_type: string;
  key_pattern: string;
  title?: string;
}

/** A schema package (schemas/sil/schema-package.schema.json). */
export interface SchemaPackage {
  package: string;
  title: string;
  description?: string;
  maintainer?: string;
  namespaces?: { records?: string[]; entities?: string[] };
  vocabularies?: Record<string, string[]>;
  shared_entity_namespaces?: SharedNamespaceDefinition[];
  sources?: SourceDefinition[];
  entity_types?: EntityTypeDefinition[];
  event_types?: EventTypeDefinition[];
  relation_types?: RelationTypeDefinition[];
  conclusion_types?: ConclusionTypeDefinition[];
  roles?: RoleDefinition[];
  measures?: MeasureDefinition[];
  logical_spaces?: LogicalSpaceDefinition[];
}

/** The error body every API error carries. */
export interface ApiErrorBody {
  code: string;
  message: string;
  details?: { path: string; message: string }[];
}
