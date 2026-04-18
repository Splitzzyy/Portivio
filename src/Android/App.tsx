import React, { useEffect } from 'react';
import { useColorScheme } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { NavigationContainer } from '@react-navigation/native';
import { Provider as PaperProvider } from 'react-native-paper';
import { QueryClientProvider } from '@tanstack/react-query';
import Toast from 'react-native-toast-message';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

import { darkTheme, lightTheme } from './src/theme/paperTheme';
import { queryClient } from './src/store/queryClient';
import { useAuthStore } from './src/store/authStore';
import { setOnAuthFailure } from './src/api/client';
import { navigationRef } from './src/navigation/navigationRef';
import { RootNavigator } from './src/navigation/RootNavigator';
import { LoadingOverlay } from './src/components/feedback/LoadingOverlay';

export default function App(): React.JSX.Element {
  const scheme = useColorScheme();
  const isDark = scheme === 'dark';
  const theme = isDark ? darkTheme : lightTheme;

  const isHydrating = useAuthStore((s) => s.isHydrating);
  const hydrate = useAuthStore((s) => s.hydrate);
  const clear = useAuthStore((s) => s.clear);

  useEffect(() => {
    setOnAuthFailure(() => {
      void clear();
    });
    void hydrate();
  }, [clear, hydrate]);

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <SafeAreaProvider>
        <PaperProvider theme={theme}>
          <QueryClientProvider client={queryClient}>
            <NavigationContainer ref={navigationRef} theme={undefined}>
              <StatusBar style={isDark ? 'light' : 'dark'} />
              {isHydrating ? <LoadingOverlay /> : <RootNavigator />}
            </NavigationContainer>
            <Toast />
          </QueryClientProvider>
        </PaperProvider>
      </SafeAreaProvider>
    </GestureHandlerRootView>
  );
}
