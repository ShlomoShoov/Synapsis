import { describe, expect, it } from 'vitest';
import type { SchemaDocument, SchemaPackage } from '../data/contracts';
import { buildSchemaGraph, linksOf, neighboursOf, signatureOf } from './graph';

function document(content: SchemaPackage, version = 1): SchemaDocument {
  return {
    name: content.package,
    version,
    fingerprint: `f-${content.package}-${version}`,
    package: content,
  };
}

const core = document({
  package: 'core',
  title: 'Core',
  entity_types: [
    { code: 'Vulnerability', title: 'A CVE' },
    { code: 'Country', title: 'A country' },
  ],
  roles: [{ code: 'subject', title: 'What it is about' }],
  measures: [{ name: 'count', title: 'Count', unit: 'count' }],
  shared_entity_namespaces: [
    { namespace: 'cve', entity_type: 'Vulnerability', key_pattern: '^CVE-' },
  ],
  vocabularies: { precision: ['day', 'year'] },
});

const kev = document({
  package: 'kev',
  title: 'KEV',
  sources: [
    {
      id: 'src:cisa:kev',
      title: 'Known exploited',
      publisher: 'CISA',
      reliability: 95,
      reliability_basis: 'A1',
      publication_lag_days: 0,
      known_at_rule: 'Published',
    },
  ],
  entity_types: [
    { code: 'Product', title: 'A product' },
    { code: 'Vendor', title: 'A vendor' },
  ],
  event_types: [
    {
      code: 'CYB.VULN.EXPLOITED',
      title: 'Exploited',
      place: ['unplaced'],
      roles: { subject: ['Vulnerability'], affected: ['Product'] },
      measures: ['count', 'days'],
    },
  ],
  relation_types: [{ code: 'PRODUCT_OF', title: 'Product of', from: ['Product'], to: ['Vendor'] }],
  measures: [{ name: 'days', title: 'Days', unit: 'day' }],
});

const nvd = document({
  package: 'nvd',
  title: 'NVD',
  event_types: [
    {
      code: 'CYB.VULN.PUBLISHED',
      title: 'Published',
      place: ['unplaced'],
      roles: { subject: ['Vulnerability'] },
    },
  ],
  // A second definition of a code core owns: kept apart, never linked.
  entity_types: [{ code: 'Country', title: 'Another country' }],
});

describe('the schema graph', () => {
  const graph = buildSchemaGraph([nvd, kev, core]);

  it('lists every definition once, by kind and code', () => {
    expect(graph.definitions.get('entity:Vulnerability')?.packageName).toBe('core');
    expect(graph.definitions.get('source:src:cisa:kev')?.title).toBe('Known exploited');
    expect(graph.definitions.get('vocabulary:precision')?.title).toBe('2 values');
    expect(graph.packageByName.get('kev')?.definitions.map((item) => item.kind)).toEqual([
      'source',
      'event',
      'entity',
      'entity',
      'relation',
      'measure',
    ]);
  });

  it('resolves the codes a definition uses, with the role an entity type plays', () => {
    const uses = graph.uses.get('event:CYB.VULN.EXPLOITED') ?? [];
    expect(
      uses.map((item) => `${item.kind} ${item.to}${item.role ? ` as ${item.role}` : ''}`),
    ).toEqual([
      'role role:subject',
      'type entity:Vulnerability as subject',
      'type entity:Product as affected',
      'measure measure:count',
      'measure measure:days',
    ]);
    expect(uses.find((item) => item.to === 'measure:days')?.crossPackage).toBe(false);
    expect(uses.find((item) => item.to === 'measure:count')?.crossPackage).toBe(true);
    // In package order: by domain (cyber first, core's "system" last), then by name.
    expect(graph.usedBy.get('entity:Vulnerability')?.map((item) => item.from)).toEqual([
      'event:CYB.VULN.EXPLOITED',
      'event:CYB.VULN.PUBLISHED',
      'namespace:cve',
    ]);
  });

  it('reports codes defined nowhere and codes defined twice', () => {
    expect(graph.unresolved).toEqual([
      { from: 'event:CYB.VULN.EXPLOITED', kind: 'role', expected: 'role', code: 'affected' },
    ]);
    expect(graph.duplicates.map((item) => item.id)).toEqual(['entity:Country@nvd']);
    expect(graph.definitions.get('entity:Country')?.packageName).toBe('core');
  });

  it('finds the package the others build on, and who depends on whom', () => {
    expect(graph.hub).toBe('core');
    expect(
      graph.dependencies.map((item) => `${item.from}>${item.to} ${item.references.length}`),
    ).toEqual(['kev>core 3', 'nvd>core 2']);
    expect(neighboursOf(graph, 'core')).toEqual({ uses: [], usedBy: ['kev', 'nvd'] });
  });

  it('lights up a definition with everything it touches', () => {
    const { ids } = linksOf(graph, 'entity:Product');
    expect([...ids].sort()).toEqual([
      'entity:Product',
      'event:CYB.VULN.EXPLOITED',
      'relation:PRODUCT_OF',
    ]);
  });

  it('orders packages by domain, then by name, and has no hub without uses', () => {
    expect(graph.packages.map((item) => item.name)).toEqual(['kev', 'nvd', 'core']);
    expect(buildSchemaGraph([core]).hub).toBeNull();
  });

  it('changes its signature when a package changes', () => {
    expect(signatureOf([core, kev])).not.toBe(
      signatureOf([core, document(kev.package as SchemaPackage, 2)]),
    );
  });
});

describe('the schema graph of the demo packages', () => {
  it('is clean: every code resolves, none is defined twice, and core is the hub', async () => {
    const documents = (await import('../data/simulation/demo-packages.json'))
      .default as SchemaDocument[];
    const graph = buildSchemaGraph(documents);
    expect(graph.unresolved).toEqual([]);
    expect(graph.duplicates).toEqual([]);
    expect(graph.hub).toBe('core');
    expect(graph.packages).toHaveLength(documents.length);
    // Every package that uses another uses core only (the vocabulary everything builds on).
    expect(new Set(graph.dependencies.map((item) => item.to))).toEqual(new Set(['core']));
  });
});
