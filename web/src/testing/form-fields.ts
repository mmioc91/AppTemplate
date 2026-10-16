import { HarnessLoader } from '@angular/cdk/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';

// What a screen reader hears of each field, in the form's order: its label, and whether it is required — the
// control's aria-required, which Angular Material sets from Validators.required. A field without a control
// the harness knows fails here rather than reading as optional.
export async function requiredFields(loader: HarnessLoader): Promise<[string, boolean][]> {
  const fields = await loader.getAllHarnesses(MatFormFieldHarness);
  return Promise.all(
    fields.map(async (field): Promise<[string, boolean]> => {
      const label = await field.getLabel();
      const control = await field.getControl();

      if (control === null) {
        throw new Error(`The field "${label}" has no control to read.`);
      }

      return [label ?? '', (await (await control.host()).getAttribute('aria-required')) === 'true'];
    }),
  );
}

// The fields whose label shows Material's asterisk. None should: an optional field says so. Material shows
// it only on a required field, so this can go red only for a form that has one.
export async function markedFields(loader: HarnessLoader): Promise<(string | null)[]> {
  const fields = await loader.getAllHarnesses(MatFormFieldHarness);
  const marked = await Promise.all(
    fields.map(async (field) =>
      (await (await field.host()).matchesSelector(':has(.mat-mdc-form-field-required-marker)'))
        ? field.getLabel()
        : undefined,
    ),
  );
  return marked.filter((label) => label !== undefined);
}
