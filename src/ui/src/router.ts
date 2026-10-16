/**
 * Which page answers which address. "/" sends the visitor on to "/home" before anything is drawn
 * (the deployed server also redirects "/" itself, see nginx.conf).
 */
import { createBrowserRouter, redirect } from 'react-router';
import { AnalyzeLoading } from './analyze/AnalyzeLoading';
import { HomePage } from './home/HomePage';
import { LoadErrorPage } from './site/LoadErrorPage';
import { ObserveLoading } from './observe/ObserveLoading';
import { NotFoundPage } from './site/NotFoundPage';

/**
 * The client's routes. A new area of the product adds one line here (and one in site/areas.ts). A page whose code
 * cannot be loaded (a tab older than the build) reloads once (LoadErrorPage).
 */
export const router = createBrowserRouter([
  { path: '/', loader: () => redirect('/home') },
  { path: '/home', Component: HomePage },
  {
    path: '/getting-started',
    ErrorBoundary: LoadErrorPage,
    lazy: async () => ({
      Component: (await import('./getting-started/GettingStartedPage')).GettingStartedPage,
    }),
  },
  // Observe carries the 3D engine and the charts: its code loads only when it is opened.
  {
    path: '/observe',
    HydrateFallback: ObserveLoading,
    ErrorBoundary: LoadErrorPage,
    lazy: async () => ({ Component: (await import('./observe/ObservePage')).ObservePage }),
  },
  // Analyze carries the map engine: its code loads only when it is opened.
  {
    path: '/analyze',
    HydrateFallback: AnalyzeLoading,
    ErrorBoundary: LoadErrorPage,
    lazy: async () => ({ Component: (await import('./analyze/AnalyzePage')).AnalyzePage }),
  },
  { path: '*', Component: NotFoundPage },
]);
