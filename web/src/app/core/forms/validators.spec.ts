import { FormControl } from '@angular/forms';
import { requiredText, textOrNull } from './validators';

describe('requiredText', () => {
  it.each([null, '', '   '])('refuses %o as empty, as the API does', (value) => {
    // Act & Assert
    expect(requiredText(new FormControl(value))).toEqual({ required: true });
  });

  it('accepts text with surrounding spaces', () => {
    // Act & Assert
    expect(requiredText(new FormControl(' Ana '))).toBeNull();
  });
});

describe('textOrNull', () => {
  it.each([
    [null, null],
    ['', null],
    ['  ', null],
    ['Ana', 'Ana'],
  ])('turns %o into %o', (value, expected) => {
    // Act & Assert
    expect(textOrNull(value)).toBe(expected);
  });
});
