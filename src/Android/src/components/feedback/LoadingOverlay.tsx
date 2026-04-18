import React from 'react';
import { ActivityIndicator, View, StyleSheet } from 'react-native';

export function LoadingOverlay(): React.JSX.Element {
  return (
    <View style={styles.wrap}>
      <ActivityIndicator size="large" />
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { flex: 1, alignItems: 'center', justifyContent: 'center' },
});
