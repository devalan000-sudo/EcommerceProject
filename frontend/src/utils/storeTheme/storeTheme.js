export function themeVariables(settings) {
  const valid = (value, fallback) => (/^#[\da-f]{6}$/i.test(value || '') ? value : fallback);
  return {
    '--color-brand': valid(settings.primaryColor, '#143f35'),
    '--color-paper': valid(settings.backgroundColor, '#f7f7f2'),
    '--color-accent': valid(settings.accentColor, '#d9e3b4'),
  };
}
