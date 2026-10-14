import type { ReactNode } from 'react';
import { topics, type TopicId } from './topics';
import { WhySynapsis, Primitives, Architecture } from './content/WhySynapsis';
import { UiMap, UiGraph, UiLogical, UiSearch, UiDetails } from './content/UiGuide';
import { ApiIngest, ApiRead, ApiFilters } from './content/ApiGuide';
import styles from './GettingStartedPage.module.css';

const content: Record<TopicId, () => ReactNode> = {
  'why-synapsis': WhySynapsis,
  primitives: Primitives,
  architecture: Architecture,
  'ui-map': UiMap,
  'ui-graph': UiGraph,
  'ui-logical': UiLogical,
  'ui-search': UiSearch,
  'ui-details': UiDetails,
  'api-ingest': ApiIngest,
  'api-read': ApiRead,
  'api-filters': ApiFilters,
};

export function DocsArticle({ onSelect }: { onSelect: (id: TopicId) => void }) {
  return (
    <div className={styles.articles}>
      {topics.map((topic, index) => {
        const Content = content[topic.id];
        const previous = topics[index - 1];
        const next = topics[index + 1];
        return (
          <article
            id={topic.id}
            key={topic.id}
            className={styles.article}
            aria-labelledby={`${topic.id}-title`}
          >
            <p className={styles.eyebrow}>
              {topic.group} · {String(index + 1).padStart(2, '0')}
            </p>
            <h2 id={`${topic.id}-title`} tabIndex={-1}>
              {topic.label}
            </h2>
            <Content />
            <nav aria-label={`${topic.label} section navigation`} className={styles.prevNext}>
              {previous ? (
                <a
                  href={`#${previous.id}`}
                  onClick={(event) => {
                    event.preventDefault();
                    onSelect(previous.id);
                  }}
                >
                  ← {previous.label}
                </a>
              ) : (
                <span />
              )}
              {next ? (
                <a
                  href={`#${next.id}`}
                  onClick={(event) => {
                    event.preventDefault();
                    onSelect(next.id);
                  }}
                >
                  {next.label} →
                </a>
              ) : (
                <span />
              )}
            </nav>
          </article>
        );
      })}
    </div>
  );
}
