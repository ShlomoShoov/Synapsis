/**
 * The client's entry point: loads the fonts and the global styles, then starts the router.
 */
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router/dom';
import '@fontsource-variable/newsreader/opsz.css';
import '@fontsource-variable/newsreader/opsz-italic.css';
import './styles.css';
import { router } from './router';

const root = document.getElementById('root');
if (!root) {
  throw new Error('index.html has no #root element.');
}

createRoot(root).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
