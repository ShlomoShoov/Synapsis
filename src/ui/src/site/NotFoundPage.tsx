/**
 * The answer to an address the client does not know (every route not listed in router.ts).
 */
import { NoticePage } from './NoticePage';

/** The not-found page. */
export function NotFoundPage() {
  return <NoticePage label="Not found" title="This page is beyond what we know." />;
}
