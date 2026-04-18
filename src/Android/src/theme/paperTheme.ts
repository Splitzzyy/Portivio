import { MD3DarkTheme, MD3LightTheme } from 'react-native-paper';

const brand = {
  primary: '#2563EB',
  secondary: '#10B981',
  tertiary: '#F59E0B',
};

export const lightTheme = {
  ...MD3LightTheme,
  colors: {
    ...MD3LightTheme.colors,
    primary: brand.primary,
    secondary: brand.secondary,
    tertiary: brand.tertiary,
  },
};

export const darkTheme = {
  ...MD3DarkTheme,
  colors: {
    ...MD3DarkTheme.colors,
    primary: '#60A5FA',
    secondary: '#34D399',
    tertiary: '#FBBF24',
  },
};
