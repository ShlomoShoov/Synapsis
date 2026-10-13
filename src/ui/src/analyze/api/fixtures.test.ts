import { describe, expect, it } from 'vitest';
import { fixtureAnswer } from './fixtures';

const api = 'http://localhost:8080';

async function ask(path: string) {
  const { status, text } = await fixtureAnswer(`${api}${path}`);
  return { status, body: JSON.parse(text) as Record<string, unknown> };
}

describe('the development mode', () => {
  it('answers the map in any window, as the API does (Rat Islands, 1965)', async () => {
    const points = await ask(
      '/map/points?bbox=170,45,-170,55&from=1965-01-01T00:00:00Z&to=1965-04-01T00:00:00Z',
    );
    expect(points.body.total).toBe(71);
    const cells = await ask(
      '/map/cells?resolution=3&bbox=170,45,-170,55&from=1965-01-01T00:00:00Z&to=1965-04-01T00:00:00Z',
    );
    expect(cells.body.records).toBe(71);
    expect((cells.body.buckets as unknown[]).length).toBe(15);
  });

  it('answers a saved request with its file, and a 404 as the API did', async () => {
    expect((await ask('/graph?id=evt%3Aucdp%3A7578')).body.total).toBe(2);
    expect((await ask('/graph?id=evt%3Agtd%3A197000000001')).status).toBe(404);
    expect((await ask('/search?q=log4j&limit=2')).body).toMatchObject({ total: 3 });
  });

  it('says when it has no saved answer, never an empty one', async () => {
    expect((await ask('/records/ent:ucdp-actor:112')).status).toBe(501);
  });
});
