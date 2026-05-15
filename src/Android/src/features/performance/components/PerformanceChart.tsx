import React from 'react';
import { StyleSheet, View } from 'react-native';
import { CartesianChart, Line } from 'victory-native';
import type { Performance } from '../../../types/dtos';

interface Props {
  history: Performance.Response[];
}

export default function PerformanceChart({ history }: Props): React.JSX.Element {
  const sorted = [...history].sort((a, b) => a.date.localeCompare(b.date));
  const data = sorted.map((p) => ({
    x: new Date(p.date).getTime(),
    y: Number(p.currentValue),
  }));

  return (
    <View style={styles.chartWrap}>
      <CartesianChart data={data} xKey="x" yKeys={['y']}>
        {({ points }) => <Line points={points.y} color="#4F46E5" strokeWidth={2} />}
      </CartesianChart>
    </View>
  );
}

const styles = StyleSheet.create({
  chartWrap: { height: 220, width: '100%' },
});
