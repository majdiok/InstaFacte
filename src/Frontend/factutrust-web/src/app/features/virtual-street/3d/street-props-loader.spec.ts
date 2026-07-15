import { getPropUrl } from './street-props-loader';

describe('street-props-loader', () => {
  it('builds a versioned URL under /assets/virtual-street/props/', () => {
    const url = getPropUrl('lantern');
    expect(url).toMatch(/^\/assets\/virtual-street\/props\/lantern\.glb\?v=/);
  });

  it('encodes the version segment', () => {
    const url = getPropUrl('planter');
    expect(url).toContain('?v=');
  });
});
