/**
 * Which page answers which address. "/" sends the visitor on to "/home" before anything is drawn
 * (the deployed server also redirects "/" itself, see nginx.conf).
 */
import { createBrowserRouter, redirect } from 'react-router';
import { ObserveLoading } from './observe/ObserveLoading';
import { HomePage } from './pages/HomePage';
import { NotFoundPage } from './pages/NotFoundPage';

/** The client's routes. A new area of the product adds one line here. */
export const router = createBrowserRouter([
  { path: '/', loader: () => redirect('/home') },
  { path: '/home', Component: HomePage },
  // Observe carries the 3D engine and the charts: its code loads only when it is opened.
  {
    path: '/observe',
    HydrateFallback: ObserveLoading,
    lazy: async () => ({ Component: (await import('./observe/ObservePage')).ObservePage }),
  },
  { path: '*', Component: NotFoundPage },
]);
