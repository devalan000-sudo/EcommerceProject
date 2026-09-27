// Copia los datos al formulario sin mantener una relación mutable con el pedido.
export function deliveryFromAddress(address, email) {
  return {
    firstName: address.firstName,
    lastName: address.lastName,
    phone: address.phone || '',
    email,
    address: address.formattedAddress,
    shippingAddress: Object.fromEntries(
      Object.keys(emptyAddress).map((key) => [key, address[key] || emptyAddress[key]]),
    ),
  };
}

export const emptyAddress = {
  street: '',
  exteriorNumber: '',
  interiorNumber: '',
  neighborhood: '',
  postalCode: '',
  state: '',
  city: '',
  country: 'México',
  references: '',
};
export const addressFields = [
  ['street', 'Calle', 100, 'address-line1'],
  ['exteriorNumber', 'Número exterior', 20, 'off'],
  ['interiorNumber', 'Número interior (opcional)', 20, 'off', true],
  ['neighborhood', 'Colonia / barrio', 80, 'address-level3'],
  ['postalCode', 'Código postal', 12, 'postal-code'],
  ['state', 'Estado / provincia', 80, 'address-level1'],
  ['city', 'Ciudad / municipio', 80, 'address-level2'],
  ['country', 'País', 60, 'country-name'],
];
export function validateAddress(value) {
  const errors = {};
  for (const [key, label, max, , optional] of addressFields) {
    const text = (value[key] || '').trim();
    if (!optional && !text) errors[key] = `Completa el campo ${label.toLowerCase()}.`;
    else if (text.length > max) errors[key] = `Utiliza como máximo ${max} caracteres.`;
  }
  const mexican = ['méxico', 'mexico', 'mx'].includes((value.country || '').trim().toLowerCase());
  if (
    value.postalCode &&
    !(mexican ? /^[0-9]{5}$/ : /^[A-Za-z0-9][A-Za-z0-9 -]{1,10}[A-Za-z0-9]$/).test(value.postalCode)
  )
    errors.postalCode = mexican
      ? 'Ingresa un código postal de cinco dígitos.'
      : 'Ingresa un código postal válido (3 a 12 caracteres).';
  return errors;
}
export function validateAddressForm(form, value, setErrors) {
  const errors = validateAddress(value);
  setErrors(errors);
  const first = Object.keys(errors)[0];
  if (first) {
    form.querySelector(`[data-address-field="${first}"]`)?.focus();
    return false;
  }
  return form.reportValidity();
}
