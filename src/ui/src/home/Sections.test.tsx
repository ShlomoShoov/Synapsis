import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { Sections } from './Sections';
import { sections } from './sections';

afterEach(cleanup);

describe('Sections', () => {
  it('shows nothing while the list is empty', () => {
    expect(sections).toHaveLength(0);
    const { container } = render(<Sections />);
    expect(container.innerHTML).toBe('');
  });

  it('shows each explanation in order, with its image or video', () => {
    render(
      <Sections
        items={[
          {
            title: 'One language',
            text: 'Every record says when it happened.',
            media: { kind: 'image', src: '/media/home/one.jpg', alt: 'A record' },
          },
          {
            title: 'Patterns',
            text: 'Routines, and what breaks them.',
            media: { kind: 'video', src: '/media/home/two.mp4', label: 'A routine breaking' },
          },
        ]}
      />,
    );

    const titles = screen.getAllByRole('heading').map((heading) => heading.textContent);
    expect(titles).toEqual(['One language', 'Patterns']);
    expect(screen.getByText('Every record says when it happened.')).toBeTruthy();
    expect(screen.getByRole('img', { name: 'A record' }).getAttribute('src')).toBe(
      '/media/home/one.jpg',
    );
    const video = screen.getByLabelText('A routine breaking');
    expect(video.tagName).toBe('VIDEO');
    expect(video.getAttribute('src')).toBe('/media/home/two.mp4');
  });
});
