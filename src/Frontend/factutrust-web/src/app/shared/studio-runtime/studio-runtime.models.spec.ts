import { CustomFieldType, parseFieldType } from './studio-runtime.models';

describe('parseFieldType', () => {
  it('maps PascalCase enum names (the API wire format) to the numeric enum', () => {
    expect(parseFieldType('Date')).toBe(CustomFieldType.Date);
    expect(parseFieldType('Select')).toBe(CustomFieldType.Select);
    expect(parseFieldType('RelationCustom')).toBe(CustomFieldType.RelationCustom);
    expect(parseFieldType('Money')).toBe(CustomFieldType.Money);
    expect(parseFieldType('Signature')).toBe(CustomFieldType.Signature);
  });

  it('passes through numbers and numeric strings', () => {
    expect(parseFieldType(5)).toBe(CustomFieldType.Date);
    expect(parseFieldType('9')).toBe(CustomFieldType.RelationCustom);
    expect(parseFieldType(0)).toBe(CustomFieldType.Text);
  });

  it('falls back to Text for unknown / missing values', () => {
    expect(parseFieldType('Inconnu')).toBe(CustomFieldType.Text);
    expect(parseFieldType(null)).toBe(CustomFieldType.Text);
    expect(parseFieldType(undefined)).toBe(CustomFieldType.Text);
    expect(parseFieldType({})).toBe(CustomFieldType.Text);
  });
});
