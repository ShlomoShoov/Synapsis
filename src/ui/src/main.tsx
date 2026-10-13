/**
 * The client's entry point: registers the fonts, loads the global styles, then starts the router.
 *
 * A font is only declared here; the browser fetches it when a page first draws with it. Newsreader is Observe's
 * serif (its --font-serif headings): the other pages never use it, so they never download it.
 */
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router/dom';
import '@fontsource-variable/newsreader/opsz.css';
import '@fontsource-variable/newsreader/opsz-italic.css';
import '@fontsource-variable/playfair-display/wght.css';
import '@fontsource-variable/playfair-display/wght-italic.css';
import { registerFonts } from './fonts/fonts';
import './styles.css';
import { router } from './router';

const root = document.getElementById('root');
if (!root) {
  throw new Error('index.html has no #root element.');
}

registerFonts();

createRoot(root).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
