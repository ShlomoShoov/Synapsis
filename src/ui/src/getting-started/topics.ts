export const topics = [
  { id: 'why-synapsis', label: 'Why Synapsis', group: 'Foundations' },
  { id: 'primitives', label: 'Four primitives', group: 'Foundations' },
  { id: 'architecture', label: 'Architecture', group: 'Foundations' },
  { id: 'ui-map', label: 'Map', group: 'Analyze UI' },
  { id: 'ui-graph', label: 'Graph', group: 'Analyze UI' },
  { id: 'ui-logical', label: 'Logical', group: 'Analyze UI' },
  { id: 'ui-search', label: 'Search and filters', group: 'Analyze UI' },
  { id: 'ui-details', label: 'Object details', group: 'Analyze UI' },
  { id: 'api-ingest', label: 'Connect and ingest', group: 'API guide' },
  { id: 'api-read', label: 'Read routes', group: 'API guide' },
  { id: 'api-filters', label: 'Filters and errors', group: 'API guide' },
] as const;

export type TopicId = (typeof topics)[number]['id'];

export function topicFromHash(hash: string): TopicId {
  try {
    const id = decodeURIComponent(hash.replace(/^#/, ''));
    return topics.find((topic) => topic.id === id)?.id ?? 'why-synapsis';
  } catch {
    return 'why-synapsis';
  }
}
