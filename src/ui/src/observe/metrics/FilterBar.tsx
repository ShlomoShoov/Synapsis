/**
 * The filter row above the dashboards: the period (presets, or a custom one), the sources, the comparison with the
 * period before, and the refresh. It sticks to the top of the screen while the panels scroll under it.
 */
import { useMemo, useState } from 'react';
import type { SourceInfo } from '../data/catalog';
import { domainById, domains } from '../domains';
import { useNow } from '../lib/clock';
import { formatAge } from '../lib/format';
import { useTimeFormats } from '../lib/timeZone';
import { DomainDot } from '../shared/OutcomeBar';
import { Popover, Segmented, Switch } from '../ui/controls';
import controls from '../ui/controls.module.css';
import { Icon } from '../ui/Icon';
import { presets, refreshes, type MetricsFilter, type PresetId, type RefreshId } from './filters';
import styles from './MetricsSection.module.css';

/** The filter row. */
export function FilterBar({
  filter,
  onChange,
  sources,
  updatedAt,
  busy,
  onRefresh,
}: {
  filter: MetricsFilter;
  onChange: (filter: MetricsFilter) => void;
  /** Every source that can be chosen. */
  sources: readonly SourceInfo[];
  updatedAt: number | null;
  busy: boolean;
  onRefresh: () => void;
}) {
  const now = useNow(5_000);
  const refreshLabel = refreshes.find((item) => item.value === filter.refresh)?.label ?? 'Off';
  const sourcesLabel = !filter.sources
    ? 'All sources'
    : filter.sources.length === 1
      ? (sources.find((source) => source.id === filter.sources?.[0])?.short ?? '1 source')
      : `${filter.sources.length} sources`;

  return (
    <div className={styles.filters} role="toolbar" aria-label="Filters of the dashboards">
      <div className={styles.filterGroup}>
        <Segmented<PresetId | ''>
          label="Period"
          value={filter.preset ?? ''}
          onChange={(value) => value && onChange({ ...filter, preset: value, custom: null })}
          options={presets.map((preset) => ({ value: preset.value, label: preset.label }))}
        />
        <CustomPeriod filter={filter} onChange={onChange} />
      </div>

      <span className={styles.divider} aria-hidden="true" />

      <Popover
        label="Sources"
        align="start"
        trigger={({ open, toggle, id }) => (
          <button
            type="button"
            className={controls.button}
            aria-expanded={open}
            aria-controls={id}
            aria-pressed={filter.sources !== null}
            onClick={toggle}
          >
            <Icon name="layers" size={14} />
            {sourcesLabel}
            <Icon name="chevronDown" size={12} />
          </button>
        )}
      >
        {(close) => (
          <SourcePicker
            sources={sources}
            chosen={filter.sources}
            onChange={(chosen) => onChange({ ...filter, sources: chosen })}
            onDone={close}
          />
        )}
      </Popover>

      <span className={styles.divider} aria-hidden="true" />

      <span title="Compare each figure with the period of the same length just before">
        <Switch checked={filter.compare} onChange={(compare) => onChange({ ...filter, compare })}>
          Compare with previous
        </Switch>
      </span>

      <div className={styles.filterEnd}>
        <span className={styles.updated} data-busy={busy || undefined} aria-live="polite">
          {busy
            ? 'Updating'
            : updatedAt
              ? `Updated ${formatAge(updatedAt, Math.max(now, updatedAt))}`
              : 'Loading'}
        </span>
        <Popover
          label="Refresh"
          align="end"
          trigger={({ open, toggle, id }) => (
            <button
              type="button"
              className={controls.ghost}
              aria-expanded={open}
              aria-controls={id}
              onClick={toggle}
              disabled={!filter.preset}
              title={
                filter.preset ? 'How often the dashboards refresh' : 'A custom period does not move'
              }
            >
              Auto {refreshLabel}
              <Icon name="chevronDown" size={12} />
            </button>
          )}
        >
          {(close) => (
            <ul className={styles.pickerList} style={{ minWidth: 160 }}>
              {refreshes.map((item) => (
                <li key={item.value}>
                  <button
                    type="button"
                    className={styles.pickerItem}
                    style={{
                      width: '100%',
                      border: 0,
                      background: 'transparent',
                      fontFamily: 'var(--font-sans)',
                    }}
                    aria-pressed={filter.refresh === item.value}
                    onClick={() => {
                      onChange({ ...filter, refresh: item.value as RefreshId });
                      close();
                    }}
                  >
                    {item.label}
                    {filter.refresh === item.value ? (
                      <Icon name="check" size={14} style={{ marginLeft: 'auto' }} />
                    ) : null}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Popover>
        <button
          type="button"
          className={controls.iconButton}
          onClick={onRefresh}
          aria-label="Refresh now"
          title="Refresh now"
        >
          <Icon name="refresh" />
        </button>
      </div>
    </div>
  );
}

/** A custom period: from and to, in the chosen time zone. */
function CustomPeriod({
  filter,
  onChange,
}: {
  filter: MetricsFilter;
  onChange: (filter: MetricsFilter) => void;
}) {
  const formats = useTimeFormats();
  const now = useNow(60_000);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const active = !filter.preset && filter.custom;
  return (
    <Popover
      label="Custom period"
      align="start"
      trigger={({ open, toggle, id }) => (
        <button
          type="button"
          className={controls.ghost}
          aria-expanded={open}
          aria-controls={id}
          aria-pressed={!!active}
          onClick={() => {
            const range = filter.custom ?? { from: now - 2 * 86_400_000, to: now };
            setFrom(toInput(range.from, formats.utc));
            setTo(toInput(range.to, formats.utc));
            toggle();
          }}
          style={{ height: 32 }}
        >
          <Icon name="clock" size={14} />
          {active
            ? `${formats.dateMinute(filter.custom?.from ?? 0)} – ${formats.dateMinute(filter.custom?.to ?? 0)}`
            : 'Custom'}
        </button>
      )}
    >
      {(close) => (
        <form
          className={styles.customRange}
          onSubmit={(event) => {
            event.preventDefault();
            const start = fromInput(from, formats.utc);
            const end = fromInput(to, formats.utc);
            if (Number.isFinite(start) && Number.isFinite(end) && start < end) {
              onChange({ ...filter, preset: null, custom: { from: start, to: end } });
              close();
            }
          }}
        >
          <label>
            From ({formats.zone})
            <input
              type="datetime-local"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              required
            />
          </label>
          <label>
            To ({formats.zone})
            <input
              type="datetime-local"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              required
            />
          </label>
          <button type="submit" className={controls.primary}>
            Show this period
          </button>
        </form>
      )}
    </Popover>
  );
}

/** The source picker: search, sources grouped by domain, a whole domain at once. */
function SourcePicker({
  sources,
  chosen,
  onChange,
  onDone,
}: {
  sources: readonly SourceInfo[];
  chosen: readonly string[] | null;
  onChange: (chosen: string[] | null) => void;
  onDone: () => void;
}) {
  const [query, setQuery] = useState('');
  const selected = useMemo(
    () => new Set(chosen ?? sources.map((source) => source.id)),
    [chosen, sources],
  );
  const groups = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return domains
      .map((domain) => ({
        domain,
        members: sources.filter(
          (source) =>
            source.domain === domain.id &&
            (!needle ||
              source.id.toLowerCase().includes(needle) ||
              (source.title ?? '').toLowerCase().includes(needle)),
        ),
      }))
      .filter((group) => group.members.length > 0);
  }, [sources, query]);

  const update = (next: Set<string>) => {
    onChange(next.size === sources.length ? null : [...next]);
  };

  return (
    <div className={styles.picker}>
      <label className={styles.pickerSearch}>
        <Icon name="search" size={14} />
        <input
          type="search"
          value={query}
          placeholder="Search sources"
          aria-label="Search sources"
          onChange={(event) => setQuery(event.target.value)}
        />
      </label>
      <ul className={styles.pickerList}>
        {groups.map(({ domain, members }) => {
          const all = members.every((source) => selected.has(source.id));
          return (
            <li key={domain.id}>
              <div className={styles.pickerGroup}>
                <span>{domainById(domain.id).name}</span>
                <button
                  type="button"
                  className={styles.pickerGroupButton}
                  onClick={() => {
                    const next = new Set(selected);
                    for (const source of members) {
                      if (all) {
                        next.delete(source.id);
                      } else {
                        next.add(source.id);
                      }
                    }
                    update(next);
                  }}
                >
                  {all ? 'None' : 'All'}
                </button>
              </div>
              {members.map((source) => (
                <label key={source.id} className={styles.pickerItem}>
                  <input
                    type="checkbox"
                    checked={selected.has(source.id)}
                    onChange={(event) => {
                      const next = new Set(selected);
                      if (event.target.checked) {
                        next.add(source.id);
                      } else {
                        next.delete(source.id);
                      }
                      update(next);
                    }}
                  />
                  <DomainDot domain={source.domain} size={7} />
                  {source.short}
                </label>
              ))}
            </li>
          );
        })}
      </ul>
      <div className={styles.pickerFooter}>
        <div style={{ display: 'flex', gap: 6 }}>
          <button
            type="button"
            className={controls.ghost}
            onClick={() => onChange(null)}
            style={{ height: 28 }}
          >
            All
          </button>
          <button
            type="button"
            className={controls.ghost}
            onClick={() => onChange([])}
            style={{ height: 28 }}
          >
            None
          </button>
        </div>
        <button type="button" className={controls.button} onClick={onDone} style={{ height: 28 }}>
          Done
        </button>
      </div>
    </div>
  );
}

/** A moment as a datetime-local value in the zone shown. */
function toInput(time: number, utc: boolean): string {
  const date = new Date(utc ? time : time - new Date(time).getTimezoneOffset() * 60_000);
  return date.toISOString().slice(0, 16);
}

/** A datetime-local value read in the zone shown. */
function fromInput(value: string, utc: boolean): number {
  return value ? (utc ? Date.parse(`${value}Z`) : new Date(value).getTime()) : Number.NaN;
}
