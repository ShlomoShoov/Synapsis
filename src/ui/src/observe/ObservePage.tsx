/**
 * Observe (route /observe): what the system is receiving, for the people who run it. One page, three sections,
 * and a sidebar to move between them:
 *
 *   01 Live flow   every batch on its way from its source to the core, in 3D, live or replayed from the past;
 *   02 Metrics     the ingestion log's numbers over a chosen period, in dashboards the viewer can arrange;
 *   03 Pipeline    below the API, stage by stage: relay, indexer, projector, engine (contracts/pipeline-log.md);
 *
 * The page turns the room dark while it is open (theme/night.css) and gives it back on leaving.
 */
import './theme/fonts';
import './theme/night.css';
import { useCallback, useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { ObserveDataProvider } from './data/ObserveDataProvider';
import { FlowSection } from './flow/FlowSection';
import { ignoreShortcut } from './lib/keyboard';
import { readStored, writeStored } from './lib/storage';
import { TimeZoneProvider } from './lib/TimeZoneProvider';
import { MetricsSection } from './metrics/MetricsSection';
import styles from './ObservePage.module.css';
import { sections } from './shell/sections';
import { ShortcutsDialog } from './shell/ShortcutsDialog';
import { Sidebar } from './shell/Sidebar';
import { PipelineSection } from './pipeline/PipelineSection';
import { scrollToId, useScrollSpy } from './shell/useScrollSpy';
import { paletteStyle } from './theme/palette';

/** The Observe page. */
export function ObservePage() {
  useNightRoom();
  const [main, setMain] = useState<HTMLElement | null>(null);
  const position = useScrollSpy(main);
  const [collapsed, setCollapsed] = useState(() => readStored('sidebar-collapsed', false));
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const palette = useMemo(() => paletteStyle(), []);

  const toggleSidebar = useCallback(() => {
    setCollapsed((value) => {
      writeStored('sidebar-collapsed', !value);
      return !value;
    });
  }, []);

  useGlobalShortcuts(toggleSidebar, () => setShortcutsOpen(true));
  useInitialAnchor(main);

  return (
    <TimeZoneProvider>
      <ObserveDataProvider>
        <div
          className={styles.page}
          style={palette}
          data-sidebar={collapsed ? 'collapsed' : 'open'}
        >
          <Sidebar
            position={position}
            collapsed={collapsed}
            onToggle={toggleSidebar}
            onShowShortcuts={() => setShortcutsOpen(true)}
          />
          <main ref={setMain} className={styles.main}>
            <FlowSection />
            <PipelineSection />
            <MetricsSection />
          </main>
          <ShortcutsDialog open={shortcutsOpen} onClose={() => setShortcutsOpen(false)} />
        </div>
      </ObserveDataProvider>
    </TimeZoneProvider>
  );
}

/** Darkens the whole document while Observe is shown: the theme, the browser's colour hints, the title. */
function useNightRoom() {
  useLayoutEffect(() => {
    const html = document.documentElement;
    const themeColor = document.querySelector<HTMLMetaElement>('meta[name="theme-color"]');
    const colorScheme = document.querySelector<HTMLMetaElement>('meta[name="color-scheme"]');
    const previous = {
      theme: html.dataset.theme,
      themeColor: themeColor?.content,
      colorScheme: colorScheme?.content,
      title: document.title,
    };
    html.dataset.theme = 'night';
    themeColor?.setAttribute('content', '#0d0c0a');
    colorScheme?.setAttribute('content', 'dark');
    document.title = 'Observe · Synapsis';
    return () => {
      if (previous.theme === undefined) {
        delete html.dataset.theme;
      } else {
        html.dataset.theme = previous.theme;
      }
      if (previous.themeColor !== undefined) {
        themeColor?.setAttribute('content', previous.themeColor);
      }
      if (previous.colorScheme !== undefined) {
        colorScheme?.setAttribute('content', previous.colorScheme);
      }
      document.title = previous.title;
    };
  }, []);
}

/** The shortcuts that work anywhere on the page: 1 2 3 to jump, [ for the sidebar, ? for the list. */
function useGlobalShortcuts(toggleSidebar: () => void, showShortcuts: () => void) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (ignoreShortcut(event)) {
        return;
      }
      const section = sections.find((item) => item.key === event.key);
      if (section) {
        event.preventDefault();
        scrollToId(section.id);
      } else if (event.key === '[') {
        event.preventDefault();
        toggleSidebar();
      } else if (event.key === '?') {
        event.preventDefault();
        showShortcuts();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [toggleSidebar, showShortcuts]);
}

/** Opens the page at the anchor in the address (/observe#metrics), once the sections are drawn. */
function useInitialAnchor(main: HTMLElement | null) {
  useEffect(() => {
    const id = window.location.hash.slice(1);
    if (main && id) {
      requestAnimationFrame(() => document.getElementById(id)?.scrollIntoView({ block: 'start' }));
    }
  }, [main]);
}
