import React from 'react';
import { StyleSheet, View } from 'react-native';
import { Button, Text } from 'react-native-paper';

export function ErrorView({
  message,
  onRetry,
}: {
  message: string;
  onRetry?: () => void;
}): React.JSX.Element {
  return (
    <View style={styles.wrap}>
      <Text variant="titleMedium">Something went wrong</Text>
      <Text variant="bodyMedium" style={styles.msg}>
        {message}
      </Text>
      {onRetry ? (
        <Button mode="contained" onPress={onRetry} style={styles.btn}>
          Retry
        </Button>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { padding: 24, alignItems: 'center' },
  msg: { textAlign: 'center', marginVertical: 12, opacity: 0.8 },
  btn: { marginTop: 8 },
});
