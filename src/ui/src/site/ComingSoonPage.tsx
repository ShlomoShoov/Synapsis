/**
 * The page of an area that is not built yet (Getting started, Analyze): its name, what it will do, and "Coming soon".
 * The area is found from the address, so one component serves them all (router.ts).
 */
import { useLocation } from 'react-router';
import { areas } from './areas';
import { NoticePage } from './NoticePage';

/** "Coming soon", for the area at the current address. */
export function ComingSoonPage() {
  const { pathname } = useLocation();
  const area = areas.find((candidate) => candidate.path === pathname);
  return <NoticePage label="Coming soon" title={area?.name ?? 'Coming soon'} line={area?.line} />;
}
