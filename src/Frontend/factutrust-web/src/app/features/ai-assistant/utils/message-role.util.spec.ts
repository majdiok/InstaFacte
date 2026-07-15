import { MessageRole } from '../models/ai-chat.models';
import { parseMessageRole } from './message-role.util';

describe('parseMessageRole', () => {
  it('maps numeric roles 0–3', () => {
    expect(parseMessageRole(0)).toBe(MessageRole.System);
    expect(parseMessageRole(1)).toBe(MessageRole.User);
    expect(parseMessageRole(2)).toBe(MessageRole.Assistant);
    expect(parseMessageRole(3)).toBe(MessageRole.Tool);
  });

  it('maps lowercase string roles', () => {
    expect(parseMessageRole('system')).toBe(MessageRole.System);
    expect(parseMessageRole('user')).toBe(MessageRole.User);
    expect(parseMessageRole('assistant')).toBe(MessageRole.Assistant);
    expect(parseMessageRole('tool')).toBe(MessageRole.Tool);
  });

  it('maps PascalCase string roles', () => {
    expect(parseMessageRole('User')).toBe(MessageRole.User);
    expect(parseMessageRole('Assistant')).toBe(MessageRole.Assistant);
    expect(parseMessageRole('Tool')).toBe(MessageRole.Tool);
  });

  it('trims whitespace on strings', () => {
    expect(parseMessageRole('  tool  ')).toBe(MessageRole.Tool);
  });

  it('defaults unknown strings to Assistant', () => {
    expect(parseMessageRole('unknown')).toBe(MessageRole.Assistant);
  });

  it('defaults invalid types to Assistant', () => {
    expect(parseMessageRole(null)).toBe(MessageRole.Assistant);
    expect(parseMessageRole(undefined)).toBe(MessageRole.Assistant);
    expect(parseMessageRole({})).toBe(MessageRole.Assistant);
  });

  it('rejects non-integer numbers', () => {
    expect(parseMessageRole(1.5)).toBe(MessageRole.Assistant);
    expect(parseMessageRole(4)).toBe(MessageRole.Assistant);
    expect(parseMessageRole(-1)).toBe(MessageRole.Assistant);
  });
});
