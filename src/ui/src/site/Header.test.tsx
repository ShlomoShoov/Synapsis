import { cleanup, render, screen, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { ComingSoonPage } from './ComingSoonPage';
import { Header } from './Header';

afterEach(cleanup);

/** Draws `element` at `path`, inside a router. */
function renderAt(path: string, element: ReactNode) {
  const router = createMemoryRouter([{ path: '*', element }], { initialEntries: [path] });
  render(<RouterProvider router={router} />);
}

describe('Header', () => {
  it('leads to the three areas, in order', () => {
    renderAt('/home', <Header />);
    const links = within(screen.getByRole('navigation', { name: 'Main' })).getAllByRole('link');
    expect(links.map((link) => [link.textContent, link.getAttribute('href')])).toEqual([
      ['Getting started', '/getting-started'],
      ['Observe', '/observe'],
      ['Analyze', '/analyze'],
    ]);
  });

  it('marks the area being shown', () => {
    renderAt('/analyze', <Header />);
    expect(screen.getByRole('link', { name: 'Analyze' }).getAttribute('aria-current')).toBe('page');
    expect(screen.getByRole('link', { name: 'Observe' }).getAttribute('aria-current')).toBeNull();
  });

  it('keeps the hidden name out of the keyboard path', () => {
    renderAt('/home', <Header wordmark={false} />);
    expect(screen.queryByRole('link', { name: 'Synapsis' })).toBeNull();
  });
});

describe('ComingSoonPage', () => {
  it('names the area at the address', () => {
    renderAt('/getting-started', <ComingSoonPage />);
    expect(screen.getByRole('heading', { level: 1 }).textContent).toBe('Getting started');
    expect(screen.getByText('Coming soon')).toBeTruthy();
  });
});
