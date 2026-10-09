import { extendTheme, type ThemeConfig } from '@chakra-ui/react';

const config: ThemeConfig = { initialColorMode: 'light', useSystemColorMode: false };

// One neutral scale + one accent. Status colours are the only other hues.
export const theme = extendTheme({
  config,
  fonts: {
    heading: `'Inter', system-ui, -apple-system, 'Segoe UI', sans-serif`,
    body: `'Inter', system-ui, -apple-system, 'Segoe UI', sans-serif`,
    mono: `'JetBrains Mono', ui-monospace, 'SF Mono', Menlo, monospace`,
  },
  colors: {
    ink: { 50: '#f6f7f9', 100: '#eceef2', 200: '#d9dde5', 300: '#b4bbc8', 400: '#838d9f', 500: '#5b6578', 600: '#434c5e', 700: '#2e3647', 800: '#1c2230', 900: '#12161f' },
    brand: { 50: '#eef2ff', 100: '#e0e7ff', 200: '#c7d2fe', 300: '#a5b4fc', 400: '#818cf8', 500: '#5b5ef0', 600: '#4a4bd6', 700: '#3c3db0', 800: '#303088', 900: '#25255f' },
  },
  styles: {
    global: {
      body: { bg: 'ink.50', color: 'ink.800', fontFeatureSettings: `'cv11','ss01'` },
      '::selection': { background: 'brand.200' },
    },
  },
  radii: { md: '8px', lg: '12px', xl: '16px' },
  shadows: {
    card: '0 1px 2px rgba(18,22,31,.04), 0 0 0 1px rgba(18,22,31,.06)',
    pop: '0 12px 32px rgba(18,22,31,.14), 0 0 0 1px rgba(18,22,31,.06)',
  },
  components: {
    Button: {
      baseStyle: { fontWeight: 600, borderRadius: 'md' },
      defaultProps: { colorScheme: 'brand' },
      variants: {
        solid: (p: any) => (p.colorScheme === 'brand' ? { bg: 'brand.600', color: 'white', _hover: { bg: 'brand.700', _disabled: { bg: 'brand.600' } }, _active: { bg: 'brand.800' } } : {}),
        subtle: { bg: 'ink.100', color: 'ink.700', _hover: { bg: 'ink.200' } },
      },
    },
    Table: {
      variants: {
        wms: {
          th: { textTransform: 'none', letterSpacing: 'normal', fontSize: 'xs', fontWeight: 600, color: 'ink.500', bg: 'ink.50', borderColor: 'ink.100', py: 3 },
          td: { fontSize: 'sm', borderColor: 'ink.100', py: 3 },
          tbody: { tr: { _hover: { bg: 'ink.50' } } },
        },
      },
      defaultProps: { variant: 'wms' },
    },
    Input: { defaultProps: { focusBorderColor: 'brand.500' } },
    Select: { defaultProps: { focusBorderColor: 'brand.500' } },
    NumberInput: { defaultProps: { focusBorderColor: 'brand.500' } },
    FormLabel: { baseStyle: { fontSize: 'xs', fontWeight: 600, color: 'ink.600', mb: 1 } },
    Modal: { baseStyle: { dialog: { borderRadius: 'xl' } } },
  },
});
