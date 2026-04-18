import React from 'react';
import { StyleSheet, View } from 'react-native';
import { Text } from 'react-native-paper';

export function EmptyState({ title, hint }: { title: string; hint?: string }): React.JSX.Element {
  return (
    <View style={styles.wrap}>
      <Text variant="titleMedium">{title}</Text>
      {hint ? <Text variant="bodyMedium" style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 32, alignItems: 'center', justifyContent: 'center' },
  hint: { marginTop: 8, opacity: 0.7, textAlign: 'center' },
});
