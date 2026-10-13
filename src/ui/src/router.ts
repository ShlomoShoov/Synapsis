/**
 * Which page answers which address. "/" sends the visitor on to "/home" before anything is drawn
 * (the deployed server also redirects "/" itself, see nginx.conf).
 */
import { createBrowserRouter, redirect } from 'react-router';
import { HomePage } from './home/HomePage';
import { ObserveLoading } from './observe/ObserveLoading';
import { ComingSoonPage } from './site/ComingSoonPage';
import { NotFoundPage } from './site/NotFoundPage';

/** The client's routes. A new area of the product adds one line here (and one in site/areas.ts). */
export const router = createBrowserRouter([
  { path: '/', loader: () => redirect('/home') },
  { path: '/home', Component: HomePage },
  { path: '/getting-started', Component: ComingSoonPage },
  // Observe carries the 3D engine and the charts: its code loads only when it is opened.
  {
    path: '/observe',
    HydrateFallback: ObserveLoading,
    lazy: async () => ({ Component: (await import('./observe/ObservePage')).ObservePage }),
  },
  { path: '/analyze', Component: ComingSoonPage },
  { path: '*', Component: NotFoundPage },
]);
