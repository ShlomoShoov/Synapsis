import { Callout, ScreenshotPlaceholder } from '../DocsBlocks';

export function UiMap() {
  return (
    <>
      <p>
        Open <a href="/analyze">Analyze</a>. The Map, Graph, and Logical switch sits alongside the
        search entry, time card, and filters. The selected object follows you between dimensions.
      </p>
      <ScreenshotPlaceholder caption="Analyze overview, October 2026" />
      <p>
        On Map, pan or zoom to a place. The view requests H3 density cells first; when the matching
        record count is 5,000 or fewer it requests individual points. Dense areas remain summarized
        as cells. Conclusions can appear in their own glyph layer. Select a point or glyph to open
        its details, and use the map zoom and reset controls to change the view.
      </p>
    </>
  );
}

export function UiGraph() {
  return (
    <>
      <p>
        Open an object, then choose Graph. It shows the selected record and its direct connections.
        Use the graph key and filter chips to understand link types, load more when available, zoom,
        refocus, or fit the whole graph.
      </p>
      <p>
        Select a connected node to pivot. The object details panel keeps the source record available
        so a connection can be checked against evidence.
      </p>
      <ScreenshotPlaceholder caption="Graph with an open object, October 2026" />
    </>
  );
}

export function UiLogical() {
  return (
    <>
      <p>
        Logical organizes records by paths. Expand a path to see its children, then open a record
        filed there. The tree is a way to browse classification, not a replacement for the original
        source record.
      </p>
      <ScreenshotPlaceholder caption="Logical tree, October 2026" />
    </>
  );
}

export function UiSearch() {
  return (
    <>
      <p>
        Open Search with its button or the <kbd>/</kbd> key. Words mode searches text prefixes;
        Meaning mode uses semantic ranking and needs a question. Choose a result to open its
        details.
      </p>
      <p>
        Search offers kind, date, source, type, logical place, entity, and limit controls. Local
        result ordering rearranges only the page returned by the API. The main filter card has
        source, type, and kind chips, and the time card sets a time window.
      </p>
      <Callout title="Current time limitation">
        The Analyze <code>as_of</code> control is visible, but it is not passed to Map, Search,
        Logical, or the object request. It does not make those views historical. The API's{' '}
        <code>GET /records/{'{id}'}?as_of=</code> route supports a historical source-state read.
      </Callout>
      <p>
        Relation is a canonical ingestion kind, but the current Map and Search kind controls do not
        offer a separate Relations choice.
      </p>
    </>
  );
}

export function UiDetails() {
  return (
    <>
      <p>
        In an object's details panel, switch between source tabs to compare states. Review facts,
        text, measures, attributes, evidence, the raw JSON, and connections. The trail helps return
        to previously opened objects.
      </p>
      <p>
        Use the available Map, Graph, or Tree actions when the record has a corresponding location,
        connection, or logical path. The raw JSON is the source-grounded view.
      </p>
      <ScreenshotPlaceholder caption="Object details and source tabs, October 2026" />
    </>
  );
}
